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

    private readonly IHaContext haContext;

    private readonly IEntities homeAssistantEntities;

    private readonly IOptionsMonitor<ElectronicsTimeOptions> options;

    private readonly ILogger<ElectronicsTimeApp> logger;

    private readonly TodoEntity maysonsToDoList;

    private readonly TodoEntity maysonsOptionalToDoList;

    private readonly TodoEntity maysonsReviewList;

    private readonly TodoEntity maysonsOptionalReviewList;

    private readonly string? chrisUserId;

    private readonly List<IDisposable> toDoListUpdateTriggers = new List<IDisposable>();

    private readonly List<IDisposable> taskTriggers = new List<IDisposable>();

    private readonly ITriggerableTask updateToDoListTask;

    public ElectronicsTimeApp(
        IServiceScopeFactory serviceScopeFactory,
        IHaContext haContext,
        IEntities homeAssistantEntities,
        IOptionsMonitor<ElectronicsTimeOptions> options,
        ILogger<ElectronicsTimeApp> logger,
        ITaskScheduler taskScheduler)
    {
        this.serviceScopeFactory = serviceScopeFactory;
        this.haContext = haContext;
        this.homeAssistantEntities = homeAssistantEntities;
        this.options = options;
        this.logger = logger;

        maysonsToDoList = homeAssistantEntities.Todo.Mayson;
        maysonsOptionalToDoList = homeAssistantEntities.Todo.MaysonOptional;
        maysonsReviewList = homeAssistantEntities.Todo.MaysonReview;
        maysonsOptionalReviewList = homeAssistantEntities.Todo.MaysonOptionalReview;
        chrisUserId = homeAssistantEntities.Person.Chris.Attributes?.UserId;
        toDoListUpdateTriggers.Add(maysonsToDoList.StateChanges().SubscribeAsync(HandleToDoListChangeAsync));
        toDoListUpdateTriggers.Add(maysonsOptionalToDoList.StateChanges().SubscribeAsync(HandleToDoListChangeAsync));
        toDoListUpdateTriggers.Add(maysonsReviewList.StateChanges().SubscribeAsync(HandleReviewListChangeAsync));
        toDoListUpdateTriggers.Add(maysonsOptionalReviewList.StateChanges().SubscribeAsync(HandleReviewListChangeAsync));

        updateToDoListTask = taskScheduler.CreateTriggerableSelfSchedulingTask(UpdateToDoListAsync, TimeSpan.FromSeconds(30));

        UpdateTaskTriggers();
        toDoListUpdateTriggers.Add(options.OnChange(o =>
        {
            UpdateTaskTriggers();
            updateToDoListTask.Trigger();
        }) !);
    }

    public void Dispose()
    {
        toDoListUpdateTriggers.ForEach(t => t.Dispose());
        taskTriggers.ForEach(t => t.Dispose());
        updateToDoListTask.Dispose();
    }

    private void UpdateTaskTriggers()
    {
        taskTriggers.ForEach(t => t.Dispose());
        taskTriggers.Clear();
        foreach (var triggeredToDoListItem in options.CurrentValue.ToDoListItems.Where(r => r.RecurringToDoListItemType == RecurringToDoListItemType.Triggered))
        {
            taskTriggers.Add(haContext.Entity(triggeredToDoListItem.TriggerSensor!).StateChanges()
                .SubscribeAsync(async s => await HandleTriggerSensorStateChangeAsync(s, triggeredToDoListItem)));
        }

        logger.LogInformation("Loaded {ToDoListCount} recurring to do list items", options.CurrentValue.ToDoListItems.Count);
    }

    private async Task HandleToDoListChangeAsync(StateChange<TodoEntity, EntityState<TodoAttributes>> stateChange)
    {
        var todoList = stateChange.Entity;
        var completedItems = await todoList.GetItemsAsync("completed");
        if (completedItems.Count > 0)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();
            var incompleteItems = await dbContext.ToDoListItems
                .Where(t => completedItems.Select(c => c.Uid).Contains(t.Uid))
                .ToDictionaryAsync(t => t.Uid);

            foreach (var completedItem in completedItems)
            {
                var incompleteItem = incompleteItems.GetValueOrDefault(completedItem.Uid, new ToDoListItemEntity
                {
                    Uid = completedItem.Uid,
                    Name = completedItem.Summary,
                });

                var reviewItem = todoList.EntityId == maysonsToDoList.EntityId
                    ? await maysonsReviewList.AddItemAsync($"{incompleteItem.Name} ({DateTime.Now:ddd h:mm tt})")
                    : await maysonsOptionalReviewList.AddItemAsync($"{incompleteItem.Name} ({DateTime.Now:ddd h:mm tt})");
                incompleteItem.ReviewUid = reviewItem.Uid;
                incompleteItem.CompletedAt = DateTime.UtcNow;

                todoList.RemoveItem(completedItem.Uid);
            }

            await dbContext.SaveChangesAsync();

            updateToDoListTask.Trigger();
        }
    }

    private async Task HandleReviewListChangeAsync(StateChange<TodoEntity, EntityState<TodoAttributes>> stateChange)
    {
        var reviewList = stateChange.Entity;
        var reviewedItems = await reviewList.GetItemsAsync("completed");
        if (reviewedItems.Count > 0)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NuttyDbContext>();

            var unreviewedItems = await dbContext.ToDoListItems
                .Where(t => t.ReviewUid != null && reviewedItems.Select(c => c.Uid).Contains(t.ReviewUid.Value))
                .ToDictionaryAsync(t => t.ReviewUid!.Value);

            foreach (var reviewedItem in reviewedItems)
            {
                if (unreviewedItems.TryGetValue(reviewedItem.Uid, out var unreviewedItem)
                    && unreviewedItem.MinutesEarned > 0
                    && stateChange.New?.Context?.UserId == chrisUserId)
                {
                    homeAssistantEntities.Counter.MaysonElectronicsTime.Increase(unreviewedItem.MinutesEarned);
                    logger.LogInformation(
                        "Added {EarnedMinutes} minutes to Mayson's time for completing to do list item {ToDoListItem}",
                        unreviewedItem.MinutesEarned,
                        unreviewedItem.Name);
                }

                if (stateChange.New?.Context?.UserId == chrisUserId)
                {
                    reviewList.RemoveItem(reviewedItem.Uid);
                }
                else
                {
                    reviewList.UpdateItem(reviewedItem.Uid, status: "needs_action");
                    homeAssistantEntities.Counter.MaysonElectronicsTime.Increase(-5);
                    logger.LogInformation(
                        "Removed 5 minutes from Mayson's time for trying to mark to do list item {ToDoListItem} as reviewed",
                        reviewedItem.Summary);
                }
            }
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
            maysonsToDoList.RemoveItem(expiredItem.Uid);
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
                    ? lastOccurence?.CompletedAt?.ToLocalTime() ?? DateTime.MinValue
                    : lastOccurence?.CreatedAt.ToLocalTime() ?? DateTime.MinValue;
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
