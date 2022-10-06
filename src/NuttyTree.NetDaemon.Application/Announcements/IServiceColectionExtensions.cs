using Microsoft.Extensions.DependencyInjection;

namespace NuttyTree.NetDaemon.Application.Announcements;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddAnnouncementsService(this IServiceCollection services)
    {
        return services
            .AddSingleton<IAnnouncementsService, AnnouncementsService>();
    }
}
