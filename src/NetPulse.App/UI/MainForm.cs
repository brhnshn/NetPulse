using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using NetPulse.App.Core;
using NetPulse.App.Infrastructure;
using NetPulse.App.UI.Controls;

namespace NetPulse.App.UI
{
    public class MainForm : Form
    {
        private readonly ConfigManager _configManager;
        private readonly LogWriter _logWriter;
        private readonly ExportService _exportService;
        private readonly NotificationService _notificationService;
        private readonly PingEngine _engine;

        // UI Kontrolleri
        private KpiCard _kpiStatus = null!;
        private KpiCard _kpiLatency = null!;
        private KpiCard _kpiOutages = null!;
        private KpiCard _kpiMaxOutage = null!;

        private DataGridView _dgvLive = null!;
        private Chart _chartPing = null!;
        private DataGridView _dgvOutages = null!;

        private Button _btnPauseResume = null!;
        private Button _btnSettings = null!;
        private Button _btnLogManager = null!;
        private Button _btnRehber = null!;

        private NotifyIcon _notifyIcon = null!;
        private ContextMenuStrip _trayMenu = null!;

        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lblStatusText = null!;
        private ToolStripStatusLabel _lblStartTime = null!;
        private DateTime? _scanStartTime = null;
        private bool _historyLoaded = false;

        // İzleme Durumu Değişkenleri
        private int _totalOutages = 0;
        private double _maxOutageSeconds = 0;
        private bool _isExiting = false;
        private readonly object _outageLock = new();

        // Aktif kesintileri izleyen sözlük. Anahtar: Hedef IP, Değer: (Kesinti Başlangıcı, Outages Tablo Satır İndeksi, DbOutageId)
        private readonly Dictionary<string, (DateTime StartTime, int RowIndex, int DbOutageId)> _activeOutages = new();

        public MainForm(
            ConfigManager configManager,
            LogWriter logWriter,
            ExportService exportService,
            NotificationService notificationService,
            PingEngine engine)
        {
            _configManager = configManager;
            _logWriter = logWriter;
            _exportService = exportService;
            _notificationService = notificationService;
            _engine = engine;

            InitializeComponents();
            ConfigureEvents();

            // Set initial scan start time since it starts immediately on launch
            _scanStartTime = DateTime.Now;
            _lblStartTime.Text = $"Tarama Başlangıcı: {_scanStartTime.Value:HH:mm:ss}";

            _lblStatusText.Text = "Sistem Aktif - Geçmiş yükleniyor...";

            // Sistem tepsisi balon bildirimi için geri çağırma kaydı
            _notificationService.RegisterBalloonFallback((title, msg) =>
            {
                SafeBeginInvoke(() =>
                {
                    try { _notifyIcon.ShowBalloonTip(3000, title, msg, ToolTipIcon.Warning); } catch { }
                });
            });
        }

