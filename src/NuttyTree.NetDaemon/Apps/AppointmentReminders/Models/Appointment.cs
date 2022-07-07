using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using NuttyTree.NetDaemon.Apps.AppointmentReminders.HomeAssistant.Models;
using NuttyTree.NetDaemon.Waze.Models;
using static NuttyTree.NetDaemon.Apps.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Apps.AppointmentReminders.Models
{
    internal class Appointment
    {
        public string? Id { get; set; }

        public string? Summary { get; set; }

        public string? Location { get; set; }

        public DateTime Start { get; set; }

        public DateTime? End { get; set; }

        public bool IsAllDay { get; set; }

        public string? Calendar { get; set; }

        public string? OverrideLocation { get; set; }

        public LocationCoordinates? LocationCoordinates { get; set; }

        public double? TravelMiles { get; set; }

        public double? TravelMinutes { get; set; }

        public DateTime LastTravelTimeUpdate { get; set; }

        public ReminderType? NextReminder { get; set; }

        public bool Ignore { get; set; }

        [JsonIgnore]
        public bool IsAtHome => HomeLocation.Equals(LocationCoordinates);

        [JsonIgnore]
        public DateTime LeaveTime => ArriveTime.AddMinutes(-1 * TravelMinutes ?? 0);

        [JsonIgnore]
        public DateTime ArriveTime => IsAtHome ? Start : Start.AddMinutes(-1 * AppointmentLeadTimeMinutes);

        [JsonIgnore]
        public double MinutesTillLeaveTime => (LeaveTime - DateTime.Now).TotalMinutes;

        [JsonIgnore]
        public bool HasLocation => !string.IsNullOrWhiteSpace(OverrideLocation ?? Location);

        [JsonIgnore]
        public bool NeedsLocationCoordinates =>
            HasLocation
            && LocationCoordinates == null;

        [JsonIgnore]
        public bool NeedsTravelTimeUpdate =>
            !IsAllDay
            && !IsAtHome
            && !NeedsLocationCoordinates
            && !Ignore
            && (LastTravelTimeUpdate == DateTime.MinValue ||
                (TravelMinutes != null
                    && DateTime.Now.AddMinutes(120 + (TravelMinutes.Value * 2) + AppointmentLeadTimeMinutes) >= Start
                    && DateTime.Now - LastTravelTimeUpdate >= TimeSpan.FromMinutes(TravelTimeUpdateIntervalMinutes)));

        [JsonIgnore]
        public bool ReminderIsDue => !IsAllDay && !Ignore && TravelMinutes != null && NextReminder != null && MinutesTillLeaveTime <= (double)NextReminder;

        [JsonIgnore]
        public string ReminderMessage
        {
            get
            {
                var homeOrLeaveMessage = IsAtHome ? $"you have {Summary}" : $"you need to leave for {Summary}";
                return NextReminder switch
                {
                    ReminderType.TwoHours => $"{homeOrLeaveMessage} in {(IsAtHome ? null : "approximately")} 2 hours",
                    ReminderType.OneHour => $"{homeOrLeaveMessage} in {(IsAtHome ? null : "approximately")} 1 hour",
                    ReminderType.ThirtyMinutes => $"{homeOrLeaveMessage} in {(IsAtHome ? null : "approximately")} 30 minutes",
                    ReminderType.FifteenMinutes => $"{homeOrLeaveMessage} in {(IsAtHome ? null : "approximately")} 15 minutes",
                    ReminderType.Now => $"{homeOrLeaveMessage} right now",
                    _ => string.Empty,
                };
            }
        }

        public static Appointment Create(HomeAssistantAppointment hassAppointment, string calendar)
        {
            var appointment = new Appointment
            {
                Id = GenerateId(hassAppointment),
                Summary = hassAppointment.Summary,
                Location = hassAppointment.Location,
                Start = hassAppointment.Start?.DateTime ?? hassAppointment.Start?.Date ?? DateTime.MinValue,
                End = hassAppointment.End?.DateTime ?? hassAppointment.End?.Date,
                IsAllDay = hassAppointment.IsAllDay
                    || (hassAppointment.Start?.DateTime?.Hour == 0 && hassAppointment.Start?.DateTime?.Minute == 0 && hassAppointment.End?.DateTime?.Hour == 23 && hassAppointment.End?.DateTime?.Minute >= 55),
                Calendar = calendar,
            };

            appointment.CheckForKnownLocationCoordinates();

            return appointment;
        }

        public void SetTravelTime(TravelTime? travelTime)
        {
            if (travelTime != null)
            {
                TravelMiles = travelTime.Miles;
                TravelMinutes = travelTime.Minutes;
                if (LastTravelTimeUpdate == DateTime.MinValue)
                {
                    if (MinutesTillLeaveTime > (int)ReminderType.TwoHours)
                    {
                        NextReminder = ReminderType.TwoHours;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderType.OneHour)
                    {
                        NextReminder = ReminderType.OneHour;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderType.ThirtyMinutes)
                    {
                        NextReminder = ReminderType.ThirtyMinutes;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderType.FifteenMinutes)
                    {
                        NextReminder = ReminderType.FifteenMinutes;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderType.Now)
                    {
                        NextReminder = ReminderType.Now;
                    }
                }
            }

            LastTravelTimeUpdate = DateTime.Now;
        }

        private static string GenerateId(HomeAssistantAppointment appointment)
        {
            using var sha256 = SHA256.Create();
            var data = sha256.ComputeHash(Encoding.UTF8.GetBytes($"{appointment.Summary}.{appointment.Location}.{appointment.Start?.DateTime ?? appointment.Start?.Date}")).ToList();
            var stringBuilder = new StringBuilder();
            data.ForEach(b => stringBuilder.Append(b.ToString("x2", CultureInfo.InvariantCulture)));
            return stringBuilder.ToString();
        }

        private void CheckForKnownLocationCoordinates()
        {
            if (Location == null)
            {
                return;
            }
            else if (Calendar == FamilyCalendar &&
                (Location.Replace(" ", string.Empty, StringComparison.Ordinal)?.Contains("18275Evener", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(Location, "Home", StringComparison.OrdinalIgnoreCase)))
            {
                // If the location is home set the home location
                LocationCoordinates = HomeLocation;
            }
            else if (Calendar == ScoutsCalendar &&
                (Location.Contains("Various Sites in EP", StringComparison.OrdinalIgnoreCase)
                || Location.Contains("Other or TBD", StringComparison.OrdinalIgnoreCase)
                || Location.Contains("Pax Christi", StringComparison.OrdinalIgnoreCase)))
            {
                // If any these values are in the location odds are it will be at the Church or somewhere close so drive time will be similar
                LocationCoordinates = DefaultScoutsLocation;
            }
            else if (Calendar == ScoutsCalendar &&
                (Location.Contains("Zoom", StringComparison.OrdinalIgnoreCase)
                || Location.Contains("Online", StringComparison.OrdinalIgnoreCase)))
            {
                // If it is online it will be at home
                LocationCoordinates = HomeLocation;
            }

            if (IsAtHome)
            {
                SetTravelTime(new TravelTime(0, 0));
            }
        }
    }
}
