namespace NuttyTree.NetDaemon.Infrastructure.Scheduler;

public interface IPeriodicTaskScheduler
{
    IDisposable SchedulePeriodicTask(TimeSpan period, Func<CancellationToken, Task> action);
}
