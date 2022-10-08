using NetDaemon.Runtime;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.Logging;
using Serilog;

namespace NuttyTree.NetDaemon;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseLogging()
                .UseNetDaemonRuntime()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
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
