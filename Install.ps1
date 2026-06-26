# NetPulse Kurulum Scripti (Install.ps1)
# Bu betik NetPulse'ı tek bir .exe olarak paketler, C:\Program Files\NetPulse dizinine kurar ve yönetici olarak çalışan kısayollar oluşturur.

# 1. Yönetici yetkisi kontrolü
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Kurulumun tamamlanabilmesi için yönetici yetkileri gerekiyor..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    Exit
}

Clear-Host
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NetPulse Kurulum Sihirbazına Hoş Geldiniz  " -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# 2. Uygulama paketleme (Publishing)
Write-Host "[1/4] Uygulama tek dosya (Self-Contained) olarak derleniyor..." -ForegroundColor Green
$projectPath = Join-Path $PSScriptRoot "src\NetPulse.App\NetPulse.App.csproj"
$publishDir = Join-Path $PSScriptRoot "publish"

# dotnet publish komutu (single file, self-contained, win-x64, Release)
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Derleme başarısız oldu! Lütfen .NET SDK'nın kurulu olduğundan emin olun." -ForegroundColor Red
    Read-Host "Çıkmak için Enter tuşuna basın..."
    Exit
}

# 3. Dosyaları kopyalama (Installation)
Write-Host ""
Write-Host "[2/4] Dosyalar kuruluyor..." -ForegroundColor Green
$installDir = "C:\Program Files\NetPulse"
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

# Çalışan uygulama varsa durdur (Erişim hakkımız var çünkü yöneticiyiz!)
$runningProc = Get-Process -Name "NetPulse.App" -ErrorAction SilentlyContinue
if ($runningProc) {
    Write-Host "Açık olan NetPulse uygulaması kapatılıyor..." -ForegroundColor Yellow
    Stop-Process -Name "NetPulse.App" -Force
    Start-Sleep -Seconds 1
}

# exe'yi kopyala
Copy-Item (Join-Path $publishDir "NetPulse.App.exe") $installDir -Force

# 4. Kısayolların Oluşturulması ve Yönetici Yetkisinin Atanması
Write-Host ""
Write-Host "[3/4] Masaüstü ve Başlat Menüsü kısayolları oluşturuluyor..." -ForegroundColor Green

$desktopPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "NetPulse.lnk")
$startMenuPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("CommonStartMenu"), "Programs\NetPulse.lnk")

$targetExe = Join-Path $installDir "NetPulse.App.exe"

function Create-AdminShortcut {
    param(
        [string]$shortcutPath,
        [string]$targetPath
    )
    # COM nesnesi ile kısayol oluştur
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $targetPath
    $shortcut.WorkingDirectory = [System.IO.Path]::GetDirectoryName($targetPath)
    $shortcut.Save()

    # Kısayola "Yönetici Olarak Çalıştır" (Run as Admin) bayrağını ekle (byte offset 21)
    $bytes = [System.IO.File]::ReadAllBytes($shortcutPath)
    $bytes[21] = $bytes[21] -bor 0x20
    [System.IO.File]::WriteAllBytes($shortcutPath, $bytes)
}

Create-AdminShortcut -shortcutPath $desktopPath -targetPath $targetExe
Create-AdminShortcut -shortcutPath $startMenuPath -targetPath $targetExe

# 5. Kaldırma Scripti (Uninstall.ps1) oluşturulması
Write-Host ""
Write-Host "[4/4] Kaldırma aracı (Uninstall.ps1) hazırlanıyor..." -ForegroundColor Green

$uninstallScriptContent = @"
# NetPulse Kaldırma Scripti (Uninstall.ps1)
`$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not `$isAdmin) {
    Write-Host "Uygulamanın kaldırılabilmesi için yönetici yetkileri gerekiyor..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File \`"``"`$PSCommandPath\`"\`"" -Verb RunAs
    Exit
}

Write-Host "NetPulse kaldırılıyor..." -ForegroundColor Yellow

# Çalışan uygulama varsa sonlandır
`$runningProc = Get-Process -Name "NetPulse.App" -ErrorAction SilentlyContinue
if (`$runningProc) {
    Stop-Process -Name "NetPulse.App" -Force
}

# Kısayolları sil
`$desktopPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("Desktop"), "NetPulse.lnk")
`$startMenuPath = [System.IO.Path]::Combine([Environment]::GetFolderPath("CommonStartMenu"), "Programs\NetPulse.lnk")

if (Test-Path `$desktopPath) { Remove-Item `$desktopPath -Force }
if (Test-Path `$startMenuPath) { Remove-Item `$startMenuPath -Force }

# Kurulum dosyalarını sil
`$installDir = "C:\Program Files\NetPulse"
if (Test-Path `$installDir) {
    # Kendini silebilmesi için klasörden çıkalım ve klasörü kaldıralım
    # Klasör içeriğini temizleyip uninstall.ps1 hariç her şeyi siliyoruz
    Get-ChildItem `$installDir | Where-Object { `$_.Name -ne "Uninstall.ps1" } | Remove-Item -Recurse -Force
    
    # Kendi kendine silinme işlemi için arka planda bir komut çalıştırıp klasörü kaldırabiliriz
    Start-Process cmd -ArgumentList "/c timeout /t 1 & rmdir /s /q \`"`$installDir\`"" -WindowStyle Hidden
}

Write-Host "NetPulse başarıyla kaldırıldı!" -ForegroundColor Green
Start-Sleep -Seconds 2
"@

$uninstallPath = Join-Path $installDir "Uninstall.ps1"
Set-Content -Path $uninstallPath -Value $uninstallScriptContent -Encoding UTF8

# Temizlik
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NetPulse Kurulumu Başarıyla Tamamlandı!   " -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Uygulama 'C:\Program Files\NetPulse' klasörüne yüklendi."
Write-Host "Masaüstünüzdeki veya Başlat Menünüzdeki 'NetPulse' kısayoluna çift tıklayarak uygulamayı başlatabilirsiniz."
Write-Host ""
Read-Host "Kapatmak için Enter tuşuna basın..."
