using Microsoft.Extensions.DependencyInjection;

namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant
{
    public static class IServiceColectionExtensions
    {
        public static IServiceCollection AddHomeAssistantEntitiesAndServices(this IServiceCollection services)
        {
            return services
                .AddScoped<IEntities, Entities>()
                .AddScoped<IServices, Services>();
        }
    }
}
