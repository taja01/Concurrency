using System.Diagnostics.CodeAnalysis;

namespace Concurrency.UnitTests
{
    internal class ResourceUsageTrackerComplexTests
    {
        private ResourceUsageTracker<Booking> _validator = null!;

        [SetUp]
        public void SetUp() => _validator = new ResourceUsageTracker<Booking>(new BookingComparer());

        [Test]
        public void Test1()
        {
            // Arrange
            var booking = new Booking()
            {
                BookingId = 1,
                EmailAddress = "test@example.com",
            };

            // Act
            var result = _validator.TryUseItem(booking);

            // Assert
            Assert.That(result, "The email should be free to use initially.");
        }

        [Test]
        public void Test2()
        {
            // Arrange
            var booking = new Booking()
            {
                BookingId = 1,
                EmailAddress = "test@example.com",
            };

            // Act
            var result = _validator.TryUseItem(booking);
            var result2 = _validator.TryUseItem(booking);

            // Assert
            Assert.That(result);
            Assert.That(result2, Is.False);
        }

        [Test]
        public void Test3()
        {
            // Arrange
            var booking1 = new Booking()
            {
                BookingId = 1,
                EmailAddress = "test@example.com",
            };

            var booking2 = new Booking()
            {
                BookingId = 1,
                EmailAddress = "test@example.com",
            };

            // Act
            var result = _validator.TryUseItem(booking1);
            var result2 = _validator.TryUseItem(booking2);

            // Assert
            Assert.That(result);
            Assert.That(result2, Is.False);
        }
    }

    class Booking
    {
        public int BookingId { get; set; }
        public string EmailAddress { get; set; }
    }

    class BookingComparer : IEqualityComparer<Booking>
    {
        /// <summary>
        /// Determines whether two Booking objects are equal based on BookingId and EmailAddress.
        /// </summary>
        public bool Equals(Booking? x, Booking? y)
        {
            // Handle null bookings
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;

            // Compare BookingId and EmailAddress case-insensitively
            return x.BookingId == y.BookingId &&
                   string.Equals(x.EmailAddress, y.EmailAddress, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Generates a hash code for a Booking object.
        /// </summary>
        public int GetHashCode([DisallowNull] Booking obj)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));

            // Combine field hash codes
            var hashBookingId = obj.BookingId.GetHashCode();
            var hashEmailAddress = obj.EmailAddress?.ToUpperInvariant().GetHashCode() ?? 0; // Case-insensitive hashing

            return HashCode.Combine(hashBookingId, hashEmailAddress); // Use built-in HashCode.Combine for better distribution
        }
    }
}
