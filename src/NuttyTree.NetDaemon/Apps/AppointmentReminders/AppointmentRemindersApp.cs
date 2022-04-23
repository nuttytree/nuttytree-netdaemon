using System.IO.Abstractions;
using System.Text.Json;
using HomeAssistantGenerated;
using Microsoft.Extensions.Configuration;
using NetDaemon.Extensions.Scheduler;
using NuttyTree.NetDaemon.Apps.AppointmentReminders.HomeAssistant;
using NuttyTree.NetDaemon.Apps.AppointmentReminders.Models;
using NuttyTree.NetDaemon.Waze;
using NuttyTree.NetDaemon.Waze.Models;
using static NuttyTree.NetDaemon.Apps.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Apps.AppointmentReminders
{
    [NetDaemonApp]
    internal class AppointmentRemindersApp : IAsyncInitializable
    {
        private readonly List<string> reminderNotices = new List<string>
        {
            "Just a friendly reminder",
            "Don't forget that",
            "Remember that"
        };

        private readonly IFileSystem fileSystem;

        private readonly INetDaemonScheduler scheduler;

        private readonly IHomeAssistantCalendarApi hassCalendarApi;

        private readonly IWazeTravelTimes wazeTravelTimes;

        private readonly IEntities entities;

        private readonly IServices services;

        private readonly ILogger<AppointmentRemindersApp> logger;

        private readonly string dataFile;

        private List<Appointment> appointments = new List<Appointment>();

        public AppointmentRemindersApp(
            IConfiguration configuration,
            IFileSystem fileSystem,
            INetDaemonScheduler scheduler,
            IHomeAssistantCalendarApi hassCalendarApi,
            IWazeTravelTimes wazeTravelTimes,
            IEntities entities,
            IServices services,
            ILogger<AppointmentRemindersApp> logger)
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
            if (fileSystem.File.Exists(dataFile))
            {
                appointments = JsonSerializer.Deserialize<List<Appointment>>(await fileSystem.File.ReadAllTextAsync(dataFile, cancellationToken)) ?? new List<Appointment>();
            }
            else
            {
                appointments = new List<Appointment>();
            }

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            scheduler.RunEvery(TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow, async () => await UpdateAppointmentsAndSendRemindersAsync());
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
        }

        private async Task UpdateAppointmentsAndSendRemindersAsync()
        {
            try
            {
                await UpdateAppointmentsAsync();

                await UpdateAppointmentCoordinatesAndTravelTimesAsync();

                SendAppointmentReminders();

                await fileSystem.File.WriteAllTextAsync(dataFile, JsonSerializer.Serialize(appointments, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while updating appointments and sending reminders");
            }
        }

        private async Task UpdateAppointmentsAsync()
        {
            var homeAssistantAppointments = (await hassCalendarApi.GetAppointmentsAsync(FamilyCalendar, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddDays(30)))
                .Select(a => Appointment.Create(a, FamilyCalendar)).ToList();
            homeAssistantAppointments.AddRange((await hassCalendarApi.GetAppointmentsAsync(ScoutsCalendar, DateTime.UtcNow, DateTime.UtcNow.AddDays(30)))
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
#pragma warning disable CA5394 // Do not use insecure randomness
                    var reminderNotice = reminderNotices[new Random().Next(reminderNotices.Count)];
#pragma warning restore CA5394 // Do not use insecure randomness

                    services.Notify.AlexaMediaDevicesInside(new NotifyAlexaMediaDevicesInsideParameters
                    {
                        Data = new { type = "announce" },
                        Message = $"{reminderNotice} {nextAppointmentToRemind.ReminderMessage}",
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
            if (appointment.NextReminder == ReminderType.TwoHours && string.Equals(entities.BinarySensor.MasterBedMelissa.State, "on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (appointment.Calendar == ScoutsCalendar && !string.Equals(entities.Person.Mayson.State, "Home", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
