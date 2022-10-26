using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.Application.AppointmentReminders.Options;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddAppointmentRemindersApp(this IServiceCollection services)
    {
        services.AddOptions<AppointmentRemindersOptions>()
            .BindConfiguration(nameof(AppointmentReminders));

        return services;
    }
}
