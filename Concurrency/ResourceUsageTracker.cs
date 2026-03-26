using System.Collections.Concurrent;
namespace Concurrency
{
    public sealed class ResourceUsageTracker<TKey> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, bool> _usedItems;
        private readonly IEqualityComparer<TKey> _comparer;

        /// <summary>
        /// Initializes the tracker with an optional custom equality comparer for keys.
        /// </summary>
        /// <param name="comparer">An equality comparer for keys. Defaults to EqualityComparer<TKey>.Default.</param>
        public ResourceUsageTracker(IEqualityComparer<TKey>? comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _usedItems = new ConcurrentDictionary<TKey, bool>(_comparer);
        }

        /// <summary>
        /// Attempts to mark the specified key as "used," if it isn’t already in use.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        /// <returns>True if the key was free and is now marked as "used," otherwise false.</returns>
        public bool TryUseItem(TKey key)
        {
            return _usedItems.TryAdd(key, true);
        }

        /// <summary>
        /// Checks if the specified key is currently in use.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        /// <returns>True if the key is in use; otherwise false.</returns>
        public bool IsItemUsed(TKey key)
        {
            return _usedItems.TryGetValue(key, out var isUsed) && isUsed;
        }

        /// <summary>
        /// Releases the specified key, marking it as "free" again.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        /// <returns>True if the key was found and released; otherwise false.</returns>
        public bool ReleaseItem(TKey key)
        {
            return _usedItems.TryRemove(key, out _);
        }

        /// <summary>
        /// Gets the total number of resources currently marked as "in use."
        /// </summary>
        public int CountUsedItems => _usedItems.Count;

        /// <summary>
        /// Clears all items, resetting the tracker.
        /// </summary>
        public void Clear()
        {
            _usedItems.Clear();
        }
    }
}