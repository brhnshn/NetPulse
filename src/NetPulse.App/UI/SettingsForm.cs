using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NetPulse.App.Core;
using NetPulse.App.Infrastructure;

namespace NetPulse.App.UI
{
    public class SettingsForm : Form
    {
        private readonly ConfigManager _configManager;
        private readonly DataGridView _dgvActiveTargets;
        private readonly DataGridView _dgvInactiveTargets;
        private readonly Button _btnMoveToInactive;
        private readonly Button _btnMoveToActive;
        private readonly NumericUpDown _numInterval;
        private readonly NumericUpDown _numRetention;
        private readonly TextBox _txtExportPath;
        private readonly TextBox _txtHmacKey;
        private readonly CheckBox _chkEnableCircuitBreaker;
        private readonly CheckBox _chkUseUtcTimestamps;
        private readonly Button _btnUninstall;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        public SettingsForm(ConfigManager configManager)
        {
            _configManager = configManager;

            Text = "NetPulse Yapılandırma Ayarları";
            Size = new Size(820, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.SetAppIcon();

            var lblTitle = new Label
            {
                Text = "Konfigürasyon Düzenleyici",
                Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
                Location = new Point(15, 15),
                Size = new Size(300, 30),
                ForeColor = Color.DodgerBlue
            };
            Controls.Add(lblTitle);

            var lblTargets = new Label
            {
                Text = "Hedef Havuzları:",
                Location = new Point(15, 55),
                Size = new Size(250, 20)
            };
            Controls.Add(lblTargets);

            var lblActiveTargets = new Label
            {
                Text = "Aktif İzleme Listesi (Ping Atılacaklar)",
                Location = new Point(15, 80),
                Size = new Size(310, 20),
                ForeColor = Color.MediumSeaGreen
            };
            Controls.Add(lblActiveTargets);

            var lblInactiveTargets = new Label
            {
                Text = "Pasif Liste (İzlenmeyenler)",
                Location = new Point(490, 80),
                Size = new Size(300, 20),
                ForeColor = Color.Silver
            };
            Controls.Add(lblInactiveTargets);

            _dgvActiveTargets = CreateTargetsGrid(new Point(15, 105));
            _dgvInactiveTargets = CreateTargetsGrid(new Point(490, 105));
            Controls.Add(_dgvActiveTargets);
            Controls.Add(_dgvInactiveTargets);

            _btnMoveToInactive = new Button
            {
                Text = ">>",
                Location = new Point(370, 145),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnMoveToInactive.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnMoveToInactive.Click += (s, e) => MoveSelectedTargets(_dgvActiveTargets, _dgvInactiveTargets);
            Controls.Add(_btnMoveToInactive);

            _btnMoveToActive = new Button
            {
                Text = "<<",
                Location = new Point(370, 185),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnMoveToActive.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnMoveToActive.Click += (s, e) => MoveSelectedTargets(_dgvInactiveTargets, _dgvActiveTargets);
            Controls.Add(_btnMoveToActive);

            var lblInterval = new Label
            {
                Text = "Ping Aralığı (ms):",
                Location = new Point(15, 300),
                Size = new Size(120, 20)
            };
            _numInterval = new NumericUpDown
            {
                Location = new Point(140, 298),
                Size = new Size(80, 25),
                Minimum = 200,
                Maximum = 60000,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            Controls.Add(lblInterval);
            Controls.Add(_numInterval);

            var lblRetention = new Label
            {
                Text = "Log Tutma (Gün):",
                Location = new Point(240, 300),
                Size = new Size(130, 20)
            };
            _numRetention = new NumericUpDown
            {
                Location = new Point(380, 298),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 365,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White
            };
            Controls.Add(lblRetention);
            Controls.Add(_numRetention);

            var lblExportPath = new Label
            {
                Text = "Dışa Aktarma Yolu:",
                Location = new Point(15, 340),
                Size = new Size(120, 20)
            };
            _txtExportPath = new TextBox
            {
                Location = new Point(140, 338),
                Size = new Size(650, 25),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(lblExportPath);
            Controls.Add(_txtExportPath);

            var lblHmacKey = new Label
            {
                Text = "HMAC Gizli Anahtarı:",
                Location = new Point(15, 380),
                Size = new Size(120, 20)
            };
            _txtHmacKey = new TextBox
            {
                Location = new Point(140, 378),
                Size = new Size(650, 25),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PasswordChar = '*'
            };
            Controls.Add(lblHmacKey);
            Controls.Add(_txtHmacKey);

            _chkEnableCircuitBreaker = new CheckBox
            {
                Text = "Devre Kesiciyi (Circuit Breaker) Etkinleştir",
                Location = new Point(140, 415),
                Size = new Size(300, 20),
                ForeColor = Color.White
            };

            _chkUseUtcTimestamps = new CheckBox
            {
                Text = "Zaman Damgası Olarak UTC Kullan",
                Location = new Point(460, 415),
                Size = new Size(300, 20),
                ForeColor = Color.White
            };

            Controls.Add(_chkEnableCircuitBreaker);
            Controls.Add(_chkUseUtcTimestamps);

            _btnSave = new Button
            {
                Text = "Ayarları Kaydet",
                Location = new Point(520, 445),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnSave.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnSave.Click += BtnSave_Click;

            _btnCancel = new Button
            {
                Text = "İptal",
                Location = new Point(680, 445),
                Size = new Size(110, 30),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnCancel.Click += (s, e) => Close();

            _btnUninstall = new Button
            {
                Text = "Uygulamayı Kaldır",
                Location = new Point(15, 445),
                Size = new Size(150, 30),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.Tomato,
                FlatStyle = FlatStyle.Flat
            };
            _btnUninstall.FlatAppearance.BorderColor = Color.Tomato;
            _btnUninstall.Click += BtnUninstall_Click;

            Controls.Add(_btnSave);
            Controls.Add(_btnCancel);
            Controls.Add(_btnUninstall);

            LoadFormValues();
        }

        private static DataGridView CreateTargetsGrid(Point location)
        {
            var grid = new DataGridView
            {
                Location = location,
                Size = new Size(310, 170),
                BackgroundColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                GridColor = Color.FromArgb(50, 50, 50),
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(40, 40, 40);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 50, 50);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.Columns.Add("Address", "IP / Host Adresi");
            grid.Columns.Add("DisplayName", "Görünen Ad");
            grid.Columns["Address"].FillWeight = 50;
            grid.Columns["DisplayName"].FillWeight = 50;

            return grid;
        }

        private static void MoveSelectedTargets(DataGridView source, DataGridView destination)
        {
            source.EndEdit();
            destination.EndEdit();

            var rows = source.SelectedRows
                .Cast<DataGridViewRow>()
                .Where(row => !row.IsNewRow)
                .OrderByDescending(row => row.Index)
                .ToList();

            foreach (var row in rows)
            {
                string? address = row.Cells["Address"].Value?.ToString()?.Trim();
                string? name = row.Cells["DisplayName"].Value?.ToString()?.Trim();

                if (string.IsNullOrEmpty(address))
                {
                    continue;
                }

                destination.Rows.Add(address, string.IsNullOrEmpty(name) ? address : name);
                source.Rows.Remove(row);
            }
        }

        private void LoadFormValues()
        {
            var conf = _configManager.Config;
            _numInterval.Value = conf.IntervalMs;
            _numRetention.Value = conf.LogRetentionDays;
            _txtExportPath.Text = conf.ExportPath;
            _txtHmacKey.Text = conf.HmacKey;
            _chkEnableCircuitBreaker.Checked = conf.EnableCircuitBreaker;
            _chkUseUtcTimestamps.Checked = conf.UseUtcTimestamps;

            _dgvActiveTargets.Rows.Clear();
            _dgvInactiveTargets.Rows.Clear();

            foreach (var target in conf.Targets)
            {
                var grid = target.IsEnabled ? _dgvActiveTargets : _dgvInactiveTargets;
                grid.Rows.Add(target.Address, target.DisplayName);
            }
        }

       private void BtnSave_Click(object? sender, EventArgs e)
        {
            _dgvActiveTargets.EndEdit();
            _dgvInactiveTargets.EndEdit();

            var conf = _configManager.Config;
            conf.IntervalMs = (int)_numInterval.Value;
            conf.LogRetentionDays = (int)_numRetention.Value;
            conf.ExportPath = _txtExportPath.Text;
            conf.HmacKey = _txtHmacKey.Text;
            conf.EnableCircuitBreaker = _chkEnableCircuitBreaker.Checked;
            conf.UseUtcTimestamps = _chkUseUtcTimestamps.Checked;

            conf.Targets.Clear();
            AddTargetsFromGrid(_dgvActiveTargets, isEnabled: true);
            AddTargetsFromGrid(_dgvInactiveTargets, isEnabled: false);

            if (conf.Targets.Count == 0)
            {
                MessageBox.Show("Lütfen en az bir hedef belirtin.", "Doğrulama Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _configManager.SaveConfig();


            _configManager.LoadConfig(); 

            DialogResult = DialogResult.OK;
            Close();
        }
        private void AddTargetsFromGrid(DataGridView grid, bool isEnabled)
        {
            var conf = _configManager.Config;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;

                string? address = row.Cells["Address"].Value?.ToString()?.Trim();
                string? name = row.Cells["DisplayName"].Value?.ToString()?.Trim();

                if (!string.IsNullOrEmpty(address))
                {
                    conf.Targets.Add(new Target
                    {
                        Address = address,
                        DisplayName = string.IsNullOrEmpty(name) ? address : name,
                        IsEnabled = isEnabled
                    });
                }
            }
        }

        private void BtnUninstall_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                "NetPulse uygulamasını tüm ayarları, logları ve kısayollarıyla birlikte sisteminizden kaldırmak istediğinize emin misiniz?",
                "Uygulamayı Kaldır",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    string installDir = @"C:\Program Files\NetPulse";
                    string uninstallScript = System.IO.Path.Combine(installDir, "Uninstall.ps1");

                    if (System.IO.File.Exists(uninstallScript))
                    {
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{uninstallScript}\"",
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        System.Diagnostics.Process.Start(startInfo);
                        
                        // Close application immediately so files can be deleted
                        Application.Exit();
                    }
                    else
                    {
                        // Geliştirme ortamı için yerel temizlik fallback'i
                        string desktopPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NetPulse.lnk");
                        string startMenuPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs\\NetPulse.lnk");
                        
                        bool deletedAny = false;
                        if (System.IO.File.Exists(desktopPath))
                        {
                            System.IO.File.Delete(desktopPath);
                            deletedAny = true;
                        }
                        if (System.IO.File.Exists(startMenuPath))
                        {
                            System.IO.File.Delete(startMenuPath);
                            deletedAny = true;
                        }

                        if (deletedAny)
                        {
                            MessageBox.Show("Masaüstü ve Başlat Menüsü kısayolları başarıyla kaldırıldı.", "Temizlik Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Application.Exit();
                        }
                        else
                        {
                            MessageBox.Show("Kaldırma betiği veya kısayollar bulunamadı. Lütfen kurulum klasörünü manuel olarak silin: " + installDir, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Kaldırma işlemi başlatılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
