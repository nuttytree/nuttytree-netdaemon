using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.Application.Announcements;
using NuttyTree.NetDaemon.Application.AppointmentReminders;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Extensions;
using NuttyTree.NetDaemon.Application.GuestPasswordUpdate;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantWebhook;
using NuttyTree.NetDaemon.ExternalServices.Unifi;
using NuttyTree.NetDaemon.ExternalServices.Waze;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;
using NuttyTree.NetDaemon.Infrastructure.Scheduler;

namespace NuttyTree.NetDaemon.Application;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddApplications(this IServiceCollection services)
    {
        // Applications
        services
            .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
            .AddAnnouncementsService()
            .AddAppointmentRemindersApp()
            .AddElectronicsTimeApp()
            .AddGuestPasswordUpdateApp();

        // External Services
        services
            .AddHomeAssistantWebhooks()
            .AddUnifi()
            .AddWaze();

        // Infrastructure
        services
            .AddDatabase()
            .AddHomeAssistantEntitiesAndServices()
            .AddRateLimiter()
            .AddTaskScheduler();

        return services;
    }
}
