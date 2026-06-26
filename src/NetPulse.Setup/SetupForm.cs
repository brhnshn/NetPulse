using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetPulse.Setup
{
    public class SetupForm : Form
    {
        private int _currentStep = 0;
        private string _installFolder = @"C:\Program Files\NetPulse";
        private bool _isInstalling = false;

        // UI Ana Panelleri
        private Panel _contentPanel = null!;
        private Panel _bottomPanel = null!;

        // Alt Butonlar
        private Button _btnBack = null!;
        private Button _btnNext = null!;
        private Button _btnCancel = null!;

        // Adım Bazlı Kontroller
        // Adım 1: Klasör Seçimi
        private TextBox _txtFolder = null!;
        // Adım 3: Yükleme İlerlemesi
        private ProgressBar _progressBar = null!;
        private Label _lblStatus = null!;
        // Adım 4: Tamamlandı
        private CheckBox _chkLaunchApp = null!;

        public SetupForm()
        {
            InitializeSetupWindow();
            ShowStep(0);
        }

        private void InitializeSetupWindow()
        {
            // Genel Pencere Özellikleri
            Text = "NetPulse — Kurulum Sihirbazı";
            ClientSize = new Size(650, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // Uygulama ikonunu yerleştir
            try
            {
                var assembly = typeof(SetupForm).Assembly;
                using (var stream = assembly.GetManifestResourceStream("NetPulse.Setup.logo.jpg"))
                {
                    if (stream != null)
                    {
                        using (var bmp = new Bitmap(stream))
                        {
                            IntPtr hIcon = bmp.GetHicon();
                            this.Icon = Icon.FromHandle(hIcon);
                        }
                    }
                }
            }
            catch { }


            // Alt Navigasyon Paneli
            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(25, 25, 25)
            };
            Controls.Add(_bottomPanel);

            // Alt Panel İnce Ayrım Çizgisi
            var sep = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(45, 45, 45)
            };
            _bottomPanel.Controls.Add(sep);

            // Navigasyon Butonları
            _btnCancel = new Button { Text = "İptal", Size = new Size(95, 28), Location = new Point(525, 16), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40) };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnCancel.Click += (s, e) => Close();

            _btnNext = new Button { Text = "İleri >", Size = new Size(95, 28), Location = new Point(420, 16), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40) };
            _btnNext.FlatAppearance.BorderColor = Color.DodgerBlue;
            _btnNext.Click += BtnNext_Click;

            _btnBack = new Button { Text = "< Geri", Size = new Size(95, 28), Location = new Point(315, 16), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40) };
            _btnBack.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnBack.Click += (s, e) => ShowStep(_currentStep - 1);

            _bottomPanel.Controls.Add(_btnCancel);
            _bottomPanel.Controls.Add(_btnNext);
            _bottomPanel.Controls.Add(_btnBack);
            _bottomPanel.Resize += (s, e) => LayoutBottomButtons();
            LayoutBottomButtons();

            // Orta İçerik Paneli
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20),
                Padding = new Padding(25)
            };
            Controls.Add(_contentPanel);
        }

        private void ShowStep(int stepIndex)
        {
            _currentStep = stepIndex;
            _contentPanel.Controls.Clear();

            // Butonların durumlarını güncelle
            _btnBack.Enabled = _currentStep > 0 && _currentStep < 3;
            _btnNext.Enabled = true;
            _btnCancel.Enabled = _currentStep < 3;

            if (_currentStep == 2)
            {
                _btnNext.Text = "Kur";
                _btnNext.FlatAppearance.BorderColor = Color.MediumSeaGreen;
            }
            else if (_currentStep == 4)
            {
                _btnNext.Text = "Bitir";
                _btnNext.FlatAppearance.BorderColor = Color.MediumSeaGreen;
                _btnBack.Visible = false;
                _btnCancel.Visible = false;
            }
            else
            {
                _btnNext.Text = "İleri >";
                _btnNext.FlatAppearance.BorderColor = Color.DodgerBlue;
            }

            switch (_currentStep)
            {
                case 0:
                    BuildWelcomeStep();
                    break;
                case 1:
                    BuildFolderSelectionStep();
                    break;
                case 2:
                    BuildReadyToInstallStep();
                    break;
                case 3:
                    BuildInstallingStep();
                    break;
                case 4:
                    BuildFinishedStep();
                    break;
            }

            FitWindowToContent();
        }

        private void LayoutBottomButtons()
        {
            const int buttonWidth = 95;
            const int buttonHeight = 28;
            const int gap = 10;
            int top = 16;
            int right = _bottomPanel.ClientSize.Width - 25;

            _btnCancel.Size = new Size(buttonWidth, buttonHeight);
            _btnNext.Size = new Size(buttonWidth, buttonHeight);
            _btnBack.Size = new Size(buttonWidth, buttonHeight);

            _btnCancel.Location = new Point(right - buttonWidth, top);
            _btnNext.Location = new Point(_btnCancel.Left - gap - buttonWidth, top);
            _btnBack.Location = new Point(_btnNext.Left - gap - buttonWidth, top);
        }

        private void FitWindowToContent()
        {
            int contentBottom = 0;
            foreach (Control control in _contentPanel.Controls)
            {
                contentBottom = Math.Max(contentBottom, control.Bottom);
            }

            int desiredHeight = Math.Max(260, contentBottom + _contentPanel.Padding.Bottom + _bottomPanel.Height);
            ClientSize = new Size(650, desiredHeight);
            LayoutBottomButtons();
        }

        // --- ADIM 0: Hoş Geldiniz ---
        private void BuildWelcomeStep()
        {
            var lblTitle = new Label
            {
                Text = "NetPulse Kurulum Sihirbazına Hoş Geldiniz",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.DodgerBlue,
                Size = new Size(560, 30),
                Location = new Point(25, 25)
            };
            _contentPanel.Controls.Add(lblTitle);

            var lblDesc = new Label
            {
                Text = "Bu sihirbaz, NetPulse — Gelişmiş Ağ İzleme & Kesinti Analizörü uygulamasını bilgisayarınıza yükleyecektir.\n\n" +
                       "Uygulamanın düzgün çalışabilmesi, ağ üzerinde kararlı ping paketleri gönderebilmesi ve bildirimleri sisteme doğru şekilde kaydedebilmesi için kurulumdan sonra yönetici olarak çalıştırılması önerilmektedir.\n\n" +
                       "Devam etmeden önce diğer uygulamaları kapatmanız tavsiye edilir.\n\n" +
                       "İlerlemek için İleri butonuna tıklayın.",
                Size = new Size(560, 170),
                Location = new Point(25, 75),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.LightGray
            };
            _contentPanel.Controls.Add(lblDesc);
        }

        // --- ADIM 1: Klasör Seçimi ---
        private void BuildFolderSelectionStep()
        {
            var lblTitle = new Label
            {
                Text = "Kurulum Klasörünü Seçin",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.DodgerBlue,
                Size = new Size(560, 30),
                Location = new Point(25, 25)
            };
            _contentPanel.Controls.Add(lblTitle);

            var lblDesc = new Label
            {
                Text = "NetPulse aşağıdaki klasöre kurulacaktır. Farklı bir klasöre kurmak için Gözat butonuna tıklayın.",
                Size = new Size(560, 45),
                Location = new Point(25, 75),
                ForeColor = Color.LightGray
            };
            _contentPanel.Controls.Add(lblDesc);

            _txtFolder = new TextBox
            {
                Text = _installFolder,
                Location = new Point(25, 140),
                Size = new Size(420, 25),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _contentPanel.Controls.Add(_txtFolder);

            var btnBrowse = new Button
            {
                Text = "Gözat...",
                Location = new Point(460, 138),
                Size = new Size(95, 26),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog { SelectedPath = _txtFolder.Text, Description = "NetPulse kurulum dizinini seçin:" })
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        _txtFolder.Text = fbd.SelectedPath;
                    }
                }
            };
            _contentPanel.Controls.Add(btnBrowse);
        }

        // --- ADIM 2: Kuruluma Hazır ---
        private void BuildReadyToInstallStep()
        {
            if (_txtFolder != null)
            {
                _installFolder = _txtFolder.Text.Trim();
            }

            var lblTitle = new Label
            {
                Text = "Kuruluma Hazır",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.DodgerBlue,
                Size = new Size(560, 30),
                Location = new Point(25, 25)
            };
            _contentPanel.Controls.Add(lblTitle);

            var lblDesc = new Label
            {
                Text = "Sihirbaz kurulumu başlatmak için gerekli bilgileri topladı.\n\n" +
                       $"Kurulum Klasörü:\n{_installFolder}\n\n" +
                       "Dosyaları çıkartmak ve kurulumu başlatmak için Kur butonuna tıklayın.",
                Size = new Size(560, 145),
                Location = new Point(25, 75),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            _contentPanel.Controls.Add(lblDesc);
        }

        // --- ADIM 3: Yükleme İlerlemesi ---
        private void BuildInstallingStep()
        {
            var lblTitle = new Label
            {
                Text = "NetPulse Kuruluyor...",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.DodgerBlue,
                Size = new Size(560, 30),
                Location = new Point(25, 25)
            };
            _contentPanel.Controls.Add(lblTitle);

            _lblStatus = new Label
            {
                Text = "Kurulum hazırlanıyor...",
                Location = new Point(25, 100),
                Size = new Size(560, 20),
                ForeColor = Color.LightGray
            };
            _contentPanel.Controls.Add(_lblStatus);

            _progressBar = new ProgressBar
            {
                Location = new Point(25, 130),
                Size = new Size(560, 23),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            _contentPanel.Controls.Add(_progressBar);
        }

        // --- ADIM 4: Kurulum Tamamlandı ---
        private void BuildFinishedStep()
        {
            var lblTitle = new Label
            {
                Text = "Kurulum Tamamlandı",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = Color.MediumSeaGreen,
                Size = new Size(560, 30),
                Location = new Point(25, 25)
            };
            _contentPanel.Controls.Add(lblTitle);

            var lblDesc = new Label
            {
                Text = "NetPulse bilgisayarınıza başarıyla yüklendi!\n\n" +
                       "Masaüstünüzde ve Başlat Menünüzde yönetici olarak çalışmaya uygun kısayollar oluşturuldu.\n\n" +
                       "Kurulum sihirbazını sonlandırmak için Bitir butonuna tıklayın.",
                Size = new Size(560, 105),
                Location = new Point(25, 75),
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            _contentPanel.Controls.Add(lblDesc);

            _chkLaunchApp = new CheckBox
            {
                Text = "NetPulse uygulamasını şimdi başlat",
                Location = new Point(25, 205),
                Size = new Size(560, 25),
                Checked = true,
                ForeColor = Color.DodgerBlue,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };
            _contentPanel.Controls.Add(_chkLaunchApp);
        }

        private async void BtnNext_Click(object? sender, EventArgs e)
        {
            if (_currentStep == 2)
            {
                // Kurulumu başlat
                ShowStep(3);
                await StartInstallationAsync();
            }
            else if (_currentStep == 4)
            {
                // Sihirbazı bitir ve gerekirse uygulamayı başlat
                if (_chkLaunchApp != null && _chkLaunchApp.Checked)
                {
                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(_installFolder, "NetPulse.App.exe"),
                            UseShellExecute = true,
                            Verb = "runas" // Force run as Administrator
                        };
                        Process.Start(startInfo);
                    }
                    catch { }
                }
                Close();
            }
            else
            {
                ShowStep(_currentStep + 1);
            }
        }

        private async Task StartInstallationAsync()
        {
            _isInstalling = true;
            _btnNext.Enabled = false;
            _btnBack.Enabled = false;
            _btnCancel.Enabled = false;

            try
            {
                // 1. Dizin oluşturma
                UpdateStatus("Kurulum dizini oluşturuluyor...", 10);
                await Task.Yield();
                if (!Directory.Exists(_installFolder))
                {
                    Directory.CreateDirectory(_installFolder);
                }

                // 2. Çalışan uygulama varsa sonlandır
                UpdateStatus("Çalışan eski NetPulse kopyaları kontrol ediliyor...", 25);
                await Task.Yield();
                var runningProc = Process.GetProcessesByName("NetPulse.App");
                if (runningProc.Length > 0)
                {
                    UpdateStatus("Çalışan uygulama sonlandırılıyor...", 35);
                    foreach (var p in runningProc)
                    {
                        try { p.Kill(); p.WaitForExit(); } catch { }
                    }
                    await Task.Yield();
                }

                // 3. Dosya çıkartma
                UpdateStatus("Dosyalar kopyalanıyor...", 50);
                await Task.Yield();
                ExtractApp(_installFolder);

                // 4. Kısayollar ve Admin yetkisi
                UpdateStatus("Masaüstü ve Başlat Menüsü kısayolları hazırlanıyor...", 75);
                await Task.Yield();
                CreateAppShortcuts();

                // 5. Kaldırma scripti (uninstall.bat) yazımı
                UpdateStatus("Kaldırma aracı yapılandırılıyor...", 90);
                await Task.Yield();
                CreateUninstallScript();

                UpdateStatus("Tamamlanıyor...", 100);
                await Task.Yield();

                ShowStep(4);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kurulum sırasında bir hata oluştu:\n{ex.Message}", "Kurulum Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowStep(1); // Hata durumunda klasör seçimine geri dön
            }
            finally
            {
                _isInstalling = false;
            }
        }

        private void UpdateStatus(string message, int progressValue)
        {
            if (_lblStatus != null) _lblStatus.Text = message;
            if (_progressBar != null) _progressBar.Value = progressValue;
        }

        private void ExtractApp(string targetFolder)
        {
            var assembly = typeof(SetupForm).Assembly;
            const string bundleResourceName = "NetPulse.Setup.Resources.AppBundle.zip";

            using var stream = assembly.GetManifestResourceStream(bundleResourceName);
            if (stream == null)
            {
                throw new FileNotFoundException("Kurulum paketi (AppBundle.zip) paket içinde bulunamadı!");
            }

            string tempBundle = Path.Combine(Path.GetTempPath(), $"NetPulse_AppBundle_{Guid.NewGuid():N}.zip");
            try
            {
                using (var fs = new FileStream(tempBundle, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }

                ZipFile.ExtractToDirectory(tempBundle, targetFolder, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempBundle))
                    {
                        File.Delete(tempBundle);
                    }
                }
                catch { }
            }
        }

        private void CreateAppShortcuts()
        {
            string targetExe = Path.Combine(_installFolder, "NetPulse.App.exe");
            string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NetPulse.lnk");
            string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "NetPulse.lnk");

            CreateAdminShortcutWithPowerShell(desktopPath, targetExe, _installFolder);
            CreateAdminShortcutWithPowerShell(startMenuPath, targetExe, _installFolder);
        }

        private void CreateAdminShortcutWithPowerShell(string shortcutPath, string targetExe, string workingDir)
        {
            try
            {
                string psScript = $@"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut('{shortcutPath}')
$shortcut.TargetPath = '{targetExe}'
$shortcut.WorkingDirectory = '{workingDir}'
$shortcut.Save()

# Kısayol baytlarından 21. byte'ı 0x20 ile VEYAlayarak yönetici modunu açıyoruz
$bytes = [System.IO.File]::ReadAllBytes('{shortcutPath}')
$bytes[21] = $bytes[21] -bor 0x20
[System.IO.File]::WriteAllBytes('{shortcutPath}', $bytes)
";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Kısayol oluşturulurken hata oluştu: {ex.Message}");
            }
        }

        private void CreateUninstallScript()
        {
            string uninstallBatContent = $@"@echo off
chcp 65001 > nul
:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Kaldırma işleminin tamamlanabilmesi için yönetici yetkisi gerekiyor.
    echo Lütfen bu dosyaya sağ tıklayıp 'Yönetici Olarak Çalıştır' seçeneğini kullanın.
    pause
    exit /b
)

