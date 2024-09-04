using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.HomeAssistantWebhook;

public interface IHomeAssistantWebhookApi
{
    [Put("/api/webhook/{webhookId}")]
    Task CallWebhookAsync(string webhookId, [Body] object data, CancellationToken cancellationToken = default);
}
