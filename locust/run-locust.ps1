param(
    [string]$HostUrl = "https://gateway.kadiryilmaz.online",
    [string]$Scenario = "realistic_shopper",
    [double]$StartSpread = 60,
    [double]$PaceMultiplier = 1,
    [switch]$Headless,
    [int]$Users = 100,
    [double]$SpawnRate = 2,
    [string]$RunTime = "10m",
    [switch]$SkipInstall
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

if (-Not $SkipInstall) {
    Write-Host "Bagimliliklar kontrol ediliyor..." -ForegroundColor Cyan
    pip install -r requirements.txt | Out-Null
}

$commonArgs = @(
    "-m", "locust",
    "-f", "locustfile.py",
    "--host", $HostUrl,
    "--scenario", $Scenario,
    "--start-spread", $StartSpread,
    "--pace-multiplier", $PaceMultiplier
)

if ($Headless) {
    Write-Host "Locust tek process headless modda baslatiliyor..." -ForegroundColor Green
    Write-Host "Users=$Users SpawnRate=$SpawnRate RunTime=$RunTime PaceMultiplier=$PaceMultiplier" -ForegroundColor Yellow

    python @commonArgs `
        --headless `
        --users $Users `
        --spawn-rate $SpawnRate `
        --run-time $RunTime `
        --only-summary
}
else {
    Write-Host "Locust Web UI tek process modda baslatiliyor..." -ForegroundColor Green
    Write-Host "Tarayicinizdan http://localhost:8089 adresine giderek testi baslatabilirsiniz." -ForegroundColor Yellow
    Write-Host "Oneri: Scenario=$Scenario, StartSpread=$StartSpread, PaceMultiplier=$PaceMultiplier" -ForegroundColor Cyan

    python @commonArgs --web-host 127.0.0.1
}
