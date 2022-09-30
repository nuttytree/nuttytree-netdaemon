using NetDaemon.Runtime;
using NuttyTree.NetDaemon;
using NuttyTree.NetDaemon.Infrastructure.Logging;

await Host.CreateDefaultBuilder(args)
    .UseLogging()
    .UseNetDaemonRuntime()
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseStartup<Startup>();
    })
    .Build()
    .RunAsync();
