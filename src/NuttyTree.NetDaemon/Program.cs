using System.Diagnostics.CodeAnalysis;
using NetDaemon.Runtime;
using NuttyTree.NetDaemon.Extensions;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.Logging;
using Serilog;

namespace NuttyTree.NetDaemon;

public static class Program
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top level try catch")]
    [SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "CloseAndFlushAsync tends to not actually flush the last log message")]
    [SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "CloseAndFlushAsync tends to not actually flush the last log message")]
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseLogging()
                .UseNetDaemonRuntime()
                .UseReloadableYamlAppSettings()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.AllowAlternateSchemes = true;
                    });
                })
                .Build();
            await host.MigrateDatabaseAsync();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start the NuttyTree NetDaemon service");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }

        return 0;
    }
}
