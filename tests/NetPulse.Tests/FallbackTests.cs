using System;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.App.Core;
using Xunit;

namespace NetPulse.Tests
{
    public class FallbackTests
    {
        [Fact]
        public async Task FallbackChecker_RunsSuccessfullyAndFormatsDetails()
        {
            var checker = new FallbackChecker();

            // Run fallback diagnostics against loopback address (most likely no DNS port 53 open locally)
            var result = await checker.RunFallbackChecksAsync("127.0.0.1", CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Details));
            Assert.Contains("TCP Port 53", result.Details);
            Assert.Contains("HTTP GET", result.Details);
        }
    }
}
