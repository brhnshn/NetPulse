using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.App.Core;

namespace NetPulse.App.Infrastructure
{
    public class LogWriter : IDisposable
    {
        private readonly ConfigManager _configManager;
        private readonly string _logDir;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly string _sessionId = Guid.NewGuid().ToString();
        private readonly SqliteDbManager _dbManager;

        public SemaphoreSlim FileLock => _semaphore;
        public string SessionId => _sessionId;
        public SqliteDbManager DbManager => _dbManager;
        public ConfigManager Config => _configManager;

        public LogWriter(ConfigManager configManager, string? logDir = null)
        {
            _configManager = configManager;
            _logDir = logDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }

            _dbManager = new SqliteDbManager(_logDir);

            RotateIfNeeded();
        }

        public string GetLogFilePath(DateTime timestamp)
        {
            bool useUtc = _configManager.Config.UseUtcTimestamps;
            DateTime date = useUtc ? timestamp.ToUniversalTime() : timestamp.ToLocalTime();
            return Path.Combine(_logDir, $"log_{date:yyyy_MM_dd}.txt");
        }

        public async Task WriteAsync(LogEntry entry, bool flushNow = false, CancellationToken ct = default)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                // Assign session ID and timestamp string
                entry.SessionId = _sessionId;
                
                DateTime time = DateTime.UtcNow;
                entry.TimestampUtcString = _configManager.Config.UseUtcTimestamps 
                    ? time.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    : time.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");

                // Serialize and sign
                string line = SerializeAndSign(entry);

                // Insert into SQLite database
                var dbLog = new DbPingLog
                {
                    Timestamp = time,
                    Target = entry.Target,
                    Event = entry.Event,
                    Status = entry.Status,
                    RttMs = entry.RttMs,
                    Seq = entry.Seq,
                    SessionId = entry.SessionId,
                    Notes = entry.Notes,
                    Hmac = entry.Hmac
                };
                await _dbManager.InsertPingLogAsync(dbLog);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private string SerializeAndSign(LogEntry entry)
        {
            entry.Hmac = null;
            string jsonWithoutHmac = JsonSerializer.Serialize(entry);

            string key = _configManager.Config.HmacKey;
            if (!string.IsNullOrEmpty(key))
            {
                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonWithoutHmac));
                    entry.Hmac = Convert.ToBase64String(hash);
                }
            }

            return JsonSerializer.Serialize(entry);
        }

        public void RotateIfNeeded()
        {
            try
            {
                int retentionDays = _configManager.Config.LogRetentionDays;
                if (retentionDays <= 0) return;

                // 1. Silinmeden önce CSV arşivini otomatik oluştur
                ArchiveOldLogsToCsv(retentionDays);

                // 2. Prune SQLite database
                _ = _dbManager.PruneOldLogsAsync(retentionDays);

                // Clean up legacy text files if they exist
                if (Directory.Exists(_logDir))
                {
                    var files = Directory.GetFiles(_logDir, "log_*.txt");
                    DateTime threshold = DateTime.UtcNow.Date.AddDays(-retentionDays);

                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        if (fileName.Length == 14 && fileName.StartsWith("log_"))
                        {
                            string dateStr = fileName.Substring(4); // extract YYYY_MM_DD
                            if (DateTime.TryParseExact(dateStr, "yyyy_MM_dd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                            {
                                if (fileDate < threshold)
                                {
                                    File.Delete(file);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Suppress background rotation errors
            }
        }

        public void ArchiveOldLogsToCsv(int retentionDays)
        {
            try
            {
                var oldLogs = _dbManager.GetOldPingLogsForArchivingAsync(retentionDays).GetAwaiter().GetResult();
                if (oldLogs.Count == 0) return;

                string archiveDir = Path.Combine(_logDir, "Archives");
                if (!Directory.Exists(archiveDir))
                {
                    Directory.CreateDirectory(archiveDir);
                }

                string archivePath = Path.Combine(archiveDir, $"archive_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                using (var writer = new StreamWriter(archivePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("zaman,hedef,olay,durum,gecikme_ms,sira_no,oturum_id,notlar,hmac");
                    foreach (var entry in oldLogs)
                    {
                        string rttStr = entry.RttMs.HasValue ? entry.RttMs.Value.ToString("F1") : "";
                        string csvLine = $"{EscapeCsvField(entry.Timestamp.ToString("o"))}," +
                                         $"{EscapeCsvField(entry.Target)}," +
                                         $"{EscapeCsvField(entry.Event)}," +
                                         $"{EscapeCsvField(entry.Status)}," +
                                         $"{rttStr}," +
                                         $"{entry.Seq}," +
                                         $"{EscapeCsvField(entry.SessionId)}," +
                                         $"{EscapeCsvField(entry.Notes)}," +
                                         $"{EscapeCsvField(entry.Hmac)}";
                        writer.WriteLine(csvLine);
                    }
                }
            }
            catch (Exception)
            {
                // Suppress archiving errors
            }
        }

        private string EscapeCsvField(string? field)
        {
            if (field == null) return string.Empty;
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
