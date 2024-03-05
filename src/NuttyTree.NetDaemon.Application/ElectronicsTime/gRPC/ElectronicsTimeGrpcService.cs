using Grpc.Core;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;
using NuttyTree.NetDaemon.Infrastructure.Extensions;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

internal sealed class ElectronicsTimeGrpcService : ElectronicsTimeGrpc.ElectronicsTimeGrpcBase
{
    private readonly IOptionsMonitor<ElectronicsTimeOptions> options;

    private readonly IEntities homeAssistantEntities;

    public ElectronicsTimeGrpcService(IOptionsMonitor<ElectronicsTimeOptions> options, IEntities homeAssistantEntities)
    {
        this.options = options;
        this.homeAssistantEntities = homeAssistantEntities;
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

    public override async Task GetStatus(StatusRequest request, IServerStreamWriter<StatusResponse> responseStream, ServerCallContext context)
    {
        var updateStatusTrigger = new TaskCompletionSource();
        using var timer = new Timer(_ => updateStatusTrigger.TrySetResult(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        while (!context.CancellationToken.IsCancellationRequested)
        {
            var response = new StatusResponse
            {
                Mode = homeAssistantEntities.InputSelect.MaysonElectronicsMode.EntityState.AsEnum<ElectronicsMode>() ?? ElectronicsMode.Restricted,
                Location = homeAssistantEntities.DeviceTracker.PhoneMayson.State,
                IsDayTime = DateTime.Now.Hour >= 8 && DateTime.Now.Hour < 21,
                AvailableTime = homeAssistantEntities.Sensor.MaysonAvailableTime.State ?? 0,
                HasIncompleteTasks = homeAssistantEntities.Todo.Mayson.EntityState.AsInt() > 0 || homeAssistantEntities.Todo.MaysonReview.EntityState.AsInt() > 0,
            };
            await responseStream.WriteAsync(response, context.CancellationToken);

            await updateStatusTrigger.Task;
            updateStatusTrigger = new TaskCompletionSource();
        }
    }

    public override Task<DeviceStatusResponse> SendDeviceStatus(DeviceStatus request, ServerCallContext context)
    {
        return Task.FromResult(new DeviceStatusResponse());
    }
}
