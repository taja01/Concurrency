namespace Concurrency.UnitTests.Implementation
{
    public class EmailLockService
    {
        private readonly AsyncResourcePool<string> _emailPool;

        // The AsyncResourcePool is injected via the constructor.
        public EmailLockService(AsyncResourcePool<string> emailPool)
        {
            _emailPool = emailPool;
        }

        /// <summary>
        /// Tries to acquire a free email from the pool.
        /// </summary>
        public ValueTask<string?> TryAcquireEmailAsync(TimeSpan? timeout, CancellationToken ct = default)
        {
            return _emailPool.TryAcquireResourceAsync(timeout, ct);
        }

        /// <summary>
        /// Releases an email back to the pool.
        /// </summary>
        public ValueTask ReleaseEmailAsync(string email)
        {
            return _emailPool.ReleaseResourceAsync(email);
        }
    }
}
