using System.Collections.Concurrent;
using System.Diagnostics;

namespace Concurrency
{
    /// <summary>
    /// Manages a pool of reusable resources, allowing concurrent tasks to acquire a lock on an available resource,
    /// use it, and then release it back to the pool.
    /// </summary>
    /// <typeparam name="T">The type of the resource being managed in the pool.</typeparam>
    public sealed class AsyncResourcePool<T> : IAsyncDisposable where T : notnull
    {
        private readonly AsyncKeyedLock<T> _internalLocker = new();
        // Stores the IAsyncDisposable releaser for each actively locked resource.
        private readonly ConcurrentDictionary<T, IAsyncDisposable> _activeLocks;
        // Queue of resources that are currently considered available for locking.
        private readonly ConcurrentQueue<T> _availableResourcesQueue;
        // The original list of all manageable resources. For re-queuing purposes if needed, though not strictly used in current logic.
        private readonly List<T> _allManagedResources;


        /// <summary>
        /// Initializes a new instance of the AsyncResourcePool class with a collection of resources to manage.
        /// </summary>
        /// <param name="resourcesToManage">The collection of resources that this pool will manage.</param>
        /// <param name="comparer">Optional equality comparer for the resource type T. Recommended if T is a complex type.</param>
        public AsyncResourcePool(IEnumerable<T> resourcesToManage, IEqualityComparer<T>? comparer = null)
        {
            if (resourcesToManage == null) throw new ArgumentNullException(nameof(resourcesToManage));

            _allManagedResources = resourcesToManage.Distinct(comparer).ToList();

            if (!_allManagedResources.Any())
            {
                throw new ArgumentException("Must provide at least one resource to manage.", nameof(resourcesToManage));
            }

            // Use the provided comparer for the dictionary to handle equality correctly for type T.
            _activeLocks = new ConcurrentDictionary<T, IAsyncDisposable>(comparer);

            // Initialize the queue with all managed resources, shuffled for better initial distribution under contention.
            _availableResourcesQueue = new ConcurrentQueue<T>(_allManagedResources.OrderBy(_ => Guid.NewGuid()));
        }

        /// <summary>
        /// Tries to acquire a lock on any free resource from the managed pool.
        /// </summary>
        /// <param name="overallTimeout">The total time to wait for a resource lock to become available.</param>
        /// <param name="ct">A cancellation token to observe for the entire operation.</param>
        /// <returns>The resource that was locked, or the default value of T (e.g., null for reference types) if no resource could be locked within the timeout or if the operation was cancelled.</returns>
        public async ValueTask<T?> TryAcquireResourceAsync(TimeSpan? overallTimeout, CancellationToken ct = default)
        {
            overallTimeout = overallTimeout ?? TimeSpan.FromSeconds(5);
            // Use a linked CancellationTokenSource to handle both the overall timeout and the external cancellation token.
            using var timeoutCts = new CancellationTokenSource(overallTimeout.Value);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var linkedToken = linkedCts.Token;

            var stopwatch = Stopwatch.StartNew();

            while (!linkedToken.IsCancellationRequested)
            {
                if (_availableResourcesQueue.TryDequeue(out T? resourceToTry))
                {
                    // Attempt to acquire the lock for this specific resource using the internal AsyncKeyedLock.
                    // Use a very short timeout here. The main waiting is handled by the outer polling loop.
                    IAsyncDisposable? resourceLockInstance = await _internalLocker.TryAcquireAsync(resourceToTry, TimeSpan.FromMilliseconds(50), linkedToken).ConfigureAwait(false);

                    if (resourceLockInstance != null)
                    {
                        // Successfully locked the specific resource. Now, register it in our _activeLocks dictionary.
                        if (_activeLocks.TryAdd(resourceToTry, resourceLockInstance))
                        {
                            return resourceToTry; // Success! Return the acquired resource.
                        }
                        else
                        {
                            // This is an unexpected state: lock acquired but couldn't add to _activeLocks.
                            // This could happen in a rare race condition.
                            // Release the acquired lock immediately and put the resource back in the queue.
                            await resourceLockInstance.DisposeAsync().ConfigureAwait(false);
                            _availableResourcesQueue.Enqueue(resourceToTry);
                        }
                    }
                    else
                    {
                        // Failed to lock this specific resource (it was busy).
                        // Add it back to the end of the queue to be tried again later by another task.
                        _availableResourcesQueue.Enqueue(resourceToTry);
                    }
                }

                // If the queue was empty or the chosen resource was busy, wait a bit before retrying.
                // This prevents a tight spin-loop and yields the thread.
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), linkedToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // This is expected if the linkedToken is cancelled during the delay. We will exit the loop.
                    break;
                }
            }

            // If we exit the loop, it's due to timeout or cancellation.
            return default;
        }

        /// <summary>
        /// Releases the lock for a specified resource, making it available for other tasks.
        /// </summary>
        /// <param name="resource">The resource to release back to the pool.</param>
        public async ValueTask ReleaseResourceAsync(T resource)
        {
            if (resource == null) return;

            if (_activeLocks.TryRemove(resource, out IAsyncDisposable? resourceLockInstance))
            {
                // Disposing this instance will trigger the Release method in AsyncKeyedLock.
                await resourceLockInstance.DisposeAsync().ConfigureAwait(false);

                // Add the resource back to the queue of available resources.
                _availableResourcesQueue.Enqueue(resource);
            }
            else
            {
                // This can happen if ReleaseResourceAsync is called multiple times for the same resource
                // or for a resource that was not successfully acquired.
                Debug.WriteLine($"AsyncResourcePool: Attempted to release resource '{resource}' which was not found in the active locks collection.");
            }
        }

        /// <summary>
        /// Disposes the pool, releasing any outstanding locks it is currently managing.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            var activeLockKeys = _activeLocks.Keys.ToList();

            foreach (var resourceKey in activeLockKeys)
            {
                if (_activeLocks.TryRemove(resourceKey, out var lockToDispose))
                {
                    await lockToDispose.DisposeAsync().ConfigureAwait(false);
                }
            }
            _activeLocks.Clear();

            // Clear the queue of any remaining items.
            while (_availableResourcesQueue.TryDequeue(out _)) { }
        }
    }
}
