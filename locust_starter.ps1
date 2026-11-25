param(
    [int]$Workers = 8,
    [int]$WebPort = 8089,
    [string]$LocustFile = "locust\locustfile.py",
    [switch]$StopOnly
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$LocustExe = Join-Path $RepoRoot "locust\.venv\Scripts\locust.exe"
$LocustFilePath = if ([System.IO.Path]::IsPathRooted($LocustFile)) { $LocustFile } else { Join-Path $RepoRoot $LocustFile }

function Stop-LocustZombies {
    param(
        [int[]]$Ports = @(8089, 5557),
        [int[]]$WorkerPids = @()
    )

    foreach ($wpid in $WorkerPids) {
        Stop-Process -Id $wpid -Force -ErrorAction SilentlyContinue
    }

    Get-Process -Name "locust" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    foreach ($port in $Ports) {
        $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        foreach ($conn in $conns) {
            if ($conn.OwningProcess -gt 0) {
                Write-Host "[cleanup] Port $port uzerindeki eski process kapatiliyor (PID=$($conn.OwningProcess))..." -ForegroundColor Yellow
                Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

function Wait-PortFree {
    param([int]$Port, [int]$TimeoutSeconds = 5)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $busy = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
        if (-not $busy) { return $true }
        Start-Sleep -Milliseconds 200
    }
    return $false
}

Write-Host "[locust] Zombie/eski processler kontrol ediliyor..." -ForegroundColor Cyan
Stop-LocustZombies -Ports @($WebPort, 5557)

if ($StopOnly) {
    Write-Host "[locust] Tum Locust processleri temizlendi." -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $LocustExe)) {
    Write-Error "Locust bulunamadi: $LocustExe`nOnce kurun: python -m venv locust\.venv; .\locust\.venv\Scripts\pip install -r locust\requirements.txt"
    exit 1
}

if (-not (Test-Path $LocustFilePath)) {
    Write-Error "Locust dosyasi bulunamadi: $LocustFilePath"
    exit 1
}

if (-not (Wait-PortFree -Port $WebPort -TimeoutSeconds 5)) {
    Write-Error "Port $WebPort hala mesgul. Lutfen portu kullanan uygulamayi kapatip tekrar deneyin."
    exit 1
}

$spawnedWorkers = @()
try {
    Write-Host "[locust] $Workers adet worker baslatiliyor..." -ForegroundColor Green
    for ($i = 1; $i -le $Workers; $i++) {
        $proc = Start-Process -FilePath $LocustExe `
            -ArgumentList "-f", "`"$LocustFilePath`"", "--worker" `
            -WorkingDirectory $RepoRoot `
            -WindowStyle Hidden `
            -PassThru
        $spawnedWorkers += $proc.Id
    }

    Write-Host "[locust] Master baslatiliyor -> http://localhost:$WebPort (Durdurmak icin Ctrl+C)" -ForegroundColor Green
    & $LocustExe -f $LocustFilePath --master --web-port $WebPort
}
finally {
    Write-Host "`n[cleanup] Worker processler kapatiliyor..." -ForegroundColor Cyan
    Stop-LocustZombies -Ports @($WebPort, 5557) -WorkerPids $spawnedWorkers
    Write-Host "[cleanup] Temizlik tamamlandi." -ForegroundColor Green
}
