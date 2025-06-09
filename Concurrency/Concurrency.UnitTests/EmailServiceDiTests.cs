using Concurrency.UnitTests.Implementation;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Concurrency.UnitTests;

[TestFixture]
public class EmailServiceDiTests
{
    private ServiceProvider? _serviceProvider;
    private List<string> _managedEmails = new List<string> { "email1@test.com", "email2@test.com" };

    [SetUp]
    public void Setup()
    {
        // Arrange: Set up the DI container for each test.
        var services = new ServiceCollection();

        // Use the extension method to register the EmailService and its underlying pool.
        services.AddEmailServicePool(_managedEmails);

        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public async Task Cleanup()
    {
        // Dispose the service provider, which will handle disposal of any IDisposable singletons.
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _serviceProvider?.Dispose();
        }
    }

    [Test]
    public async Task CanAcquireAndReleaseEmail_Successfully()
    {
        // Arrange
        Assert.That(_serviceProvider, Is.Not.Null, "Service provider should be initialized.");
        var emailService = _serviceProvider.GetRequiredService<EmailLockService>();
        var acquireTimeout = TimeSpan.FromSeconds(1);

        // Act: Acquire an email
        var acquiredEmail1 = await emailService.TryAcquireEmailAsync(acquireTimeout);

        // Assert
        Assert.That(acquiredEmail1, Is.Not.Null, "Should be able to acquire an email from the pool.");
        Assert.That(_managedEmails.Contains(acquiredEmail1, StringComparer.OrdinalIgnoreCase), "Acquired email should be one of the managed emails.");

        // Act: Release the email
        await emailService.ReleaseEmailAsync(acquiredEmail1);

        // Act: Re-acquire to prove it was released
        var acquiredEmail2 = await emailService.TryAcquireEmailAsync(acquireTimeout);

        // Assert
        Assert.That(acquiredEmail2, Is.Not.Null, "Should be able to acquire an email again after it was released.");
    }

    [Test]
    public async Task TryAcquireEmail_ReturnsNull_WhenPoolIsExhaustedAndTimeoutOccurs()
    {
        // Arrange
        Assert.That(_serviceProvider, Is.Not.Null, "Service provider should be initialized.");
        var emailService = _serviceProvider.GetRequiredService<EmailLockService>();
        var acquireTimeout = TimeSpan.FromSeconds(1);
        var shortTimeoutForFailure = TimeSpan.FromMilliseconds(50);

        // Act: Exhaust the pool
        var lock1 = await emailService.TryAcquireEmailAsync(acquireTimeout);
        var lock2 = await emailService.TryAcquireEmailAsync(acquireTimeout);
        Assert.That(lock1, Is.Not.Null);
        Assert.That(lock2, Is.Not.Null);

        // Act: Try to acquire one more email with a short timeout
        var shouldBeNull = await emailService.TryAcquireEmailAsync(shortTimeoutForFailure);

        // Assert
        Assert.That(shouldBeNull, Is.Null, "Acquiring from an exhausted pool with a short timeout should return null.");

        // Cleanup: Release the locks
        await emailService.ReleaseEmailAsync(lock1);
        await emailService.ReleaseEmailAsync(lock2);
    }

    [Test]
    public async Task ConcurrentTasks_CanSuccessfullyUseEmailService()
    {
        // Arrange
        Assert.That(_serviceProvider, Is.Not.Null, "Service provider should be initialized.");

        int numberOfTasks = 5; // More tasks than emails to force waiting and reuse
        var successfulTasks = 0;
        var log = new ConcurrentBag<string>();
        var simulatedWorkDuration = TimeSpan.FromMilliseconds(200);
        var acquireTimeout = TimeSpan.FromSeconds(5); // Long enough for tasks to wait their turn

        // Action
        var workerTasks = new List<Task>();
        for (int i = 0; i < numberOfTasks; i++)
        {
            int taskId = i + 1;
            workerTasks.Add(Task.Run(async () =>
            {
                // Each task resolves its own scoped instance of EmailService
                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailLockService>();

                var acquiredEmail = await emailService.TryAcquireEmailAsync(acquireTimeout);
                if (acquiredEmail != null)
                {
                    try
                    {
                        log.Add($"Task {taskId} acquired {acquiredEmail}.");
                        await Task.Delay(simulatedWorkDuration);
                        Interlocked.Increment(ref successfulTasks);
                    }
                    finally
                    {
                        await emailService.ReleaseEmailAsync(acquiredEmail);
                        log.Add($"Task {taskId} released {acquiredEmail}.");
                    }
                }
                else
                {
                    log.Add($"Task {taskId} FAILED to acquire an email.");
                }
            }));
        }

        await Task.WhenAll(workerTasks);

        // Assert
        Debug.WriteLine(string.Join("\n", log));
        Assert.That(successfulTasks, Is.EqualTo(numberOfTasks), "All tasks should have successfully acquired and released an email.");
    }
}