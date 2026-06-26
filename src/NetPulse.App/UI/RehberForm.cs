using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetPulse.App.UI
{
    public class RehberForm : Form
    {
        public RehberForm()
        {
            Text = "NetPulse Kullanıcı Rehberi ve Bilgi Paneli";
            Size = new Size(600, 500);
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
                Text = "NetPulse Kullanım ve Teşhis Rehberi",
                Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(400, 30),
                ForeColor = Color.DodgerBlue
            };
            Controls.Add(lblTitle);

            var rtbGuide = new RichTextBox
            {
                Location = new Point(20, 60),
                Size = new Size(545, 330),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Padding = new Padding(10)
            };

            // Populate Turkish guide text
            rtbGuide.SelectedText = "1. NetPulse Nedir?\n";
            rtbGuide.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtbGuide.SelectionColor = Color.DodgerBlue;
            rtbGuide.SelectedText = "NetPulse, saniyelik hassas ping sinyalleri göndererek yerel ağ geçidinizdeki veya harici sunuculardaki mikro kesintileri (10-15 saniyelik kopmalar dahil) milisaniye hassasiyetinde tespit eden ve raporlayan gelişmiş bir ağ analiz aracıdır.\n\n";

            rtbGuide.SelectedText = "2. Yönetici Yetkileri Neden Önerilir?\n";
            rtbGuide.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtbGuide.SelectionColor = Color.DodgerBlue;
            rtbGuide.SelectedText = "Uygulamanın saniyelik yüksek doğruluklu ICMP ping paketlerini işletim sisteminden doğrudan gönderebilmesi ve internet koptuğunda Windows Toast Bildirimlerini sisteme doğru bir şekilde kaydedebilmesi için yönetici yetkileri ile çalıştırılması önerilir.\n\n";

            rtbGuide.SelectedText = "3. Otomatik Hata Teşhisleri (Fallback Kontrolü)\n";
            rtbGuide.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtbGuide.SelectionColor = Color.DodgerBlue;
            rtbGuide.SelectedText = "Bir izleme hedefinde üst üste 3 ping zaman aşımı (Timeout) oluştuğunda, NetPulse arka planda otomatik teşhis başlatır: TCP Port 53 (DNS) bağlantısı ve Google HTTP GET (generate_204) sorgusu gönderir. Bu testlerin sonucu log dosyasına kaydedilir ve problemin sadece ICMP engellemesi mi yoksa genel bir internet kopması mı olduğu anlaşılır.\n\n";

            rtbGuide.SelectedText = "4. Devre Kesici Sistemi (Circuit Breaker)\n";
            rtbGuide.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtbGuide.SelectionColor = Color.DodgerBlue;
            rtbGuide.SelectedText = "Eğer bir hedefe gönderilen pingler üst üste 10 kez başarısız olursa, devre kesici otomatik olarak tetiklenerek açık (Open) duruma gelir. Ağdaki gereksiz tıkanmaları önlemek adına, bu hedef 60 saniye boyunca izleme dışı bırakılır ve bu süre boyunca ping gönderilmez.\n\n";

            rtbGuide.SelectedText = "5. Log Bütünlüğü ve HMAC Koruması (Tamper-Evident)\n";
            rtbGuide.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtbGuide.SelectionColor = Color.DodgerBlue;
            rtbGuide.SelectedText = "Ayarlar üzerinden bir 'HMAC Anahtarı' belirleyerek, üretilen her log satırının kriptografik olarak imzalanmasını sağlayabilirsiniz. Bu imza, log dosyalarının manuel olarak değiştirilip değiştirilmediğini kanıtlamak ve servis sağlayıcınıza (ISS) manipüle edilmemiş kanıt sunabilmek için kullanılır.\n\n";

            rtbGuide.SelectedText = "6. Verileri Dışa Aktarma\n";
            rtbGuide.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtbGuide.SelectionColor = Color.DodgerBlue;
            rtbGuide.SelectedText = "Dashboard ekranının altındaki butonları kullanarak tüm izleme geçmişini Türkçe kolon başlıkları ile CSV (Excel için), düz metin TXT veya resmi rapor formatında tasarlanmış PDF dosyası olarak dışa aktarabilirsiniz.\n";

            // Reset selection to start
            rtbGuide.SelectionStart = 0;
            rtbGuide.SelectionLength = 0;

            Controls.Add(rtbGuide);

            var btnClose = new Button
            {
                Text = "Kapat",
                Location = new Point(450, 410),
                Size = new Size(115, 30),
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderColor = Color.DodgerBlue;
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);
        }
    }
}
