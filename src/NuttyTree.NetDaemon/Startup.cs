using Microsoft.OpenApi.Models;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Runtime;
using NuttyTree.NetDaemon.Application;
using Serilog;

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
            .AddNetDaemonScheduler();

        services.AddApplication();

        services.AddRazorPages();

        services.AddMvc();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Nuttytree NetDaemon Service", Version = "v1" });
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseStaticFiles();

        app.UseSerilogRequestLogging();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapRazorPages();
            endpoints.MapSwagger();
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Nuttytree NetDaemon Service");
        });
    }
}
