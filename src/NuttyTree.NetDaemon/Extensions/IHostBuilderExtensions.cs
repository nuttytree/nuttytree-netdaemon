using NetDaemon.AppModel;
using NetDaemon.Runtime;

namespace NuttyTree.NetDaemon.Extensions;

public static class IHostBuilderExtensions
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
