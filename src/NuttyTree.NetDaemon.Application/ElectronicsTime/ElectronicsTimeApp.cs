using FluentDateTime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Models;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;
using NuttyTree.NetDaemon.Infrastructure.Database;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using NuttyTree.NetDaemon.Infrastructure.Extensions;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.Scheduler;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime;

[NetDaemonApp]
internal sealed class ElectronicsTimeApp : IDisposable
{
    private readonly IServiceScopeFactory serviceScopeFactory;

    private readonly IOptionsMonitor<ElectronicsTimeOptions> options;

    private readonly IEntities homeAssistantEntities;

    private readonly ILogger<ElectronicsTimeApp> logger;

    private readonly TodoEntity maysonsToDoList;

    private readonly List<IDisposable> taskTriggers = new List<IDisposable>();

    private readonly ITriggerableTask updateToDoListTask;

    public ElectronicsTimeApp(
        IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<ElectronicsTimeOptions> options,
        ILogger<ElectronicsTimeApp> logger,
        IEntities homeAssistantEntities,
        ITaskScheduler taskScheduler,
        IHaContext haContext)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.options = options;
        this.homeAssistantEntities = homeAssistantEntities;
        this.logger = logger;

        maysonsToDoList = homeAssistantEntities.Todo.Mayson;
        taskTriggers.Add(maysonsToDoList.StateChanges().SubscribeAsync(HandleToDoListChangeAsync));

        updateToDoListTask = taskScheduler.CreateTriggerableSelfSchedulingTask(UpdateToDoListAsync, TimeSpan.FromSeconds(30));

        foreach (var triggeredToDoListItem in options.CurrentValue.ToDoListItems.Where(r => r.RecurringToDoListItemType == RecurringToDoListItemType.Triggered))
        {
            taskTriggers.Add(haContext.Entity(triggeredToDoListItem.TriggerSensor!).StateChanges()
                .SubscribeAsync(async s => await HandleTriggerSensorStateChangeAsync(s, triggeredToDoListItem)));
        }

