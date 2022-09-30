using NetDaemon.Extensions.Scheduler;
using NetDaemon.Runtime;
using NuttyTree.NetDaemon.Application;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .ConfigureNetDaemonServices(Configuration)
            .AddNetDaemonStateManager()
            .AddNetDaemonScheduler()
            .AddScoped<IEntities, Entities>()
            .AddScoped<IServices, Services>();

        services.AddApplication();

        services.AddRazorPages();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
        });
    }
}
