using Microsoft.Extensions.DependencyInjection;

namespace NuttyTree.NetDaemon.Infrastructure.Scheduler
{
    public static class IServiceColectionExtensions
    {
        public static IServiceCollection AddPeriodicScheduler(this IServiceCollection services)
        {
            return services
                .AddTransient<IPeriodicTaskScheduler, PeriodicTaskScheduler>();
        }
    }
}
