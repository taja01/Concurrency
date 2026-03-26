using BaseProject;

namespace Concurrency.UnitTests
{
    [TestFixture]
    internal class AsyncKeyedLockTests : BaseTest
    {
        private AsyncKeyedLock<string> _lock = null!;

        [SetUp]
        public void SetUp()
        {
            _lock = new AsyncKeyedLock<string>();
        }

        #region AcquireAsync Tests

        [Test]
        public async Task AcquireAsync_Should_Allow_Single_Access_Per_Key()
        {
            // Arrange
            const string key = "test-key";

            // Act
            await using var lock1 = await _lock.AcquireAsync(key);

            // Assert
            Assert.That(lock1, Is.Not.Null);
        }

        [Test]
        public async Task AcquireAsync_Should_Block_Concurrent_Access_To_Same_Key()
        {
            // Arrange
            const string key = "test-key";
            var firstAcquired = false;
            var secondBlocked = false;
            var secondAcquired = false;

            // Act
            await using (var lock1 = await _lock.AcquireAsync(key))
            {
                firstAcquired = true;

                var task2 = Task.Run(async () =>
                {
                    secondBlocked = true;
                    await using var lock2 = await _lock.AcquireAsync(key);
                    secondAcquired = true;
                });

                // Give task2 time to attempt acquisition
                await Task.Delay(100);

                // Assert - second should be blocked
                Assert.That(firstAcquired, Is.True);
                Assert.That(secondBlocked, Is.True);
                Assert.That(secondAcquired, Is.False, "Second lock should be blocked while first is held");

                // Release first lock
            }

            // Wait for second to complete
            await Task.Delay(100);
            Assert.That(secondAcquired, Is.True, "Second lock should acquire after first is released");
        }

        [Test]
        public async Task AcquireAsync_Should_Allow_Concurrent_Access_To_Different_Keys()
        {
            // Arrange
            const string key1 = "key-1";
            const string key2 = "key-2";
            var lock1Acquired = false;
            var lock2Acquired = false;

            // Act
            await using (var lock1 = await _lock.AcquireAsync(key1))
            {
                lock1Acquired = true;

                await using (var lock2 = await _lock.AcquireAsync(key2))
                {
                    lock2Acquired = true;
                }
            }

            // Assert
            Assert.That(lock1Acquired, Is.True);
            Assert.That(lock2Acquired, Is.True);
        }

        [Test]
        public async Task AcquireAsync_Should_Allow_Reacquisition_After_Release()
        {
            // Arrange
            const string key = "test-key";

            // Act & Assert - First acquisition
            await using (var lock1 = await _lock.AcquireAsync(key))
            {
                Assert.That(lock1, Is.Not.Null);
            }

            // Act & Assert - Second acquisition after release
            await using (var lock2 = await _lock.AcquireAsync(key))
            {
                Assert.That(lock2, Is.Not.Null);
            }
        }

