using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NetPulse.App.Core;
using NetPulse.App.Infrastructure;

namespace NetPulse.App.UI
{
    public class LogYonetimForm : Form
    {
        private readonly LogWriter _logWriter;
        private readonly ExportService _exportService;
        private bool _isExporting = false;

        // UI Controls
        private Label _lblDbSize = null!;
        private Label _lblTotalPings = null!;
        private Label _lblTotalOutages = null!;

        private ComboBox _cbTargets = null!;
        private DateTimePicker _dtpStart = null!;
        private DateTimePicker _dtpEnd = null!;
        private DataGridView _dgvLogs = null!;
        private Label _lblResultCount = null!;

        private Button _btnQuery = null!;
        private Button _btnVacuum = null!;
        private Button _btnClearPings = null!;
        private Button _btnOpenArchives = null!;
        
        private Button _btnExportCsv = null!;
        private Button _btnExportTxt = null!;
        private Button _btnExportPdf = null!;

        public LogYonetimForm(LogWriter logWriter, ExportService exportService)
        {
            _logWriter = logWriter;
            _exportService = exportService;

            InitializeFormComponents();
            LoadTargets();
            RefreshMetrics();
        }

        private void InitializeFormComponents()
        {
            Text = "NetPulse — Log ve Arşiv Yönetimi";
            Size = new Size(1000, 640); // Saat alanları için dikey pay biraz artırıldı
            MinimumSize = new Size(950, 550);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.SetAppIcon();

            // Master TableLayoutPanel Layout
            var masterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(15)
            };
            masterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F)); // Left Control/Metric Panel
            masterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // Right Query/Grid Panel
            Controls.Add(masterLayout);

            // ================= LEFT PANEL (METRICS & MAINTENANCE) =================
            var leftLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0, 0, 15, 0)
            };
            masterLayout.Controls.Add(leftLayout, 0, 0);

            // Metrics GroupBox
            var gbMetrics = new GroupBox
            {
                Text = "Veritabanı İstatistikleri",
                Width = 265,
                Height = 160,
                ForeColor = Color.DodgerBlue,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(10)
            };
            leftLayout.Controls.Add(gbMetrics);

            var metricsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            gbMetrics.Controls.Add(metricsFlow);

            _lblDbSize = new Label { Text = "Dosya Boyutu: Hesaplanıyor...", ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Regular), AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            _lblTotalPings = new Label { Text = "Toplam Ham Ping: Hesaplanıyor...", ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Regular), AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            _lblTotalOutages = new Label { Text = "Kayıtlı Kesinti: Hesaplanıyor...", ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Regular), AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
            
            metricsFlow.Controls.Add(_lblDbSize);
            metricsFlow.Controls.Add(_lblTotalPings);
            metricsFlow.Controls.Add(_lblTotalOutages);

            // Maintenance GroupBox
            var gbMaintenance = new GroupBox
            {
                Text = "Bakım ve Temizlik",
                Width = 265,
                Height = 180,
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(0, 15, 0, 0)
            };
            leftLayout.Controls.Add(gbMaintenance);

            var maintenanceFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            gbMaintenance.Controls.Add(maintenanceFlow);

            _btnVacuum = new Button
            {
                Text = "Manuel Arşivle ve Sıkıştır",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Size = new Size(225, 30),
                Margin = new Padding(0, 5, 0, 5),
                Cursor = Cursors.Hand
            };
            _btnVacuum.FlatAppearance.BorderColor = Color.Gold;
            _btnVacuum.Click += BtnVacuum_Click;

            _btnClearPings = new Button
            {
                Text = "Tüm Canlı Pingleri Sil",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Size = new Size(225, 30),
                Margin = new Padding(0, 5, 0, 5),
                Cursor = Cursors.Hand
            };
            _btnClearPings.FlatAppearance.BorderColor = Color.Tomato;
            _btnClearPings.Click += BtnClearPings_Click;

            _btnOpenArchives = new Button
            {
                Text = "Arşiv Klasörünü Aç",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Size = new Size(225, 30),
                Margin = new Padding(0, 5, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnOpenArchives.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnOpenArchives.Click += BtnOpenArchives_Click;

            maintenanceFlow.Controls.Add(_btnVacuum);
            maintenanceFlow.Controls.Add(_btnClearPings);
            maintenanceFlow.Controls.Add(_btnOpenArchives);


            // ================= RIGHT PANEL (QUERY, GRID & EXPORTS) =================
            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));  // Filtre paneli yüksekliği rahat okunsun diye 90F yapıldı
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // DataGridView
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));  // Exports Bar
            masterLayout.Controls.Add(rightLayout, 1, 0);

            // 1. Geliştirilmiş Filtre Paneli (Akıllı Sütun Genişlikleri)
            var filterLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85F));   // Etiketler için sabit genişlik
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));   // Başlangıç/Hedef alanı (Dinamik esneklik)
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65F));   // Bitiş Etiketi / Sorgula Buton boşluğu
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));   // Bitiş Alanı (Dinamik esneklik)
            filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));  // Sorgula butonu için ayrılmış sağ sütun
            
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            filterLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            rightLayout.Controls.Add(filterLayout, 0, 0);

            var lblTarget = new Label { Text = "Hedef Cihaz:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.LightGray };
            _cbTargets = new ComboBox
            {
                Dock = DockStyle.Fill, // Hücreyi tam kaplasın
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 5, 10, 5),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            var lblStart = new Label { Text = "Başlangıç:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.LightGray };
            _dtpStart = new DateTimePicker
            {
                Dock = DockStyle.Fill, // Sıkışmayı önlemek için tam kaplama yapıldı
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd   HH:mm:ss", // Saat ve tarih arası okunabilirlik için açıldı
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Value = DateTime.Now.AddDays(-1),
                Margin = new Padding(0, 5, 10, 5),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            var lblEnd = new Label { Text = "Bitiş Tarihi:", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.LightGray };
            _dtpEnd = new DateTimePicker
            {
                Dock = DockStyle.Fill,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd   HH:mm:ss",
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Value = DateTime.Now,
                Margin = new Padding(0, 5, 10, 5),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };

            _btnQuery = new Button
            {
                Text = "Sorgula",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Fill, // Hücre boyutuna göre esneklik
                Height = 30,
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 5, 0, 5)
            };
            _btnQuery.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnQuery.Click += BtnQuery_Click;

            // Filtre elemanlarının düzgün koordinatlarla eklenmesi
            filterLayout.Controls.Add(lblTarget, 0, 0);
            filterLayout.Controls.Add(_cbTargets, 1, 0);
            filterLayout.Controls.Add(_btnQuery, 4, 0); // Sorgula butonunu sağ en köşeye, tarihlerin yanına çektik
            filterLayout.SetRowSpan(_btnQuery, 2);      // Buton iki satır yüksekliğinde devasa ve konforlu oldu

            filterLayout.Controls.Add(lblStart, 0, 1);
            filterLayout.Controls.Add(_dtpStart, 1, 1);
            filterLayout.Controls.Add(lblEnd, 2, 1);
            filterLayout.Controls.Add(_dtpEnd, 3, 1);

            // 2. DataGridView
            var gridPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            gridPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            gridPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            rightLayout.Controls.Add(gridPanel, 0, 1);

            _dgvLogs = new DataGridView
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
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            };
            _dgvLogs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);
            _dgvLogs.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgvLogs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgvLogs.EnableHeadersVisualStyles = false;
            _dgvLogs.DefaultCellStyle.BackColor = Color.FromArgb(24, 24, 24);
            _dgvLogs.DefaultCellStyle.ForeColor = Color.White;
            _dgvLogs.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 40, 40);

            _dgvLogs.Columns.Add("Time", "Zaman");
            _dgvLogs.Columns.Add("Target", "Hedef Sunucu");
            _dgvLogs.Columns.Add("Status", "Durum");
            _dgvLogs.Columns.Add("Latency", "Gecikme (ms)");

            gridPanel.Controls.Add(_dgvLogs, 0, 0);

            _lblResultCount = new Label
            {
                Text = "Sorgu sonucu: 0 kayıt listelendi (Maksimum 2000 satır sınırlandırılmıştır).",
                ForeColor = Color.DarkGray,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                AutoSize = true,
                Padding = new Padding(0, 3, 0, 0)
            };
            gridPanel.Controls.Add(_lblResultCount, 0, 1);

            // 3. Export Buttons Bar
            var exportBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 10, 0, 0)
            };
            rightLayout.Controls.Add(exportBar, 0, 2);

            _btnExportCsv = new Button { Text = "Seçileni CSV Aktar", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(150, 30), Margin = new Padding(0, 0, 15, 0) };
            _btnExportCsv.Click += BtnExportCsv_Click;

            _btnExportTxt = new Button { Text = "Seçileni TXT Aktar", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(150, 30), Margin = new Padding(0, 0, 15, 0) };
            _btnExportTxt.Click += BtnExportTxt_Click;

            _btnExportPdf = new Button { Text = "Seçileni PDF Rapor Yap", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 40, 40), Size = new Size(180, 30) };
            _btnExportPdf.Click += BtnExportPdf_Click;

            exportBar.Controls.Add(_btnExportCsv);
            exportBar.Controls.Add(_btnExportTxt);
            exportBar.Controls.Add(_btnExportPdf);

            UpdateExportButtonsState(false);
        }

        private void LoadTargets()
        {
            _cbTargets.Items.Clear();
            _cbTargets.Items.Add("Tüm Hedefler");
            
            var targets = _logWriter.Config.Config.Targets;
            foreach (var target in targets)
            {
                _cbTargets.Items.Add(target.Address);
            }
            _cbTargets.SelectedIndex = 0;
        }

        private void RefreshMetrics()
        {
            Task.Run(async () =>
            {
                var metrics = await _logWriter.DbManager.GetDatabaseMetricsAsync();
                this.BeginInvoke(new Action(() =>
                {
                    _lblDbSize.Text = $"Dosya Boyutu: {metrics.SizeMb:F2} MB";
                    _lblTotalPings.Text = $"Toplam Ham Ping: {metrics.PingCount:N0}";
                    _lblTotalOutages.Text = $"Kayıtlı Kesinti: {metrics.OutageCount:N0}";
                }));
            }); 
        }

        private void UpdateExportButtonsState(bool hasResults)
        {
            if (hasResults)
            {
                _btnExportCsv.Enabled = true;
                _btnExportCsv.ForeColor = Color.White;
                _btnExportCsv.FlatAppearance.BorderColor = Color.MediumSeaGreen;
                _btnExportCsv.Cursor = Cursors.Hand;

                _btnExportTxt.Enabled = true;
                _btnExportTxt.ForeColor = Color.White;
                _btnExportTxt.FlatAppearance.BorderColor = Color.MediumSeaGreen;
                _btnExportTxt.Cursor = Cursors.Hand;

                _btnExportPdf.Enabled = true;
                _btnExportPdf.ForeColor = Color.White;
                _btnExportPdf.FlatAppearance.BorderColor = Color.MediumSeaGreen;
                _btnExportPdf.Cursor = Cursors.Hand;
            }
            else
            {
                _btnExportCsv.Enabled = false;
                _btnExportCsv.ForeColor = Color.Gray;
                _btnExportCsv.FlatAppearance.BorderColor = Color.DimGray;
                _btnExportCsv.Cursor = Cursors.No;

                _btnExportTxt.Enabled = false;
                _btnExportTxt.ForeColor = Color.Gray;
                _btnExportTxt.FlatAppearance.BorderColor = Color.DimGray;
                _btnExportTxt.Cursor = Cursors.No;

                _btnExportPdf.Enabled = false;
                _btnExportPdf.ForeColor = Color.Gray;
                _btnExportPdf.FlatAppearance.BorderColor = Color.DimGray;
                _btnExportPdf.Cursor = Cursors.No;
            }
        }

        private async void BtnQuery_Click(object? sender, EventArgs e)
        {
            _btnQuery.Enabled = false;
            Cursor = Cursors.WaitCursor;

            string? target = _cbTargets.SelectedIndex == 0 ? null : _cbTargets.SelectedItem?.ToString();
            DateTime start = _dtpStart.Value;
            DateTime end = _dtpEnd.Value;

            try
            {
                var logs = await _logWriter.DbManager.QueryPingLogsAsync(target, start, end);
                
                _dgvLogs.Rows.Clear();
                
                int count = Math.Min(logs.Count, 2000);
                for (int i = 0; i < count; i++)
                {
                    var log = logs[i];
                    bool isSuccess = log.Status == "Başarılı";
                    _dgvLogs.Rows.Add(
                        log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        log.Target,
                        isSuccess ? "Başarılı" : "Zaman Aşımı",
                        log.RttMs.HasValue ? $"{log.RttMs.Value:F1}" : "-"
                    );

                    var row = _dgvLogs.Rows[i];
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

                _lblResultCount.Text = $"Sorgu sonucu: {logs.Count:N0} kayıt bulundu (Görüntülenen: {count:N0} satır).";
                
                bool hasResults = logs.Count > 0;
                UpdateExportButtonsState(hasResults);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Loglar sorgulanamadı: {ex.Message}", "Sorgu Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnQuery.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private async void BtnVacuum_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Eski ham ping kayıtları veritabanından silinip diskteki sıkıştırılmış CSV arşivlerine taşınacaktır. Bu işlem veritabanı boyutunu küçültür ve performansı artırır.\n\nDevam etmek istiyor musiniz?",
                "Veritabanı Arşivlemeและ Sıkıştırma",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                _btnVacuum.Enabled = false;
                Cursor = Cursors.WaitCursor;
                try
                {
                    int retentionDays = _logWriter.Config.Config.LogRetentionDays;
                    await _exportService.ArchiveOldLogsAsync(retentionDays);
                    await _logWriter.DbManager.PruneOldLogsAndVacuumAsync(retentionDays);

                    MessageBox.Show("Arşivleme ve sıkıştırma (VACUUM) işlemi başarıyla tamamlandı!", "Bakım Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshMetrics();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Bakım işlemi gerçekleştirilemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _btnVacuum.Enabled = true;
                    Cursor = Cursors.Default;
                }
            }
        }

        private async void BtnClearPings_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "DİKKAT: Veritabanındaki TÜM ham ping sinyal logları tamamen silinecektir. Ağ kesinti geçmişiniz korunacaktır.\n\nBu işlemi onaylıyor musunuz?",
                "Tüm Canlı Pingleri Sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                _btnClearPings.Enabled = false;
                Cursor = Cursors.WaitCursor;
                try
                {
                    await _logWriter.DbManager.DeleteAllPingLogsAndVacuumAsync();
                    _dgvLogs.Rows.Clear();
                    _lblResultCount.Text = "Sorgu sonucu: 0 kayıt listelendi.";
                    UpdateExportButtonsState(false);

                    MessageBox.Show("Tüm ping sinyalleri başarıyla silindi ve veritabanı temizlendi!", "Temizlik Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshMetrics();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Silme işlemi gerçekleştirilemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _btnClearPings.Enabled = true;
                    Cursor = Cursors.Default;
                }
            }
        }

        private void BtnOpenArchives_Click(object? sender, EventArgs e)
        {
            string archiveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Archives");
            if (!Directory.Exists(archiveDir))
            {
                Directory.CreateDirectory(archiveDir);
            }

            try
            {
                Process.Start("explorer.exe", archiveDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Klasör açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            if (_dgvLogs.Rows.Count == 0)
            {
                MessageBox.Show("Lütfen önce sorgulama yapın ve sonuçların listelenmesini bekleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_isExporting) return;
            _isExporting = true;
            Cursor = Cursors.WaitCursor;

            try
            {
                using (var sfd = new SaveFileDialog
                {
                    Filter = "CSV dosyaları (*.csv)|*.csv",
                    FileName = $"NetPulse_Filtreli_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string? target = _cbTargets.SelectedIndex == 0 ? null : _cbTargets.SelectedItem?.ToString();
                        DateTime start = _dtpStart.Value;
                        DateTime end = _dtpEnd.Value;

                        await _exportService.ExportToCsvAsync(sfd.FileName, target, start, end);
                        MessageBox.Show("Filtrelenmiş CSV verisi başarıyla kaydedildi!", "Dışa Aktarma Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV dışa aktarılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isExporting = false;
                Cursor = Cursors.Default;
            }
        }

        private async void BtnExportTxt_Click(object? sender, EventArgs e)
        {
            if (_dgvLogs.Rows.Count == 0)
            {
                MessageBox.Show("Lütfen önce sorgulama yapın ve sonuçların listelenmesini bekleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_isExporting) return;
            _isExporting = true;
            Cursor = Cursors.WaitCursor;

            try
            {
                using (var sfd = new SaveFileDialog
                {
                    Filter = "Metin dosyaları (*.txt)|*.txt",
                    FileName = $"NetPulse_Filtreli_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string? target = _cbTargets.SelectedIndex == 0 ? null : _cbTargets.SelectedItem?.ToString();
                        DateTime start = _dtpStart.Value;
                        DateTime end = _dtpEnd.Value;

                        await _exportService.ExportToTxtAsync(sfd.FileName, target, start, end);
                        MessageBox.Show("Filtrelenmiş TXT verisi başarıyla kaydedildi!", "Dışa Aktarma Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"TXT dışa aktarılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isExporting = false;
                Cursor = Cursors.Default;
            }
        }

        private async void BtnExportPdf_Click(object? sender, EventArgs e)
        {
            if (_dgvLogs.Rows.Count == 0)
            {
                MessageBox.Show("Lütfen önce sorgulama yapın ve sonuçların listelenmesini bekleyin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_isExporting) return;
            _isExporting = true;
            Cursor = Cursors.WaitCursor;

            try
            {
                using (var sfd = new SaveFileDialog
                {
                    Filter = "PDF dosyaları (*.pdf)|*.pdf",
                    FileName = $"NetPulse_Filtreli_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        string? target = _cbTargets.SelectedIndex == 0 ? null : _cbTargets.SelectedItem?.ToString();
                        DateTime start = _dtpStart.Value;
                        DateTime end = _dtpEnd.Value;

                        await _exportService.ExportToPdfAsync(sfd.FileName, target, start, end);
                        MessageBox.Show("Filtrelenmiş PDF raporu başarıyla oluşturuldu!", "Rapor Hazır", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF raporu oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isExporting = false;
                Cursor = Cursors.Default;
            }
        }
    }
}