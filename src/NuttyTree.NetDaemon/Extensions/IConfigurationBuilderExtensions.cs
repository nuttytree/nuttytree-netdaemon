using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NetDaemon.AppModel;

namespace NuttyTree.NetDaemon.Extensions;

internal static class IConfigurationBuilderExtensions
{
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1010:Opening square brackets should be spaced correctly", Justification = "Can't make the analyzer happy")]
    public static IConfigurationBuilder AddReloadableYamlAppConfigs(this IConfigurationBuilder builder, IConfiguration configuration)
    {
        var appConfigurationLocationSetting = configuration.GetSection("NetDaemon")?.Get<AppConfigurationLocationSetting>();
        if (!string.IsNullOrEmpty(appConfigurationLocationSetting?.ApplicationConfigurationFolder))
        {
            var fullPath = Path.GetFullPath(appConfigurationLocationSetting.ApplicationConfigurationFolder);
            var addYamlMethod = typeof(ConfigurationBuilderExtensions).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .First(m => m.GetParameters().Length == 4);
            foreach (var file in Directory.EnumerateFiles(fullPath, "*.y*", SearchOption.AllDirectories))
            {
                addYamlMethod!.Invoke(null, parameters: [builder, file, false, true]);
            }
        }

        return builder;
    }
}
