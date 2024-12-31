using NetDaemon.Runtime;

namespace NuttyTree.NetDaemon.Extensions;

internal static class IHostBuilderExtensions
{
    public static IHostBuilder UseReloadableYamlAppSettings(this IHostBuilder hostBuilder)
    {
        return hostBuilder
            .RegisterAppSettingsJsonToHost()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddReloadableYamlAppConfigs(context.Configuration);
            });
    }
}
