using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.App.Core;
using NetPulse.App.Infrastructure;
using Xunit;

namespace NetPulse.Tests
{
    public class LogWriterTests : IDisposable
    {
        private readonly string _tempLogDir;
        private readonly string _configPath;
        private readonly ConfigManager _configManager;

        public LogWriterTests()
        {
            _tempLogDir = Path.Combine(Path.GetTempPath(), "NetPulse_Test_Logs_" + Guid.NewGuid());
            _configPath = Path.Combine(_tempLogDir, "config.json");

            if (!Directory.Exists(_tempLogDir))
            {
                Directory.CreateDirectory(_tempLogDir);
            }

            var defaultConf = new AppConfig
            {
                Targets = new(),
                IntervalMs = 1000,
                LogRetentionDays = 2,
                HmacKey = "TestSecretKey",
                UseUtcTimestamps = true
            };
            string json = JsonSerializer.Serialize(defaultConf);
            File.WriteAllText(_configPath, json);

            _configManager = new ConfigManager(_configPath);
        }

        [Fact]
        public async Task LogWriter_WritesJsonWithHmacSignature()
        {
            using (var logWriter = new LogWriter(_configManager, _tempLogDir))
            {
                var entry = new LogEntry
                {
                    Target = "8.8.8.8",
                    Event = "PingResult",
                    Status = "Success",
                    RttMs = 12.3,
                    Seq = 1
                };

                await logWriter.WriteAsync(entry, false, CancellationToken.None);

                string dbPath = Path.Combine(_tempLogDir, "netpulse.db");
                Assert.True(File.Exists(dbPath));

                var logs = await logWriter.DbManager.GetAllPingLogsAsync();
                Assert.Single(logs);

                var readEntry = logs[0];
                Assert.NotNull(readEntry);
                Assert.Equal("8.8.8.8", readEntry.Target);
                Assert.Equal("PingResult", readEntry.Event);
                Assert.Equal("Success", readEntry.Status);
                Assert.Equal(12.3, readEntry.RttMs);
                Assert.Equal(1, readEntry.Seq);
                Assert.False(string.IsNullOrEmpty(readEntry.Hmac));
            }
        }

        [Fact]
        public void LogWriter_RotatesOldFiles()
        {
            using (var logWriter = new LogWriter(_configManager, _tempLogDir))
            {
                string todayFile = Path.Combine(_tempLogDir, $"log_{DateTime.UtcNow:yyyy_MM_dd}.txt");
                string oldFile = Path.Combine(_tempLogDir, $"log_{DateTime.UtcNow.AddDays(-5):yyyy_MM_dd}.txt");

                File.WriteAllText(todayFile, "Today's logs");
                File.WriteAllText(oldFile, "Old logs to delete");

                logWriter.RotateIfNeeded();

                Assert.True(File.Exists(todayFile));
                Assert.False(File.Exists(oldFile)); // Should be deleted as log retention is 2 days
            }
        }

        [Fact]
        public async Task LogWriter_ArchivesOldLogsToCsvBeforePruning()
        {
            // Set retention days to 1
            var customConf = new AppConfig
            {
                Targets = new(),
                IntervalMs = 1000,
                LogRetentionDays = 1,
                HmacKey = "TestSecretKey",
                UseUtcTimestamps = true
            };
            string customConfigPath = Path.Combine(_tempLogDir, "custom_config.json");
            File.WriteAllText(customConfigPath, JsonSerializer.Serialize(customConf));

            using (var configManager = new ConfigManager(customConfigPath))
            using (var logWriter = new LogWriter(configManager, _tempLogDir))
            {
                var entry = new LogEntry
                {
                    Target = "8.8.8.8",
                    Event = "PingResult",
                    Status = "Success",
                    RttMs = 15.0,
                    Seq = 1
                };
                await logWriter.WriteAsync(entry, false, CancellationToken.None);

                // Backdate the logs in SQLite to 5 days ago to trigger pruning
                string dbPath = Path.Combine(_tempLogDir, "netpulse.db");
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    string backdateQuery = "UPDATE PingLogs SET Timestamp = $timestamp;";
                    using (var command = new Microsoft.Data.Sqlite.SqliteCommand(backdateQuery, connection))
                    {
                        command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.AddDays(-5).ToString("o"));
                        command.ExecuteNonQuery();
                    }
                }

                var oldPings = await logWriter.DbManager.GetOldPingLogsForArchivingAsync(1);
                Assert.Single(oldPings);

                logWriter.RotateIfNeeded();

                string archiveDir = Path.Combine(_tempLogDir, "Archives");
                Assert.True(Directory.Exists(archiveDir));

                string[] csvFiles = Directory.GetFiles(archiveDir, "archive_*.csv");
                Assert.Single(csvFiles);

                string[] csvLines = await File.ReadAllLinesAsync(csvFiles[0]);
                Assert.Equal(2, csvLines.Length);
                Assert.Contains("zaman,hedef,olay,durum,gecikme_ms", csvLines[0]);
                Assert.Contains("8.8.8.8", csvLines[1]);

                var pingsInDb = await logWriter.DbManager.GetAllPingLogsAsync();
                Assert.Empty(pingsInDb);
            }
        }

        public void Dispose()
        {
            _configManager.Dispose();
            if (Directory.Exists(_tempLogDir))
            {
                try
                {
                    Directory.Delete(_tempLogDir, true);
                }
                catch { }
            }
        }
    }
}
