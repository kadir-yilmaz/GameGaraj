<#
.SYNOPSIS
    GameGaraj Go Servisleri Yonetim Scripti (Search API & Notification API)

.DESCRIPTION
    Bu script, projede yer alan Go mikroservislerini (Search API ve Notification API)
    kolayca baslatmanizi, durdurmanizi, yeniden baslatmanizi ve durumlarini izlemenizi saglar.

.EXAMPLE
    .\services.ps1 start
    .\services.ps1 stop
    .\services.ps1 restart
    .\services.ps1 status
    .\services.ps1 start search
    .\services.ps1 stop notification
#>

[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [ValidateSet("start", "stop", "restart", "status", "logs", "help")]
    [string]$Action = "status",

    [Parameter(Position = 1)]
    [ValidateSet("all", "search", "notification", "notif")]
    [string]$Target = "all"
)

# UTF-8 Encoding for Console Output
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$RootDir = $PSScriptRoot
$LogDir = Join-Path $RootDir "ConsoleLogs"
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

$SearchDir = Join-Path $RootDir "GameGaraj.Search.API"
$NotifDir  = Join-Path $RootDir "GameGaraj.Notification.API"

$SearchPort = 5082
$NotifPort  = 5025

$SearchLog = Join-Path $LogDir "search-api.log"
$NotifLog  = Join-Path $LogDir "notification-api.log"

function Get-PortPID($Port) {
    try {
        $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn) {
            return $conn.OwningProcess
        }
    } catch {}
    return $null
}

function Show-Header {
    Write-Host ""
    Write-Host "=================================================" -ForegroundColor DarkCyan
    Write-Host "   GameGaraj Go Mikroservis Yoneticisi           " -ForegroundColor Cyan
    Write-Host "=================================================" -ForegroundColor DarkCyan
    Write-Host ""
}

function Show-Status {
    Show-Header
    Write-Host "Servis Durumlari:" -ForegroundColor Yellow
    Write-Host "-------------------------------------------------" -ForegroundColor Gray

    # Search API
    $searchPID = Get-PortPID $SearchPort
    if ($searchPID) {
        $proc = Get-Process -Id $searchPID -ErrorAction SilentlyContinue
        $ram = if ($proc) { [math]::Round($proc.WorkingSet64 / 1MB, 1) } else { 0 }
        Write-Host " [+] Search API       " -NoNewline -ForegroundColor Green
        Write-Host " -> AKTIF  " -NoNewline -ForegroundColor Green
        Write-Host "(Port: $SearchPort | PID: $searchPID | RAM: $ram MB)" -ForegroundColor DarkGray
    } else {
        Write-Host " [-] Search API       " -NoNewline -ForegroundColor Red
        Write-Host " -> KAPALI " -NoNewline -ForegroundColor Red
        Write-Host "(Port: $SearchPort)" -ForegroundColor DarkGray
    }

    # Notification API
    $notifPID = Get-PortPID $NotifPort
    if ($notifPID) {
        $proc = Get-Process -Id $notifPID -ErrorAction SilentlyContinue
        $ram = if ($proc) { [math]::Round($proc.WorkingSet64 / 1MB, 1) } else { 0 }
        Write-Host " [+] Notification API " -NoNewline -ForegroundColor Green
        Write-Host " -> AKTIF  " -NoNewline -ForegroundColor Green
        Write-Host "(Port: $NotifPort | PID: $notifPID | RAM: $ram MB)" -ForegroundColor DarkGray
    } else {
        Write-Host " [-] Notification API " -NoNewline -ForegroundColor Red
        Write-Host " -> KAPALI " -NoNewline -ForegroundColor Red
        Write-Host "(Port: $NotifPort)" -ForegroundColor DarkGray
    }

    Write-Host "-------------------------------------------------" -ForegroundColor Gray
    Write-Host ""
}

function Start-SearchAPI {
    $existingPID = Get-PortPID $SearchPort
    if ($existingPID) {
        Write-Host " [!] Search API zaten calisiyor (Port: $SearchPort, PID: $existingPID)" -ForegroundColor Yellow
        return
    }

    Write-Host " [>] Search API baslatiliyor (:5082)..." -ForegroundColor Cyan
    
    $exePath = Join-Path $SearchDir "search-api.exe"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    if (Test-Path $exePath) {
        $psi.FileName = $exePath
    } else {
        $psi.FileName = "go"
        $psi.Arguments = "run cmd/server/main.go"
    }
    $psi.WorkingDirectory = $SearchDir
    $psi.UseShellExecute = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    [System.Diagnostics.Process]::Start($psi) | Out-Null

    Start-Sleep -Seconds 2

    $newPID = Get-PortPID $SearchPort
    if ($newPID) {
        Write-Host " [OK] Search API basariyla baslatildi (PID: $newPID, Port: $SearchPort)" -ForegroundColor Green
    } else {
        Write-Host " [OK] Search API baslatildi, port dinleniyor..." -ForegroundColor Yellow
    }
}

