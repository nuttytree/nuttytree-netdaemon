using NuttyTree.NetDaemon.ExternalServices.Waze.Models;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders
{
    internal static class AppointmentConstants
    {
        public const string FamilyCalendar = "family";

        public const string ScoutsCalendar = "troop_479";

        public const string Chris = nameof(Chris);

        public const string Melissa = nameof(Melissa);

        public const string Mayson = nameof(Mayson);

        public const int AppointmentLeadTimeMinutes = 5;

        public const int TravelTimeUpdateIntervalMinutes = 5;

        public static readonly LocationCoordinates HomeLocation = new LocationCoordinates { Latitude = 44.867370866233676, Longitude = -93.50943597588578 };

        public static readonly LocationCoordinates RidgewoodChurchLocation = new LocationCoordinates { Latitude = 44.923126220703125, Longitude = -93.50469207763672 };

        public static readonly LocationCoordinates DefaultScoutsLocation = new LocationCoordinates { Latitude = 44.83056640625, Longitude = -93.43046569824219 };

        public static readonly List<string> ReminderPrefixes = new List<string>
        {
            "Just a friendly reminder",
            "Don't forget that",
            "Remember that"
        };
    }
}
