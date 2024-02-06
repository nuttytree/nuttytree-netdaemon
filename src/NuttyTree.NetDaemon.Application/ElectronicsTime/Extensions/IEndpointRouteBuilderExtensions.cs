using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Extensions;

public static class IEndpointRouteBuilderExtensions
{
    public static GrpcServiceEndpointConventionBuilder MapElectronicsTimeGrpcService(this IEndpointRouteBuilder builder)
    {
        return builder.MapGrpcService<ElectronicsTimeGrpcService>();
    }
}