        [Test]
        public async Task AcquireAsync_Should_Throw_TaskCanceledException_When_Cancelled()
        {
            // Arrange
            const string key = "test-key";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _lock.AcquireAsync(key, cts.Token);
            });
        }

        [Test]
        public async Task AcquireAsync_Should_Release_RefCount_On_Cancellation()
        {
            // Arrange
            const string key = "test-key";
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act - Acquire first lock
            await using (var lock1 = await _lock.AcquireAsync(key))
            {
                // Try to acquire with cancellation in another task
                var task = Task.Run(async () =>
                {
                    await _lock.AcquireAsync(key, cts.Token);
                });

                // Wait and expect cancellation
                var ex = Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
            }

            // Assert - Should be able to reacquire after cancellation
            await using (var lock2 = await _lock.AcquireAsync(key))
            {
                Assert.That(lock2, Is.Not.Null);
            }
        }

        [Test]
        public async Task AcquireAsync_Should_Support_Multiple_Sequential_Operations()
        {
            // Arrange
            const string key = "test-key";
            const int iterations = 100;

            // Act & Assert
            for (int i = 0; i < iterations; i++)
            {
                await using var lockHandle = await _lock.AcquireAsync(key);
                Assert.That(lockHandle, Is.Not.Null);
            }
        }

        [Test]
        public async Task AcquireAsync_Should_Handle_Multiple_Waiters()
        {
            // Arrange
            const string key = "test-key";
            var completedCount = 0;
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var lockHandle = await _lock.AcquireAsync(key);
                    await Task.Delay(10);
                    Interlocked.Increment(ref completedCount);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.That(completedCount, Is.EqualTo(10));
        }

        #endregion

        #region TryAcquireAsync Tests

        [Test]
        public async Task TryAcquireAsync_Should_Return_Lock_When_Available()
        {
            // Arrange
            const string key = "test-key";
            var timeout = TimeSpan.FromSeconds(1);

            // Act
            var result = await _lock.TryAcquireAsync(key, timeout);

            // Assert
            Assert.That(result, Is.Not.Null);

            await result!.DisposeAsync();
        }

        [Test]
        public async Task TryAcquireAsync_Should_Return_Null_When_Timeout_Expires()
        {
            // Arrange
            const string key = "test-key";
            var timeout = TimeSpan.FromMilliseconds(100);

            // Act
            await using (var lock1 = await _lock.TryAcquireAsync(key, TimeSpan.FromSeconds(5)))
            {
                Assert.That(lock1, Is.Not.Null);

                // Try to acquire with short timeout
                var lock2 = await _lock.TryAcquireAsync(key, timeout);

                // Assert
                Assert.That(lock2, Is.Null, "Should return null when timeout expires");
            }
        }

        [Test]
        public async Task TryAcquireAsync_Should_Succeed_If_Lock_Released_Before_Timeout()
        {
            // Arrange
            const string key = "test-key";
            var timeout = TimeSpan.FromSeconds(2);
            IAsyncDisposable? lock2 = null;

            // Act
            var lock1Task = Task.Run(async () =>
            {
                await using var l = await _lock.TryAcquireAsync(key, timeout);
                await Task.Delay(500);
            });

            await Task.Delay(100); // Ensure first lock is acquired

            var lock2Task = Task.Run(async () =>
            {
                lock2 = await _lock.TryAcquireAsync(key, timeout);
            });

            await Task.WhenAll(lock1Task, lock2Task);

            // Assert
            Assert.That(lock2, Is.Not.Null, "Should acquire lock after first is released within timeout");

            if (lock2 != null)
                await lock2.DisposeAsync();
        }

        [Test]
        public async Task TryAcquireAsync_Should_Allow_Different_Keys_Concurrently()
        {
            // Arrange
            const string key1 = "key-1";
            const string key2 = "key-2";
            var timeout = TimeSpan.FromSeconds(1);

            // Act
            await using var lock1 = await _lock.TryAcquireAsync(key1, timeout);
            await using var lock2 = await _lock.TryAcquireAsync(key2, timeout);

            // Assert
            Assert.That(lock1, Is.Not.Null);
            Assert.That(lock2, Is.Not.Null);
        }

        [Test]
        public void TryAcquireAsync_Should_Return_Null_When_Cancelled()
        {
            // Arrange
            const string key = "test-key";
            var timeout = TimeSpan.FromSeconds(5);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _lock.TryAcquireAsync(key, timeout, cts.Token);
            });
        }

        [Test]
        public async Task TryAcquireAsync_Should_Cleanup_RefCount_On_Timeout()
        {
            // Arrange
            const string key = "test-key";
            var shortTimeout = TimeSpan.FromMilliseconds(100);
            var longTimeout = TimeSpan.FromSeconds(2);

            // Act
            await using (var lock1 = await _lock.TryAcquireAsync(key, longTimeout))
            {
                Assert.That(lock1, Is.Not.Null);

                // Try with timeout
                var lock2 = await _lock.TryAcquireAsync(key, shortTimeout);
                Assert.That(lock2, Is.Null);
            }

            // Assert - Should be able to reacquire
            await using (var lock3 = await _lock.TryAcquireAsync(key, longTimeout))
            {
                Assert.That(lock3, Is.Not.Null);
            }
        }

        [Test]
        public async Task TryAcquireAsync_Should_Handle_Zero_Timeout()
        {
            // Arrange
            const string key = "test-key";
            var zeroTimeout = TimeSpan.Zero;

            // Act - First should succeed immediately
            await using (var lock1 = await _lock.TryAcquireAsync(key, TimeSpan.FromSeconds(1)))
            {
                Assert.That(lock1, Is.Not.Null);

                // Second with zero timeout should fail immediately
                var lock2 = await _lock.TryAcquireAsync(key, zeroTimeout);

                // Assert
                Assert.That(lock2, Is.Null);
            }
        }

        [Test]
        public async Task TryAcquireAsync_Should_Support_Concurrent_Timeouts()
        {
            // Arrange
            const string key = "test-key";
            var timeout = TimeSpan.FromMilliseconds(100);
            var failedCount = 0;

            // Act
            await using (var lock1 = await _lock.TryAcquireAsync(key, TimeSpan.FromSeconds(5)))
            {
                var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
                {
                    var result = await _lock.TryAcquireAsync(key, timeout);
                    if (result == null)
                    {
                        Interlocked.Increment(ref failedCount);
                    }
                    else
                    {
                        await result.DisposeAsync();
                    }
                }));

                await Task.WhenAll(tasks);
            }

            // Assert
            Assert.That(failedCount, Is.GreaterThan(0), "Some acquisitions should timeout");
        }

        #endregion

        #region Disposal Tests

        [Test]
        public async Task Releaser_Should_Handle_Multiple_Dispose_Calls()
        {
            // Arrange
            const string key = "test-key";
            var timeout = TimeSpan.FromSeconds(1);

            // Act
            var lockHandle = await _lock.TryAcquireAsync(key, timeout);
            Assert.That(lockHandle, Is.Not.Null);

            // Dispose multiple times
            await lockHandle!.DisposeAsync();
            await lockHandle.DisposeAsync();
            await lockHandle.DisposeAsync();

            // Assert - Should be able to acquire again
            await using var lock2 = await _lock.TryAcquireAsync(key, timeout);
            Assert.That(lock2, Is.Not.Null);
        }

        #endregion

        #region Custom Comparer Tests

        [Test]
        public async Task Constructor_Should_Support_Custom_Comparer()
        {
            // Arrange
            var caseInsensitiveLock = new AsyncKeyedLock<string>(StringComparer.OrdinalIgnoreCase);
            const string key1 = "TEST";
            const string key2 = "test";
            var timeout = TimeSpan.FromMilliseconds(100);

            // Act
            await using (var lock1 = await caseInsensitiveLock.AcquireAsync(key1))
            {
                // Should block because keys are equal with case-insensitive comparer
                var lock2 = await caseInsensitiveLock.TryAcquireAsync(key2, timeout);

                // Assert
                Assert.That(lock2, Is.Null, "Should treat 'TEST' and 'test' as same key");
            }
        }

        #endregion
    }
}