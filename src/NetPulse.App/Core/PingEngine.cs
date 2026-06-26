using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.App.Infrastructure; // ConfigManager'a erişmek için eklendi

namespace NetPulse.App.Core
{
    public class FallbackEventArgs : EventArgs
    {
        public DateTime Timestamp { get; }
        public string Target { get; }
        public bool TcpSuccess { get; }
        public bool HttpSuccess { get; }
        public string Details { get; }

        public FallbackEventArgs(DateTime timestamp, string target, bool tcpSuccess, bool httpSuccess, string details)
        {
            Timestamp = timestamp;
            Target = target;
            TcpSuccess = tcpSuccess;
            HttpSuccess = httpSuccess;
            Details = details;
        }
    }

    public class CircuitBreakerEventArgs : EventArgs
    {
        public DateTime Timestamp { get; }
        public string Target { get; }
        public bool Tripped { get; }
        public string Message { get; }

        public CircuitBreakerEventArgs(DateTime timestamp, string target, bool tripped, string message)
        {
            Timestamp = timestamp;
            Target = target;
            Tripped = tripped;
            Message = message;
        }
    }

    public class PingEngine
    {
        private readonly IPingProvider _pingProvider;
        private readonly CircuitBreaker _circuitBreaker;
        private readonly FallbackChecker _fallbackChecker;
        private readonly ConfigManager _configManager; // Altyapı bağlantısı eklendi

        private readonly List<Target> _targets = new();
        private readonly Dictionary<string, bool> _connectionStates = new();
        private readonly Dictionary<string, long> _sequences = new();

        private CancellationTokenSource? _cts;
        private Task? _runTask;
        private bool _isRunning;
        private readonly object _stateLock = new();

        public int IntervalMs { get; set; } = 1000;
        public int PingTimeoutMs { get; set; } = 900;

        public event EventHandler<PingResultEventArgs>? OnPingCompleted;
        public event EventHandler<ConnectionEventArgs>? OnConnectionDropped;
        public event EventHandler<ConnectionEventArgs>? OnConnectionRestored;
        public event EventHandler<FallbackEventArgs>? OnFallbackTriggered;
        public event EventHandler<CircuitBreakerEventArgs>? OnCircuitBreakerTriggered;

        public IReadOnlyList<Target> Targets
        {
            get
            {
                lock (_stateLock)
                {
                    return _targets.ToList().AsReadOnly();
                }
            }
        }

        public bool IsRunning => _isRunning;

        // Constructor for testing / mocking
        public PingEngine(
            IPingProvider pingProvider,
            CircuitBreaker circuitBreaker,
            FallbackChecker fallbackChecker,
            IEnumerable<Target> targets)
        {
            _pingProvider = pingProvider;
            _circuitBreaker = circuitBreaker;
            _fallbackChecker = fallbackChecker;
            _configManager = null!;

            UpdateTargets(targets);
        }

        // Constructor, ConfigManager'ı bağımlılık olarak alacak şekilde güncellendi
        public PingEngine(
            IPingProvider pingProvider,
            CircuitBreaker circuitBreaker,
            FallbackChecker fallbackChecker,
            ConfigManager configManager)
        {
            _pingProvider = pingProvider;
            _circuitBreaker = circuitBreaker;
            _fallbackChecker = fallbackChecker;
            _configManager = configManager;

            // config.json her değiştiğinde hedefleri ve aralığı dinamik güncelleme köprüsü
            _configManager.OnConfigChanged += (sender, args) =>
            {
                UpdateTargets(args.Config.Targets);
                IntervalMs = args.Config.IntervalMs;
            };

            // İlk açılışta mevcut konfigürasyonu motora dolduruyoruz
            UpdateTargets(_configManager.Config.Targets);
            IntervalMs = _configManager.Config.IntervalMs;
        }

public void UpdateTargets(IEnumerable<Target> newTargets)
        {
            lock (_stateLock)
            {
                _targets.Clear();
                foreach (var target in newTargets)
                {
                    _targets.Add(target);
                    if (!_connectionStates.ContainsKey(target.Address))
                    {
                        _connectionStates[target.Address] = true; // assume connected initially
                    }
                    if (!_sequences.ContainsKey(target.Address))
                    {
                        // Düzenlenen Kısım: C# Dictionary indeksleyicilerinde "address:" şeklinde isimlendirme yapılamaz
                        _sequences[target.Address] = 0;
                    }
                }
            }
        }

