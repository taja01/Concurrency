# AsyncKeyedLock

A high-performance, thread-safe implementation of keyed asynchronous locking for .NET. This library provides a reusable concurrency primitive that allows you to acquire locks based on keys, ensuring that only one operation per key can execute at a time.

[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download)
[![C#](https://img.shields.io/badge/C%23-14.0-brightgreen.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Features

✅ **Keyed Locking** - Lock operations based on specific keys  
✅ **Async/Await Support** - Fully asynchronous with `ValueTask` for optimal performance  
✅ **Timeout Support** - Try to acquire locks with configurable timeouts  
✅ **Cancellation Support** - Integrates with `CancellationToken`  
✅ **Resource Cleanup** - Automatic cleanup of unused semaphores  
✅ **Custom Comparers** - Support for custom key equality comparers  
✅ **Thread-Safe** - Built on `ConcurrentDictionary` and `SemaphoreSlim`  
✅ **Reference Counting** - Prevents race conditions during cleanup  
✅ **Zero Allocations** - Uses `IAsyncDisposable` pattern for efficient resource management  

## Installation
Clone the repository
git clone https://github.com/taja01/Concurrency.git

Or add as a project reference
dotnet add reference path/to/Concurrency.csproj
## Quick Start

### Basic Usage
```
using Concurrency;

var keyedLock = new AsyncKeyedLock<string>(); // Acquire a lock for a specific key 
await using (var lockHandle = await keyedLock.AcquireAsync("user-123")) 
{ 
	// Critical section - only one operation per key 
	await ProcessUserDataAsync("user-123"); 
}
	// Lock is automatically released
```
### With Timeout
```
var keyedLock = new AsyncKeyedLock<string>();
// Try to acquire with a timeout 
var lockHandle = await keyedLock.TryAcquireAsync( "resource-456", timeout: TimeSpan.FromSeconds(5) );
if (lockHandle != null) 
{ 
	await using (lockHandle) 
	{ 
		// Lock acquired successfully 
		await ProcessResourceAsync("resource-456");
	} 
} 
else 2
{ 
	// Timeout occurred 
	Console.WriteLine("Could not acquire lock within timeout");
}
```
### With Cancellation
```
var keyedLock = new AsyncKeyedLock<string>();
var cts = new CancellationTokenSource();
try 
{ 
	await using var lockHandle = await keyedLock.AcquireAsync( "task-789", cts.Token );
	await LongRunningOperationAsync("task-789", cts.Token);
} catch (OperationCanceledException) 
{
	Console.WriteLine("Operation was cancelled"); 
}
```
### Custom Key Comparer
```
// Case-insensitive string keys 
var keyedLock = new AsyncKeyedLock<string>(StringComparer.OrdinalIgnoreCase);

// "User" and "user" are treated as the same key 
await using (var lock1 = await keyedLock.AcquireAsync("User")) 
{ 
	// This will block until lock1 is released 
	var lock2 = await keyedLock.TryAcquireAsync("user", TimeSpan.FromSeconds(1)); // lock2 will be null (timeout) 
}
```

## Use Cases

### 1. User-Specific Operations
Ensure only one operation per user is executing at a time:

```
private readonly AsyncKeyedLock<string> _userLock = new();
public async Task<bool> UpdateUserProfileAsync(string userId, UserProfile profile)
{
	await using var lockHandle = await _userLock.AcquireAsync(userId);

	// Ensure only one profile update per user at a time
	return await _userRepository.UpdateAsync(userId, profile);
}
```

### 2. Resource Access Control
Control access to shared resources:
```
private readonly AsyncKeyedLock<string> _fileLock = new();
public async Task WriteToFileAsync(string filename, string content) 
{ 
	await using var lockHandle = await _fileLock.AcquireAsync(filename);
	
	// Ensure exclusive write access per file
	await File.WriteAllTextAsync(filename, content);
}
```

### 3. API Rate Limiting
Implement per-key rate limiting:
```
private readonly AsyncKeyedLock<string> _apiLock = new();
public async Task<ApiResponse> CallExternalApiAsync(string apiKey) 
{ 
	var lockHandle = await _apiLock.TryAcquireAsync( apiKey, TimeSpan.FromMilliseconds(100) );
	if (lockHandle == null)
	{
		return new ApiResponse { Error = "Rate limit exceeded" };
	}

	await using (lockHandle)
	{
		return await _httpClient.GetAsync($"api/endpoint?key={apiKey}");
	}
}

```

### 4. Database Row Locking
Prevent concurrent modifications to the same database row:
```
private readonly AsyncKeyedLock<int> _rowLock = new();
public async Task<bool> UpdateOrderAsync(int orderId, Order order) 
{
	await using var lockHandle = await _rowLock.AcquireAsync(orderId);
	
	// Prevent race conditions on order updates
	var existing = await _db.Orders.FindAsync(orderId);
	existing.Status = order.Status;
	existing.UpdatedAt = DateTime.UtcNow;
	
	return await _db.SaveChangesAsync() > 0;
}
```

## API Reference

### Constructor
```
public AsyncKeyedLock(IEqualityComparer<TKey>? comparer = null)
```

Creates a new instance with an optional custom equality comparer for keys.

### AcquireAsync
```
public ValueTask<IAsyncDisposable> AcquireAsync( TKey key, CancellationToken token = default)

```
Acquires a lock for the specified key. Waits indefinitely until the lock is acquired.

**Parameters:**
- `key` - The key to acquire the lock for
- `token` - Optional cancellation token

**Returns:** An `IAsyncDisposable` that releases the lock when disposed

**Throws:** `OperationCanceledException` if cancelled

### TryAcquireAsync
```
public ValueTask<IAsyncDisposable?> TryAcquireAsync( TKey key, TimeSpan timeout, CancellationToken token = default)
```

Attempts to acquire a lock for the specified key within the timeout period.

**Parameters:**
- `key` - The key to acquire the lock for
- `timeout` - Maximum time to wait for the lock
- `token` - Optional cancellation token

**Returns:** An `IAsyncDisposable` if successful, `null` if timeout expires

**Throws:** `OperationCanceledException` if cancelled

## Performance Characteristics

- **Lock Acquisition**: O(1) average case using `ConcurrentDictionary`
- **Memory**: Semaphores are automatically cleaned up when no longer in use
- **Allocation**: Minimal allocations using `ValueTask` and reference counting
- **Contention**: Uses `SemaphoreSlim` for efficient async waiting

## Best Practices

1. **Always use `await using`** to ensure locks are properly released:
```
await using var lockHandle = await keyedLock.AcquireAsync(key);
```
2. **Use `TryAcquireAsync` for timeout scenarios** to avoid indefinite blocking:
```
var lockHandle = await keyedLock.TryAcquireAsync(key, TimeSpan.FromSeconds(30)); if (lockHandle == null) { /* handle timeout */ }
```

3. **Keep critical sections short** to minimize lock contention

4. **Consider key granularity** - too coarse reduces concurrency, too fine increases memory usage

5. **Pass cancellation tokens** for long-running operations:

```
await using var lockHandle = await keyedLock.AcquireAsync(key, cancellationToken);
```

## Thread Safety

`AsyncKeyedLock<TKey>` is fully thread-safe and can be safely shared across multiple threads and tasks. Internal synchronization is handled using:

- `ConcurrentDictionary` for thread-safe key storage
- `SemaphoreSlim` for async lock coordination
- `Interlocked` operations for reference counting

## Requirements

- .NET 10.0 or higher
- C# 14.0 or higher

## Testing

The library includes comprehensive unit tests covering:
- Concurrent access scenarios
- Timeout handling
- Cancellation support
- Resource cleanup
- Reference counting
- Custom comparers

Run tests:
dotnet test


## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

Built with performance and safety in mind using modern .NET async patterns.

## Support

For issues, questions, or contributions, please visit the [GitHub repository](https://github.com/taja01/Concurrency).