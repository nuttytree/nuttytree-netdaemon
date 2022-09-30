using System.IO.Abstractions;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using HomeAssistantGenerated;
using Microsoft.Extensions.Configuration;
using NetDaemon.AppModel;
using NuttyTree.NetDaemon.Application.AppointmentReminders.HomeAssistant;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

[NetDaemonApp]
internal class AppointmentRemindersApp : IAsyncInitializable
{
    private readonly IFileSystem fileSystem;

    private readonly IHomeAssistantCalendarApi hassCalendarApi;

    private readonly IWazeTravelTimes wazeTravelTimes;

    private readonly IEntities entities;

    private readonly IServices services;

    private readonly IScheduler scheduler;

    private readonly ILogger<AppointmentRemindersApp> logger;

    private readonly string dataFile;

    private List<Appointment> appointments = new List<Appointment>();

    public AppointmentRemindersApp(
        IFileSystem fileSystem,
        IScheduler scheduler,
        IHomeAssistantCalendarApi hassCalendarApi,
        IWazeTravelTimes wazeTravelTimes,
        IEntities entities,
        IServices services,
        ILogger<AppointmentRemindersApp> logger,
        IConfiguration configuration)
    {
        this.fileSystem = fileSystem;
        this.scheduler = scheduler;
        this.hassCalendarApi = hassCalendarApi;
        this.wazeTravelTimes = wazeTravelTimes;
        this.entities = entities;
        this.services = services;
        this.logger = logger;

        var dataFolder = fileSystem.Path.GetFullPath(configuration.GetValue<string>("DataFolder"));
        fileSystem.Directory.CreateDirectory(dataFolder);
        dataFile = fileSystem.Path.GetFullPath("appointments.json", dataFolder);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await UpdateAppointmentsAndSendRemindersAsync();

        Observable.Interval(TimeSpan.FromMinutes(1), scheduler)
            .Select(_ => Observable.FromAsync(async () => await UpdateAppointmentsAndSendRemindersAsync()))
            .Concat()
            .Subscribe();
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
                services.Notify.AlexaMediaDevicesInside(new NotifyAlexaMediaDevicesInsideParameters
                {
                    Data = new { type = "announce" },
                    Message = nextAppointmentToRemind.ReminderMessage,
                });
            }

            nextAppointmentToRemind.NextReminder = nextAppointmentToRemind.NextReminder switch
            {
                ReminderType.TwoHours => ReminderType.OneHour,
                ReminderType.OneHour => ReminderType.ThirtyMinutes,
                ReminderType.ThirtyMinutes => ReminderType.FifteenMinutes,
                ReminderType.FifteenMinutes => ReminderType.Now,
                _ => null,
            };
        }
    }

    private bool SkipNotifyFor(Appointment appointment)
    {
        if (appointment.NextReminder == ReminderType.TwoHours && string.Equals(entities.BinarySensor.MelissaIsInBed.State, "on", StringComparison.OrdinalIgnoreCase))
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
