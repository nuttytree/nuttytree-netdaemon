using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using NuttyTree.NetDaemon.Application.AppointmentReminders.HomeAssistant.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Models
{
    internal class Appointment
    {
        public string? Id { get; set; }

        public string? Summary { get; set; }

        public string? Description { get; set; }

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

        public ReminderTypeV1? NextReminder { get; set; }

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
#pragma warning disable CA5394 // Do not use insecure randomness
#pragma warning disable SA1118 // Parameter should not span multiple lines
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} you {1} {2} {3}",
                    ReminderPrefixes[new Random().Next(ReminderPrefixes.Count)],
                    IsAtHome ? "have" : "need to leave for",
                    Summary,
                    NextReminder switch
                    {
                        ReminderTypeV1.TwoHours => $"in {(IsAtHome ? null : "approximately")} 2 hours",
                        ReminderTypeV1.OneHour => $"in {(IsAtHome ? null : "approximately")} 1 hour",
                        ReminderTypeV1.ThirtyMinutes => $"in {(IsAtHome ? null : "approximately")} 30 minutes",
                        ReminderTypeV1.FifteenMinutes => $"in {(IsAtHome ? null : "approximately")} 15 minutes",
                        ReminderTypeV1.Now => $"right now",
                        _ => string.Empty,
                    });
#pragma warning restore SA1118 // Parameter should not span multiple lines
#pragma warning restore CA5394 // Do not use insecure randomness
            }
        }

        public static Appointment Create(HomeAssistantAppointment hassAppointment, string calendar)
        {
            var appointment = new Appointment
            {
                Id = GenerateId(hassAppointment),
                Summary = hassAppointment.Summary,
                Description = hassAppointment.Description,
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

        public void Update(Appointment hassAppointment)
        {
            Description = hassAppointment.Description;
        }

        public void SetTravelTime(TravelTime? travelTime)
        {
            if (travelTime != null)
            {
                TravelMiles = travelTime.Miles;
                TravelMinutes = travelTime.Minutes;
                if (LastTravelTimeUpdate == DateTime.MinValue)
                {
                    if (MinutesTillLeaveTime > (int)ReminderTypeV1.TwoHours)
                    {
                        NextReminder = ReminderTypeV1.TwoHours;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderTypeV1.OneHour)
                    {
                        NextReminder = ReminderTypeV1.OneHour;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderTypeV1.ThirtyMinutes)
                    {
                        NextReminder = ReminderTypeV1.ThirtyMinutes;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderTypeV1.FifteenMinutes)
                    {
                        NextReminder = ReminderTypeV1.FifteenMinutes;
                    }
                    else if (MinutesTillLeaveTime > (int)ReminderTypeV1.Now)
                    {
                        NextReminder = ReminderTypeV1.Now;
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
