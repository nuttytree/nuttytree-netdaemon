using Microsoft.Extensions.Logging;

namespace NuttyTree.NetDaemon.Infrastructure.Scheduler;

internal sealed class TaskScheduler : ITaskScheduler
{
    private readonly ILogger<TaskScheduler> logger;

    public TaskScheduler(ILogger<TaskScheduler> logger)
    {
        this.logger = logger;
    }

    public IDisposable CreatePeriodicTask(TimeSpan period, Func<CancellationToken, Task> action)
    {
        var task = new TriggerableTask();
        _ = RunTaskAsync(
                async c =>
                {
                    await action(c);
                    return period;
                },
                period,
                task);
        return task;
    }

    public IDisposable CreateSelfSchedulingTask(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn)
    {
        var task = new TriggerableTask();
        _ = RunTaskAsync(action, onExceptionRetryIn, task);
        return task;
    }

    public IDisposable CreateSelfSchedulingTask(Func<CancellationToken, Task<DateTime>> action, TimeSpan onExceptionRetryIn)
    {
        var task = new TriggerableTask();
        _ = RunTaskAsync(async c => await action(c) - DateTime.UtcNow, onExceptionRetryIn, task);
        return task;
    }

    public ITriggerableTask CreateTriggerableSelfSchedulingTask(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn)
    {
        var task = new TriggerableTask();
        _ = RunTaskAsync(action, onExceptionRetryIn, task);
        return task;
    }

    private async Task RunTaskAsync(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn, TriggerableTask task)
    {
        // Force the task creation to continue
        await Task.Delay(1, task.StopRequestedToken);

        while (!task.StopRequestedToken.IsCancellationRequested)
        {
            TimeSpan? nextIterationIn = null;
            try
            {
                nextIterationIn = await action(task.StopRequestedToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected exception in task: {Task}", $"{action.Target?.GetType().Name}.{action.Method.Name}");
            }

            if (!task.StopRequestedToken.IsCancellationRequested)
            {
                task.ScheduleNextRun(nextIterationIn ?? onExceptionRetryIn);
                await task.AwaitNextRunAsync();
            }
        }
    }

    private sealed class TriggerableTask : ITriggerableTask
    {
        private readonly CancellationTokenSource stopRequested = new CancellationTokenSource();

        private CancellationTokenSource? nextRunTrigger;

        private TaskCompletionSource nextRunDue = new TaskCompletionSource();

        internal CancellationToken StopRequestedToken => stopRequested.Token;

        public void Trigger()
        {
            nextRunTrigger?.Cancel();
        }

        public void Dispose()
        {
            nextRunTrigger?.Cancel();
            nextRunTrigger?.Dispose();
            stopRequested.Cancel();
            stopRequested.Dispose();
        }

        internal void ScheduleNextRun(TimeSpan nextRunIn)
        {
            if (nextRunIn < TimeSpan.Zero)
            {
                nextRunIn = TimeSpan.Zero;
            }
            else if (nextRunIn.TotalMilliseconds > int.MaxValue)
            {
                nextRunIn = TimeSpan.FromMilliseconds(int.MaxValue);
            }

            nextRunDue = new TaskCompletionSource();
            nextRunTrigger = new CancellationTokenSource();
            nextRunTrigger.Token.Register(() => nextRunDue.SetResult());
            nextRunTrigger.CancelAfter(nextRunIn);
        }

        internal Task AwaitNextRunAsync()
        {
            return nextRunDue.Task.WaitAsync(StopRequestedToken);
        }
    }
}
