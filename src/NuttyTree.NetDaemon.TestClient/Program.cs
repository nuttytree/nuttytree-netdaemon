using Grpc.Core;
using Grpc.Net.Client.Configuration;
using NuttyTree.NetDaemon.TestClient.gRPC;

var builder = WebApplication.CreateSlimBuilder(args);

//AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

builder.Services.AddGrpcClient<ElectronicsTimeGrpc.ElectronicsTimeGrpcClient>(o =>
{
    //o.Address = new Uri("http://localhost:5000");
    o.Address = new Uri("https://netdaemon.nuttytree.dynu.net");
    o.ChannelOptionsActions.Add(co =>
    {
        var defaultMethodConfig = new MethodConfig
        {
            Names = { MethodName.Default },
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialBackoff = TimeSpan.FromSeconds(1),
                MaxBackoff = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 1.5,
                RetryableStatusCodes = { StatusCode.Unavailable }
            }
        };
        co.ServiceConfig = new ServiceConfig
        {
            MethodConfigs = { defaultMethodConfig },
        };
    });
});

var app = builder.Build();

var client = app.Services.GetRequiredService<ElectronicsTimeGrpc.ElectronicsTimeGrpcClient>();

var stream = client.GetApplicationConfig(new ApplicationConfigRequest());

GetRespAsync(stream).GetAwaiter().GetResult();

async Task GetRespAsync(AsyncServerStreamingCall<ApplicationConfigResponse> stream)
{
    await foreach (var response in stream.ResponseStream.ReadAllAsync())
    {
        foreach (var app in response.Applications)
        {
            Console.WriteLine($"{app.Name}");
        }
    }
}
