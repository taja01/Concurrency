using System.Collections.Concurrent;
namespace Concurrency
{
    public sealed class ResourceUsageTracker<TKey> where TKey : notnull
    {
        // ConcurrentDictionary to track the state of each item (true = in use, false = free).
        private readonly ConcurrentDictionary<TKey, bool> _usedItems;

        /// <summary>
        /// Initializes the validator.
        /// </summary>
        public ResourceUsageTracker()
        {
            _usedItems = new ConcurrentDictionary<TKey, bool>();
        }

        /// <summary>
        /// Validates whether the item is free to use, and marks it as "used" if free.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        /// <returns>
        /// Returns true if the item was free and is now marked as "used".
        /// Returns false if the item is already in use.
        /// </returns>
        public bool TryUseItem(TKey key)
        {
            // Add the item to the dictionary only if it does not exist (i.e., it's free).
            return _usedItems.TryAdd(key, true);
        }

        /// <summary>
        /// Checks whether an item is already in use.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        /// <returns>True if the item is in use, otherwise false.</returns>
        public bool IsItemUsed(TKey key)
        {
            return _usedItems.TryGetValue(key, out var isUsed) && isUsed;
        }

        /// <summary>
        /// Releases the item, marking it as "free" to use again.
        /// </summary>
        /// <param name="key">The resource identifier.</param>
        /// <returns>
        /// Returns true if the item was successfully released (i.e., found and removed),
        /// Returns false if the item was not found.
        /// </returns>
        public bool ReleaseItem(TKey key)
        {
            return _usedItems.TryRemove(key, out _);
        }
    }
}