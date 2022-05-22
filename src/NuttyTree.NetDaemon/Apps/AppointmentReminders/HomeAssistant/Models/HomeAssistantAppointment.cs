namespace NuttyTree.NetDaemon.Apps.AppointmentReminders.HomeAssistant.Models
{
    internal class HomeAssistantAppointment
    {
        public string? Summary { get; set; }

        public string? Description { get; set; }

        public string? Location { get; set; }

        public HomeAssistantAppointmentDateTime? Start { get; set; }

        public HomeAssistantAppointmentDateTime? End { get; set; }

        public bool IsAllDay => Start?.DateTime == null;
    }
}
