using Grpc.Core;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

internal sealed class ElectronicsTimeGrpcService : ElectronicsTimeGrpc.ElectronicsTimeGrpcBase
{
    private readonly IOptionsMonitor<ElectronicsTimeOptions> options;

    private TaskCompletionSource updateConfigTrigger;

    public ElectronicsTimeGrpcService(IOptionsMonitor<ElectronicsTimeOptions> options)
    {
        this.options = options;
        updateConfigTrigger = new TaskCompletionSource();
        options.OnChange((_, _) => updateConfigTrigger.TrySetResult());
    }

    public override async Task GetApplicationConfig(ApplicationConfigRequest request, IServerStreamWriter<ApplicationConfigResponse> responseStream, ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var response = new ApplicationConfigResponse();
            response.Applications.AddRange(options.CurrentValue.Applications.Select(a =>
            {
                var app = new Application
                {
                    Name = a.Name,
                    RequiresTime = a.RequiresTime,
                    AllowType = a.AllowType,
                };
                app.AllowedWindowTitles.AddRange(a.AllowedWindowTitles);
                app.DeniedWindowTitles.AddRange(a.DeniedWindowTitles);
                return app;
            }));
            await responseStream.WriteAsync(response, context.CancellationToken);

            await updateConfigTrigger.Task;
            updateConfigTrigger = new TaskCompletionSource();
        }
    }

    public override Task<DeviceStatusResponse> SendDeviceStatus(DeviceStatus request, ServerCallContext context)
    {
        return Task.FromResult(new DeviceStatusResponse());
    }
}
