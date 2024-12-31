namespace NuttyTree.NetDaemon.Infrastructure.Scheduler;

public interface ITriggerableTask : IDisposable
{
    void Trigger();
}
