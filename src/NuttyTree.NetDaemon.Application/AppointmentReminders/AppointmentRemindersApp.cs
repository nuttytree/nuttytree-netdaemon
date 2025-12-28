using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.Announcements.Models;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Models;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Options;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Extensions;
using NuttyTree.NetDaemon.Infrastructure.Scheduler;
using Refit;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

[Focus]
[NetDaemonApp]
internal sealed class AppointmentRemindersApp : IDisposable
{
    private readonly AppointmentRemindersOptions options;

    private readonly ITaskScheduler taskScheduler;

    private readonly IServiceScopeFactory serviceScopeFactory;

    private readonly IEntities homeAssistantEntities;

    private readonly IWazeTravelTimes wazeTravelTimes;

    private readonly IAnnouncementsService announcementsService;

    private readonly IServices homeAssistantServices;

    private readonly CancellationToken applicationStopping;

    private readonly ILogger<AppointmentRemindersApp> logger;

    private readonly IDisposable appointmentUpdatesTask;

    private readonly ITriggerableTask travelTimeUpdatesTask;

    private readonly ITriggerableTask announcementsTask;

    private TaskCompletionSource<AppointmentRemindersServiceType> serviceTrigger = new();

    public AppointmentRemindersApp(
        IOptions<AppointmentRemindersOptions> options,
        ITaskScheduler taskScheduler,
        IServiceScopeFactory serviceScopeFactory,
        IEntities homeAssistantEntities,
        IWazeTravelTimes wazeTravelTimes,
        IAnnouncementsService announcementsService,
        IHaContext haContext,
        IServices homeAssistantServices,
        IHostApplicationLifetime applicationLifetime,
        ILogger<AppointmentRemindersApp> logger)
    {
        this.options = options.Value;
        this.taskScheduler = taskScheduler;
        this.serviceScopeFactory = serviceScopeFactory;
        this.homeAssistantEntities = homeAssistantEntities;
        this.wazeTravelTimes = wazeTravelTimes;
        this.announcementsService = announcementsService;
        this.homeAssistantServices = homeAssistantServices;
        applicationStopping = applicationLifetime.ApplicationStopping;
        this.logger = logger;

        var t = wazeTravelTimes.GetAddressLocationFromAddressAsync("6801 w 24th st, st. louis park, mn").GetAwaiter().GetResult();
        var t3 = wazeTravelTimes.GetTravelTimeAsync(options.Value.HomeLocation, t!.Location, DateTime.UtcNow.AddMinutes(30)).GetAwaiter().GetResult();

        appointmentUpdatesTask = taskScheduler.CreatePeriodicTask(TimeSpan.FromSeconds(options.Value.AppointmentUpdatesSchedulePeriod), UpdateAppointmentsFromHomeAssistantAsync);
        travelTimeUpdatesTask = taskScheduler.CreateTriggerableSelfSchedulingTask(UpdateAppointmentReminderTravelTimesAsync, TimeSpan.FromSeconds(30));
        announcementsTask = taskScheduler.CreateTriggerableSelfSchedulingTask(AnnounceAppointmentRemindersAsync, TimeSpan.FromSeconds(30));

        _ = HandleServiceCallAsync();
        haContext.RegisterServiceCallBack<object>(
            "appointment_reminders_cancel_last_reminder",
            _ => serviceTrigger.TrySetResult(AppointmentRemindersServiceType.CancelLastReminder));
    }

    public void Dispose()
    {
        appointmentUpdatesTask.Dispose();
        travelTimeUpdatesTask.Dispose();
        announcementsTask.Dispose();
    }