echo NetPulse kaldırılıyor, lütfen bekleyin...
taskkill /f /im NetPulse.App.exe >nul 2>&1
timeout /t 1 >nul

:: Copy cleanup commands to a temp batch file and run it
echo @echo off > ""%temp%\netpulse_cleanup.bat""
echo timeout /t 1 >nul >> ""%temp%\netpulse_cleanup.bat""
echo del /f /q ""C:\Users\All Users\Desktop\NetPulse.lnk"" >> ""%temp%\netpulse_cleanup.bat""
echo del /f /q ""%%userprofile%%\Desktop\NetPulse.lnk"" >> ""%temp%\netpulse_cleanup.bat""
echo del /f /q ""C:\ProgramData\Microsoft\Windows\Start Menu\Programs\NetPulse.lnk"" >> ""%temp%\netpulse_cleanup.bat""
echo rd /s /q ""{_installFolder}"" >> ""%temp%\netpulse_cleanup.bat""
echo echo NetPulse başarıyla kaldırıldı! >> ""%temp%\netpulse_cleanup.bat""
echo pause >> ""%temp%\netpulse_cleanup.bat""
echo del ""%%~f0"" >> ""%temp%\netpulse_cleanup.bat""

start """" ""%temp%\netpulse_cleanup.bat""
exit
";

            string uninstallPath = Path.Combine(_installFolder, "uninstall.bat");
            File.WriteAllText(uninstallPath, uninstallBatContent, System.Text.Encoding.UTF8);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isInstalling)
            {
                e.Cancel = true;
                MessageBox.Show("Kurulum devam ederken sihirbaz kapatılamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                base.OnFormClosing(e);
            }
        }
    }
}





