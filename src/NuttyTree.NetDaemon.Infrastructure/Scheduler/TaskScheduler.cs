using Microsoft.Extensions.Logging;

namespace NuttyTree.NetDaemon.Infrastructure.Scheduler;

internal sealed class TaskScheduler(ILogger<TaskScheduler> logger) : ITaskScheduler
{
    private readonly ILogger<TaskScheduler> logger = logger;

    public IDisposable CreatePeriodicTask(TimeSpan period, Func<CancellationToken, Task> action)
        => CreateTriggerableSelfSchedulingTask(
            async c =>
            {
                await action(c);
                return period;
            },
            period);

    public IDisposable CreateSelfSchedulingTask(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn)
        => CreateTriggerableSelfSchedulingTask(action, onExceptionRetryIn);

    public IDisposable CreateSelfSchedulingTask(Func<CancellationToken, Task<DateTime>> action, TimeSpan onExceptionRetryIn)
        => CreateTriggerableSelfSchedulingTask(action, onExceptionRetryIn);

    public ITriggerableTask CreateTriggerableSelfSchedulingTask(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn)
    {
        var task = new TriggerableTask();
        _ = RunTaskAsync(action, onExceptionRetryIn, task);
        return task;
    }

    public ITriggerableTask CreateTriggerableSelfSchedulingTask(Func<CancellationToken, Task<DateTime>> action, TimeSpan onExceptionRetryIn)
    {
        var task = new TriggerableTask();
        _ = RunTaskAsync(async c => await action(c) - DateTime.UtcNow, onExceptionRetryIn, task);
        return task;
    }

    private async Task RunTaskAsync(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn, TriggerableTask task)
    {
        // Force the task creation to continue
        await Task.Yield();

        while (!task.StopRequestedToken.IsCancellationRequested)
        {
            TimeSpan? nextIterationIn = null;
            try
            {
                nextIterationIn = await action(task.StopRequestedToken);
            }
            catch (OperationCanceledException) when (task.StopRequestedToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected exception in task: {Task}", $"{action.Target?.GetType().Name}.{action.Method.Name}");
            }

            if (!task.StopRequestedToken.IsCancellationRequested)
            {
                await task.WaitForNextRunAsync(nextIterationIn ?? onExceptionRetryIn);
            }
        }
    }

    private sealed class TriggerableTask : ITriggerableTask
    {
        private readonly CancellationTokenSource stopRequested = new();
        private readonly SemaphoreSlim scheduleLock = new(1, 1);

        private CancellationTokenSource? delayTokenSource;

        internal CancellationToken StopRequestedToken => stopRequested.Token;

        public void Trigger()
        {
            // Thread-safe cancellation of current delay
            Interlocked.Exchange(ref delayTokenSource, null)?.Cancel();
        }

        public void Dispose()
        {
            stopRequested.Cancel();
            delayTokenSource?.Dispose();
            stopRequested.Dispose();
            scheduleLock.Dispose();
        }

        internal async Task WaitForNextRunAsync(TimeSpan delay)
        {
            if (delay < TimeSpan.FromMilliseconds(100))
            {
                delay = TimeSpan.FromMilliseconds(100);  // Minimum 100ms between runs
            }
            else if (delay.TotalMilliseconds > int.MaxValue)
            {
                delay = TimeSpan.FromMilliseconds(int.MaxValue);
            }

            await scheduleLock.WaitAsync(StopRequestedToken);
            try
            {
                // Dispose old token source and create new one
                var oldTokenSource = Interlocked.Exchange(ref delayTokenSource, null);
                oldTokenSource?.Dispose();

                var newTokenSource = CancellationTokenSource.CreateLinkedTokenSource(StopRequestedToken);
                delayTokenSource = newTokenSource;

                try
                {
                    await Task.Delay(delay, newTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when triggered or stopped
                }
            }
            finally
            {
                scheduleLock.Release();
            }
        }
    }
}
