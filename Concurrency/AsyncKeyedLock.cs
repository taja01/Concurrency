using System.Collections.Concurrent;

namespace Concurrency
{
    /// <summary>
    /// Provides a mechanism for acquiring keyed asynchronous locks.
    /// This is a reusable, not domain-specific, concurrency primitive.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used for locking.</typeparam>
    public sealed class AsyncKeyedLock<TKey> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, RefCountedSemaphore> _gates;

        public AsyncKeyedLock(IEqualityComparer<TKey>? comparer = null)
        {
            _gates = new ConcurrentDictionary<TKey, RefCountedSemaphore>(comparer);
        }

        /// <summary>
        /// Acquires a lock for the specified key asynchronously.
        /// </summary>
        /// <param name="key">The key to acquire the lock for.</param>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns>An IAsyncDisposable that releases the lock when disposed.</returns>
        public async ValueTask<IAsyncDisposable> AcquireAsync(
            TKey key,
            CancellationToken token = default)
        {
            RefCountedSemaphore refCounted;

            while (true)
            {
                refCounted = _gates.GetOrAdd(key, _ => new RefCountedSemaphore());

                if (refCounted.TryAddRef())
                {
                    break;
                }

                // Semaphore was disposed, retry
                _gates.TryRemove(key, out _);
            }

            try
            {
                await refCounted.Semaphore.WaitAsync(token).ConfigureAwait(false);
                return new Releaser(this, key, refCounted);
            }
            catch
            {
                refCounted.RemoveRef(_gates, key);
                throw;
            }
        }

        /// <summary>
        /// Tries to acquire a lock for the specified key asynchronously.
        /// </summary>
        /// <param name="key">The key to acquire the lock for.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns>An IAsyncDisposable that releases the lock when disposed. Returns null if the lock was not acquired.</returns>
        public async ValueTask<IAsyncDisposable?> TryAcquireAsync(
            TKey key,
            TimeSpan timeout,
            CancellationToken token = default)
        {
            RefCountedSemaphore refCounted;

            while (true)
            {
                refCounted = _gates.GetOrAdd(key, _ => new RefCountedSemaphore());

                if (refCounted.TryAddRef())
                {
                    break;
                }

                _gates.TryRemove(key, out _);
            }

            try
            {
                if (!await refCounted.Semaphore.WaitAsync(timeout, token).ConfigureAwait(false))
                {
                    refCounted.RemoveRef(_gates, key);
                    return null;
                }

                return new Releaser(this, key, refCounted);
            }
            catch
            {
                refCounted.RemoveRef(_gates, key);
                throw;
            }
        }

        private sealed class RefCountedSemaphore
        {
            public SemaphoreSlim Semaphore { get; }
            private int _refCount = 1;
            private int _disposed;

            public RefCountedSemaphore()
            {
                Semaphore = new SemaphoreSlim(1, 1);
            }

            public bool TryAddRef()
            {
                int current;
                do
                {
                    current = Volatile.Read(ref _refCount);
                    if (current <= 0) return false;
                }
                while (Interlocked.CompareExchange(ref _refCount, current + 1, current) != current);

                return true;
            }

            public void RemoveRef(ConcurrentDictionary<TKey, RefCountedSemaphore> gates, TKey key)
            {
                if (Interlocked.Decrement(ref _refCount) == 0)
                {
                    gates.TryRemove(key, out _);

                    if (Interlocked.Exchange(ref _disposed, 1) == 0)
                    {
                        Semaphore.Dispose();
                    }
                }
            }
        }

        private sealed class Releaser(AsyncKeyedLock<TKey> owner, TKey key, AsyncKeyedLock<TKey>.RefCountedSemaphore refCounted) : IAsyncDisposable
        {
            private int _disposed;

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    refCounted.Semaphore.Release();
                    refCounted.RemoveRef(owner._gates, key);
                }
                return default;
            }
        }
    }
}