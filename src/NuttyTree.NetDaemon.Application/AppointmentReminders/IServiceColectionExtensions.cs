using System.IO.Abstractions;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetDaemon.Client.Settings;
using NuttyTree.NetDaemon.Application.AppointmentReminders.HomeAssistant;
using Refit;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddAppointmentRemindersApp(this IServiceCollection services)
    {
        services.AddRefitClient<IHomeAssistantCalendarApi>()
            .AddDefaultRetryPolicy()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<HomeAssistantSettings>>().Value;
                client.BaseAddress = new UriBuilder(settings.Ssl ? "https" : "http", settings.Host, settings.Port).Uri;
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
            });

        return services
            .AddSingleton<IFileSystem, FileSystem>();
    }
}
