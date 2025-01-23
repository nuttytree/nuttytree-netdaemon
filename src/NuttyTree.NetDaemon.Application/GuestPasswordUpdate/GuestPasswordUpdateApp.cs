using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Bogus;
using Bogus.DataSets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.GuestPasswordUpdate.Options;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantWebhook;
using NuttyTree.NetDaemon.ExternalServices.Unifi;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Application.GuestPasswordUpdate
{
    [NetDaemonApp]
    internal sealed partial class GuestPasswordUpdateApp
    {
        private static readonly Faker Faker = new();

        private readonly IOptionsMonitor<GuestPasswordUpdateOptions> options;

        private readonly IHaContext haContext;

        private readonly IUnifiApi unifiApi;

        private readonly IHomeAssistantWebhookApi homeAssistantWebhookApi;

        private readonly ILogger<GuestPasswordUpdateApp> logger;

        private readonly IServices homeAssistantServices;

        private readonly CancellationToken applicationStopping;

        private string guestNetworkId = string.Empty;

        private TaskCompletionSource serviceTrigger = new();

        public GuestPasswordUpdateApp(
            IOptionsMonitor<GuestPasswordUpdateOptions> options,
            IHaContext haContext,
            IUnifiApi unifiApi,
            IHomeAssistantWebhookApi homeAssistantWebhookApi,
            ILogger<GuestPasswordUpdateApp> logger,
            IServices homeAssistantServices,
            IHostApplicationLifetime applicationLifetime)
        {
            this.options = options;
            this.haContext = haContext;
            this.unifiApi = unifiApi;
            this.homeAssistantWebhookApi = homeAssistantWebhookApi;
            this.homeAssistantServices = homeAssistantServices;
            this.logger = logger;
            applicationStopping = applicationLifetime.ApplicationStopping;

            _ = HandleServiceCallAsync();
            haContext.RegisterServiceCallBack<object>(
                "guest_network_update_password",
                _ => serviceTrigger.TrySetResult());
        }

        [GeneratedRegex(@"^[a-zA-Z0-9]{5,}$")]
        private static partial Regex ValidPassphraseWord();

        [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Not truly a foreign task")]
        private async Task HandleServiceCallAsync()
        {
            while (!applicationStopping.IsCancellationRequested)
            {
                try
                {
                    await serviceTrigger.Task;
                    serviceTrigger = new();

                    logger.LogInformation("Service Update Guest Network Password was called");

                    var password = $"{GetPassphraseWord(h => h.Adjective())}{GetPassphraseWord(h => h.Noun())}{Faker.Random.Int(1, 99):D2}";

                    await unifiApi.UpdateWirelessNetworkPassphraseAsync(await GetGuestNetworkIdAsync(), password, applicationStopping);

                    await homeAssistantWebhookApi.CallWebhookAsync(options.CurrentValue.WebhookId!, new { Password = password }, applicationStopping);
                }
                catch (Exception ex)
                {
                    HandleException(ex, nameof(HandleServiceCallAsync));
                }
            }
        }

        private string GetPassphraseWord(Func<Hacker, string> generator)
        {
            var word = generator(Faker.Hacker);

            while (!ValidPassphraseWord().IsMatch(word))
            {
                word = generator(Faker.Hacker);
            }

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(word);
        }

        private async Task<string> GetGuestNetworkIdAsync()
        {
            if (string.IsNullOrEmpty(guestNetworkId))
            {
                var networksResponse = await unifiApi.GetWirelessNetworksAsync(applicationStopping);
                guestNetworkId = networksResponse.Content?.Data.FirstOrDefault(n => n.Name == options.CurrentValue.GuestNetwork)?.Id ?? string.Empty;
            }

            return guestNetworkId;
        }

        private void HandleException(Exception exception, string taskName)
        {
            logger.LogError(exception, "Exception in guest password update task: {TaskName}", taskName);
            if (options.CurrentValue.NotificationOfExceptions)
            {
                homeAssistantServices.Notify.MobileAppPhoneChris(new NotifyMobileAppPhoneChrisParameters
                {
                    Title = $"Guest Password Update Exception: {taskName}",
                    Message = exception.Message,
                });
            }
        }
    }
}
