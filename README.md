# NetPulse — Gelişmiş Ağ İzleme ve Kesinti Analizörü

NetPulse, **.NET 8.0** tabanlı, özellikle **mikro kesintileri** (10-15 saniye süren kısa kopmaları) hassas bir şekilde tespit etmek ve raporlamak için geliştirilmiş yüksek performanslı bir Windows Forms (WinForms) uygulamasıdır. 

Bu doküman, projenin amacını, kesintileri nasıl ve nereden izlediğini, saat dilimi ayarlarını ve genel yapısını detaylandırmak amacıyla hazırlanmıştır.

---

## 🎯 Projenin Amacı

İnternet servis sağlayıcıları (ISS), genellikle uzun süreli kesintileri kayıt altına alırken, 10-15 saniyelik mikro kopmaları gözden kaçırabilirler. NetPulse'ın temel amacı:
- Bağlantıdaki mikro kesintileri sub-saniye (milisaniye hassasiyetinde) doğrulukla yakalamak.
- ICMP filtrelemelerinden kaynaklanan hatalı alarmları engellemek için akıllı doğrulama yapmak.
- Değiştirilemez ve doğrulanabilir log kayıtları (HMAC imzalı) üreterek internet servis sağlayıcınıza (ISS) sunabileceğiniz resmi nitelikte **downtime (kesinti) kanıt raporları** oluşturmaktır.

---

## 🔍 İnternet Kesintileri Nereden ve Nasıl Tespit Edilir?

NetPulse, bağlantı durumunu analiz etmek için çok katmanlı bir yaklaşım benimser:

### 1. Drift Düzeltmeli Paralel ICMP Ping Motoru (Nereden Sorusu)
- **Nereye:** Uygulama, `config.json` dosyasında belirlediğiniz hedef adreslere (örneğin yerel modem geçidiniz `192.168.1.1` ve harici güvenilir DNS sunucusu `8.8.8.8`) paralel olarak ping gönderir.
- **Nasıl:** Standart zamanlayıcılardaki (Timer) kayma payını sıfırlamak için `Stopwatch` destekli bir kayma düzeltme mekanizması (Drift Correction) kullanılır. Pingleme işlemi tam olarak her 1000ms'de bir (veya ayarlanan aralıkta) hassas şekilde tekrarlanır.

### 2. Akıllı Fallback (Yedek) Teşhis Mekanizması (Nasıl Sorusu)
ICMP ping istekleri bazen ağdaki bir firewall tarafından engellenebilir. Sadece ping yanıtı gelmediği için "internet kesildi" dememek adına NetPulse şu adımları izler:
- Eğer bir hedef **ardışık 3 kez** ping yanıtı vermezse, arka planda hemen **TCP ve HTTP Teşhis Testleri** başlatılır.
- Google DNS'e (`8.8.8.8`) 53 numaralı port üzerinden hızlıca bir **TCP Bağlantısı** denenir.
- Google'ın bağlantı doğrulama servisine (`https://www.google.com/generate_204`) bir **HTTP GET** isteği atılır.
- Bu yedek testler başarılı olursa, internetin aslında kesilmediği, sadece ICMP (ping) paketlerinin engellendiği/kaybolduğu anlaşılır ve loglara teşhis notu olarak düşülür. Her iki test de başarısız olursa gerçek bir internet kesintisi ilan edilir.

### 3. Devre Kesici (Circuit Breaker) Koruması
Bir hedef adres **ardışık 10 kez** başarısız olursa, ağ trafiğini gereksiz yere boğmamak ve sistemi yormamak için **Devre Kesici** açılır. İlgili hedef 60 saniye boyunca dinlendirilir ve bu süreçte aktif ping gönderimi durdurulur.

---

## ⏰ Saat Dilimi (Timezone) ve Zaman Damgası Yapılandırması

Kesintilerin ne zaman yaşandığını ISS loglarıyla eşleştirebilmek için zaman damgalarının doğruluğu kritiktir. NetPulse bu sorunu yapılandırılabilir iki seçenekle çözer:

- **UTC Zaman Dilimi (`"UseUtcTimestamps": true`)**: 
  Loglar, dünya standart saati olan **UTC (ISO-8601)** formatında (`YYYY-MM-DDTHH:mm:ssZ`) kaydedilir. Bu ayar, farklı sunucular ve uluslararası standart log okuyucularla tam uyumluluk sağlar.
- **Yerel Zaman Dilimi (`"UseUtcTimestamps": false`)**: 
  Loglar, bilgisayarınızın o anki bölgesel saat dilimine ve yerel saatine göre kaydedilir. ISS'nize doğrudan Türkiye saatiyle (veya kendi yerel saatinizle) rapor sunmak istiyorsanız bu seçeneği tercih edebilirsiniz.

