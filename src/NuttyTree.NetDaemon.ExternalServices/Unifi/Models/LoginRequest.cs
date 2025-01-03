namespace NuttyTree.NetDaemon.ExternalServices.Unifi.Models;

internal sealed class LoginRequest
{
    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool RememberMe => true;
}
