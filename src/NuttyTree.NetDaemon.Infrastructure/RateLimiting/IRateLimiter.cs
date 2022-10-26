namespace NuttyTree.NetDaemon.Infrastructure.RateLimiting;

public interface IRateLimiter<T>
{
    TimeSpan DefaultDelayBetweenTasks { get; set; }

    Task WaitAsync(TimeSpan delayBetweenTasks, CancellationToken cancellationToken = default);

    Task WaitAsync(string rateLimiterName, CancellationToken cancellationToken = default);

    Task WaitAsync(CancellationToken cancellationToken = default);

    Task WaitAsync(string rateLimiterName, TimeSpan? delayBetweenTasks = null, CancellationToken cancellationToken = default);
}