        public Task StartAsync(CancellationToken ct)
        {
            lock (_stateLock)
            {
                if (_isRunning) return Task.CompletedTask;
                _isRunning = true;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _runTask = RunEngineLoopAsync(_cts.Token);
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? cts = null;
            Task? runTask = null;

            lock (_stateLock)
            {
                if (_isRunning)
                {
                    _isRunning = false;
                    cts = _cts;
                }
                runTask = _runTask;
            }

            if (cts != null)
            {
                try { cts.Cancel(); } catch { }
            }

            if (runTask != null)
            {
                try
                {
                    await runTask;
                }
                catch (OperationCanceledException) { }
                finally
                {
                    lock (_stateLock)
                    {
                        if (cts != null && _cts == cts)
                        {
                            try { cts.Dispose(); } catch { }
                            _cts = null;
                            _runTask = null;
                        }
                    }
                }
            }
        }

        private async Task RunEngineLoopAsync(CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            long targetTime = IntervalMs;

            while (!ct.IsCancellationRequested)
            {
                long startTime = stopwatch.ElapsedMilliseconds;

                List<Target> targetsCopy;
                lock (_stateLock)
                {
                    // Sadece aktif (IsEnabled = true) olan hedefleri süzüyoruz
                    targetsCopy = _targets.Where(t => t.IsEnabled).ToList();
                }

                if (targetsCopy.Count > 0)
                {
                    var tasks = targetsCopy.Select(target => PingTargetAsync(target, ct));
                    await Task.WhenAll(tasks);
                }

                long elapsed = stopwatch.ElapsedMilliseconds;
                long delay = targetTime - elapsed;

                if (delay > 0)
                {
                    try
                    {
                        await Task.Delay((int)delay, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                targetTime += IntervalMs;
                
                if (stopwatch.ElapsedMilliseconds > targetTime + IntervalMs * 2)
                {
                    targetTime = stopwatch.ElapsedMilliseconds + IntervalMs;
                }
            }
        }

        private async Task PingTargetAsync(Target target, CancellationToken ct)
        {
            string address = target.Address;

            // 1. Check Circuit Breaker status
            if (_circuitBreaker.IsTripped(address))
            {
                return;
            }

            long seq;
            lock (_stateLock)
            {
                _sequences[address]++;
                seq = _sequences[address];
            }

            // 2. Perform Ping
            var reply = await _pingProvider.SendPingAsync(address, PingTimeoutMs, ct);

            // 3. Process Reply
            if (reply.Status == IPStatus.Success)
            {
                _circuitBreaker.RecordSuccess(address);

                bool wasDropped = false;
                lock (_stateLock)
                {
                    if (!_connectionStates[address])
                    {
                        _connectionStates[address] = true;
                        wasDropped = true;
                    }
                }

                if (wasDropped)
                {
                    OnConnectionRestored?.Invoke(this, new ConnectionEventArgs(DateTime.Now, address, "Bağlantı başarıyla sağlandı (Ping başarılı)", seq));
                }

                OnPingCompleted?.Invoke(this, new PingResultEventArgs(DateTime.Now, address, true, reply.RoundtripTime, seq));
            }
            else
            {
                // Ping Failed
                bool trippedNow;
                _circuitBreaker.RecordFailure(address, out trippedNow);

                bool wasConnected = false;
                lock (_stateLock)
                {
                    if (_connectionStates[address])
                    {
                        _connectionStates[address] = false;
                        wasConnected = true;
                    }
                }

                if (wasConnected)
                {
                    string trReason = GetTurkishIPStatusReason(reply.Status);
                    OnConnectionDropped?.Invoke(this, new ConnectionEventArgs(DateTime.Now, address, trReason, seq));
                }

                OnPingCompleted?.Invoke(this, new PingResultEventArgs(DateTime.Now, address, false, null, seq));

                int failureCount = _circuitBreaker.GetConsecutiveFailures(address);

                // Run Fallback Checks on exactly 3 consecutive failures
                if (failureCount == 3)
                {
                    _ = RunFallbackAndDispatchAsync(address, seq, ct);
                }

                // Notify if Circuit Breaker tripped
                if (trippedNow)
                {
                    string msg = $"{address} için üst üste 10 başarısız ping denemesinden sonra devre kesici (Circuit Breaker) tetiklendi. İzleme 60 saniye duraklatıldı.";
                    OnCircuitBreakerTriggered?.Invoke(this, new CircuitBreakerEventArgs(DateTime.Now, address, true, msg));
                }
            }
        }

        private string GetTurkishIPStatusReason(IPStatus status)
        {
            return status switch
            {
                IPStatus.TimedOut => "Zaman Aşımı (Timeout)",
                IPStatus.DestinationHostUnreachable => "Hedef Bilgisayara Ulaşılamıyor",
                IPStatus.DestinationNetworkUnreachable => "Hedef Ağa Ulaşılamıyor",
                IPStatus.HardwareError => "Donanım Hatası",
                IPStatus.PacketTooBig => "Paket Çok Büyük",
                IPStatus.BadRoute => "Hatalı Rota",
                _ => $"Bilinmeyen Hata ({status})"
            };
        }

        private async Task RunFallbackAndDispatchAsync(string address, long seq, CancellationToken ct)
        {
            try
            {
                var fallbackResult = await _fallbackChecker.RunFallbackChecksAsync(address, ct);
                OnFallbackTriggered?.Invoke(this, new FallbackEventArgs(
                    DateTime.Now,
                    address,
                    fallbackResult.TcpPort53Success,
                    fallbackResult.HttpGetSuccess,
                    fallbackResult.Details
                ));
            }
            catch (Exception)
            {
                // Suppress exception in background diagnostic check
            }
        }
    }
}