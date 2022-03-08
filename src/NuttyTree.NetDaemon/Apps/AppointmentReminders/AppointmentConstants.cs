using NuttyTree.NetDaemon.Waze.Models;

namespace NuttyTree.NetDaemon.Apps.AppointmentReminders
{
    internal static class AppointmentConstants
    {
        public const string FamilyCalendar = "family";

        public const string ScoutsCalendar = "troop_479";

        public const int AppointmentLeadTimeMinutes = 5;

        public const int TravelTimeUpdateIntervalMinutes = 5;

        public static readonly LocationCoordinates HomeLocation = new LocationCoordinates { Latitude = 44.86762237548828, Longitude = -93.50953674316406 };

        public static readonly LocationCoordinates DefaultScoutsLocation = new LocationCoordinates { Latitude = 44.83056640625, Longitude = -93.43046569824219 };
    }
}
