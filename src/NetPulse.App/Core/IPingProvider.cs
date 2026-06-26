using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace NetPulse.App.Core
{
    public class PingReplyWrapper
    {
        public IPStatus Status { get; set; }
        public long RoundtripTime { get; set; }
        public IPAddress? Address { get; set; }
    }

    public interface IPingProvider
    {
        Task<PingReplyWrapper> SendPingAsync(string address, int timeout, CancellationToken cancellationToken);
    }
}