    private async Task UpdateAppointmentsFromHomeAssistantAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            foreach (var calendar in new[] { homeAssistantEntities.Calendar.Family, homeAssistantEntities.Calendar.Troop479 })
            {
                var homeAssistantAppointments = (await calendar
                    .GetEventsAsync(DateTime.Now.AddHours(-1), TimeSpan.FromDays(30)))
                    .Where(a => !string.IsNullOrWhiteSpace(a.Location))
                    .Where(a => a.Calendar != ScoutsCalendarEntityId)
                    .ToList();
                var appointments = await dbContext.Appointments
                    .Where(a => a.Calendar == calendar.EntityId)
                    .Include(a => a.Reminders)
                    .ToListAsync(cancellationToken);

                var oldAppointments = appointments
                    .Where(a => !homeAssistantAppointments.Any(h => h.Id == a.Id));
                if (oldAppointments.Any())
                {
                    dbContext.Appointments.RemoveRange(oldAppointments);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                foreach (var homeAssistantAppointment in homeAssistantAppointments)
                {
                    var appointment = appointments.FirstOrDefault(a => a.Id == homeAssistantAppointment.Id);
                    if (appointment == null)
                    {
                        logger.LogInformation(
                            "Found new appointment {AppointmentSummary} at {AppointmentStart} in Home Assistant",
                            homeAssistantAppointment.Summary,
                            homeAssistantAppointment.Start);

                        appointment = homeAssistantAppointment.ToAppointmentEntity();
                        appointment.SetAppointmentPerson();
                        appointment.SetLocationCoordinates(appointment.GetKnownLocationCoordinates(options)
                            ?? (await wazeTravelTimes.GetAddressLocationFromAddressAsync(appointment.Location))?.Location
                            ?? LocationCoordinates.Empty);
                        appointment.SetAppointmentReminderOptions(options);

                        dbContext.Appointments.Add(appointment);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        travelTimeUpdatesTask.Trigger();
                    }
                    else if (appointment.HasChanged(homeAssistantAppointment))
                    {
                        logger.LogInformation(
                            "Found updated appointment {AppointmentSummary} at {AppointmentStart} in Home Assistant",
                            homeAssistantAppointment.Summary,
                            homeAssistantAppointment.Start);

                        appointment.Update(homeAssistantAppointment);
                        appointment.SetAppointmentPerson();
                        appointment.SetAppointmentReminderOptions(options);

                        await dbContext.SaveChangesAsync(cancellationToken);
                        travelTimeUpdatesTask.Trigger();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(UpdateAppointmentsFromHomeAssistantAsync));
        }
    }

    private async Task<TimeSpan> UpdateAppointmentReminderTravelTimesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var nextTwoReminders = await dbContext.AppointmentReminders
                .Where(r => r.NextTravelTimeUpdate != null)
                .OrderBy(r => r.NextTravelTimeUpdate)
                .Take(2)
                .Include(r => r.Appointment)
                .ToListAsync(cancellationToken);

            var reminderToUpdate = nextTwoReminders.FirstOrDefault();
            if (reminderToUpdate == null)
            {
                return TimeSpan.MaxValue;
            }
            else if (reminderToUpdate.NextTravelTimeUpdate <= DateTime.UtcNow)
            {
                if (reminderToUpdate.GetLeaveDateTime() > DateTime.UtcNow)
                {
                    logger.LogInformation(
                        "Updating the travel time for appointment {AppointmentSummary} at {AppointmentStart}",
                        reminderToUpdate.Appointment.Summary,
                        reminderToUpdate.Appointment.StartDateTime);

                    var travelTime = reminderToUpdate.GetLocationCoordinates().Equals(options.HomeLocation)
                        ? new TravelTime(0, 0)
                        : await wazeTravelTimes.GetTravelTimeAsync(options.HomeLocation, reminderToUpdate.GetLocationCoordinates(), reminderToUpdate.GetArriveDateTime());

                    // If a Scouts appointment is more than 25 miles away it is pretty sure bet we are meeting at the Church so update the location and re-calculate the travel time
                    if (reminderToUpdate.Appointment.Calendar == ScoutsCalendarEntityId && travelTime?.Miles > 25)
                    {
                        reminderToUpdate.Appointment.SetLocationCoordinates(DefaultScoutsLocation);
                        travelTime = await wazeTravelTimes.GetTravelTimeAsync(options.HomeLocation, DefaultScoutsLocation, reminderToUpdate.GetArriveDateTime());
                    }

                    reminderToUpdate.SetTravelTime(travelTime, options);
                }
                else
                {
                    reminderToUpdate.NextTravelTimeUpdate = null;
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                announcementsTask.Trigger();

                return nextTwoReminders
                    .Where(r => r.NextTravelTimeUpdate != null)
                    .OrderBy(r => r.NextTravelTimeUpdate)
                    .Select(r => r.NextTravelTimeUpdate - DateTime.UtcNow)
                    .FirstOrDefault() ?? TimeSpan.MaxValue;
            }
            else
            {
                return reminderToUpdate.NextTravelTimeUpdate!.Value - DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(UpdateAppointmentReminderTravelTimesAsync));
            return TimeSpan.FromSeconds(30);
        }
    }

