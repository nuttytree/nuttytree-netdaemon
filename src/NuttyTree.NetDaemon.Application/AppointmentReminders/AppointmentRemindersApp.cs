using System.IO.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.AppointmentReminders.HomeAssistant;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

//[NetDaemonApp]
internal class AppointmentRemindersApp : IAsyncInitializable
{
    private readonly IFileSystem fileSystem;

    private readonly IHomeAssistantCalendarApi hassCalendarApi;

    private readonly IWazeTravelTimes wazeTravelTimes;

    private readonly IEntities entities;

    private readonly IServices services;

    private readonly IScheduler scheduler;

    private readonly IAnnouncementsService announcementsService;

    private readonly ILogger<AppointmentRemindersApp> logger;

    private readonly bool disable;

    private readonly string dataFile;

    private List<Appointment> appointments = new List<Appointment>();

    public AppointmentRemindersApp(
        IFileSystem fileSystem,
        IScheduler scheduler,
        IHomeAssistantCalendarApi hassCalendarApi,
        IWazeTravelTimes wazeTravelTimes,
        IEntities entities,
        IServices services,
        IAnnouncementsService announcementsService,
        ILogger<AppointmentRemindersApp> logger,
        IConfiguration configuration)
    {
        this.fileSystem = fileSystem;
        this.scheduler = scheduler;
        this.hassCalendarApi = hassCalendarApi;
        this.wazeTravelTimes = wazeTravelTimes;
        this.entities = entities;
        this.services = services;
        this.announcementsService = announcementsService;
        this.logger = logger;

        disable = configuration.GetValue<bool>("DisableAnnouncements");

        var dataFolder = fileSystem.Path.GetFullPath(configuration.GetValue<string>("DataFolder"));
        fileSystem.Directory.CreateDirectory(dataFolder);
        dataFile = fileSystem.Path.GetFullPath("appointments.json", dataFolder);
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Observable.Interval(TimeSpan.FromMinutes(1), scheduler)
            .Select(_ => Observable.FromAsync(async () => await UpdateAppointmentsAndSendRemindersAsync()))
            .Concat()
            .Subscribe();

        return Task.CompletedTask;
    }

    private async Task UpdateAppointmentsAndSendRemindersAsync()
    {
        try
        {
            appointments = fileSystem.File.Exists(dataFile)
                ? JsonSerializer.Deserialize<List<Appointment>>(await fileSystem.File.ReadAllTextAsync(dataFile)) ?? new List<Appointment>()
                : new List<Appointment>();

            await UpdateAppointmentsAsync();

            await UpdateAppointmentCoordinatesAndTravelTimesAsync();

            SendAppointmentReminders();

            await fileSystem.File.WriteAllTextAsync(dataFile, JsonSerializer.Serialize(appointments, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while updating appointments and sending reminders");
            services.Notify.MobileAppPhoneChris(new NotifyMobileAppPhoneChrisParameters
            {
                Title = "Appointment Reminders Exception",
                Message = ex.Message,
            });
        }
    }

    private async Task UpdateAppointmentsAsync()
    {
        var homeAssistantAppointments = (await hassCalendarApi.GetAppointmentsAsync(FamilyCalendar, DateTime.Now.AddMinutes(-5), DateTime.Now.AddDays(30)))
            .Select(a => Appointment.Create(a, FamilyCalendar)).ToList();
        homeAssistantAppointments.AddRange((await hassCalendarApi.GetAppointmentsAsync(ScoutsCalendar, DateTime.Now, DateTime.Now.AddDays(30)))
            .Select(a => Appointment.Create(a, ScoutsCalendar)).ToList());

        // Remove old appointments
        appointments = appointments
            .Where(a => homeAssistantAppointments.Any(h => h.Id == a.Id))
            .ToList();

        // Add and update appointments
        foreach (var homeAssistantAppointment in homeAssistantAppointments)
        {
            var appointment = appointments.FirstOrDefault(a => a.Id == homeAssistantAppointment.Id);
            if (appointment == null)
            {
                appointments.Add(homeAssistantAppointment);
            }
            else
            {
                appointment.Update(homeAssistantAppointment);
            }
        }

        // Order by start time
        appointments = appointments.OrderBy(a => a.Start).ToList();
    }

    private async Task UpdateAppointmentCoordinatesAndTravelTimesAsync()
    {
        // We only update one appointment per cycle to limit the rate of calls to Waze
        var nextAppointmentToUpdate = appointments.FirstOrDefault(a => a.NeedsLocationCoordinates);
        if (nextAppointmentToUpdate != null)
        {
            nextAppointmentToUpdate.LocationCoordinates
                = (await wazeTravelTimes.GetAddressLocationFromAddressAsync(nextAppointmentToUpdate.OverrideLocation ?? nextAppointmentToUpdate.Location))?.Location
                ?? LocationCoordinates.Empty;
        }
        else
        {
            nextAppointmentToUpdate = appointments.FirstOrDefault(a => a.NeedsTravelTimeUpdate);
            if (nextAppointmentToUpdate != null)
            {
                var travelTime = await wazeTravelTimes.GetTravelTimeAsync(
                    HomeLocation,
                    nextAppointmentToUpdate.LocationCoordinates,
                    nextAppointmentToUpdate.ArriveTime);

                // If a Scouts appointment is more than 25 miles away it is pretty sure bet we are meeting at the Church so update the location and re-calculate the travel time
                if (nextAppointmentToUpdate.Calendar == ScoutsCalendar && travelTime?.Miles > 25)
                {
                    nextAppointmentToUpdate.LocationCoordinates = DefaultScoutsLocation;
                    travelTime = await wazeTravelTimes.GetTravelTimeAsync(HomeLocation, DefaultScoutsLocation, nextAppointmentToUpdate.ArriveTime);
                }

                nextAppointmentToUpdate.SetTravelTime(travelTime);
            }
        }
    }

    private void SendAppointmentReminders()
    {
        // We only do one reminder at a time
        var nextAppointmentToRemind = appointments.FirstOrDefault(a => a.ReminderIsDue);
        if (nextAppointmentToRemind != null)
        {
            if (!SkipNotifyFor(nextAppointmentToRemind))
            {
                announcementsService.SendAnnouncement(nextAppointmentToRemind.ReminderMessage, person: nextAppointmentToRemind.Calendar == ScoutsCalendar ? Mayson : null);
            }

            nextAppointmentToRemind.NextReminder = nextAppointmentToRemind.NextReminder switch
            {
                ReminderTypeV1.TwoHours => ReminderTypeV1.OneHour,
                ReminderTypeV1.OneHour => ReminderTypeV1.ThirtyMinutes,
                ReminderTypeV1.ThirtyMinutes => ReminderTypeV1.FifteenMinutes,
                ReminderTypeV1.FifteenMinutes => ReminderTypeV1.Now,
                _ => null,
            };
        }
    }

    private bool SkipNotifyFor(Appointment appointment)
    {
        if (disable)
        {
            return true;
        }
        else if (appointment.NextReminder == ReminderTypeV1.TwoHours && string.Equals(entities.BinarySensor.MelissaIsInBed.State, "on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        else if (appointment.Calendar == ScoutsCalendar && !string.Equals(entities.Person.Mayson.State, "Home", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        else if (appointment.Calendar == ScoutsCalendar && appointment.Summary?.Contains("cancel", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
