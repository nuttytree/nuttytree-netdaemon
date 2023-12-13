using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.AppointmentReminders;
using NuttyTree.NetDaemon.ExternalServices.Frigate;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar;
using NuttyTree.NetDaemon.ExternalServices.RandomWords;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;
using NuttyTree.NetDaemon.Infrastructure.Scheduler;

namespace NuttyTree.NetDaemon.Application;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Applications
        services
            .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
            .AddAnnouncementsService()
            .AddAppointmentRemindersApp();

        // External Services
        services
            .AddHomeAssistantCalendar()
            .AddRandomWords()
            .AddWaze()
            .AddFrigate();

        // Infrastructure
        services
            .AddDatabase()
            .AddHomeAssistantEntitiesAndServices()
            .AddRateLimiter()
            .AddTaskScheduler();

        return services;
    }
}
