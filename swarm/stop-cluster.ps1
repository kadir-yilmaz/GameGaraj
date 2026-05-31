Write-Host "GameGaraj Swarm Cluster Durduruluyor ve Siliniyor..." -ForegroundColor Yellow

docker stack rm gamegaraj_swarm

Write-Host "Kapatma işlemi tamamlandı." -ForegroundColor Green