> [!TIP]
> Saat dilimi ayarını `config.json` dosyasındaki `"UseUtcTimestamps"` alanından `true` veya `false` yaparak değiştirebilirsiniz. Uygulama, dosyadaki değişiklikleri anında algılayarak yeniden başlatmaya gerek kalmadan uygulamaya koyar.

---

## 📂 Proje Bileşenleri ve Yapısı (Neyin Ne Olduğu)

Proje, temiz ve birbiriyle doğrudan bağımlı olmayan katmanlı bir mimariye (Decoupled Architecture) sahiptir:

### 1. Çekirdek Katman (Core Layer)
- [PingEngine.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Core/PingEngine.cs): Tüm ping döngüsünü, milisaniye hassasiyetini ve olay tetiklemelerini yöneten ana motor.
- [FallbackChecker.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Core/FallbackChecker.cs): Ping başarısızlığında devreye giren TCP ve HTTP kontrol mekanizması.
- [CircuitBreaker.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Core/CircuitBreaker.cs): Hedef çöktüğünde ping atmayı geçici olarak durduran koruma sistemi.

### 2. Altyapı Katman (Infrastructure Layer)
- [ConfigManager.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Infrastructure/ConfigManager.cs): Yapılandırma dosyalarını okur, yazar ve dosya değişikliklerini anlık izler (`FileSystemWatcher`).
- [LogWriter.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Infrastructure/LogWriter.cs): Olayları JSON formatında diske yazar. Logların değiştirilmediğini garanti etmek için isteğe bağlı **HMAC-SHA256** şifreleme imzası ekler.
- [SqliteDbManager.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Infrastructure/SqliteDbManager.cs): Logların ve kesinti geçmişinin yerel bir SQLite veritabanında yapılandırılmış olarak saklanmasını sağlar.
- [ExportService.cs](file:///c:/Users/sahin/Desktop/NetPulse/src/NetPulse.App/Infrastructure/ExportService.cs): SQLite veritabanı veya JSON loglarından ISS'ye gönderilmeye hazır CSV ve metin (TXT) raporları üretir.

### 3. Arayüz Katmanı (UI Layer)
- **MainForm**: Gerçek zamanlı grafikler, anlık gecikme süreleri (Latency), kesinti geçmişi listesi ve alarm göstergelerinin yer aldığı modern karanlık tema (Dark Mode) kontrol paneli.
- **KpiCard**: Kritik durumları (Çevrimiçi/Çevrimdışı, Ortalama Gecikme, En Uzun Kesinti vb.) gösteren görsel kart bileşenleri.

---

## 🛠️ Kurulum ve Çalıştırma

### 🚀 Doğrudan Çalıştırılabilir (.exe) Sürüm
Projeyi kaynak koddan derlemek istemiyorsanız, önceden derlenmiş en güncel çalıştırılabilir `.exe` dosyasını GitHub üzerindeki **Releases** (Sürümler) bölümünden indirip doğrudan yönetici olarak çalıştırabilirsiniz.

### Gereksinimler (Kaynak Koddan Çalıştırma İçin)
- Windows 10 (Build 17763 veya üstü) / Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Yönetici Yetkisi:** ICMP soketlerini doğrudan yönetmek ve Windows Bildirimlerini (Toast Notifications) sisteme kaydedebilmek için uygulamanın **Yönetici olarak çalıştırılması** tavsiye edilir.

### Çalıştırma Adımları
1. Terminalinizi açın ve proje dizinine gidin:
   ```powershell
   cd c:\Users\sahin\Desktop\NetPulse
   ```
2. Bağımlılıkları geri yükleyin ve projeyi derleyin:
   ```powershell
   dotnet restore
   dotnet build -c Release
   ```
3. Uygulamayı başlatın:
   ```powershell
   dotnet run --project src/NetPulse.App/NetPulse.App.csproj
   ```

---

## ⚙️ Yapılandırma Seçenekleri (`config.json`)

Uygulamanın çalıştığı dizinde otomatik olarak oluşturulan `config.json` üzerinden tüm ayarlar dinamik olarak güncellenebilir:

```json
{
  "Targets": [
    {
      "Address": "192.168.1.1",
      "DisplayName": "Yerel Ağ Geçidi",
      "IsEnabled": true
    },
    {
      "Address": "8.8.8.8",
      "DisplayName": "Dış DNS (Google)",
      "IsEnabled": true
    }
  ],
  "IntervalMs": 1000,
  "LogRetentionDays": 7,
  "ExportPath": "Exports",
  "HmacKey": "LogGuvenlikAnahtari",
  "UseUtcTimestamps": true
}
```