        logger.LogInformation("Loaded {ToDoListCount} recurring to do list items", options.CurrentValue.ToDoListItems.Count);
        taskTriggers.Add(options.OnChange(o =>
        {
            updateToDoListTask.Trigger();
            logger.LogInformation("Loaded {ToDoListCount} recurring to do list items", o.ToDoListItems.Count);
        }) !);
    }

    public void Dispose()
    {
        taskTriggers.ForEach(t => t.Dispose());
        updateToDoListTask.Dispose();
    }

    private async Task HandleToDoListChangeAsync(StateChange<TodoEntity, EntityState<TodoAttributes>> stateChange)
    {
        var completedItems = await maysonsToDoList.GetItemsAsync("completed");
        if (completedItems.Count > 0)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();

            var incompleteItems = await dbContext.ToDoListItems
                .Where(t => completedItems.Select(c => c.Uid).Contains(t.Uid))
                .ToDictionaryAsync(t => t.Uid);

            foreach (var completedItem in completedItems)
            {
                if (incompleteItems.TryGetValue(completedItem.Uid, out var incompleteItem) && incompleteItem.MinutesEarned > 0)
                {
                    homeAssistantEntities.Counter.MaysonElectronicsTime.Increase(incompleteItem.MinutesEarned);
                    logger.LogInformation(
                        "Added {EarnedMinutes} minutes to Mayson's time for completing to do list item {ToDoListItem}",
                        incompleteItem.MinutesEarned,
                        incompleteItem.Name);
                }

                maysonsToDoList.RemoveItem(completedItem.Summary);
            }

            await dbContext.ToDoListItems
                .Where(t => incompleteItems.Keys.Contains(t.Uid))
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.CompletedAt, DateTime.UtcNow));

            updateToDoListTask.Trigger();
        }
    }

    private async Task<DateTime> UpdateToDoListAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
        var utcNow = DateTime.UtcNow;

        await RemoveExpiredToDoListItemsAsync(dbContext, utcNow, cancellationToken);

        await AddNewToDoListItemsAsync(dbContext, utcNow, cancellationToken);

        await UpdateRecurringToDoListItemsWithNextOccurrenceAsync(dbContext, utcNow, cancellationToken);

        var nextExpiration = await dbContext.ToDoListItems
            .Where(t => t.ExpiresAt != null && t.CompletedAt == null)
            .Select(t => t.ExpiresAt)
            .MinAsync(cancellationToken);
        var nextRecurring = options.CurrentValue.ToDoListItems.Select(r => r.NextOccurrence).Min();
        return new[] { nextExpiration, nextRecurring }.Min() ?? utcNow.AddHours(1);
    }

    private async Task RemoveExpiredToDoListItemsAsync(NuttyDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken)
    {
        var expiredItems = await dbContext.ToDoListItems
            .Where(t => t.CompletedAt == null && t.ExpiresAt <= utcNow)
            .ToListAsync(cancellationToken);

        foreach (var expiredItem in expiredItems)
        {
            maysonsToDoList.RemoveItem(expiredItem.Name);
            logger.LogInformation("Removed expired to do list item {ToDoListItem}", expiredItem.Name);
        }

        await dbContext.ToDoListItems
            .Where(t => expiredItems.Select(e => e.Id).Contains(t.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.ExpiresAt, _ => null), cancellationToken);
    }

    private async Task AddNewToDoListItemsAsync(NuttyDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken)
    {
        foreach (var recurringItem in options.CurrentValue.ToDoListItems
            .Where(r => r.NextOccurrence.HasValue && r.NextOccurrence <= utcNow))
        {
            await AddNewToDoListItemAsync(recurringItem, dbContext, cancellationToken);
        }
    }

    private async Task UpdateRecurringToDoListItemsWithNextOccurrenceAsync(NuttyDbContext dbContext, DateTime utcNow, CancellationToken cancellationToken)
    {
        var now = utcNow.ToLocalTime();
        var today = now.Date;
        var tomorrow = today.NextDay();
        var currentTime = TimeOnly.FromDateTime(now);
        var dayOfWeek = today.DayOfWeek;
        var lastOccurences = await dbContext.ToDoListItems
            .Where(t => options.CurrentValue.ToDoListItems.Select(r => r.Name).Contains(t.Name))
            .Where(t => t.Id == dbContext.ToDoListItems.Where(n => n.Name == t.Name).Select(n => n.Id).Max())
            .ToListAsync(cancellationToken);
        foreach (var recurringItem in options.CurrentValue.ToDoListItems.Where(r => !r.NextOccurrence.HasValue))
        {
            var lastOccurence = lastOccurences.FirstOrDefault(l => l.Name == recurringItem.Name);
            if (lastOccurence == null || lastOccurence.CompletedAt.HasValue || !lastOccurence.ExpiresAt.HasValue)
            {
                var lastOccurenceAt = recurringItem.RecurringToDoListItemType == RecurringToDoListItemType.EveryXDays
                    ? lastOccurence?.CompletedAt ?? DateTime.MinValue
                    : lastOccurence?.CreatedAt ?? DateTime.MinValue;
                var nextOccurenceDate = recurringItem.RecurringToDoListItemType switch
                {
                    RecurringToDoListItemType.Daily => lastOccurenceAt < today ? today : tomorrow,
                    RecurringToDoListItemType.Weekly => lastOccurenceAt < today && recurringItem.WeeklyDayOfWeek == dayOfWeek ? today : today.Next(recurringItem.WeeklyDayOfWeek),
                    RecurringToDoListItemType.EveryXDays => lastOccurenceAt < today.AddDays(recurringItem.DaysBetween * -1) ? today : lastOccurenceAt.AddDays(recurringItem.DaysBetween).Date,
                    _ => DateTime.MinValue,
                };
                recurringItem.NextOccurrence = nextOccurenceDate == DateTime.MinValue
                    ? null
                    : (nextOccurenceDate + recurringItem.StartAt.ToTimeSpan()).ToUniversalTime();
            }
        }
    }

    private async Task HandleTriggerSensorStateChangeAsync(StateChange stateChange, RecurringToDoListItem recurringToDoListItem)
    {
        if (stateChange.Old?.State == recurringToDoListItem.TriggerFromState && stateChange.New?.State == recurringToDoListItem.TriggerToState)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();

            await AddNewToDoListItemAsync(recurringToDoListItem, dbContext);
        }
    }

    private async Task AddNewToDoListItemAsync(RecurringToDoListItem recurringToDoListItem, NuttyDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var createdItem = await maysonsToDoList.AddItemAsync(
            recurringToDoListItem.Name,
            description: recurringToDoListItem.MinutesEarned > 0 ? $"{recurringToDoListItem.MinutesEarned} Minutes" : null,
            cancellationToken: cancellationToken);
        dbContext.ToDoListItems.Add(new ToDoListItemEntity
        {
            Uid = createdItem.Uid,
            Name = createdItem.Summary,
            MinutesEarned = recurringToDoListItem.MinutesEarned,
            CreatedAt = utcNow,
            ExpiresAt = recurringToDoListItem.ExpiresAfter.HasValue
                ? (recurringToDoListItem.NextOccurrence ?? utcNow) + recurringToDoListItem.ExpiresAfter
                : DateTime.MaxValue,
        });
        recurringToDoListItem.NextOccurrence = null;
        logger.LogInformation("Added new to do list item {ToDoListItem}", recurringToDoListItem.Name);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
