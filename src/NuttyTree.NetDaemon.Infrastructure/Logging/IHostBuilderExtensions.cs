using Microsoft.Extensions.Hosting;
using Serilog;

namespace NuttyTree.NetDaemon.Infrastructure.Logging;

public static class IHostBuilderExtensions
{
    public static IHostBuilder UseLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, config) =>
        {
            config.WriteTo.Async(sinkConfig =>
            {
                sinkConfig.Console();
            });
        });
    }
}
