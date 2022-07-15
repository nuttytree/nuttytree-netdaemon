using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentDateTime;
using Net.Codecrete.QrCodeGenerator;
using NetDaemon.Extensions.Scheduler;
using NuttyTree.NetDaemon.ExternalServices.RandomWords;

namespace NuttyTree.NetDaemon.Apps.GuestPasswordUpdate
{
    [NetDaemonApp]
    internal class GuestPasswordUpdateApp
    {
        private const string GuestSSID = "NuttyHome-Guest";

        private readonly INetDaemonScheduler scheduler;

        private readonly IRandomWordApi randomWordApi;

        public GuestPasswordUpdateApp(INetDaemonScheduler scheduler, IRandomWordApi randomWordApi)
        {
            this.scheduler = scheduler;
            this.randomWordApi = randomWordApi;

            var nextMonday = new DateTimeOffset(DateTime.Now.Next(DayOfWeek.Monday).SetTime(2, 0, 0));
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            scheduler.RunEvery(TimeSpan.FromDays(7), nextMonday, async () =>
            {
                await UpdatePasswordAsync();
            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

            UpdatePasswordAsync().GetAwaiter().GetResult();
        }

        private async Task UpdatePasswordAsync()
        {
            var password = await GenerateNewPasswordAsync();
            CreateQRCode(password);
        }

        private async Task<string> GenerateNewPasswordAsync()
        {
            var words = await randomWordApi.GetRandomWordsAsync(number: 2, length: 7);
            var textInfo = CultureInfo.CurrentCulture.TextInfo;
            return $"{textInfo.ToTitleCase(words[0])}{textInfo.ToTitleCase(words[1])}{RandomNumberGenerator.GetInt32(1, 99):D2}";
        }

        private void CreateQRCode(string password)
        {
            var wifiText = $"WIFI:T:WPA;S:{GuestSSID};P:{password};;";
            var qrCode = QrCode.EncodeText(wifiText, QrCode.Ecc.Medium);
            var svg = qrCode.ToSvgString(3);
            File.WriteAllText("test.svg", svg, Encoding.UTF8);
        }
    }
}
