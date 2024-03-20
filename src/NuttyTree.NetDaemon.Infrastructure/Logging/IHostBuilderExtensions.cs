using System.Globalization;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NuttyTree.NetDaemon.Infrastructure.Logging;

public static class IHostBuilderExtensions
{
    public static IHostBuilder UseLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, config) =>
        {
            config.MinimumLevel.Debug();
            config.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
            config.MinimumLevel.Override("Serilog", LogEventLevel.Warning);
            config.MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
            config.WriteTo.Async(sinkConfig =>
            {
                sinkConfig.Console(theme: AnsiConsoleTheme.Sixteen, formatProvider: CultureInfo.CurrentCulture);
            });
        });
    }
}
