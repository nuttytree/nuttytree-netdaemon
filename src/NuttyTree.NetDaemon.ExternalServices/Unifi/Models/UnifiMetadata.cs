using System.Net;
using System.Text.Json.Serialization;

namespace NuttyTree.NetDaemon.ExternalServices.Unifi.Models;

public sealed class UnifiMetadata
{
    [JsonPropertyName("rc")]
    public HttpStatusCode? ResponseCode { get; set; }
}
