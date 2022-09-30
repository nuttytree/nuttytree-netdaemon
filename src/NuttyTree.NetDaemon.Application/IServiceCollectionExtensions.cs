using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NetDaemon.AppModel;
using NuttyTree.NetDaemon.ExternalServices.RandomWords;
using NuttyTree.NetDaemon.ExternalServices.Waze;

namespace NuttyTree.NetDaemon.Application;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services
            .AddAppsFromAssembly(Assembly.GetExecutingAssembly());

        services
            .AddAppointmentRemindersApp();

        services
            .AddWaze()
            .AddRandomWords();

        return services;
    }
}
