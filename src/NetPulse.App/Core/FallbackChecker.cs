using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetPulse.App.Core
{
    public class FallbackResult
    {
        public bool TcpPort53Success { get; set; }
        public bool HttpGetSuccess { get; set; }
        public string Details { get; set; } = string.Empty;
    }

    public class FallbackChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        public async Task<FallbackResult> RunFallbackChecksAsync(string targetAddress, CancellationToken ct)
        {
            var result = new FallbackResult();

            // 1. TCP Connect to Port 53 on Target Address
            try
            {
                using (var client = new TcpClient())
                {
                    // Use a short 2-second timeout for TCP connect
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        cts.CancelAfter(2000);
                        await client.ConnectAsync(targetAddress, 53, cts.Token);
                        result.TcpPort53Success = client.Connected;
                    }
                }
            }
            catch (Exception)
            {
                result.TcpPort53Success = false;
            }

            // 2. HTTP GET to https://www.google.com/generate_204
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(3000);
                    var response = await _httpClient.GetAsync("https://www.google.com/generate_204", cts.Token);
                    result.HttpGetSuccess = response.IsSuccessStatusCode;
                }
            }
            catch (Exception)
            {
                result.HttpGetSuccess = false;
            }

            result.Details = $"TCP Port 53 (DNS): {(result.TcpPort53Success ? "Başarılı" : "Başarısız")}, HTTP GET (Google): {(result.HttpGetSuccess ? "Başarılı" : "Başarısız")}";
            return result;
        }
    }
}
