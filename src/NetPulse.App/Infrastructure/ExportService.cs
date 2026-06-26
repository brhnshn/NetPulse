using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetPulse.App.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace NetPulse.App.Infrastructure
{
    public class ExportService
    {
        private readonly LogWriter _logWriter;
        private readonly string _logDir;

        public ExportService(LogWriter logWriter, string? logDir = null)
        {
            _logWriter = logWriter;
            _logDir = logDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        }

        public async Task ArchiveOldLogsAsync(int retentionDays)
        {
            if (retentionDays <= 0) return;

            var oldLogs = await _logWriter.DbManager.GetOldPingLogsForArchivingAsync(retentionDays);
            if (oldLogs.Count == 0) return;

            string archiveDir = Path.Combine(_logDir, "Archives");
            if (!Directory.Exists(archiveDir))
            {
                Directory.CreateDirectory(archiveDir);
            }

            string archivePath = Path.Combine(archiveDir, $"archive_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            using (var writer = new StreamWriter(archivePath, false, Encoding.UTF8))
            {
                await writer.WriteLineAsync("zaman,hedef,olay,durum,gecikme_ms,sira_no,oturum_id,notlar,hmac");
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
                    await writer.WriteLineAsync(csvLine);
                }
            }
        }

        public async Task ExportToCsvAsync(string path, string? target = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
        {
            await _logWriter.FileLock.WaitAsync(ct);
            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool useUtc = _logWriter.Config.Config.UseUtcTimestamps;
                DateTime startVal = start ?? DateTime.MinValue;
                DateTime endVal = end ?? DateTime.MaxValue;

                var logs = await _logWriter.DbManager.QueryPingLogsAsync(target, startVal, endVal);

                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    // Türkçe CSV Başlığı
                    await writer.WriteLineAsync("zaman,hedef,olay,durum,gecikme_ms,sira_no,oturum_id,notlar");

                    foreach (var entry in logs)
                    {
                        DateTime displayTime = useUtc ? entry.Timestamp.ToUniversalTime() : entry.Timestamp.ToLocalTime();
                        string timestampStr = useUtc 
                            ? displayTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                            : displayTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

                        string rttStr = entry.RttMs.HasValue ? entry.RttMs.Value.ToString("F1") : "";
                        string csvLine = $"{EscapeCsvField(timestampStr)}," +
                                         $"{EscapeCsvField(entry.Target)}," +
                                         $"{EscapeCsvField(entry.Event)}," +
                                         $"{EscapeCsvField(entry.Status)}," +
                                         $"{rttStr}," +
                                         $"{entry.Seq}," +
                                         $"{EscapeCsvField(entry.SessionId)}," +
                                         $"{EscapeCsvField(entry.Notes)}";
                        await writer.WriteLineAsync(csvLine);
                    }
                }
            }
            finally
            {
                _logWriter.FileLock.Release();
            }
        }

        public async Task ExportToTxtAsync(string path, string? target = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
        {
            await _logWriter.FileLock.WaitAsync(ct);
            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool useUtc = _logWriter.Config.Config.UseUtcTimestamps;
                DateTime startVal = start ?? DateTime.MinValue;
                DateTime endVal = end ?? DateTime.MaxValue;

                var logs = await _logWriter.DbManager.QueryPingLogsAsync(target, startVal, endVal);

                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    await writer.WriteLineAsync("=========================================================================");
                    await writer.WriteLineAsync("                  NETPULSE BAĞLANTI İZLEME RAPORU");
                    await writer.WriteLineAsync($"                  Oluşturulma Tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss} Local");
                    await writer.WriteLineAsync("=========================================================================");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync(string.Format("{0,-30} | {1,-15} | {2,-20} | {3,-12} | {4,-10} | {5,-6} | {6,-10}",
                        "Zaman Damgası", "Hedef", "Olay", "Durum", "RTT (ms)", "Sıra", "Notlar"));
                    await writer.WriteLineAsync(new string('-', 120));

                    foreach (var entry in logs)
                    {
                        DateTime displayTime = useUtc ? entry.Timestamp.ToUniversalTime() : entry.Timestamp.ToLocalTime();
                        string timestampStr = useUtc 
                            ? displayTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                            : displayTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

                        string rttStr = entry.RttMs.HasValue ? entry.RttMs.Value.ToString("F1") : "N/A";
                        string txtLine = string.Format("{0,-30} | {1,-15} | {2,-20} | {3,-12} | {4,-10} | {5,-6} | {6,-10}",
                            timestampStr,
                            entry.Target,
                            entry.Event,
                            entry.Status,
                            rttStr,
                            entry.Seq,
                            entry.Notes ?? "");
                        await writer.WriteLineAsync(txtLine);
                    }
                }
            }
            finally
            {
                _logWriter.FileLock.Release();
            }
        }

        public async Task ExportToPdfAsync(string path, string? target = null, DateTime? start = null, DateTime? end = null, CancellationToken ct = default)
        {
            await _logWriter.FileLock.WaitAsync(ct);
            try
            {
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool useUtc = _logWriter.Config.Config.UseUtcTimestamps;
                DateTime startVal = start ?? DateTime.MinValue;
                DateTime endVal = end ?? DateTime.MaxValue;

                var logs = await _logWriter.DbManager.QueryPingLogsAsync(target, startVal, endVal);

                using (var document = new PdfDocument())
                {
                    document.Info.Title = "NetPulse Bağlantı Analiz Raporu";
                    document.Info.Author = "NetPulse Analyzer";

                    PdfPage? page = null;
                    XGraphics? gfx = null;
                    double y = 40;
                    int pageNum = 0;

                    // Standart PDF yazı tipleri
                    XFont fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                    XFont fontSub = new XFont("Arial", 9, XFontStyleEx.Regular);
                    XFont fontHeader = new XFont("Arial", 9, XFontStyleEx.Bold);
                    XFont fontBody = new XFont("Arial", 8, XFontStyleEx.Regular);

                    Action addNewPage = () =>
                    {
                        page = document.AddPage();
                        page.Size = PdfSharp.PageSize.A4;
                        gfx = XGraphics.FromPdfPage(page);
                        pageNum++;

                        // Başlık Çizimi
                        gfx.DrawString("NETPULSE BAĞLANTI İZLEME VE ANALİZ RAPORU", fontTitle, XBrushes.DarkBlue, new XPoint(40, 40));
                        gfx.DrawString($"Rapor Tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Sayfa: {pageNum}", fontSub, XBrushes.DarkGray, new XPoint(40, 55));
                        gfx.DrawLine(XPens.DodgerBlue, 40, 65, 555, 65);

                        // Tablo Sütun Başlıkları
                        gfx.DrawString("Zaman", fontHeader, XBrushes.Black, new XPoint(45, 80));
                        gfx.DrawString("Hedef IP", fontHeader, XBrushes.Black, new XPoint(145, 80));
                        gfx.DrawString("Olay Tipi", fontHeader, XBrushes.Black, new XPoint(220, 80));
                        gfx.DrawString("Durum", fontHeader, XBrushes.Black, new XPoint(295, 80));
                        gfx.DrawString("Gecikme RTT", fontHeader, XBrushes.Black, new XPoint(365, 80));
                        gfx.DrawString("Açıklama / Notlar", fontHeader, XBrushes.Black, new XPoint(430, 80));
                        gfx.DrawLine(XPens.Gray, 40, 85, 555, 85);

                        y = 98;
                    };

                    addNewPage();

                    foreach (var entry in logs)
                    {
                        if (y > 790)
                        {
                            addNewPage();
                        }

                        DateTime displayTime = useUtc ? entry.Timestamp.ToUniversalTime() : entry.Timestamp.ToLocalTime();
                        string timestampStr = useUtc 
                            ? displayTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                            : displayTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

                        string rttStr = entry.RttMs.HasValue ? $"{entry.RttMs.Value:F1} ms" : "Timeout";
                        string noteStr = entry.Notes ?? "";

                        gfx!.DrawString(Truncate(timestampStr, 22), fontBody, XBrushes.Black, new XPoint(45, y));
                        gfx!.DrawString(Truncate(entry.Target, 16), fontBody, XBrushes.Black, new XPoint(145, y));
                        gfx!.DrawString(Truncate(entry.Event, 16), fontBody, XBrushes.Black, new XPoint(220, y));
                        gfx!.DrawString(Truncate(entry.Status, 15), fontBody, XBrushes.Black, new XPoint(295, y));
                        gfx!.DrawString(Truncate(rttStr, 12), fontBody, XBrushes.Black, new XPoint(365, y));
                        gfx!.DrawString(Truncate(noteStr, 28), fontBody, XBrushes.Black, new XPoint(430, y));

                        gfx!.DrawLine(XPens.LightGray, 40, y + 4, 555, y + 4);

                        y += 15;
                    }

                    document.Save(path);
                }
            }
            finally
            {
                _logWriter.FileLock.Release();
            }
        }

        private string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
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
    }
}
