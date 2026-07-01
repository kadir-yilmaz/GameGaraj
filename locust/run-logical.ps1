param(
    [string]$HostUrl = "https://gateway.kadiryilmaz.online",
    [int]$LogicalUsers = 10000,
    [double]$Rps = 125,
    [int]$Seconds = 60,
    [int]$MaxConnections = 200,
    [double]$SummaryInterval = 1,
    [string]$OutputDir = "results",
    [switch]$NoLiveConsole,
    [switch]$SkipInstall
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

if (-Not $SkipInstall) {
    Write-Host "Bagimliliklar kontrol ediliyor..." -ForegroundColor Cyan
    pip install -r requirements.txt | Out-Null
}

Write-Host "Logical user runner baslatiliyor..." -ForegroundColor Green
Write-Host "LogicalUsers=$LogicalUsers Rps=$Rps Seconds=$Seconds MaxConnections=$MaxConnections" -ForegroundColor Yellow

$runnerArgs = @(
    "logical_runner.py",
    "--host", $HostUrl,
    "--logical-users", $LogicalUsers,
    "--rps", $Rps,
    "--duration", $Seconds,
    "--max-connections", $MaxConnections,
    "--summary-interval", $SummaryInterval,
    "--output-dir", $OutputDir
)

if ($NoLiveConsole) {
    $runnerArgs += "--no-live-console"
}

python @runnerArgs
