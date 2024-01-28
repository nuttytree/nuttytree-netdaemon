using Grpc.Core;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

public sealed class ElectronicsTimeStatusService : ElectronicsTimeStatus.ElectronicsTimeStatusBase
{
    public override async Task GetStatus(GetStatusRequest request, IServerStreamWriter<StatusResponse> responseStream, ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5000);
            await responseStream.WriteAsync(new StatusResponse { Mode = ElectronicsMode.Normal });
        }
    }
}
