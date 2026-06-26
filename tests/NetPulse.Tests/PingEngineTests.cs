using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.App.Core;
using Moq;
using Xunit;

namespace NetPulse.Tests
{
    public class PingEngineTests
    {
        [Fact]
        public async Task PingEngine_DispatchesSuccessEvent()
        {
            var mockProvider = new Mock<IPingProvider>();
            mockProvider
                .Setup(p => p.SendPingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PingReplyWrapper
                {
                    Status = IPStatus.Success,
                    RoundtripTime = 15,
                    Address = null
                });

            var cb = new CircuitBreaker();
            var fallback = new FallbackChecker();
            var targets = new List<Target> { new Target { Address = "8.8.8.8", DisplayName = "Google" } };

            var engine = new PingEngine(mockProvider.Object, cb, fallback, targets)
            {
                IntervalMs = 50, // Short interval to run test quickly
                PingTimeoutMs = 40
            };

            bool eventFired = false;
            PingResultEventArgs? firedArgs = null;
            engine.OnPingCompleted += (s, e) =>
            {
                eventFired = true;
                firedArgs = e;
            };

            var cts = new CancellationTokenSource();
            await engine.StartAsync(cts.Token);

            // Wait briefly to allow the loop to run at least once
            await Task.Delay(100);

            await engine.StopAsync();
            cts.Cancel();

            Assert.True(eventFired);
            Assert.NotNull(firedArgs);
            Assert.True(firedArgs.Success);
            Assert.Equal(15, firedArgs.RttMs);
            Assert.Equal("8.8.8.8", firedArgs.Target);
        }

        [Fact]
        public async Task PingEngine_DetectsConnectionDropAndRestore()
        {
            var mockProvider = new Mock<IPingProvider>();

            // Setup mock: call 1 fails (TimedOut), call 2 succeeds
            int callCount = 0;
            mockProvider
                .Setup(p => p.SendPingAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return Task.FromResult(new PingReplyWrapper { Status = IPStatus.TimedOut });
                    }
                    return Task.FromResult(new PingReplyWrapper { Status = IPStatus.Success, RoundtripTime = 5 });
                });

            var cb = new CircuitBreaker();
            var fallback = new FallbackChecker();
            var targets = new List<Target> { new Target { Address = "8.8.8.8", DisplayName = "Google" } };

            var engine = new PingEngine(mockProvider.Object, cb, fallback, targets)
            {
                IntervalMs = 50,
                PingTimeoutMs = 40
            };

            bool droppedFired = false;
            bool restoredFired = false;

            engine.OnConnectionDropped += (s, e) => droppedFired = true;
            engine.OnConnectionRestored += (s, e) => restoredFired = true;

            var cts = new CancellationTokenSource();
            await engine.StartAsync(cts.Token);

            // Wait for pings to execute
            await Task.Delay(150);

            await engine.StopAsync();
            cts.Cancel();

            Assert.True(droppedFired);
            Assert.True(restoredFired);
        }
    }
}
