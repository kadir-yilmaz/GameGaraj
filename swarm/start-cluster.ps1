$ErrorActionPreference = "Stop"

# Scriptin bulunduğu klasöre (swarm klasörüne) git
Set-Location $PSScriptRoot

Write-Host "GameGaraj Swarm Cluster Başlatılıyor..." -ForegroundColor Cyan

docker stack deploy `
  -c gateway.yaml `
  -c catalog-api.yaml `
  -c photostock-api.yaml `
  -c basket-api.yaml `
  -c discount-api.yaml `
  -c order-api.yaml `
  -c payment-api.yaml `
  -c invoice-api.yaml `
  -c campaign-api.yaml `
  -c webui.yaml `
  gamegaraj_swarm

Write-Host "İşlem tamamlandı! Servis durumlarını kontrol etmek için: docker service ls" -ForegroundColor Green
