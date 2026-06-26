$ErrorActionPreference = "Stop"

$installDir = "C:\Program Files\NetPulse"
$packageDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$bundlePath = Join-Path $packageDir "AppBundle.zip"

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
    $handoffDir = Join-Path $env:TEMP "NetPulseInstall"
    if (Test-Path $handoffDir) {
        Remove-Item $handoffDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $handoffDir -Force | Out-Null
    Copy-Item -LiteralPath $MyInvocation.MyCommand.Path -Destination (Join-Path $handoffDir "InstallNetPulse.ps1") -Force
    Copy-Item -LiteralPath $bundlePath -Destination (Join-Path $handoffDir "AppBundle.zip") -Force

    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$handoffDir\InstallNetPulse.ps1`"" -Verb RunAs
    exit
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path $bundlePath)) {
    throw "AppBundle.zip bulunamadı: $bundlePath"
}

$runningProc = Get-Process -Name "NetPulse.App" -ErrorAction SilentlyContinue
if ($runningProc) {
    Stop-Process -Name "NetPulse.App" -Force
}

if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}

[System.IO.Compression.ZipFile]::ExtractToDirectory($bundlePath, $installDir, $true)

$targetExe = Join-Path $installDir "NetPulse.App.exe"
$desktopPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "NetPulse.lnk"
$startMenuPath = Join-Path ([Environment]::GetFolderPath("CommonStartMenu")) "Programs\NetPulse.lnk"

function New-NetPulseShortcut {
    param(
        [string]$ShortcutPath,
        [string]$TargetPath
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = [System.IO.Path]::GetDirectoryName($TargetPath)
    $shortcut.Save()

    $bytes = [System.IO.File]::ReadAllBytes($ShortcutPath)
    $bytes[21] = $bytes[21] -bor 0x20
    [System.IO.File]::WriteAllBytes($ShortcutPath, $bytes)
}

New-NetPulseShortcut -ShortcutPath $desktopPath -TargetPath $targetExe
New-NetPulseShortcut -ShortcutPath $startMenuPath -TargetPath $targetExe

$uninstallPath = Join-Path $installDir "Uninstall.ps1"
$uninstallContent = @"
`$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not `$isAdmin) {
    Start-Process powershell.exe -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File ```"`$PSCommandPath```"" -Verb RunAs
    exit
}

Stop-Process -Name "NetPulse.App" -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$desktopPath" -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$startMenuPath" -Force -ErrorAction SilentlyContinue
Start-Process cmd.exe -ArgumentList "/c timeout /t 1 > nul & rmdir /s /q ```"$installDir```"" -WindowStyle Hidden
"@
Set-Content -Path $uninstallPath -Value $uninstallContent -Encoding UTF8

Start-Process -FilePath $targetExe -WorkingDirectory $installDir
