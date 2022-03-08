namespace NuttyTree.NetDaemon.Apps.AppointmentReminders.HomeAssistant.Models
{
    internal class HomeAssistantAppointment
    {
        public string? Id { get; set; }

        public string? Summary { get; set; }

        public string? Location { get; set; }

        public HomeAssistantAppointmentDateTime? Start { get; set; }

        public HomeAssistantAppointmentDateTime? End { get; set; }

        public DateTime Created { get; set; }

        public DateTime Updated { get; set; }

        public bool IsAllDay => Start?.DateTime == null;
    }
}
