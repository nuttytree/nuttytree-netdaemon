using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Models;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Options;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.Scheduler;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

[Focus]
[NetDaemonApp]
internal sealed class AppointmentRemindersAppV2 : IDisposable
{
    private readonly AppointmentRemindersOptions options;

    private readonly ITaskScheduler taskScheduler;

    private readonly IServiceScopeFactory serviceScopeFactory;

    private readonly IHomeAssistantCalendarApi homeAssistantCalendar;

    private readonly IWazeTravelTimes wazeTravelTimes;

    private readonly IAnnouncementsService announcementsService;

    private readonly IServices homeAssistantServices;

    private readonly CancellationToken applicationStopping;

    private readonly ILogger<AppointmentRemindersAppV2> logger;

    private readonly IDisposable appointmentUpdatesTask;

    private readonly ITriggerableTask travelTimeUpdatesTask;

    private readonly ITriggerableTask announcementsTask;

    private TaskCompletionSource<AppointmentRemindersServiceType> serviceTrigger = new TaskCompletionSource<AppointmentRemindersServiceType>();

    public AppointmentRemindersAppV2(
        IOptions<AppointmentRemindersOptions> options,
        ITaskScheduler taskScheduler,
        IServiceScopeFactory serviceScopeFactory,
        IHomeAssistantCalendarApi homeAssistantCalendar,
        IWazeTravelTimes wazeTravelTimes,
        IAnnouncementsService announcementsService,
        IHaContext haContext,
        IServices homeAssistantServices,
        IHostApplicationLifetime applicationLifetime,
        ILogger<AppointmentRemindersAppV2> logger)
    {
        this.options = options.Value;
        this.taskScheduler = taskScheduler;
        this.serviceScopeFactory = serviceScopeFactory;
        this.homeAssistantCalendar = homeAssistantCalendar;
        this.wazeTravelTimes = wazeTravelTimes;
        this.announcementsService = announcementsService;
        this.homeAssistantServices = homeAssistantServices;
        applicationStopping = applicationLifetime.ApplicationStopping;
        this.logger = logger;

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
            foreach (var calendar in new[] { FamilyCalendar, ScoutsCalendar })
            {
                var homeAssistantAppointments = (await homeAssistantCalendar
                    .GetAppointmentsAsync(calendar, DateTime.Now.AddHours(-1), DateTime.Now.AddDays(30), cancellationToken))
                    .Where(a => !string.IsNullOrWhiteSpace(a.Location));
                var appointments = await dbContext.Appointments
                    .Where(a => a.Calendar == calendar)
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
                    if (appointment?.HasChanged(homeAssistantAppointment) == true)
                    {
                        logger.LogInformation(
                            "Found updated appointment {AppointmentSummary} at {AppointmentStart} in Home Assistant",
                            homeAssistantAppointment.Summary,
                            homeAssistantAppointment.GetStartDateTime());

                        appointment.Update(homeAssistantAppointment);
                        appointment.SetAppointmentPerson();
                        appointment.SetAppointmentReminderOptions(options);

                        await dbContext.SaveChangesAsync(cancellationToken);
                        travelTimeUpdatesTask.Trigger();
                    }
                }

                foreach (var homeAssistantAppointment in homeAssistantAppointments
                    .Where(h => !appointments.Any(a => a.Id == h.Id)))
                {
                    logger.LogInformation(
                        "Found new appointment {AppointmentSummary} at {AppointmentStart} in Home Assistant",
                        homeAssistantAppointment.Summary,
                        homeAssistantAppointment.GetStartDateTime());

                    var appointment = homeAssistantAppointment.ToAppointmentEntity(calendar);
                    appointment.SetAppointmentPerson();
                    appointment.SetLocationCoordinates(appointment.GetKnownLocationCoordinates(options)
                        ?? (await wazeTravelTimes.GetAddressLocationFromAddressAsync(appointment.Location))?.Location
                        ?? LocationCoordinates.Empty);
                    appointment.SetAppointmentReminderOptions(options);

                    dbContext.Appointments.Add(appointment);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    travelTimeUpdatesTask.Trigger();
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
                logger.LogInformation(
                    "Updating the travel time for appointment {AppointmentSummary} at {AppointmentStart}",
                    reminderToUpdate.Appointment.Summary,
                    reminderToUpdate.Appointment.StartDateTime);

                var travelTime = reminderToUpdate.GetLocationCoordinates().Equals(options.HomeLocation)
                    ? new TravelTime(0, 0)
                    : await wazeTravelTimes.GetTravelTimeAsync(options.HomeLocation, reminderToUpdate.GetLocationCoordinates(), reminderToUpdate.GetArriveDateTime());

                // If a Scouts appointment is more than 25 miles away it is pretty sure bet we are meeting at the Church so update the location and re-calculate the travel time
                if (reminderToUpdate.Appointment.Calendar == ScoutsCalendar && travelTime?.Miles > 25)
                {
                    reminderToUpdate.Appointment.SetLocationCoordinates(DefaultScoutsLocation);
                    travelTime = await wazeTravelTimes.GetTravelTimeAsync(options.HomeLocation, DefaultScoutsLocation, reminderToUpdate.GetArriveDateTime());
                }

                reminderToUpdate.SetTravelTime(travelTime, options);
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

                    ////await announcementsService.SendAnnouncementAsync(
                    ////    reminderToAnnounce.GetReminderMessage(options),
                    ////    AnnouncementPriority.Information,
                    ////    reminderToAnnounce.Type == ReminderType.Start ? reminderToAnnounce.Appointment.Person : null,
                    ////    cancellationToken);

                    homeAssistantServices.Notify.MobileAppPhoneChris(new NotifyMobileAppPhoneChrisParameters
                    {
                        Title = $"Test Appointment Reminder For: {(reminderToAnnounce.Type == ReminderType.Start ? reminderToAnnounce.Appointment.Person : null) ?? "Everyone"}",
                        Message = reminderToAnnounce.GetReminderMessage(options),
                    });
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
                var serviceType = await serviceTrigger.Task;
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
