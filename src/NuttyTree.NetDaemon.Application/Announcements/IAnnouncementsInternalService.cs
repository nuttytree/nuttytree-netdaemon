using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Application.Announcements;

internal interface IAnnouncementsInternalService : IAnnouncementsService
{
    void Initialize(IEntities homeAssistantEntities, IHaContext haContext, IServices homeAssistantServices);
}
