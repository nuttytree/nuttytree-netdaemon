using Grpc.Core;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

internal sealed class ElectronicsTimeGrpcService : ElectronicsTimeGrpc.ElectronicsTimeGrpcBase
{
    private readonly IOptionsMonitor<ElectronicsTimeOptions> options;

    public ElectronicsTimeGrpcService(IOptionsMonitor<ElectronicsTimeOptions> options)
    {
        this.options = options;
    }

    public override async Task GetApplicationConfig(ApplicationConfigRequest request, IServerStreamWriter<ApplicationConfigResponse> responseStream, ServerCallContext context)
    {
        var updateConfigTrigger = new TaskCompletionSource();
        using var onChange = options.OnChange((_, _) => updateConfigTrigger.TrySetResult());

        while (!context.CancellationToken.IsCancellationRequested)
        {
            var response = new ApplicationConfigResponse
            {
                Applications =
                {
                    options.CurrentValue.Applications.Select(a => new Application
                    {
                        Name = a.Name,
                        AllowedWindowTitles = { a.AllowedWindowTitles },
                        DeniedWindowTitles = { a.DeniedWindowTitles },
                        AllowOffline = a.AllowOffline,
                        RequiresTime = a.RequiresTime,
                        AllowType = a.AllowType,
                        AllowedLocations = { a.AllowedLocations.Select(l => new AllowedLocation { Location = l.Location, AllowType = l.AllowType }) },
                    })
                }
            };
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
