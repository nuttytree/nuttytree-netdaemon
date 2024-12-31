using NuttyTree.NetDaemon.ExternalServices.Waze.Models;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

internal static class AppointmentConstants
{
    public const string FamilyCalendarEntityId = "calendar.family";

    public const string ScoutsCalendarEntityId = "calendar.troop_479";

    public const string Chris = nameof(Chris);

    public const string Melissa = nameof(Melissa);

    public const string Mayson = nameof(Mayson);

    public static readonly LocationCoordinates RidgewoodChurchLocation = new() { Latitude = 44.923126220703125, Longitude = -93.50469207763672 };

    public static readonly LocationCoordinates DefaultScoutsLocation = new() { Latitude = 44.83056640625, Longitude = -93.43046569824219 };
}
