using System;
using System.Text.Json.Serialization;

namespace NetPulse.App.Core
{
    public class Target
    {
        public string Address { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public class PingResultEventArgs : EventArgs
    {
        public DateTime Timestamp { get; }
        public string Target { get; }
        public bool Success { get; }
        public double? RttMs { get; }
        public long Seq { get; }

        public PingResultEventArgs(DateTime timestamp, string target, bool success, double? rttMs, long seq)
        {
            Timestamp = timestamp;
            Target = target;
            Success = success;
            RttMs = rttMs;
            Seq = seq;
        }
    }

    public class ConnectionEventArgs : EventArgs
    {
        public DateTime Timestamp { get; }
        public string Target { get; }
        public string Reason { get; }
        public long Seq { get; }

        public ConnectionEventArgs(DateTime timestamp, string target, string reason, long seq)
        {
            Timestamp = timestamp;
            Target = target;
            Reason = reason;
            Seq = seq;
        }
    }

    public class LogEntry
    {
        [JsonPropertyName("timestamp_utc")]
        public string TimestampUtcString { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty; // e.g., "PingResult", "ConnectionDropped", "ConnectionRestored"

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty; // e.g., "Success", "Timeout", "Dropped", "Restored", "CircuitBreakerTrip"

        [JsonPropertyName("rtt_ms")]
        public double? RttMs { get; set; }

        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Notes { get; set; }

        [JsonPropertyName("hmac")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Hmac { get; set; }
    }
}
