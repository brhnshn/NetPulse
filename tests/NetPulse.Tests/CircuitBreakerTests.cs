using System;
using System.Threading;
using NetPulse.App.Core;
using Xunit;

namespace NetPulse.Tests
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void CircuitBreaker_StartsClosed()
        {
            var cb = new CircuitBreaker(3, TimeSpan.FromSeconds(1));
            Assert.False(cb.IsTripped("8.8.8.8"));
        }

        [Fact]
        public void CircuitBreaker_TripsAfterMaxFailures()
        {
            var cb = new CircuitBreaker(3, TimeSpan.FromSeconds(1));

            cb.RecordFailure("8.8.8.8", out bool tripped1);
            Assert.False(tripped1);
            Assert.False(cb.IsTripped("8.8.8.8"));

            cb.RecordFailure("8.8.8.8", out bool tripped2);
            Assert.False(tripped2);
            Assert.False(cb.IsTripped("8.8.8.8"));

            cb.RecordFailure("8.8.8.8", out bool tripped3);
            Assert.True(tripped3);
            Assert.True(cb.IsTripped("8.8.8.8"));
        }

        [Fact]
        public void CircuitBreaker_ResetsOnSuccess()
        {
            var cb = new CircuitBreaker(3, TimeSpan.FromSeconds(1));
            cb.RecordFailure("8.8.8.8", out _);
            cb.RecordFailure("8.8.8.8", out _);

            cb.RecordSuccess("8.8.8.8");

            cb.RecordFailure("8.8.8.8", out bool tripped);
            Assert.False(tripped);
            Assert.False(cb.IsTripped("8.8.8.8"));
        }

        [Fact]
        public void CircuitBreaker_CoolDownExpires()
        {
            // Set cooldown to 100 milliseconds
            var cb = new CircuitBreaker(2, TimeSpan.FromMilliseconds(100));
            cb.RecordFailure("8.8.8.8", out _);
            cb.RecordFailure("8.8.8.8", out bool tripped);
            Assert.True(tripped);
            Assert.True(cb.IsTripped("8.8.8.8"));

            Thread.Sleep(150); // wait for cooldown

            Assert.False(cb.IsTripped("8.8.8.8"));
        }
    }
}
