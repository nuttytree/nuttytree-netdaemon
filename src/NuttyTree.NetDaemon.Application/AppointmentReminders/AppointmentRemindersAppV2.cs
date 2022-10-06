using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetDaemon.AppModel;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Integration;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.Announcements.Models;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Models;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.Scheduler;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

////[Focus]
////[NetDaemonApp]
internal sealed class AppointmentRemindersAppV2 : IDisposable
{
    private readonly IPeriodicTaskScheduler taskScheduler;

    private readonly IServiceScopeFactory serviceScopeFactory;

    private readonly IHomeAssistantCalendarApi homeAssistantCalendar;

    private readonly IWazeTravelTimes wazeTravelTimes;

    private readonly IAnnouncementsService announcementsService;

    private readonly IServices homeAssistantServices;

    private readonly ILogger<AppointmentRemindersAppV2> logger;

    private readonly List<IDisposable> periodicTasks = new List<IDisposable>();

    private TaskCompletionSource<AppointmentRemindersServiceType> serviceTrigger = new TaskCompletionSource<AppointmentRemindersServiceType>();

    public AppointmentRemindersAppV2(
        IPeriodicTaskScheduler taskScheduler,
        IServiceScopeFactory serviceScopeFactory,
        IHomeAssistantCalendarApi homeAssistantCalendar,
        IWazeTravelTimes wazeTravelTimes,
        IAnnouncementsService announcementsService,
        IHaContext haContext,
        IServices homeAssistantServices,
        ILogger<AppointmentRemindersAppV2> logger)
    {
        this.taskScheduler = taskScheduler;
        this.serviceScopeFactory = serviceScopeFactory;
        this.homeAssistantCalendar = homeAssistantCalendar;
        this.wazeTravelTimes = wazeTravelTimes;
        this.announcementsService = announcementsService;
        this.homeAssistantServices = homeAssistantServices;
        this.logger = logger;

        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(30), UpdateAppointmentsFromHomeAssistantAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(10), SetAppointmentLocationCoordinatesAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(10), CreateAppointmentRemindersAsync));
        periodicTasks.Add(taskScheduler.SchedulePeriodicTask(TimeSpan.FromSeconds(10), UpdateAppointmentReminderTravelTimesAsync));

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
                    .ForEach(h => appointments.FirstOrDefault(a => a.HasChanged(h))?.Update(h));

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
        appointment.Person ??= appointment.Calendar == ScoutsCalendar ? Mayson : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.Contains("chris'", StringComparison.OrdinalIgnoreCase) ? Chris : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.Contains("melissa's", StringComparison.OrdinalIgnoreCase) ? Melissa : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.Contains("mayson's", StringComparison.OrdinalIgnoreCase) ? Mayson : null;
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
        if (appointment.Person == Mayson)
        {
            // We are going to work on reducing announcements before we introduce more announcements
            // appointment.GetEndReminder().NextAnnouncementType = NextAnnouncementType.FifteenMinutes;
        }

        appointment.GetStartReminder().NextAnnouncementType ??= NextAnnouncementType.TwoHours;
        appointment.GetEndReminder().NextAnnouncementType ??= NextAnnouncementType.None;
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
                .OrderBy(r => r.Appointment.StartDateTime)
                .Include(r => r.Appointment)
                .ToListAsync(cancellationToken);

            foreach (var reminder in reminders)
            {
                var travelTime = await wazeTravelTimes.GetTravelTimeAsync(HomeLocation, reminder.GetLocationCoordinates(), reminder.GetArriveDateTime());
                reminder.SetTravelTime(travelTime);
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
        await announcementsService.SendAnnouncementAsync("test message", AnnouncementPriority.Warning, cancellationToken: cancellationToken);
    }

    private async Task HandleServiceCallAsync()
    {
        while (true)
        {
            var serviceType = await serviceTrigger.Task;

            logger.LogInformation("Service {ServiceType} was called", serviceType);

            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
                switch (serviceType)
                {
                    case AppointmentRemindersServiceType.CancelLastReminder:
                        var lastReminder = await dbContext.AppointmentReminders
                            .OrderByDescending(r => r.LastAnnouncement)
                            .FirstOrDefaultAsync();
                        lastReminder?.Cancel();
                        break;
                    default:
                        break;
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                HandleException(ex, nameof(HandleServiceCallAsync));
            }

            serviceTrigger = new TaskCompletionSource<AppointmentRemindersServiceType>();
        }
    }

    private void HandleException(Exception exception, string taskName)
    {
        logger.LogError(exception, "Exception in appointment reminders task: {TaskName}", taskName);
        homeAssistantServices.Notify.MobileAppPhoneChris(new NotifyMobileAppPhoneChrisParameters
        {
            Title = $"Appointment Reminders Exception: {taskName}",
            Message = exception.Message,
        });
    }
}