    private async Task<TimeSpan> AnnounceAppointmentRemindersAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var nextTwoReminders = await dbContext.AppointmentReminders
                .Where(r => r.NextAnnouncement != null)
                .OrderBy(r => r.NextAnnouncement)
                .Take(2)
                .Include(r => r.Appointment)
                .ToListAsync(cancellationToken);

            var reminderToAnnounce = nextTwoReminders.FirstOrDefault();
            if (reminderToAnnounce == null)
            {
                return TimeSpan.MaxValue;
            }
            else if (reminderToAnnounce.NextAnnouncement <= DateTime.UtcNow)
            {
                // Only announce if the reminder is no more than 5 minutes late
                if (reminderToAnnounce.NextAnnouncement > DateTime.UtcNow.AddMinutes(-5))
                {
                    logger.LogInformation(
                        "Announcing {AnnouncementType} reminder for appointment {AppointmentSummary} at {AppointmentStart}",
                        reminderToAnnounce.NextAnnouncementType,
                        reminderToAnnounce.Appointment.Summary,
                        reminderToAnnounce.Appointment.StartDateTime);

                    await announcementsService.SendAnnouncementAsync(
                        reminderToAnnounce.GetReminderMessage(options),
                        AnnouncementType.Reminder,
                        reminderToAnnounce.Priority ? AnnouncementPriority.Critical : AnnouncementPriority.Information,
                        reminderToAnnounce.Type == ReminderType.Start ? reminderToAnnounce.Appointment.Person : null,
                        cancellationToken);
                }

                reminderToAnnounce.UpdateNextAnnouncementTypeAndTime();
                await dbContext.SaveChangesAsync(cancellationToken);

                return nextTwoReminders
                    .Where(r => r.NextAnnouncement != null)
                    .OrderBy(r => r.NextAnnouncement)
                    .Select(r => r.NextAnnouncement - DateTime.UtcNow)
                    .FirstOrDefault() ?? TimeSpan.MaxValue;
            }
            else
            {
                return reminderToAnnounce.NextAnnouncement!.Value - DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(AnnounceAppointmentRemindersAsync));
            return TimeSpan.FromSeconds(30);
        }
    }

    private async Task HandleServiceCallAsync()
    {
        while (!applicationStopping.IsCancellationRequested)
        {
            try
            {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                var serviceType = await serviceTrigger.Task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
                serviceTrigger = new TaskCompletionSource<AppointmentRemindersServiceType>();

                logger.LogInformation("Service {ServiceType} was called", serviceType);

                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
                switch (serviceType)
                {
                    case AppointmentRemindersServiceType.CancelLastReminder:
                        var lastReminder = await dbContext.AppointmentReminders
                            .OrderByDescending(r => r.LastAnnouncement)
                            .FirstOrDefaultAsync(applicationStopping);
                        lastReminder?.Cancel();
                        break;
                    default:
                        break;
                }

                await dbContext.SaveChangesAsync(applicationStopping);
            }
            catch (Exception ex)
            {
                HandleException(ex, nameof(HandleServiceCallAsync));
            }
        }
    }

    private void HandleException(Exception exception, string taskName)
    {
        logger.LogError(exception, "Exception in appointment reminders task: {TaskName}", taskName);
        if (options.NotificationOfExceptions)
        {
            homeAssistantServices.Notify.MobileAppPhoneChris(new NotifyMobileAppPhoneChrisParameters
            {
                Title = $"Appointment Reminders Exception: {taskName}",
                Message = exception.Message,
            });
        }
    }
}
