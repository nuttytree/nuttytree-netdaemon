namespace NuttyTree.NetDaemon.ExternalServices.Unifi.Models;

public sealed class UnifResponse<T>
    where T : new()
{
    public UnifiMetadata Meta { get; set; } = new();

    public ICollection<T> Data { get; set; } = [];
}
