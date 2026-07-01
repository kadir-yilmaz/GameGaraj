$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $ScriptDir

Write-Host "Bagimliliklar kontrol ediliyor..." -ForegroundColor Cyan
pip install -r requirements.txt | Out-Null

Write-Host "Locust Web UI baslatiliyor..." -ForegroundColor Green
Write-Host "Tarayicinizdan http://localhost:8089 adresine giderek testi baslatabilirsiniz." -ForegroundColor Yellow
python -m locust -f locustfile.py
