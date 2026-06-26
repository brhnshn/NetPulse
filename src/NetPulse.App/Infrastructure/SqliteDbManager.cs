using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NetPulse.App.Core;

namespace NetPulse.App.Infrastructure
{
    public class DbPingLog
    {
        public DateTime Timestamp { get; set; }
        public string Target { get; set; } = "";
        public string Event { get; set; } = "";
        public string Status { get; set; } = "";
        public double? RttMs { get; set; }
        public long Seq { get; set; }
        public string SessionId { get; set; } = "";
        public string? Notes { get; set; }
        public string? Hmac { get; set; }
    }

    public class DbOutage
    {
        public int Id { get; set; }
        public string Target { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double? Duration { get; set; }
        public string? Reason { get; set; }
        public long Seq { get; set; }
        public string SessionId { get; set; } = "";
    }

    public class SqliteDbManager
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly object _dbLock = new();

        public SqliteDbManager(string logDir)
        {
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            _dbPath = Path.Combine(logDir, "netpulse.db");
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            lock (_dbLock)
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    // PingLogs Tablosu
                    string createPingLogsTable = @"
                        CREATE TABLE IF NOT EXISTS PingLogs (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Timestamp TEXT NOT NULL,
                            Target TEXT NOT NULL,
                            Event TEXT NOT NULL,
                            Status TEXT NOT NULL,
                            RttMs REAL,
                            Seq INTEGER NOT NULL,
                            SessionId TEXT NOT NULL,
                            Notes TEXT,
                            Hmac TEXT
                        );
                        CREATE INDEX IF NOT EXISTS IX_PingLogs_Timestamp ON PingLogs(Timestamp);
                    ";
                    using (var command = new SqliteCommand(createPingLogsTable, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    // Outages Tablosu
                    string createOutagesTable = @"
                        CREATE TABLE IF NOT EXISTS Outages (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Target TEXT NOT NULL,
                            StartTime TEXT NOT NULL,
                            EndTime TEXT,
                            Duration REAL,
                            Reason TEXT,
                            Seq INTEGER NOT NULL,
                            SessionId TEXT NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS IX_Outages_StartTime ON Outages(StartTime);
                    ";
                    using (var command = new SqliteCommand(createOutagesTable, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public async Task InsertPingLogAsync(DbPingLog log)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    INSERT INTO PingLogs (Timestamp, Target, Event, Status, RttMs, Seq, SessionId, Notes, Hmac)
                    VALUES ($timestamp, $target, $event, $status, $rttMs, $seq, $sessionId, $notes, $hmac);
                ";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("$timestamp", log.Timestamp.ToString("o"));
                    command.Parameters.AddWithValue("$target", log.Target);
                    command.Parameters.AddWithValue("$event", log.Event);
                    command.Parameters.AddWithValue("$status", log.Status);
                    command.Parameters.AddWithValue("$rttMs", log.RttMs.HasValue ? log.RttMs.Value : DBNull.Value);
                    command.Parameters.AddWithValue("$seq", log.Seq);
                    command.Parameters.AddWithValue("$sessionId", log.SessionId);
                    command.Parameters.AddWithValue("$notes", (object?)log.Notes ?? DBNull.Value);
                    command.Parameters.AddWithValue("$hmac", (object?)log.Hmac ?? DBNull.Value);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<int> InsertOutageAsync(DbOutage outage)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    INSERT INTO Outages (Target, StartTime, EndTime, Duration, Reason, Seq, SessionId)
                    VALUES ($target, $startTime, $endTime, $duration, $reason, $seq, $sessionId);
                    SELECT last_insert_rowid();
                ";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("$target", outage.Target);
                    command.Parameters.AddWithValue("$startTime", outage.StartTime.ToString("o"));
                    command.Parameters.AddWithValue("$endTime", outage.EndTime.HasValue ? outage.EndTime.Value.ToString("o") : DBNull.Value);
                    command.Parameters.AddWithValue("$duration", outage.Duration.HasValue ? outage.Duration.Value : DBNull.Value);
                    command.Parameters.AddWithValue("$reason", (object?)outage.Reason ?? DBNull.Value);
                    command.Parameters.AddWithValue("$seq", outage.Seq);
                    command.Parameters.AddWithValue("$sessionId", outage.SessionId);
                    
                    var result = await command.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : 0;
                }
            }
        }

        public async Task UpdateOutageAsync(int id, DateTime endTime, double duration)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    UPDATE Outages 
                    SET EndTime = $endTime, Duration = $duration 
                    WHERE Id = $id;
                ";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("$id", id);
                    command.Parameters.AddWithValue("$endTime", endTime.ToString("o"));
                    command.Parameters.AddWithValue("$duration", duration);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<DbPingLog>> GetRecentPingLogsAsync(int limit)
        {
            var list = new List<DbPingLog>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT Timestamp, Target, Event, Status, RttMs, Seq, SessionId, Notes, Hmac
                    FROM PingLogs
                    WHERE Event = 'PingResult'
                    ORDER BY Timestamp DESC
                    LIMIT $limit;
                ";
                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("$limit", limit);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbPingLog
                            {
                                Timestamp = DateTime.Parse(reader.GetString(0)),
                                Target = reader.GetString(1),
                                Event = reader.GetString(2),
                                Status = reader.GetString(3),
                                RttMs = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                                Seq = reader.GetInt64(5),
                                SessionId = reader.GetString(6),
                                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Hmac = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            // Reverse list to show oldest to newest for UI insert sequence
            list.Reverse();
            return list;
        }

        public async Task<List<DbOutage>> GetAllOutagesAsync()
        {
            var list = new List<DbOutage>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT Id, Target, StartTime, EndTime, Duration, Reason, Seq, SessionId
                    FROM Outages
                    ORDER BY StartTime ASC;
                ";
                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbOutage
                            {
                                Id = reader.GetInt32(0),
                                Target = reader.GetString(1),
                                StartTime = DateTime.Parse(reader.GetString(2)),
                                EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                                Duration = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                                Reason = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Seq = reader.GetInt64(6),
                                SessionId = reader.GetString(7)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task<List<DbPingLog>> GetAllPingLogsAsync()
        {
            var list = new List<DbPingLog>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT Timestamp, Target, Event, Status, RttMs, Seq, SessionId, Notes, Hmac
                    FROM PingLogs
                    ORDER BY Timestamp ASC;
                ";
                using (var command = new SqliteCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbPingLog
                            {
                                Timestamp = DateTime.Parse(reader.GetString(0)),
                                Target = reader.GetString(1),
                                Event = reader.GetString(2),
                                Status = reader.GetString(3),
                                RttMs = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                                Seq = reader.GetInt64(5),
                                SessionId = reader.GetString(6),
                                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Hmac = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task CloseOpenOutagesOnStartupAsync(string currentSessionId)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                // Find all outages where EndTime is NULL
                string selectQuery = "SELECT Id, StartTime FROM Outages WHERE EndTime IS NULL;";
                var openOutageIds = new List<(int Id, DateTime StartTime)>();
                
                using (var selectCommand = new SqliteCommand(selectQuery, connection))
                {
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            openOutageIds.Add((reader.GetInt32(0), DateTime.Parse(reader.GetString(1))));
                        }
                    }
                }

                if (openOutageIds.Count > 0)
                {
                    DateTime now = DateTime.Now;
                    string updateQuery = @"
                        UPDATE Outages 
                        SET EndTime = $endTime, Duration = $duration, Reason = $reason, SessionId = $sessionId
                        WHERE Id = $id;
                    ";
                    foreach (var outage in openOutageIds)
                    {
                        using (var updateCommand = new SqliteCommand(updateQuery, connection))
                        {
                            double duration = (now - outage.StartTime).TotalSeconds;
                            if (duration < 0) duration = 0;

                            updateCommand.Parameters.AddWithValue("$id", outage.Id);
                            updateCommand.Parameters.AddWithValue("$endTime", now.ToString("o"));
                            updateCommand.Parameters.AddWithValue("$duration", duration);
                            updateCommand.Parameters.AddWithValue("$reason", "Uygulama Kapanışı/Kesintili Sonlanma");
                            updateCommand.Parameters.AddWithValue("$sessionId", currentSessionId);
                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        public async Task PruneOldLogsAsync(int retentionDays)
        {
            if (retentionDays <= 0) return;
            DateTime threshold = DateTime.UtcNow.AddDays(-retentionDays);
            string thresholdStr = threshold.ToString("o");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                // Sadece ham ping sinyal loglarını sil (Kesintiler sistemde süresiz kalır)
                string prunePings = "DELETE FROM PingLogs WHERE Timestamp < $threshold;";
                using (var command = new SqliteCommand(prunePings, connection))
                {
                    command.Parameters.AddWithValue("$threshold", thresholdStr);
                    await command.ExecuteNonQueryAsync();
                }

                // Veritabanını sıkıştır
                string vacuum = "VACUUM;";
                using (var command = new SqliteCommand(vacuum, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<(double SizeMb, long PingCount, long OutageCount)> GetDatabaseMetricsAsync()
        {
            double sizeMb = 0;
            long pingCount = 0;
            long outageCount = 0;

            try
            {
                if (File.Exists(_dbPath))
                {
                    sizeMb = new FileInfo(_dbPath).Length / (1024.0 * 1024.0);
                }

                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM PingLogs WHERE Event = 'PingResult';", connection))
                    {
                        pingCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }

                    using (var cmd = new SqliteCommand("SELECT COUNT(*) FROM Outages;", connection))
                    {
                        outageCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    }
                }
            }
            catch (Exception) { }

            return (sizeMb, pingCount, outageCount);
        }

        public async Task<List<DbPingLog>> QueryPingLogsAsync(string? target, DateTime start, DateTime end)
        {
            var list = new List<DbPingLog>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                string query = @"
                    SELECT Timestamp, Target, Event, Status, RttMs, Seq, SessionId, Notes, Hmac
                    FROM PingLogs
                    WHERE Event = 'PingResult'
                      AND Timestamp >= $start AND Timestamp <= $end
                      AND ($target IS NULL OR Target = $target)
                    ORDER BY Timestamp ASC;
                ";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("$start", start.ToString("o"));
                    command.Parameters.AddWithValue("$end", end.ToString("o"));
                    command.Parameters.AddWithValue("$target", string.IsNullOrEmpty(target) ? DBNull.Value : target);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbPingLog
                            {
                                Timestamp = DateTime.Parse(reader.GetString(0)),
                                Target = reader.GetString(1),
                                Event = reader.GetString(2),
                                Status = reader.GetString(3),
                                RttMs = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                                Seq = reader.GetInt64(5),
                                SessionId = reader.GetString(6),
                                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Hmac = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task<List<DbPingLog>> GetOldPingLogsForArchivingAsync(int retentionDays)
        {
            var list = new List<DbPingLog>();
            if (retentionDays <= 0) return list;

            DateTime threshold = DateTime.UtcNow.AddDays(-retentionDays);
            string thresholdStr = threshold.ToString("o");

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"
                    SELECT Timestamp, Target, Event, Status, RttMs, Seq, SessionId, Notes, Hmac
                    FROM PingLogs
                    WHERE Event = 'PingResult' AND Timestamp < $threshold
                    ORDER BY Timestamp ASC;
                ";

                using (var command = new SqliteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("$threshold", thresholdStr);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new DbPingLog
                            {
                                Timestamp = DateTime.Parse(reader.GetString(0)),
                                Target = reader.GetString(1),
                                Event = reader.GetString(2),
                                Status = reader.GetString(3),
                                RttMs = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                                Seq = reader.GetInt64(5),
                                SessionId = reader.GetString(6),
                                Notes = reader.IsDBNull(7) ? null : reader.GetString(7),
                                Hmac = reader.IsDBNull(8) ? null : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task PruneOldLogsAndVacuumAsync(int retentionDays)
        {
            await PruneOldLogsAsync(retentionDays);
        }

        public async Task DeleteAllPingLogsAndVacuumAsync()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                using (var command = new SqliteCommand("DELETE FROM PingLogs;", connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = new SqliteCommand("VACUUM;", connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
