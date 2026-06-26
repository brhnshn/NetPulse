using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetPulse.App.Core
{
    public class PingProvider : IPingProvider, IDisposable
    {
        // Cache Ping instances by target address to avoid concurrency collisions
        private readonly ConcurrentDictionary<string, Ping> _pings = new();

        public async Task<PingReplyWrapper> SendPingAsync(string address, int timeout, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[32]; // standard 32 bytes ping
            var options = new PingOptions(64, true); // TTL 64, don't fragment
            
            try
            {
                // Resolve hostname to IPAddress first
                IPAddress ipAddress;
                if (!IPAddress.TryParse(address, out ipAddress!))
                {
                    var ipAddresses = await Dns.GetHostAddressesAsync(address, cancellationToken);
                    if (ipAddresses.Length > 0)
                    {
                        ipAddress = ipAddresses[0];
                    }
                    else
                    {
                        return new PingReplyWrapper
                        {
                            Status = IPStatus.Unknown,
                            RoundtripTime = 0,
                            Address = null
                        };
                    }
                }

                // Retrieve or create the dedicated Ping instance for this address
                var ping = _pings.GetOrAdd(address, _ => new Ping());

                var reply = await ping.SendPingAsync(ipAddress, TimeSpan.FromMilliseconds(timeout), buffer, options, cancellationToken);
                return new PingReplyWrapper
                {
                    Status = reply.Status,
                    RoundtripTime = reply.RoundtripTime,
                    Address = reply.Address
                };
            }
            catch (Exception)
            {
                return new PingReplyWrapper
                {
                    Status = IPStatus.Unknown,
                    RoundtripTime = 0,
                    Address = null
                };
            }
        }

        public void Dispose()
        {
            foreach (var ping in _pings.Values)
            {
                try
                {
                    ping.Dispose();
                }
                catch
                {
                    // Suppress disposal errors
                }
            }
            _pings.Clear();
        }
    }
}
