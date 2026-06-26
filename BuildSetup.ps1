# NetPulse Inno Setup build script.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$appPublishDir = Join-Path $PSScriptRoot "temp_publish\AppBundle"
$outputDir = Join-Path $PSScriptRoot "SetupOutput"
$innoScript = Join-Path $PSScriptRoot "installer\NetPulse.iss"

function Find-InnoCompiler {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

if (Test-Path $appPublishDir) {
    Remove-Item $appPublishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $appPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
Get-ChildItem -Path $outputDir -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   NetPulse Inno Setup Builder" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/2] Publishing NetPulse.App self-contained bundle..." -ForegroundColor Green
$appProject = Join-Path $PSScriptRoot "src\NetPulse.App\NetPulse.App.csproj"
dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false -o $appPublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "NetPulse.App publish failed." -ForegroundColor Red
    Exit $LASTEXITCODE
}

$iscc = Find-InnoCompiler
if (-not $iscc) {
    Write-Host "Inno Setup compiler (ISCC.exe) was not found." -ForegroundColor Red
    Write-Host "Install it with: winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
    Exit 1
}

Write-Host ""
Write-Host "[2/2] Building Inno Setup installer..." -ForegroundColor Green
& $iscc $innoScript

if ($LASTEXITCODE -ne 0) {
    Write-Host "Inno Setup build failed." -ForegroundColor Red
    Exit $LASTEXITCODE
}

$setupExe = Join-Path $outputDir "NetPulseSetup.exe"
$setupSizeMb = if (Test-Path $setupExe) { [math]::Round((Get-Item $setupExe).Length / 1MB, 2) } else { 0 }

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "   Inno setup build completed" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Setup file: SetupOutput\NetPulseSetup.exe ($setupSizeMb MB)"
Write-Host ""