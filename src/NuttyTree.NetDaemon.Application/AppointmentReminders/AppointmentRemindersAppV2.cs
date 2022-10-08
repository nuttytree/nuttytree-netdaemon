using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.Announcements.Models;
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

    private readonly IPeriodicTaskScheduler taskScheduler;

    private readonly IServiceScopeFactory serviceScopeFactory;

    private readonly IHomeAssistantCalendarApi homeAssistantCalendar;

    private readonly IWazeTravelTimes wazeTravelTimes;

    private readonly IAnnouncementsService announcementsService;

    private readonly IServices homeAssistantServices;

    private readonly CancellationToken applicationStopping;

    private readonly ILogger<AppointmentRemindersAppV2> logger;

    private readonly List<IDisposable> periodicTasks = new List<IDisposable>();

    private TaskCompletionSource<AppointmentRemindersServiceType> serviceTrigger = new TaskCompletionSource<AppointmentRemindersServiceType>();

    public AppointmentRemindersAppV2(
        IOptions<AppointmentRemindersOptions> options,
        IPeriodicTaskScheduler taskScheduler,
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

        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(options.Value.AppointmentUpdatesSchedulePeriod), UpdateAppointmentsFromHomeAssistantAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(options.Value.CoordinateUpdatesSchedulePeriod), SetAppointmentLocationCoordinatesAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(options.Value.CreateRemindersSchedulePeriod), CreateAppointmentRemindersAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(options.Value.TravelTimeUpdatesSchedulePeriod), UpdateAppointmentReminderTravelTimesAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(options.Value.AnnounceRemindersSchedulePeriod), AnnounceAppointmentRemindersAsync));

        _ = HandleServiceCallAsync();
        haContext.RegisterServiceCallBack<object>(
            "appointment_reminders_cancel_last_reminder",
            _ => serviceTrigger.TrySetResult(AppointmentRemindersServiceType.CancelLastReminder));
    }

    public void Dispose()
    {
        periodicTasks.ForEach(p => p.Dispose());
    }

    private async Task UpdateAppointmentsFromHomeAssistantAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating appointments from Home Assistant appointments");

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            foreach (var calendar in new[] { FamilyCalendar, ScoutsCalendar })
            {
                var homeAssistantAppointments = await homeAssistantCalendar
                    .GetAppointmentsAsync(calendar, DateTime.Now.AddDays(-1), DateTime.Now.AddDays(30), cancellationToken);
                var appointments = await dbContext.Appointments
                    .Where(a => a.Calendar == calendar)
                    .ToListAsync(cancellationToken);

                dbContext.Appointments.RemoveRange(appointments
                    .Where(a => !homeAssistantAppointments.Any(h => h.Id == a.Id)));

                homeAssistantAppointments
                    .ForEach(h =>
                    {
                        var appointment = appointments.FirstOrDefault(a => a.Id == h.Id);
                        if (appointment?.HasChanged(h) == true)
                        {
                            appointment.Update(h);
                            ApplyAppointmentPersonDefaults(appointment);
                        }
                    });

                dbContext.Appointments
                    .AddRange(homeAssistantAppointments
                        .Where(h => !appointments.Any(a => a.Id == h.Id))
                        .Select(h =>
                        {
                            var appointment = h.ToAppointmentEntity(calendar);
                            ApplyAppointmentPersonDefaults(appointment);
                            return appointment;
                        }));

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(UpdateAppointmentsFromHomeAssistantAsync));
        }
    }

    private void ApplyAppointmentPersonDefaults(AppointmentEntity appointment)
    {
        appointment.Person = (appointment.Calendar == FamilyCalendar ? appointment.GetOverrideValue(nameof(appointment.Person)) : null) ?? appointment.Person;
        appointment.Person ??= appointment.Calendar == ScoutsCalendar ? Mayson : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.StartsWith("chris' ", StringComparison.OrdinalIgnoreCase) ? Chris : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.StartsWith("melissa's ", StringComparison.OrdinalIgnoreCase) ? Melissa : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.StartsWith("mayson's ", StringComparison.OrdinalIgnoreCase) ? Mayson : null;
    }

    private async Task SetAppointmentLocationCoordinatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Setting appointment location coordinates");

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var appointments = await dbContext.Appointments
                .Where(a => !string.IsNullOrEmpty(a.Location) && (a.Latitude == null || a.Longitude == null))
                .OrderBy(a => a.StartDateTime)
                .ToListAsync(cancellationToken);

            foreach (var appointment in appointments)
            {
                if (!CheckForKnownLocationCoordinates(appointment))
                {
                    var coordinates = (await wazeTravelTimes.GetAddressLocationFromAddressAsync(appointment.Location))?.Location ?? LocationCoordinates.Empty;
                    appointment.SetLocationCoordinates(coordinates);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(SetAppointmentLocationCoordinatesAsync));
        }
    }

    private bool CheckForKnownLocationCoordinates(AppointmentEntity appointment)
    {
        if (appointment.Calendar == FamilyCalendar &&
            (appointment.Location!.Replace(" ", string.Empty, StringComparison.Ordinal)?.Contains("18275Evener", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(appointment.Location, "Home", StringComparison.OrdinalIgnoreCase)))
        {
            appointment.SetLocationCoordinates(HomeLocation);
        }
        else if (appointment.Calendar == FamilyCalendar &&
            appointment.Location!.Replace(" ", string.Empty, StringComparison.Ordinal)?.Contains("RidgewoodChurch", StringComparison.OrdinalIgnoreCase) == true)
        {
            appointment.SetLocationCoordinates(RidgewoodChurchLocation);
        }
        else if (appointment.Calendar == ScoutsCalendar &&
            (appointment.Location!.Contains("Various Sites in EP", StringComparison.OrdinalIgnoreCase)
            || appointment.Location.Contains("Other or TBD", StringComparison.OrdinalIgnoreCase)
            || appointment.Location.Contains("Pax Christi", StringComparison.OrdinalIgnoreCase)))
        {
            // If any these values are in the location odds are it will be at the Church or somewhere close so drive time will be similar
            appointment.SetLocationCoordinates(DefaultScoutsLocation);
        }
        else if (appointment.Calendar == ScoutsCalendar &&
            (appointment.Location!.Contains("Zoom", StringComparison.OrdinalIgnoreCase)
            || appointment.Location.Contains("Online", StringComparison.OrdinalIgnoreCase)))
        {
            // If it is online it will be at home
            appointment.SetLocationCoordinates(HomeLocation);
        }

        return appointment.Latitude != null && appointment.Longitude != null;
    }

    private async Task CreateAppointmentRemindersAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Creating reminders for appointments");

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var appointments = await dbContext.Appointments
                .Where(a => a.Latitude != null && a.Longitude != null && !a.Reminders.Any())
                .ToListAsync(cancellationToken);

            foreach (var appointment in appointments)
            {
                appointment.Reminders.Add(new AppointmentReminderEntity($"{appointment.Id}-Start", ReminderType.Start));
                appointment.Reminders.Add(new AppointmentReminderEntity($"{appointment.Id}-End", ReminderType.End));
                ApplyAppointmentReminderDefaults(appointment);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(CreateAppointmentRemindersAsync));
        }
    }

    private void ApplyAppointmentReminderDefaults(AppointmentEntity appointment)
    {
        if (appointment.Calendar == ScoutsCalendar && appointment.Summary.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            appointment.GetStartReminder().NextAnnouncementType = NextAnnouncementType.None;
            appointment.GetEndReminder().NextAnnouncementType = NextAnnouncementType.None;
        }

        if (appointment.Person == Mayson)
        {
            // We are going to work on reducing announcements before we introduce more announcements
            // appointment.GetEndReminder().NextAnnouncementType ??= NextAnnouncementType.FifteenMinutes;
        }

        appointment.GetStartReminder().NextAnnouncementType ??= NextAnnouncementType.TwoHours;
        appointment.GetEndReminder().NextAnnouncementType ??= NextAnnouncementType.None;
        appointment.Reminders.ForEach(r => r.ArriveLeadMinutes ??= options.DefaultArriveLeadMinutes);
        appointment.Reminders.ForEach(r => r.NextTravelTimeUpdate = r.NextAnnouncementType == NextAnnouncementType.None ? null : DateTime.MinValue);
    }

    private async Task UpdateAppointmentReminderTravelTimesAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Updating travel times for appointment reminders");

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var reminders = await dbContext.AppointmentReminders
                .Where(r => r.NextTravelTimeUpdate <= DateTime.UtcNow)
                .OrderBy(r => r.NextAnnouncement)
                .Include(r => r.Appointment)
                .ToListAsync(cancellationToken);

            foreach (var reminder in reminders)
            {
                var travelTime = await wazeTravelTimes.GetTravelTimeAsync(HomeLocation, reminder.GetLocationCoordinates(), reminder.GetArriveDateTime());

                // If a Scouts appointment is more than 25 miles away it is pretty sure bet we are meeting at the Church so update the location and re-calculate the travel time
                if (reminder.Appointment.Calendar == ScoutsCalendar && travelTime?.Miles > 25)
                {
                    reminder.Appointment.SetLocationCoordinates(DefaultScoutsLocation);
                    travelTime = await wazeTravelTimes.GetTravelTimeAsync(HomeLocation, DefaultScoutsLocation, reminder.GetArriveDateTime());
                }

                reminder.SetTravelTime(travelTime, options.MaxReminderMiles);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(UpdateAppointmentReminderTravelTimesAsync));
        }
    }

    private async Task AnnounceAppointmentRemindersAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Announcing appointment reminders");

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var reminder = await dbContext.AppointmentReminders
                .Where(r => r.NextAnnouncement <= DateTime.UtcNow)
                .OrderBy(r => r.NextAnnouncement)
                .Include(r => r.Appointment)
                .FirstOrDefaultAsync(cancellationToken);
            if (reminder != null)
            {
                if (reminder.NextAnnouncement > DateTime.UtcNow.AddMinutes(-5))
                {
                    ////await announcementsService.SendAnnouncementAsync(
                    ////    reminder.GetReminderMessage(),
                    ////    AnnouncementPriority.Information,
                    ////    reminder.Type == ReminderType.Start ? reminder.Appointment.Person : null,
                    ////    cancellationToken);

                    homeAssistantServices.Notify.MobileAppPhoneChris(new NotifyMobileAppPhoneChrisParameters
                    {
                        Title = $"Test Appointment Reminder For: {(reminder.Type == ReminderType.Start ? reminder.Appointment.Person : null) ?? "Everyone"}",
                        Message = reminder.GetReminderMessage(),
                    });
                }

                reminder.SetNextAnnouncementTypeAndTime();
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(AnnounceAppointmentRemindersAsync));
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
