using Microsoft.Extensions.DependencyInjection;

namespace NuttyTree.NetDaemon.Infrastructure.Scheduler
{
    public static class IServiceColectionExtensions
    {
        public static IServiceCollection AddTaskScheduler(this IServiceCollection services)
        {
            return services
                .AddTransient<ITaskScheduler, TaskScheduler>();
        }
    }
}
