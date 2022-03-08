using System.IO.Abstractions;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using NetDaemon.Client.Settings;
using NuttyTree.NetDaemon.Apps.AppointmentReminders.HomeAssistant;
using Refit;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IServiceColectionExtensions
    {
        public static IServiceCollection AddAppointmentRemindersApp(this IServiceCollection services)
        {
            services.AddRefitClient<IHomeAssistantCalendarApi>()
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
}
