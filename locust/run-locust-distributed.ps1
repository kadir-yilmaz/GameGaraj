$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

Write-Host "Arka planda kalmis eski islemler temizleniyor..." -ForegroundColor DarkGray
Stop-Process -Name python -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$LogsDir = Join-Path $ScriptDir "logs"
if (-Not (Test-Path $LogsDir)) {
    New-Item -ItemType Directory -Path $LogsDir | Out-Null
}

# Eski loglari tamamen sifirla (Uzerine yazmasi icin)
Remove-Item "$LogsDir\*.log" -Force -ErrorAction SilentlyContinue

Write-Host "Locust Master (Yonetici) baslatiliyor..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit -Command `"cd '$ScriptDir'; python -m locust -f locustfile.py --master --logfile logs\master.log`""

Start-Sleep -Seconds 2

Write-Host "4 Adet Locust Worker (Isci) baslatiliyor..." -ForegroundColor Yellow
for ($i=1; $i -le 4; $i++) {
    Start-Process powershell -ArgumentList "-NoExit -Command `"cd '$ScriptDir'; python -m locust -f locustfile.py --worker --logfile logs\worker-$i.log`""
    Start-Sleep -Milliseconds 500
}

Write-Host "--------------------------------------------------------" -ForegroundColor White
Write-Host "Sistem hazir! Tarayicinizdan http://localhost:8089 adresine giris yapin." -ForegroundColor Green
Write-Host "Arayuze girdiginizde sag ust kosede 'Workers: 4' yazdigini goreceksiniz." -ForegroundColor Yellow
Write-Host "Hata kayitlari anlik olarak 'locust/logs' klasorune kaydedilmektedir." -ForegroundColor Cyan
Write-Host "Testi bitirdiginizde acilan 5 ayri siyah pencereyi carpiya basarak kapatmayi unutmayin." -ForegroundColor Gray
