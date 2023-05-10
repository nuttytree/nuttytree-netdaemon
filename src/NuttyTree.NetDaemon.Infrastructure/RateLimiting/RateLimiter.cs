using System.Collections.Concurrent;

namespace NuttyTree.NetDaemon.Infrastructure.RateLimiting;

internal sealed class RateLimiter<T> : IDisposable, IRateLimiter<T>
    where T : class
{
    private readonly ConcurrentDictionary<string, InternalRateLimiter> rateLimiters = new ();

    private readonly string defaultRateLimiterName = typeof(T).FullName!;

    public TimeSpan DefaultDelayBetweenTasks { get; set; } = TimeSpan.FromMinutes(1);

    public Task WaitAsync(TimeSpan delayBetweenTasks, CancellationToken cancellationToken = default)
        => WaitAsync(defaultRateLimiterName, delayBetweenTasks, cancellationToken);

    public Task WaitAsync(string rateLimiterName, CancellationToken cancellationToken = default)
        => WaitAsync(rateLimiterName, null, cancellationToken);

    public Task WaitAsync(CancellationToken cancellationToken = default)
        => WaitAsync(defaultRateLimiterName, null, cancellationToken);

    public Task WaitAsync(string rateLimiterName, TimeSpan? delayBetweenTasks, CancellationToken cancellationToken = default)
        => rateLimiters.GetOrAdd(rateLimiterName, _ => new (delayBetweenTasks ?? DefaultDelayBetweenTasks)).WaitAsync(cancellationToken);

    public void Dispose()
    {
        foreach (var rateLimitedTask in rateLimiters.Values)
        {
            rateLimitedTask.Dispose();
        }
    }

    private sealed class InternalRateLimiter : IDisposable
    {
        private readonly SemaphoreSlim semaphore = new (1, 1);

        private readonly TimeSpan delayBetweenTasks;

        private CancellationTokenSource? nextTaskTrigger;

        public InternalRateLimiter(TimeSpan delayBetweenTasks)
        {
            this.delayBetweenTasks = delayBetweenTasks;
        }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            finally
            {
                nextTaskTrigger = new CancellationTokenSource(delayBetweenTasks);
                nextTaskTrigger.Token.Register(() =>
                {
                    semaphore.Release();
                });
            }
        }

        public void Dispose()
        {
            semaphore.Dispose();
            nextTaskTrigger?.Dispose();
        }
    }
}
