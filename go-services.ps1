<#
.SYNOPSIS
    GameGaraj Go Servisleri Yonetim Scripti (Search API & Notification API)

.DESCRIPTION
    Bu script, projede yer alan Go mikroservislerini (Search API ve Notification API)
    kolayca baslatmanizi, durdurmanizi, yeniden baslatmanizi ve durumlarini izlemenizi saglar.

.EXAMPLE
    .\go-services.ps1 start
    .\go-services.ps1 stop
    .\go-services.ps1 restart
    .\go-services.ps1 status
    .\go-services.ps1 start search
    .\go-services.ps1 stop notification
    .\go-services.ps1 logs notification
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

function Get-ServicePID($Port, $ProcessName) {
    try {
        $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($conn -and $conn.OwningProcess) {
            return $conn.OwningProcess
        }
    } catch {}

    try {
        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($proc) {
            return $proc.Id
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
    $searchPID = Get-ServicePID $SearchPort "search-api"
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
    $notifPID = Get-ServicePID $NotifPort "notif-api"
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
    $existingPID = Get-ServicePID $SearchPort "search-api"
    if ($existingPID) {
        Write-Host " [!] Search API zaten calisiyor (Port: $SearchPort, PID: $existingPID)" -ForegroundColor Yellow
        return
    }

    Write-Host " [>] Search API baslatiliyor (:5082)..." -ForegroundColor Cyan
    
    $exePath = Join-Path $SearchDir "search-api.exe"
    if (-not (Test-Path $exePath)) {
        Push-Location $SearchDir
        go build -o search-api.exe cmd/server/main.go
        Pop-Location
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exePath
    $psi.WorkingDirectory = $SearchDir
    $psi.UseShellExecute = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    [System.Diagnostics.Process]::Start($psi) | Out-Null

    Start-Sleep -Seconds 2

    $newPID = Get-ServicePID $SearchPort "search-api"
    if ($newPID) {
        Write-Host " [OK] Search API basariyla baslatildi (PID: $newPID, Port: $SearchPort)" -ForegroundColor Green
    } else {
        Write-Host " [OK] Search API baslatildi, port dinleniyor..." -ForegroundColor Yellow
    }
}

function Stop-SearchAPI {
    $pids = @()
    $p1 = Get-ServicePID $SearchPort "search-api"
    if ($p1) { $pids += $p1 }
    $p2 = (Get-Process -Name "search-api" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    if ($p2) { $pids += $p2 }
    $pids = $pids | Select-Object -Unique

    if ($pids.Count -gt 0) {
        foreach ($pidToKill in $pids) {
            Write-Host " [x] Search API durduruluyor (PID: $pidToKill)..." -ForegroundColor Yellow
            Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Milliseconds 500
        Write-Host " [OK] Search API durduruldu." -ForegroundColor Green
    } else {
        Write-Host " [-] Search API zaten calismiyor." -ForegroundColor DarkGray
    }
}

function Start-NotifAPI {
    $existingPID = Get-ServicePID $NotifPort "notif-api"
    if ($existingPID) {
        Write-Host " [!] Notification API zaten calisiyor (Port: $NotifPort, PID: $existingPID)" -ForegroundColor Yellow
        return
    }

    Write-Host " [>] Notification API baslatiliyor (:5025)..." -ForegroundColor Cyan
    
    $exePath = Join-Path $NotifDir "notif-api.exe"
    if (-not (Test-Path $exePath)) {
        Push-Location $NotifDir
        go build -o notif-api.exe cmd/api/main.go
        Pop-Location
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exePath
    $psi.WorkingDirectory = $NotifDir
    $psi.UseShellExecute = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    [System.Diagnostics.Process]::Start($psi) | Out-Null

    Start-Sleep -Seconds 2

    $newPID = Get-ServicePID $NotifPort "notif-api"
    if ($newPID) {
        Write-Host " [OK] Notification API basariyla baslatildi (PID: $newPID, Port: $NotifPort)" -ForegroundColor Green
    } else {
        Write-Host " [OK] Notification API baslatildi, port dinleniyor..." -ForegroundColor Yellow
    }
}

function Stop-NotifAPI {
    $pids = @()
    $p1 = Get-ServicePID $NotifPort "notif-api"
    if ($p1) { $pids += $p1 }
    $p2 = (Get-Process -Name "notif-api" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
    if ($p2) { $pids += $p2 }
    $pids = $pids | Select-Object -Unique

    if ($pids.Count -gt 0) {
        foreach ($pidToKill in $pids) {
            Write-Host " [x] Notification API durduruluyor (PID: $pidToKill)..." -ForegroundColor Yellow
            Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
        }
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
        Write-Host "Kullanim: .\go-services.ps1 logs search | notification" -ForegroundColor Yellow
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
        Write-Host "  .\go-services.ps1 start              -> Tum Go servislerini baslatir" -ForegroundColor White
        Write-Host "  .\go-services.ps1 stop               -> Tum Go servislerini durdurur" -ForegroundColor White
        Write-Host "  .\go-services.ps1 restart            -> Tum Go servislerini yeniden baslatir" -ForegroundColor White
        Write-Host "  .\go-services.ps1 status             -> Servislerin port ve calisma durumunu gosterir" -ForegroundColor White
        Write-Host "  .\go-services.ps1 start search       -> Sadece Search API'yi (:5082) baslatir" -ForegroundColor White
        Write-Host "  .\go-services.ps1 stop search        -> Sadece Search API'yi durdurur" -ForegroundColor White
        Write-Host "  .\go-services.ps1 start notif        -> Sadece Notification API'yi (:5025) baslatir" -ForegroundColor White
        Write-Host "  .\go-services.ps1 stop notif         -> Sadece Notification API'yi durdurur" -ForegroundColor White
        Write-Host "  .\go-services.ps1 logs search        -> Search API loglarini listeler" -ForegroundColor White
        Write-Host "  .\go-services.ps1 logs notification  -> Notification API loglarini listeler" -ForegroundColor White
        Write-Host ""
    }
}
