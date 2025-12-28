using Microsoft.OpenApi.Models;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Runtime;
using NuttyTree.NetDaemon.Application;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Extensions;
using Serilog;

namespace NuttyTree.NetDaemon;

internal sealed class Startup()
{
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .ConfigureNetDaemonServices()
            .AddNetDaemonStateManager()
            .AddNetDaemonScheduler();

        services.AddApplications();

        services.AddMvc();

        services.AddHealthChecks();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Nuttytree NetDaemon Service", Version = "v1" });
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapElectronicsTimeGrpcService();
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
            endpoints.MapSwagger();
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "Nuttytree NetDaemon Service");
        });
    }
}
