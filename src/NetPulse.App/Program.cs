using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using NetPulse.App.Core;
using NetPulse.App.Infrastructure;
using NetPulse.App.UI;
using PdfSharp.Fonts; // PDF Font ayarları için gerekli

namespace NetPulse.App
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        private static void Main(string[] args)
        {
            // 0. Tekil Örnek (Single Instance) Kontrolü
            _mutex = new Mutex(true, "NetPulse_SingleInstance_Mutex", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "NetPulse zaten çalışıyor. Sağ alttaki sistem tepsisinden (System Tray) uygulamaya erişebilirsiniz.",
                    "NetPulse - Zaten Çalışıyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            try
            {
                // 1. PDF Çözümü: PDFSharp kütüphanesine Windows yazı tiplerini kullanmasını söylüyoruz
                GlobalFontSettings.FontResolver = new WindowsFontResolver();

                // 2. WinForms DPI ve Görsel Stil Ayarları (Eksik olan kısım eklendi)
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.SystemAware);

                // 3. Yönetici Yetkisi Kontrolü (UAC)
                if (!IsRunningAsAdmin())
                {
                    // Sonsuz döngüyü engellemek için parametreyi kontrol et
                    if (args.Length == 0 || args[0] != "--no-admin-check")
                    {
                        var result = MessageBox.Show(
                            "NetPulse'ın yüksek doğruluklu saniyelik ICMP ping sinyalleri gönderebilmesi ve Windows Bildirimlerini sisteme doğru şekilde kaydedebilmesi için Yönetici (Administrator) yetkileri ile çalıştırılması önerilir.\n\nUygulamayı Yönetici olarak yeniden başlatmak ister misiniz?",
                            "NetPulse - Yönetici Yetkisi Gerekiyor",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );

                        if (result == DialogResult.Yes)
                        {
                            if (RelaunchAsAdmin())
                            {
                                return; // Mevcut örneği sonlandır
                            }
                            else
                            {
                                MessageBox.Show("Yönetici yetkileri ile yeniden başlatılamadı. Uygulama standart yetkilerle başlatılıyor.", "NetPulse Bilgilendirme", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }

                // 4. Servis Kayıtları ve Dependency Injection
                using (var configManager = new ConfigManager())
                using (var logWriter = new LogWriter(configManager))
                {
                    var exportService = new ExportService(logWriter);
                    var notificationService = new NotificationService();

                    using (var pingProvider = new PingProvider())
                    {
                        var circuitBreaker = new CircuitBreaker();
                        var fallbackChecker = new FallbackChecker();

                        // PingEngine constructor artık targets yerine direkt configManager alıyor
                        var engine = new PingEngine(
                            pingProvider,
                            circuitBreaker,
                            fallbackChecker,
                            configManager
                        );

                        // Arka plan ping motoru görevinin başlatılması
                        var cts = new CancellationTokenSource();
                        _ = engine.StartAsync(cts.Token);

                        // 5. Ana Dashboard Ekranını Başlat
                        Application.Run(new MainForm(configManager, logWriter, exportService, notificationService, engine));

                        // Kapanışta arka plan izleme motorunu iptal et
                        cts.Cancel();
                        try
                        {
                            engine.StopAsync().GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }
            }
        }

        private static bool IsRunningAsAdmin()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static bool RelaunchAsAdmin()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath,
                Arguments = "--no-admin-check",
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}