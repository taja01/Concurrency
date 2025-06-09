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
        private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _gates;

        public AsyncKeyedLock(IEqualityComparer<TKey>? comparer = null)
        {
            _gates = new ConcurrentDictionary<TKey, SemaphoreSlim>(comparer);
        }

        /// <summary>
        /// Tries to acquire a lock for the specified key asynchronously.
        /// </summary>
        /// <param name="key">The key to acquire the lock for.</param>
        /// <param name="timeout">The maximum time to wait for the lock.</param>
        /// <param name="token">A cancellation token to observe.</param>
        /// <returns>An IAsyncDisposable that releases the lock when disposed. Returns null if the lock was not acquired within the timeout or was cancelled.</returns>
        public async ValueTask<IAsyncDisposable?> TryAcquireAsync(
            TKey key,
            TimeSpan timeout,
            CancellationToken token = default)
        {
            var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            if (!await gate.WaitAsync(timeout, token).ConfigureAwait(false))
            {
                return null;
            }

            return new Releaser(this, key, gate);
        }

        private void Release(TKey key, SemaphoreSlim gate)
        {
            gate.Release();

            // Clean up the semaphore if it's no longer in use to conserve resources.
            if (gate.CurrentCount == 1 && _gates.TryRemove(key, out var removedGate))
            {
                if (ReferenceEquals(gate, removedGate))
                {
                    removedGate.Dispose();
                }
                else
                {
                    // This is a rare race condition where the gate for the key was replaced.
                    // Dispose the one that was actually removed.
                    removedGate.Dispose();
                }
            }
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private readonly AsyncKeyedLock<TKey> _owner;
            private readonly TKey _key;
            private readonly SemaphoreSlim _gate;
            private int _disposed; // 0 for not disposed, 1 for disposed.

            public Releaser(AsyncKeyedLock<TKey> owner, TKey key, SemaphoreSlim gate)
            {
                _owner = owner;
                _key = key;
                _gate = gate;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _owner.Release(_key, _gate);
                }
                return default;
            }
        }
    }
}
