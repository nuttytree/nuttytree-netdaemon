using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Integration;
using NuttyTree.NetDaemon.Application.Announcements.Models;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;

namespace NuttyTree.NetDaemon.Application.Announcements;

internal class AnnouncementsService : IAnnouncementsService, IDisposable
{
    private readonly SynchronizedCollection<Announcement> announcements = new SynchronizedCollection<Announcement>();

    private readonly SemaphoreSlim rateLimiter = new SemaphoreSlim(1, 1);

    private readonly IServiceScope serviceScope;

    private readonly ILogger<AnnouncementsService> logger;

    private readonly CancellationToken applicationStopping;

    private readonly IEntities homeAssistantEntities;

    private readonly IHaContext haContext;

    private readonly IServices homeAssistantServices;

    private TaskCompletionSource nextAnnouncementAvailable = new TaskCompletionSource();

    private CancellationTokenSource? delayedAnnouncementDue;

    private DateTime? disabledUntil;

    private CancellationTokenSource? releaseRateLimiter;

    public AnnouncementsService(
        IServiceScopeFactory serviceScopeFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<AnnouncementsService> logger)
    {
        serviceScope = serviceScopeFactory.CreateScope();
        applicationStopping = applicationLifetime.ApplicationStopping;
        this.logger = logger;

        homeAssistantEntities = serviceScope.ServiceProvider.GetRequiredService<IEntities>();
        haContext = serviceScope.ServiceProvider.GetRequiredService<IHaContext>();
        homeAssistantServices = serviceScope.ServiceProvider.GetRequiredService<IServices>();

        homeAssistantEntities.InputSelect.HouseMode.StateChanges().Subscribe(_ => HandleStateChanges());
        homeAssistantEntities.BinarySensor.MelissaIsInBed.StateChanges().Subscribe(_ => HandleStateChanges());
        HandleStateChanges();

        haContext.RegisterServiceCallBack<AnnouncementRequest>(
            "announcements_send_announcement",
            r =>
            {
                if (!string.IsNullOrWhiteSpace(r.message))
                {
                    Enum.TryParse<AnnouncementPriority>(r.priority, true, out var priority);
                    Send(r.message, priority, r.person);
                }
            });
        haContext.RegisterServiceCallBack<DisableRequest>(
            "announcements_disable_announcements",
            r => DisableAnnouncements(r.minutes));
        haContext.RegisterServiceCallBack<object>(
            "announcements_enable_announcements",
            r => EnableAnnouncements());

        _ = SendAnnouncementsAsync();
    }

    public void DisableAnnouncements(int minutes)
    {
        if (minutes > 0)
        {
            delayedAnnouncementDue?.Dispose();
            disabledUntil = minutes == int.MaxValue ? DateTime.UtcNow.AddMilliseconds(int.MaxValue) : DateTime.UtcNow.AddMinutes(minutes);
            delayedAnnouncementDue = new CancellationTokenSource(disabledUntil.Value.Subtract(DateTime.UtcNow));
            delayedAnnouncementDue.Token.Register(() =>
            {
                disabledUntil = null;
                nextAnnouncementAvailable.TrySetResult();
            });
            logger.LogInformation("Announcements disabled until {DisabledUntil}", disabledUntil);
            haContext.SetEntityState("binary_sensor.announcments_enabled", "off", new { until = $"{(minutes == int.MaxValue ? "Indefinitely" : disabledUntil)}" });
        }
    }

    public void EnableAnnouncements()
    {
        delayedAnnouncementDue?.Dispose();
        disabledUntil = null;
        nextAnnouncementAvailable.TrySetResult();
        logger.LogInformation("Announcements enabled", disabledUntil);
        haContext.SetEntityState("binary_sensor.announcments_enabled", "on", new { });
    }

    public void SendAnnouncement(
        string message,
        AnnouncementPriority priority = AnnouncementPriority.Information,
        string? person = null)
            => Send(message, priority, person);

    public async Task SendAnnouncementAsync(
        string message,
        AnnouncementPriority priority = AnnouncementPriority.Information,
        string? person = null,
        CancellationToken cancellationToken = default)
    {
        await Send(message, priority, person)
            .IsComplete.Task.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        rateLimiter?.Dispose();
        releaseRateLimiter?.Dispose();
        delayedAnnouncementDue?.Dispose();
        serviceScope?.Dispose();
    }

    private void HandleStateChanges()
    {
        if (homeAssistantEntities.InputSelect.HouseMode.State.CaseInsensitiveEquals("Day")
            && homeAssistantEntities.BinarySensor.MelissaIsInBed.IsOff())
        {
            EnableAnnouncements();
        }
        else
        {
            DisableAnnouncements(int.MaxValue);
        }
    }

    private Announcement Send(
        string message,
        AnnouncementPriority priority,
        string? person)
    {
        var announcement = new Announcement(message, priority, person);
        announcements.Add(announcement);
        nextAnnouncementAvailable.TrySetResult();
        return announcement;
    }

    private async Task SendAnnouncementsAsync()
    {
        // Force the constructor to stop waiting
        await Task.Delay(1);

        while (!applicationStopping.IsCancellationRequested)
        {
            try
            {
                await nextAnnouncementAvailable.Task.WaitAsync(applicationStopping);
                if (!applicationStopping.IsCancellationRequested)
                {
                    nextAnnouncementAvailable = new TaskCompletionSource();

                    var nextAnnouncement = announcements
                        .Where(a => disabledUntil == null || a.Priority != AnnouncementPriority.Warning)
                        .OrderBy(a => a.QueuedAt)
                        .FirstOrDefault();
                    if (nextAnnouncement != null)
                    {
                        if (disabledUntil == null || nextAnnouncement.Priority == AnnouncementPriority.Critical)
                        {
                            if (nextAnnouncement.Person == null || haContext.Entity($"person.{nextAnnouncement.Person.ToLowerInvariant()}").State.CaseInsensitiveEquals("home"))
                            {
                                homeAssistantServices.Notify.AlexaMediaDevicesInside(new NotifyAlexaMediaDevicesInsideParameters
                                {
                                    Data = new { type = "announce" },
                                    Message = nextAnnouncement.Message,
                                });
                            }
                        }

                        announcements.Remove(nextAnnouncement);
                        nextAnnouncement.IsComplete.SetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exception in the in the {Method} loop", nameof(SendAnnouncementsAsync));
            }
        }
    }

    private async Task<T> RateLimitAnnouncementsAsync<T>(Func<Task<T>> request)
    {
        try
        {
            await rateLimiter.WaitAsync();
            return await request();
        }
        finally
        {
            releaseRateLimiter = new CancellationTokenSource(60000);
            releaseRateLimiter.Token.Register(() =>
            {
                rateLimiter.Release();
            });
        }
    }
}
