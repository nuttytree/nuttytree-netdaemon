using System.Globalization;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace NuttyTree.NetDaemon.Infrastructure.Logging;

public static class IHostBuilderExtensions
{
    public static IHostBuilder UseLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, config) =>
        {
            config.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning);
            config.MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
            config.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning);
            config.WriteTo.Async(sinkConfig =>
            {
                sinkConfig.Console(formatProvider: CultureInfo.CurrentCulture, outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
            });
        });
    }
}