        private void SafeBeginInvoke(Action action)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            try
            {
                this.BeginInvoke(action);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void InitializeComponents()
        {
            // Form Genel Özellikleri
            Text = "NetPulse — Gelişmiş Ağ İzleme & Mikro Kesinti Analizörü";
            Size = new Size(1100, 700);
            MinimumSize = new Size(1024, 650);
            BackColor = Color.FromArgb(18, 18, 18);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            StartPosition = FormStartPosition.CenterScreen;

            // Logo ve İkon Kurulumu
            this.SetAppIcon();
            Icon? appIcon = IconHelper.GetAppIcon();

            // Sistem Tepsisi (Tray Icon) Kurulumu
            _notifyIcon = new NotifyIcon
            {
                Text = "NetPulse Ağ İzleyici",
                Icon = appIcon ?? SystemIcons.Application,
                Visible = true
            };
            _notifyIcon.DoubleClick += (s, e) => RestoreForm();

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Gösterge Panelini Aç", null, (s, e) => RestoreForm());
            _trayMenu.Items.Add("İzlemeyi Duraklat", null, (s, e) => ToggleMonitoring(false));
            _trayMenu.Items.Add("İzlemeyi Başlat", null, (s, e) => ToggleMonitoring(true));
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Uygulamadan Çık", null, (s, e) => ExitApplication());
            _notifyIcon.ContextMenuStrip = _trayMenu;

            // Layout Düzeni
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(15),
                BackColor = Color.FromArgb(18, 18, 18)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110F)); // Üst KPI Bölümü
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));   // Orta Canlı Akış & Grafik Bölümü
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));   // Alt Kesinti Geçmişi Bölümü
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // En Alt Butonlar Bölümü
            Controls.Add(mainLayout);

            // 1. KPI Kartları Paneli
            var kpiPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 10)
            };
            kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            _kpiStatus = new KpiCard { Title = "BAĞLANTI DURUMU", Value = "BAĞLI", Subtitle = "Tüm sistemler kararlı", ValueColor = Color.LightGreen, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            _kpiLatency = new KpiCard { Title = "GECİKME ORTALAMASI", Value = "- ms", Subtitle = "Aktif hedefler üzerinden", ValueColor = Color.DeepSkyBlue, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            _kpiOutages = new KpiCard { Title = "TOPLAM KESİNTİ", Value = "0", Subtitle = "Uygulama açılışından beri", ValueColor = Color.Orange, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            _kpiMaxOutage = new KpiCard { Title = "EN UZUN KESİNTİ", Value = "0.0 sn", Subtitle = "Kaydedilen en uzun kesinti", ValueColor = Color.Tomato, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 0) };

            kpiPanel.Controls.Add(_kpiStatus, 0, 0);
            kpiPanel.Controls.Add(_kpiLatency, 1, 0);
            kpiPanel.Controls.Add(_kpiOutages, 2, 0);
            kpiPanel.Controls.Add(_kpiMaxOutage, 3, 0);
            mainLayout.Controls.Add(kpiPanel, 0, 0);

            // 2. Orta Bölüm (Canlı Akış ve Grafik)
            var middleSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 10)
            };
            middleSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            middleSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));

            // Canlı Akış Grid
            _dgvLive = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.White,
                GridColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            };
            _dgvLive.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);
            _dgvLive.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvLive.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgvLive.EnableHeadersVisualStyles = false;
            _dgvLive.DefaultCellStyle.BackColor = Color.FromArgb(24, 24, 24);
            _dgvLive.DefaultCellStyle.ForeColor = Color.White;
            _dgvLive.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 40);

            _dgvLive.Columns.Add("Time", "Zaman");
            _dgvLive.Columns.Add("Target", "Hedef Sunucu");
            _dgvLive.Columns.Add("Status", "Durum");
            _dgvLive.Columns.Add("Latency", "Gecikme (ms)");
            middleSplit.Controls.Add(_dgvLive, 0, 0);

            // Çizgi Grafik (Latency Chart)
            _chartPing = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            var chartArea = new ChartArea("PingArea")
            {
                BackColor = Color.FromArgb(24, 24, 24)
            };
            chartArea.AxisX.LineColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisY.LineColor = Color.FromArgb(60, 60, 60);
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(35, 35, 35);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(35, 35, 35);
            chartArea.AxisX.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisY.LabelStyle.ForeColor = Color.LightGray;
            chartArea.AxisY.Minimum = 0;
            _chartPing.ChartAreas.Add(chartArea);

            var chartLegend = new Legend("PingLegend")
            {
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Docking = Docking.Top,
                Alignment = StringAlignment.Center
            };
            _chartPing.Legends.Add(chartLegend);
            middleSplit.Controls.Add(_chartPing, 1, 0);

            mainLayout.Controls.Add(middleSplit, 0, 1);

            // 3. Kesinti Geçmişi
            var outageContainer = new GroupBox
            {
                Text = "Kayıtlı Bağlantı Kesinti Geçmişi",
                Dock = DockStyle.Fill,
                ForeColor = Color.DodgerBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(10)
            };
            _dgvOutages = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.White,
                GridColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _dgvOutages.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);
            _dgvOutages.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvOutages.EnableHeadersVisualStyles = false;
            _dgvOutages.DefaultCellStyle.BackColor = Color.FromArgb(24, 24, 24);
            _dgvOutages.DefaultCellStyle.ForeColor = Color.White;
            _dgvOutages.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 40);

            _dgvOutages.Columns.Add("Target", "Hedef IP");
            _dgvOutages.Columns.Add("Start", "Kesinti Başlangıcı");
            _dgvOutages.Columns.Add("End", "Geri Gelme Zamanı");
            _dgvOutages.Columns.Add("Duration", "Toplam Süre");
            outageContainer.Controls.Add(_dgvOutages);
            mainLayout.Controls.Add(outageContainer, 0, 2);

            // 4. Alt Butonlar Paneli (Footer)
            var footerPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _btnPauseResume = new Button { Text = "İzlemeyi Duraklat", FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(150, 30), Margin = new Padding(0, 0, 10, 0) };
            _btnPauseResume.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnPauseResume.Click += BtnPauseResume_Click;

            _btnSettings = new Button { Text = "Ayarlar", FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(100, 30), Margin = new Padding(0, 0, 10, 0) };
            _btnSettings.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnSettings.Click += BtnSettings_Click;

            _btnLogManager = new Button { Text = "Log & Arşiv Yönetimi", FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(180, 30), Margin = new Padding(0, 0, 10, 0) };
            _btnLogManager.FlatAppearance.BorderColor = Color.MediumSeaGreen;
            _btnLogManager.Click += BtnLogManager_Click;

            _btnRehber = new Button { Text = "Kullanıcı Rehberi", FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(150, 30) };
            _btnRehber.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnRehber.Click += BtnRehber_Click;

            // StatusStrip (Bilgi Çubuğu) Kurulumu
            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(24, 24, 24),
                ForeColor = Color.LightGray,
                SizingGrip = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            _lblStatusText = new ToolStripStatusLabel
            {
                Text = "Sistem Aktif - İzleniyor",
                ForeColor = Color.DodgerBlue
            };

            _lblStartTime = new ToolStripStatusLabel
            {
                Text = "Tarama Başlangıcı: -",
                ForeColor = Color.DarkGray
            };

            var springLabel = new ToolStripStatusLabel { Spring = true };

            _statusStrip.Items.Add(_lblStatusText);
            _statusStrip.Items.Add(springLabel);
            _statusStrip.Items.Add(_lblStartTime);

            footerPanel.Controls.Add(_btnPauseResume);
            footerPanel.Controls.Add(_btnSettings);
            footerPanel.Controls.Add(_btnLogManager);
            footerPanel.Controls.Add(_btnRehber);
            mainLayout.Controls.Add(footerPanel, 0, 3);

            Controls.Add(_statusStrip);
            mainLayout.BringToFront();

            RebuildChartSeries();
        }

        private void ConfigureEvents()
        {
            _configManager.OnConfigChanged += ConfigManager_OnConfigChanged;
            _engine.OnPingCompleted += Engine_OnPingCompleted;
            _engine.OnConnectionDropped += Engine_OnConnectionDropped;
            _engine.OnConnectionRestored += Engine_OnConnectionRestored;
            _engine.OnFallbackTriggered += Engine_OnFallbackTriggered;
            _engine.OnCircuitBreakerTriggered += Engine_OnCircuitBreakerTriggered;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_historyLoaded) return;
            _historyLoaded = true;
            await LoadHistoryAsync();
        }

        private void RebuildChartSeries()
        {
            _chartPing.Series.Clear();
            var targets = _engine.Targets;

            foreach (var target in targets)
            {
                if (!target.IsEnabled) continue;

                var series = new Series(target.DisplayName)
                {
                    ChartType = SeriesChartType.Line,
                    BorderWidth = 2,
                    XValueType = ChartValueType.String
                };
                _chartPing.Series.Add(series);
            }
        }

        private void ConfigManager_OnConfigChanged(object? sender, ConfigChangedEventArgs e)
        {
            SafeBeginInvoke(() =>
            {
                _engine.UpdateTargets(e.Config.Targets);
                _engine.IntervalMs = e.Config.IntervalMs;

                // Active outages cleanup for disabled targets
                lock (_outageLock)
                {
                    var disabledAddresses = e.Config.Targets.Where(t => !t.IsEnabled).Select(t => t.Address).ToList();
                    foreach (var address in disabledAddresses)
                    {
                        if (_activeOutages.TryGetValue(address, out var outageInfo))
                        {
                            // Finish outage in DB if it was open
                            int dbOutageId = outageInfo.DbOutageId;
                            if (dbOutageId > 0)
                            {
                                try
                                {
                                    _ = _logWriter.DbManager.UpdateOutageAsync(dbOutageId, DateTime.Now, (DateTime.Now - outageInfo.StartTime).TotalSeconds);
                                }
                                catch { }
                            }
                            
                            // Update grid row
                            int rowIndex = outageInfo.RowIndex;
                            if (rowIndex >= 0 && rowIndex < _dgvOutages.Rows.Count)
                            {
                                var row = _dgvOutages.Rows[rowIndex];
                                row.Cells["End"].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                row.Cells["Duration"].Value = "Kapatıldı";
                                row.DefaultCellStyle.ForeColor = Color.LightGray;
                            }
                            
                            _activeOutages.Remove(address);
                        }
                    }

                    if (_activeOutages.Count == 0)
                    {
                        _kpiStatus.Value = "BAĞLI";
                        _kpiStatus.Subtitle = "Tüm sistemler kararlı";
                        _kpiStatus.IsFlashing = false;
                    }
                }

                RebuildChartSeries();
            });
        }

        private void Engine_OnPingCompleted(object? sender, PingResultEventArgs e)
        {
            SafeBeginInvoke(() =>
            {
                // Canlı izleme gridine satır ekle
                _dgvLive.Rows.Insert(0,
                    e.Timestamp.ToString("HH:mm:ss.fff"),
                    e.Target,
                    e.Success ? "Başarılı" : "Zaman Aşımı",
                    e.RttMs.HasValue ? $"{e.RttMs.Value:F1}" : "-"
                );

                var row = _dgvLive.Rows[0];
                if (e.Success)
                {
                    row.DefaultCellStyle.ForeColor = Color.LightGreen;
                    row.DefaultCellStyle.SelectionForeColor = Color.LightGreen;
                }
                else
                {
                    row.DefaultCellStyle.ForeColor = Color.Red;
                    row.DefaultCellStyle.SelectionForeColor = Color.Red;
                }

                // Grafik güncellemesi (son 60 nokta sliding window)
                var series = _chartPing.Series.FirstOrDefault(s => s.Name == e.Target || s.Name == GetDisplayName(e.Target));
                if (series != null)
                {
                    double rtt = e.RttMs ?? 0;
                    string timeLabel = e.Timestamp.ToString("HH:mm:ss");

                    series.Points.AddXY(timeLabel, rtt);

                    if (!e.Success)
                    {
                        var lastPoint = series.Points.Last();
                        lastPoint.MarkerColor = Color.Red;
                        lastPoint.MarkerStyle = MarkerStyle.Circle;
                        lastPoint.MarkerSize = 8;
                    }

                    if (series.Points.Count > 60)
                    {
                        series.Points.RemoveAt(0);
                    }
                    _chartPing.ResetAutoValues();
                }

                UpdateLatencyKpi();

                // Log dosyasına yazım
                _ = _logWriter.WriteAsync(new LogEntry
                {
                    Target = e.Target,
                    Event = "PingResult",
                    Status = e.Success ? "Basarili" : "Zaman Asimi",
                    RttMs = e.RttMs,
                    Seq = e.Seq
                }, false);
            });
        }

        private void Engine_OnConnectionDropped(object? sender, ConnectionEventArgs e)
        {
            SafeBeginInvoke(async () =>
            {
                int dbOutageId = 0;
                try
                {
                    dbOutageId = await _logWriter.DbManager.InsertOutageAsync(new DbOutage
                    {
                        Target = e.Target,
                        StartTime = e.Timestamp,
                        Seq = e.Seq,
                        SessionId = _logWriter.SessionId,
                        Reason = e.Reason
                    });
                }
                catch (Exception) { /* Suppress database errors */ }

                int rowIndex = -1;
                lock (_outageLock)
                {
                    _totalOutages++;
                    _kpiOutages.Value = _totalOutages.ToString();

                    // Bağlantı durumunu kesintiye al ve kartı kırmızı yakıp söndür
                    _kpiStatus.Value = "KESİLDİ";
                    _kpiStatus.Subtitle = $"{GetDisplayName(e.Target)} bağlantısı kesildi!";
                    _kpiStatus.IsFlashing = true;

                    // Kesinti geçmişi tablosuna ekle
                    rowIndex = _dgvOutages.Rows.Add(
                        GetDisplayName(e.Target),
                        e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        "Aktif",
                        "-"
                    );
                    _dgvOutages.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.OrangeRed;

                    _activeOutages[e.Target] = (e.Timestamp, rowIndex, dbOutageId);
                }

                // Kritik log yazımı ve disk anında eşleme
                _ = _logWriter.WriteAsync(new LogEntry
                {
                    Target = e.Target,
                    Event = "ConnectionDropped",
                    Status = "Kesildi",
                    Seq = e.Seq,
                    Notes = $"Baglanti koptu. Detay: {e.Reason}"
                }, true);

                // Toast/Sistem Tepsisi Bildirimi
                _ = _notificationService.ShowToastAsync("Bağlantı Koptu!", $"{GetDisplayName(e.Target)} kesintisi algılandı: {e.Timestamp:HH:mm:ss}");
            });
        }

        private void Engine_OnConnectionRestored(object? sender, ConnectionEventArgs e)
        {
            SafeBeginInvoke(async () =>
            {
                double durationSeconds = 0;
                bool wasLoggedOutage = false;
                int dbOutageId = 0;
                int rowIndex = -1;

                lock (_outageLock)
                {
                    if (_activeOutages.TryGetValue(e.Target, out var outageInfo))
                    {
                        DateTime startTime = outageInfo.StartTime;
                        rowIndex = outageInfo.RowIndex;
                        dbOutageId = outageInfo.DbOutageId;

                        durationSeconds = (e.Timestamp - startTime).TotalSeconds;
                        wasLoggedOutage = true;

                        if (durationSeconds > _maxOutageSeconds)
                        {
                            _maxOutageSeconds = durationSeconds;
                            _kpiMaxOutage.Value = $"{_maxOutageSeconds:F1} sn";
                        }

                        _activeOutages.Remove(e.Target);
                    }

                    if (_activeOutages.Count == 0)
                    {
                        _kpiStatus.Value = "BAĞLI";
                        _kpiStatus.Subtitle = "Tüm sistemler kararlı";
                        _kpiStatus.IsFlashing = false;
                    }
                }

                if (wasLoggedOutage)
                {
                    // Veritabanında kesintiyi sonlandır
                    if (dbOutageId > 0)
                    {
                        try
                        {
                            await _logWriter.DbManager.UpdateOutageAsync(dbOutageId, e.Timestamp, durationSeconds);
                        }
                        catch (Exception) { /* Suppress database errors */ }
                    }

                    // Grid güncellemesi
                    if (rowIndex >= 0 && rowIndex < _dgvOutages.Rows.Count)
                    {
                        var row = _dgvOutages.Rows[rowIndex];
                        row.Cells["End"].Value = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                        row.Cells["Duration"].Value = $"{durationSeconds:F1} sn";
                        row.DefaultCellStyle.ForeColor = Color.LightGreen;
                    }
                }

                // Kritik log kurtarma yazımı
                _ = _logWriter.WriteAsync(new LogEntry
                {
                    Target = e.Target,
                    Event = "ConnectionRestored",
                    Status = "Geri Geldi",
                    Seq = e.Seq,
                    Notes = wasLoggedOutage
                        ? $"Baglanti tekrar saglandi. Kesinti Suresi: {durationSeconds:F1}sn. Teoris: {e.Reason}"
                        : $"Baglanti tekrar saglandi. Teoris: {e.Reason}"
                }, true);

                if (wasLoggedOutage)
                {
                    _ = _notificationService.ShowToastAsync("Bağlantı Geri Geldi", $"{GetDisplayName(e.Target)} tekrar bağlandı. Kesinti: {durationSeconds:F1}sn");
                }
            });
        }

        private void Engine_OnFallbackTriggered(object? sender, FallbackEventArgs e)
        {
            _ = _logWriter.WriteAsync(new LogEntry
            {
                Target = e.Target,
                Event = "FallbackCheck",
                Status = e.HttpSuccess ? "KismiErisim" : "Cevrimdisi",
                Notes = $"Otomatik Teshis Sonucu: {e.Details}"
            }, false);
        }

        private void Engine_OnCircuitBreakerTriggered(object? sender, CircuitBreakerEventArgs e)
        {
            SafeBeginInvoke(() =>
            {
                _dgvLive.Rows.Insert(0,
                    e.Timestamp.ToString("HH:mm:ss.fff"),
                    e.Target,
                    "BREAKER_ACIK",
                    "Es Gecildi (60sn)"
                );

                var row = _dgvLive.Rows[0];
                row.DefaultCellStyle.ForeColor = Color.Gold;
                row.DefaultCellStyle.SelectionForeColor = Color.Gold;

                _ = _logWriter.WriteAsync(new LogEntry
                {
                    Target = e.Target,
                    Event = "CircuitBreakerTrip",
                    Status = "Acik",
                    Notes = e.Message
                }, true);

                _notificationService.ShowTrayBalloon("Devre Kesici Tetiklendi", e.Message);
            });
        }

        private void UpdateLatencyKpi()
        {
            double total = 0;
            int count = 0;

            foreach (DataGridViewRow row in _dgvLive.Rows)
            {
                if (row.Cells["Latency"].Value != null)
                {
                    string rttStr = row.Cells["Latency"].Value.ToString()!;
                    if (double.TryParse(rttStr, out double rtt))
                    {
                        total += rtt;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                _kpiLatency.Value = $"{total / count:F1} ms";
            }
            else
            {
                _kpiLatency.Value = "- ms";
            }
        }

        private string GetDisplayName(string address)
        {
            var target = _engine.Targets.FirstOrDefault(t => t.Address == address);
            return target != null ? target.DisplayName : address;
        }

        private void BtnPauseResume_Click(object? sender, EventArgs e)
        {
            if (_engine.IsRunning)
            {
                ToggleMonitoring(false);
            }
            else
            {
                ToggleMonitoring(true);
            }
        }

        private void ToggleMonitoring(bool resume)
        {
            SafeBeginInvoke(async () =>
            {
                if (resume)
                {
                    await _engine.StartAsync(CancellationToken.None);
                    _btnPauseResume.Text = "İzlemeyi Duraklat";
                    _btnPauseResume.FlatAppearance.BorderColor = Color.DodgerBlue;
                    _kpiStatus.Value = "BAĞLI";
                    _kpiStatus.Subtitle = "İzleme devam ediyor.";

                    // Tarama başlatıldığında başlangıç zamanını saat:dakika:saniye olarak güncelle ve silme
                    _scanStartTime = DateTime.Now;
                    _lblStartTime.Text = $"Tarama Başlangıcı: {_scanStartTime.Value:HH:mm:ss}";
                    _lblStatusText.Text = "Sistem Aktif - İzleniyor";
                    _lblStatusText.ForeColor = Color.DodgerBlue;
                }
                else
                {
                    await _engine.StopAsync();
                    _btnPauseResume.Text = "İzlemeyi Başlat";
                    _btnPauseResume.FlatAppearance.BorderColor = Color.Gold;
                    _kpiStatus.Value = "DURDURULDU";
                    _kpiStatus.Subtitle = "İzleme kullanıcı tarafından durduruldu.";
                    _kpiStatus.IsFlashing = false;

                    // Durdurulduğunda da başlangıç zamanı silinmeden devam etmeli
                    _lblStatusText.Text = "İzleme Durduruldu";
                    _lblStatusText.ForeColor = Color.Gold;
                }
            });
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using (var settings = new SettingsForm(_configManager))
            {
                if (settings.ShowDialog(this) == DialogResult.OK)
                {
                    // targets auto updated via ConfigChanged event
                }
            }
        }

        private void BtnRehber_Click(object? sender, EventArgs e)
        {
            using (var rehber = new RehberForm())
            {
                rehber.ShowDialog(this);
            }
        }

        private void BtnLogManager_Click(object? sender, EventArgs e)
        {
            using (var logForm = new LogYonetimForm(_logWriter, _exportService))
            {
                logForm.ShowDialog(this);
            }
        }

        private void RestoreForm()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            this.SetAppIcon();
            Activate();
        }

        private void ExitApplication()
        {
            _isExiting = true;
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_isExiting)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;
                _notifyIcon.ShowBalloonTip(3000, "NetPulse Çalışmaya Devam Ediyor", "Uygulama arka plana küçültüldü. Tekrar açmak için tepsiyi çift tıklayın.", ToolTipIcon.Info);
            }
            else
            {
                Cleanup();
                base.OnFormClosing(e);
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                // 1. Ucu açık kalmış (önceki çökmelerden vs.) kesintileri kapat
                await _logWriter.DbManager.CloseOpenOutagesOnStartupAsync(_logWriter.SessionId);

                // 2. Canlı Akış Tablosunu Son 100 Ping İle Doldur
                var recentPings = await _logWriter.DbManager.GetRecentPingLogsAsync(100);
                foreach (var ping in recentPings)
                {
                    bool isSuccess = ping.Status == "Başarılı";
                    _dgvLive.Rows.Insert(0,
                        ping.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"),
                        ping.Target,
                        isSuccess ? "Başarılı" : "Zaman Aşımı",
                        ping.RttMs.HasValue ? $"{ping.RttMs.Value:F1}" : "-"
                    );

                    var row = _dgvLive.Rows[0];
                    if (isSuccess)
                    {
                        row.DefaultCellStyle.ForeColor = Color.LightGreen;
                        row.DefaultCellStyle.SelectionForeColor = Color.LightGreen;
                    }
                    else
                    {
                        row.DefaultCellStyle.ForeColor = Color.Red;
                        row.DefaultCellStyle.SelectionForeColor = Color.Red;
                    }
                }

                // 3. Kesinti Geçmişi Tablosunu Doldur
                var outages = await _logWriter.DbManager.GetAllOutagesAsync();
                foreach (var outage in outages)
                {
                    int rowIndex = _dgvOutages.Rows.Add(
                        GetDisplayName(outage.Target),
                        outage.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        outage.EndTime.HasValue ? outage.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Aktif",
                        outage.Duration.HasValue ? $"{outage.Duration.Value:F1} sn" : "-"
                    );

                    if (outage.EndTime.HasValue)
                    {
                        _dgvOutages.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.LightGreen;
                    }
                    else
                    {
                        _dgvOutages.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.OrangeRed;
                    }

                    // En uzun kesinti süresini güncelle
                    if (outage.Duration.HasValue && outage.Duration.Value > _maxOutageSeconds)
                    {
                        _maxOutageSeconds = outage.Duration.Value;
                    }
                    
                    _totalOutages++;
                }

                _kpiOutages.Value = _totalOutages.ToString();
                _kpiMaxOutage.Value = $"{_maxOutageSeconds:F1} sn";
                
                UpdateLatencyKpi();
                _lblStatusText.Text = "Sistem Aktif - İzleniyor";
                _lblStatusText.ForeColor = Color.DodgerBlue;
            }
            catch (Exception ex)
            {
                _lblStatusText.Text = "Geçmiş loglar yüklenemedi";
                _lblStatusText.ForeColor = Color.Gold;
                MessageBox.Show($"Geçmiş log verileri veritabanından yüklenemedi: {ex.Message}", "Veritabanı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Cleanup()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            // UI Thread kilitlenmesini engellemek için arka planda StopAsync çalıştır
            _ = Task.Run(async () => await _engine.StopAsync());
        }
    }
}
