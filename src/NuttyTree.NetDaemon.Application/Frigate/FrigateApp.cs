using NuttyTree.NetDaemon.Application.Frigate.Models;
using NuttyTree.NetDaemon.ExternalServices.Frigate;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Application.Frigate;

[NetDaemonApp]
internal sealed class FrigateApp
{
    public FrigateApp(IFrigateApi frigateApi, IHaContext haContext, IServices homeAssistantServices)
    {
        haContext.RegisterServiceCallBack<CreateEventRequest>(
            "create_frigate_event",
            r =>
            {
                var response = frigateApi.CreateEventAsync(r.camera, r.event_name).GetAwaiter().GetResult();
                if (response.Success)
                {
                    homeAssistantServices.InputText.SetValue(ServiceTarget.FromEntity(r.event_id_entity), new InputTextSetValueParameters { Value = response.EventId });
                }
            });
        haContext.RegisterServiceCallBack<EndEventRequest>(
            "end_frigate_event",
            r =>
            {
                var state = haContext.GetState(r.event_id_entity);
                if (!string.IsNullOrWhiteSpace(state?.State))
                {
                    var response = frigateApi.EndEventAsync(state.State).GetAwaiter().GetResult();
                    if (response.Success)
                    {
                        homeAssistantServices.InputText.SetValue(ServiceTarget.FromEntity(r.event_id_entity), new InputTextSetValueParameters { Value = string.Empty });
                    }
                }
            });
    }
}
