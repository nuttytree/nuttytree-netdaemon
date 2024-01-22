namespace NuttyTree.NetDaemon.Infrastructure.Scheduler;

public interface ITaskScheduler
{
    IDisposable CreatePeriodicTask(TimeSpan period, Func<CancellationToken, Task> action);

    IDisposable CreateSelfSchedulingTask(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn);

    IDisposable CreateSelfSchedulingTask(Func<CancellationToken, Task<DateTime>> action, TimeSpan onExceptionRetryIn);

    ITriggerableTask CreateTriggerableSelfSchedulingTask(Func<CancellationToken, Task<TimeSpan>> action, TimeSpan onExceptionRetryIn);
}
