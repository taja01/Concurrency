namespace Concurrency.UnitTests
{
    internal class ResourceUsageTrackerTests
    {
        private ResourceUsageTracker<string> _validator = null!;

        [SetUp]
        public void SetUp() => _validator = new ResourceUsageTracker<string>();

        [Test]
        public void TryUseItem_Should_Return_True_For_New_Email()
        {
            // Arrange
            var email = "test@example.com";

            // Act
            var result = _validator.TryUseItem(email);

            // Assert
            Assert.That(result, "The email should be free to use initially.");
        }

        [Test]
        public void TryUseItem_Should_Return_False_For_Email_Already_In_Use()
        {
            // Arrange
            var email = "test@example.com";
            _validator.TryUseItem(email); // Mark it as "used"

            // Act
            var result = _validator.TryUseItem(email);

            // Assert
            Assert.That(result, Is.False, "The email should not be free to use once it's marked as used.");
        }


        [Test]
        public void ReleaseItem_Should_Make_Email_Free()
        {
            // Arrange
            var email = "test@example.com";
            _validator.TryUseItem(email); // Mark it as "used"

            // Act
            var releaseResult = _validator.ReleaseItem(email);
            var reuseResult = _validator.TryUseItem(email);

            // Assert
            Assert.That(releaseResult, "Releasing the email should succeed.");
            Assert.That(reuseResult, "The email should be free again after releasing.");
        }

        [Test]
        public async Task TryUseItem_Should_Be_ThreadSafe_For_Emails()
        {
            // Arrange
            var email = "test@example.com";
            var numThreads = 10;
            var tasks = new Task<bool>[numThreads];

            // Act: Launch 10 concurrent threads, all trying to use the same email
            for (var i = 0; i < numThreads; i++)
            {
                tasks[i] = Task.Run(() => _validator.TryUseItem(email));
            }

            var results = await Task.WhenAll(tasks);

            // Assert: Only one thread should be able to use the email
            var successfulUses = 0;
            foreach (var result in results)
            {
                if (result) successfulUses++;
            }

            Assert.That(successfulUses, Is.EqualTo(1));
        }

        [Test]
        public void IsItemUsed_Should_Return_Correct_State()
        {
            // Arrange
            var email = "test@example.com";

            // Act & Assert
            Assert.That(_validator.IsItemUsed(email), Is.False, "Initially, the email should not be in use.");
            _validator.TryUseItem(email);
            Assert.That(_validator.IsItemUsed(email), "The email should be marked as in use after being used.");
            _validator.ReleaseItem(email);
            Assert.That(_validator.IsItemUsed(email), Is.False, "The email should no longer be marked as in use after being released.");
        }
    }
}
