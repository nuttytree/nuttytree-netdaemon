﻿using FluentDateTime;
using Grpc.Core;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantWebhook;
using NuttyTree.NetDaemon.Infrastructure.Extensions;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

#pragma warning disable SA1300 // Element should begin with upper-case letter
namespace NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;
#pragma warning restore SA1300 // Element should begin with upper-case letter

internal sealed class ElectronicsTimeGrpcService(
    IOptionsMonitor<ElectronicsTimeOptions> options,
    IEntities homeAssistantEntities,
    IHomeAssistantWebhookApi homeAssistantWebhook)
        : ElectronicsTimeGrpc.ElectronicsTimeGrpcBase
{
    private readonly IOptionsMonitor<ElectronicsTimeOptions> options = options;

    private readonly IEntities homeAssistantEntities = homeAssistantEntities;

    private readonly IHomeAssistantWebhookApi homeAssistantWebhook = homeAssistantWebhook;

    public override async Task GetApplicationConfig(ApplicationConfigRequest request, IServerStreamWriter<ApplicationConfigResponse> responseStream, ServerCallContext context)
    {
        var updateConfigTrigger = new TaskCompletionSource();
        using var onChange = options.OnChange((_, _) => updateConfigTrigger.TrySetResult());

        while (!context.CancellationToken.IsCancellationRequested)
        {
            var response = new ApplicationConfigResponse
            {
                AdminPassword = options.CurrentValue.AdminPassword,
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
        using var modeChange = homeAssistantEntities.InputSelect.MaysonElectronicsMode.StateChanges().Subscribe(_ => updateStatusTrigger.TrySetResult());
        using var locationChange = homeAssistantEntities.DeviceTracker.PhoneMayson.StateChanges().Subscribe(_ => updateStatusTrigger.TrySetResult());
        using var availableTimeChange = homeAssistantEntities.Sensor.MaysonAvailableTime.StateChanges().Subscribe(_ => updateStatusTrigger.TrySetResult());
        using var tasksChange = homeAssistantEntities.Todo.Mayson.StateChanges().Subscribe(_ => updateStatusTrigger.TrySetResult());

        while (!context.CancellationToken.IsCancellationRequested)
        {
            // We have to recreate the timer each time because the time to next daytime change can vary based on Daylight Saving Time
            using var daytimeChange = new Timer(_ => updateStatusTrigger.TrySetResult(), null, GetTimeToNextDaytimeChange(), TimeSpan.FromDays(1));

            var response = new StatusResponse
            {
                Mode = homeAssistantEntities.InputSelect.MaysonElectronicsMode.EntityState.AsEnum<ElectronicsMode>() ?? ElectronicsMode.Restricted,
                Location = homeAssistantEntities.DeviceTracker.PhoneMayson.State,
                IsDayTime = DateTime.Now.Hour >= 8 && DateTime.Now.Hour < 21,
                AvailableTime = homeAssistantEntities.Sensor.MaysonAvailableTime.EntityState.AsInt() ?? 0,
                HasIncompleteTasks = homeAssistantEntities.Todo.Mayson.EntityState.AsInt() > 0 || homeAssistantEntities.Todo.MaysonReview.EntityState.AsInt() > 0,
            };
            await responseStream.WriteAsync(response, context.CancellationToken);

            await updateStatusTrigger.Task;
            updateStatusTrigger = new TaskCompletionSource();
        }
    }

    public override async Task<DeviceStatusResponse> SendDeviceStatus(DeviceStatus request, ServerCallContext context)
    {
        await homeAssistantWebhook.CallWebhookAsync(
            options.CurrentValue.WebhookId,
            new
            {
                request.CurrentApp,
                request.CurrentPipApp,
                IsUsingTime = options.CurrentValue.Applications.Any(a => a.RequiresTime && (a.Name == request.CurrentApp || a.Name == request.CurrentPipApp)),
            },
            context.CancellationToken);

        return new DeviceStatusResponse();
    }

    private TimeSpan GetTimeToNextDaytimeChange()
    {
        var now = DateTime.Now;

        var morning = now.At(8, 0);
        if (now <= morning)
        {
            return morning - now;
        }

        var evening = now.At(21, 0);
        if (now <= evening)
        {
            return evening - now;
        }

        return morning.NextDay() - now;
    }
}
