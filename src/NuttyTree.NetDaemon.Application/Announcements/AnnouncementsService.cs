﻿using Microsoft.Extensions.Hosting;
using NuttyTree.NetDaemon.Application.Announcements.Models;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;

namespace NuttyTree.NetDaemon.Application.Announcements;

internal sealed class AnnouncementsService : IAnnouncementsService, IAnnouncementsInternalService, IDisposable
{
    private static readonly List<string> ReminderPrefixes =
    [
        "Just a friendly reminder",
        "Don't forget that",
        "Remember that",
    ];

    //private static readonly List<string> WarningPrefixes = new List<string>
    //{
    //    "Important reminder",
    //};

    //private static readonly List<string> CriticalPrefixes = new List<string>
    //{
    //    "Alert",
    //    "Critical notice"
    //};

    private readonly SynchronizedCollection<Announcement> announcements = [];

    private readonly IRateLimiter<AnnouncementsService> rateLimiter;

    private readonly ILogger<AnnouncementsService> logger;

    private readonly CancellationToken applicationStopping;

    private IEntities? homeAssistantEntities;

    private IHaContext? haContext;

    private IServices? homeAssistantServices;

    private TaskCompletionSource nextAnnouncementAvailable = new();

    private CancellationTokenSource? delayedAnnouncementDue;

    private DateTime? disabledUntil;

    public AnnouncementsService(
        IHostApplicationLifetime applicationLifetime,
        IRateLimiter<AnnouncementsService> rateLimiter,
        ILogger<AnnouncementsService> logger)
    {
        applicationStopping = applicationLifetime.ApplicationStopping;
        this.rateLimiter = rateLimiter;
        this.logger = logger;

        _ = SendAnnouncementsAsync();
    }

    public void Initialize(IEntities homeAssistantEntities, IHaContext haContext, IServices homeAssistantServices)
    {
        this.homeAssistantEntities = homeAssistantEntities;
        this.haContext = haContext;
        this.homeAssistantServices = homeAssistantServices;

        homeAssistantEntities.Sensor.HouseMode.StateChanges().Subscribe(_ => HandleStateChanges());
        homeAssistantEntities.BinarySensor.MelissaIsInBed.StateChanges().Subscribe(_ => HandleStateChanges());

        haContext.RegisterServiceCallBack<AnnouncementRequest>(
            "announcements_send_announcement",
            r =>
            {
                if (!string.IsNullOrWhiteSpace(r.message))
                {
                    Enum.TryParse<AnnouncementType>(r.type, true, out var type);
                    Enum.TryParse<AnnouncementPriority>(r.priority, true, out var priority);
                    Send(r.message, type, priority, r.person);
                }
            });
        haContext.RegisterServiceCallBack<DisableRequest>(
            "announcements_disable_announcements",
            r => DisableAnnouncements(r.minutes));
        haContext.RegisterServiceCallBack<object>(
            "announcements_enable_announcements",
            r => EnableAnnouncements());

        HandleStateChanges();
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
        }
    }

    public void EnableAnnouncements()
    {
        delayedAnnouncementDue?.Dispose();
        disabledUntil = null;
        nextAnnouncementAvailable.TrySetResult();
        logger.LogInformation("Announcements enabled");
    }

    public void SendAnnouncement(
        string message,
        AnnouncementType type = AnnouncementType.General,
        AnnouncementPriority priority = AnnouncementPriority.Information,
        string? person = null)
            => Send(message, type, priority, person);

    public async Task SendAnnouncementAsync(
        string message,
        AnnouncementType type = AnnouncementType.General,
        AnnouncementPriority priority = AnnouncementPriority.Information,
        string? person = null,
        CancellationToken cancellationToken = default)
    {
        await Send(message, type, priority, person)
            .IsComplete.Task.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        delayedAnnouncementDue?.Dispose();
    }

    private void HandleStateChanges()
    {
        if (homeAssistantEntities != null
            && homeAssistantEntities.Sensor.HouseMode.State.CaseInsensitiveEquals("Day")
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
        AnnouncementType type,
        AnnouncementPriority priority,
        string? person)
    {
        var announcement = new Announcement(message, type, priority, person);
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
                        await rateLimiter.WaitAsync(applicationStopping);
                        if (disabledUntil == null || nextAnnouncement.Priority == AnnouncementPriority.Critical)
                        {
                            if (nextAnnouncement.Person == null
                                || haContext?.GetState($"person.{nextAnnouncement.Person.ToLowerInvariant()}")?.State?.CaseInsensitiveEquals("home") == true)
                            {
                                var message = nextAnnouncement.Type switch
                                {
                                    AnnouncementType.Reminder => $"{ReminderPrefixes.PickRandom()}, ",
                                    _ => string.Empty,
                                };
                                message += nextAnnouncement.Message;
                                homeAssistantServices?.Notify.AlexaMediaDevicesInside(new NotifyAlexaMediaDevicesInsideParameters
                                {
                                    Data = new { type = "announce" },
                                    Message = message,
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
}
