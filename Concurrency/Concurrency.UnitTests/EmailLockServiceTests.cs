using BaseProject;
using Concurrency.UnitTests.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Concurrency.UnitTests
{
    [TestFixture]
    internal class EmailLockServiceTests : BaseTest
    {
        private EmailLockService _service = null!;

        [SetUp]
        public void SetUp() => _service = base.ServiceProvider.GetRequiredService<EmailLockService>();

        [Test]
        public async Task Test()
        {
            var acquireTimeout = TimeSpan.FromSeconds(1);
            var email1 = await _service.TryAcquireEmailAsync(acquireTimeout);
            var email2 = await _service.TryAcquireEmailAsync(acquireTimeout);
            var email3 = await _service.TryAcquireEmailAsync(acquireTimeout);

            Assert.That(email1, Is.Not.Null);
            Assert.That(email2, Is.Not.Null);
            Assert.That(email3, Is.Null);


            await _service.ReleaseEmailAsync(email1);
            await _service.ReleaseEmailAsync(email2);
        }

        [Test]
        public async Task Test2()
        {
            var acquireTimeout = TimeSpan.FromSeconds(1);
            var email1 = await _service.TryAcquireEmailAsync(acquireTimeout);
            var email2 = await _service.TryAcquireEmailAsync(acquireTimeout);

            Assert.That(email1, Is.Not.Null);
            Assert.That(email2, Is.Not.Null);

            await _service.ReleaseEmailAsync(email1);
            await _service.ReleaseEmailAsync(email2);

            var email3 = await _service.TryAcquireEmailAsync(acquireTimeout);

            Assert.That(email3, Is.Not.Null);

            await _service.ReleaseEmailAsync(email3);
        }
    }
}
