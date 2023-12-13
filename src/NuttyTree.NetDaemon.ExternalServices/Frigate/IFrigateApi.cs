using NuttyTree.NetDaemon.ExternalServices.Frigate.Models;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Frigate;

public interface IFrigateApi
{
    [Post("/api/events/{camera}/{eventName}/create")]
    Task<CreateEventResponse> CreateEventAsync(string camera, string eventName, CancellationToken cancellationToken = default);

    [Put("/api/events/{eventId}/end")]
    Task<EndEventResponse> EndEventAsync(string eventId, CancellationToken cancellationToken = default);
}
