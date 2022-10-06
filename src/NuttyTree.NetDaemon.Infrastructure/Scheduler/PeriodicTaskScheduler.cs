using Microsoft.Extensions.Logging;

namespace NuttyTree.NetDaemon.Infrastructure.Scheduler;

internal class PeriodicTaskScheduler : IPeriodicTaskScheduler
{
    private readonly ILogger<PeriodicTaskScheduler> logger;

    public PeriodicTaskScheduler(ILogger<PeriodicTaskScheduler> logger)
    {
        this.logger = logger;
    }

    public IDisposable SchedulePeriodicTask(TimeSpan period, Func<CancellationToken, Task> action)
    {
        var stopRequested = new StopRequestedDisposable();
        _ = RunPeriodicTaskAsync(period, action, stopRequested.Token);
        return stopRequested;
    }

    private async Task RunPeriodicTaskAsync(TimeSpan period, Func<CancellationToken, Task> action, CancellationToken stopRequested)
    {
        // Force the SchedulePeriodicTask to continue
        await Task.Delay(1, stopRequested);

        CancellationTokenSource? nextIterationTrigger = null;

        while (!stopRequested.IsCancellationRequested)
        {
            try
            {
                await action(stopRequested);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Unexpected exception in periodic task: {action.Target?.GetType().Name}.{action.Method.Name}");
            }

            if (!stopRequested.IsCancellationRequested)
            {
                var nextIterationDue = new TaskCompletionSource();
                nextIterationTrigger = new CancellationTokenSource(period);
                nextIterationTrigger.Token.Register(() => nextIterationDue.SetResult());
                await nextIterationDue.Task.WaitAsync(stopRequested);
            }
        }

        nextIterationTrigger?.Dispose();
    }

    private class StopRequestedDisposable : IDisposable
    {
        private readonly CancellationTokenSource stopRequested = new CancellationTokenSource();

        public CancellationToken Token => stopRequested.Token;

        public void Dispose()
        {
            stopRequested.Cancel();
            stopRequested.Dispose();
        }
    }
}
