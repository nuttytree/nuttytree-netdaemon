using System.Reactive.Linq;
using Serilog;

namespace System.Reactive.Concurrency;
public static class ISchedulerExtensions
{
    private static TaskCompletionSource<bool> nextTaskReady = new TaskCompletionSource<bool>();

    public static IDisposable SchedulePeriodic(this IScheduler scheduler, TimeSpan period, Func<CancellationToken, Task> action)
    {
        return Observable
            .Interval(period, scheduler)
            .Select(_ => Observable.FromAsync(async c =>
            {
                try
                {
                    await action.Invoke(c);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected exception in periodic task");
                }
            }))
            .Concat()
            .Subscribe();
    }

    public static IDisposable Sched(TimeSpan period, Func<CancellationToken, Task> action)
    {
        var stopRequested = new CancellationTokenSource();

        _ = RunLoopAsync(period, action, stopRequested.Token);

        return stopRequested;
    }

    private static async Task RunLoopAsync(TimeSpan period, Func<CancellationToken, Task> action, CancellationToken stopRequested)
    {
        CancellationTokenSource? nextIterationTrigger = null;

        while (!stopRequested.IsCancellationRequested)
        {
            await action(stopRequested);
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

    private static async Task RunAsync(CancellationToken applicationStopping)
    {
        CancellationTokenSource? nextTaskDue = null;
        while (!applicationStopping.IsCancellationRequested)
        {
            try
            {
                await nextTaskReady.Task.WaitAsync(applicationStopping);

                if (!(await nextTaskReady.Task.ConfigureAwait(false)))
                {
                    nextTaskDue?.Dispose();
                    nextTaskReady = new TaskCompletionSource<bool>();
                    continue;
                }

                var test = new TaskCompletionSource();
                test.TrySetResult();
                nextTaskReady = new TaskCompletionSource<bool>();
                nextTaskDue = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                nextTaskDue.Token.Register(() =>
                {
                    if (true)
                    {
                        nextTaskReady.TrySetResult(true);
                    }
                });
            }
            catch (Exception)
            {
                // This catch only exists to ensure we don't crash the handler
            }
        }

        nextTaskDue?.Dispose();
    }
}
