using System.ComponentModel.DataAnnotations;

namespace NuttyTree.NetDaemon.Application.GuestPasswordUpdate.Options;

internal sealed class GuestPasswordUpdateOptions
{
    [Required]
    public string? GuestNetwork { get; set; }

    [Required]
    public string? WebhookId { get; set; }

    public bool NotificationOfExceptions { get; set; } = true;
}