function Stop-SearchAPI {
    $pidToKill = Get-PortPID $SearchPort
    if ($pidToKill) {
        Write-Host " [x] Search API durduruluyor (PID: $pidToKill)..." -ForegroundColor Yellow
        Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        Write-Host " [OK] Search API durduruldu." -ForegroundColor Green
    } else {
        Write-Host " [-] Search API zaten calismiyor." -ForegroundColor DarkGray
    }
}

function Start-NotifAPI {
    $existingPID = Get-PortPID $NotifPort
    if ($existingPID) {
        Write-Host " [!] Notification API zaten calisiyor (Port: $NotifPort, PID: $existingPID)" -ForegroundColor Yellow
        return
    }

    Write-Host " [>] Notification API baslatiliyor (:5025)..." -ForegroundColor Cyan
    
    $exePath = Join-Path $NotifDir "notif-api.exe"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    if (Test-Path $exePath) {
        $psi.FileName = $exePath
    } else {
        $psi.FileName = "go"
        $psi.Arguments = "run cmd/api/main.go"
    }
    $psi.WorkingDirectory = $NotifDir
    $psi.UseShellExecute = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    [System.Diagnostics.Process]::Start($psi) | Out-Null

    Start-Sleep -Seconds 2

    $newPID = Get-PortPID $NotifPort
    if ($newPID) {
        Write-Host " [OK] Notification API basariyla baslatildi (PID: $newPID, Port: $NotifPort)" -ForegroundColor Green
    } else {
        Write-Host " [OK] Notification API baslatildi, port dinleniyor..." -ForegroundColor Yellow
    }
}

function Stop-NotifAPI {
    $pidToKill = Get-PortPID $NotifPort
    if ($pidToKill) {
        Write-Host " [x] Notification API durduruluyor (PID: $pidToKill)..." -ForegroundColor Yellow
        Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
        Write-Host " [OK] Notification API durduruldu." -ForegroundColor Green
    } else {
        Write-Host " [-] Notification API zaten calismiyor." -ForegroundColor DarkGray
    }
}

function Show-Logs($targetService) {
    if ($targetService -eq "search") {
        if (Test-Path $SearchLog) {
            Write-Host "=== Search API Son Loglari ($SearchLog) ===" -ForegroundColor Cyan
            Get-Content -Path $SearchLog -Tail 30
        } else {
            Write-Host "Henuz Search API logu bulunamadi." -ForegroundColor Yellow
        }
    } elseif ($targetService -in @("notification", "notif")) {
        if (Test-Path $NotifLog) {
            Write-Host "=== Notification API Son Loglari ($NotifLog) ===" -ForegroundColor Cyan
            Get-Content -Path $NotifLog -Tail 30
        } else {
            Write-Host "Henuz Notification API logu bulunamadi." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Kullanim: .\services.ps1 logs search | notification" -ForegroundColor Yellow
    }
}

# Main Execution Switch
switch ($Action.ToLower()) {
    "start" {
        Show-Header
        if ($Target -in @("all", "search")) { Start-SearchAPI }
        if ($Target -in @("all", "notification", "notif")) { Start-NotifAPI }
        Write-Host ""
        Show-Status
    }
    "stop" {
        Show-Header
        if ($Target -in @("all", "search")) { Stop-SearchAPI }
        if ($Target -in @("all", "notification", "notif")) { Stop-NotifAPI }
        Write-Host ""
        Show-Status
    }
    "restart" {
        Show-Header
        if ($Target -in @("all", "search")) { Stop-SearchAPI; Start-SearchAPI }
        if ($Target -in @("all", "notification", "notif")) { Stop-NotifAPI; Start-NotifAPI }
        Write-Host ""
        Show-Status
    }
    "status" {
        Show-Status
    }
    "logs" {
        Show-Logs $Target
    }
    "help" {
        Show-Header
        Write-Host "Kullanilabilir Komutlar:" -ForegroundColor Yellow
        Write-Host "  .\services.ps1 start              -> Tum Go servislerini baslatir" -ForegroundColor White
        Write-Host "  .\services.ps1 stop               -> Tum Go servislerini durdurur" -ForegroundColor White
        Write-Host "  .\services.ps1 restart            -> Tum Go servislerini yeniden baslatir" -ForegroundColor White
        Write-Host "  .\services.ps1 status             -> Servislerin port ve calisma durumunu gosterir" -ForegroundColor White
        Write-Host "  .\services.ps1 start search       -> Sadece Search API'yi (:5082) baslatir" -ForegroundColor White
        Write-Host "  .\services.ps1 stop search        -> Sadece Search API'yi durdurur" -ForegroundColor White
        Write-Host "  .\services.ps1 start notif        -> Sadece Notification API'yi (:5025) baslatir" -ForegroundColor White
        Write-Host "  .\services.ps1 stop notif         -> Sadece Notification API'yi durdurur" -ForegroundColor White
        Write-Host "  .\services.ps1 logs search        -> Search API loglarini listeler" -ForegroundColor White
        Write-Host ""
    }
}
