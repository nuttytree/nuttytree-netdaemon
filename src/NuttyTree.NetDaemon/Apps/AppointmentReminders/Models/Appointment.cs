﻿using System.Text.Json.Serialization;
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

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public bool IsAllDay { get; set; }

        public string? Calendar { get; set; }

        public string? OverrideLocation { get; set; }

        public LocationCoordinates? LocationCoordinates { get; set; }

        public double? TravelMiles { get; set; }

        public double? TravelMinutes { get; set; }

        public DateTime LastTravelTimeUpdate { get; set; }

        public ReminderType? NextReminder { get; set; }

        [JsonIgnore]
        public bool IsAtHome => LocationCoordinates == HomeLocation;

        [JsonIgnore]
        public DateTime LeaveTime => ArriveTime.AddMinutes(-1 * TravelMinutes ?? 0);

        [JsonIgnore]
        public DateTime ArriveTime => IsAtHome ? Start : Start.AddMinutes(-1 * AppointmentLeadTimeMinutes);

        [JsonIgnore]
        public double MinutesTillLeaveTime => (LeaveTime - DateTime.UtcNow).TotalMinutes;

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
            && (LastTravelTimeUpdate == DateTime.MinValue ||
                (TravelMinutes != null
                    && DateTime.UtcNow.AddMinutes(120 + (TravelMinutes.Value * 2) + AppointmentLeadTimeMinutes) >= Start
                    && DateTime.UtcNow - LastTravelTimeUpdate >= TimeSpan.FromMinutes(TravelTimeUpdateIntervalMinutes)));

        [JsonIgnore]
        public bool ReminderIsDue => !IsAllDay && TravelMinutes != null && NextReminder != null && MinutesTillLeaveTime <= (double)NextReminder;

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
                Id = hassAppointment.Id,
                Summary = hassAppointment.Summary,
                Location = hassAppointment.Location,
                Start = hassAppointment.Start?.DateTime ?? hassAppointment.Start?.Date ?? DateTime.MinValue,
                End = hassAppointment.End?.DateTime ?? hassAppointment.End?.Date,
                Created = hassAppointment.Created,
                Updated = hassAppointment.Updated,
                IsAllDay = hassAppointment.IsAllDay,
                Calendar = calendar,
            };

            appointment.CheckForKnownLocationCoordinates();

            return appointment;
        }

        public void Update(Appointment hassAppointment)
        {
            if (Updated != hassAppointment.Updated)
            {
                Summary = hassAppointment.Summary;
                Start = hassAppointment.Start;
                End = hassAppointment.End;
                Updated = hassAppointment.Updated;
                IsAllDay = hassAppointment.IsAllDay;
                if (Location != hassAppointment.Location)
                {
                    Location = hassAppointment.Location;
                    LocationCoordinates = null;
                    TravelMiles = null;
                    TravelMinutes = null;
                    LastTravelTimeUpdate = DateTime.MinValue;
                    CheckForKnownLocationCoordinates();
                }
            }
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

            LastTravelTimeUpdate = DateTime.UtcNow;
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
