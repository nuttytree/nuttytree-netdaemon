using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Application.Announcements;

[NetDaemonApp]
internal sealed class AnnouncementsApp
{
    public AnnouncementsApp(IAnnouncementsInternalService announcementsService, IEntities homeAssistantEntities, IHaContext haContext, IServices homeAssistantServices)
    {
        announcementsService.Initialize(homeAssistantEntities, haContext, homeAssistantServices);
    }
}
