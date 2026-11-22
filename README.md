# GameGaraj - Microservices E-Commerce Platform

GameGaraj, modern mikroservis mimarisi, polyglot yaklaşımı (.NET 8 & Go) ve dağıtık sistem prensipleri üzerine inşa edilmiş bir e-ticaret platformudur.

---

## Hızlı Kurulum (Local Development)

### Gereksinimler
- **.NET 8 SDK**
- **Go 1.23+** (`brew install go`)
- **Docker Desktop**
- *(macOS kullanıcıları için)* Gateway `:5000` portunu kullandığından **Sistem Ayarları -> Genel -> AirDrop ve Handoff -> AirPlay Alıcısı** kapalı olmalıdır.

### 1. Ortam Değişkenlerini (`.env`) Oluşturun
```bash
cp .env.example .env
```
`.env` içindeki `SA_PASSWORD`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `SMTP_USER` ve `SMTP_PASSWORD` alanlarını kendi değerlerinizle güncelleyin.

### 2. Tüm `appsettings.json` Dosyalarını Kopyalayın
Her servisin klasöründeki `appsettings.example.json` şablonları local Docker portlarına göre hazırdır.

**macOS / Linux:**
```bash
for f in GameGaraj.*/appsettings.example.json; do
  cp -n "$f" "${f%.example.json}.json"
done
```

**Windows (PowerShell):**
```powershell
Get-ChildItem -Recurse -Filter "appsettings.example.json" | ForEach-Object {
    Copy-Item $_.FullName ($_.FullName -replace ".example.json", ".json")
}
```
Kopyalama sonrası `Order.API`, `Campaign.API` (SQL Server `SA_PASSWORD`) ve `Payment.API` (`Iyzipay` sandbox anahtarları) içindeki placeholder değerleri doldurun.

### 3. Altyapı Container'larını Başlatın
```bash
docker compose up -d
```
Keycloak ilk kez ayağa kalkarken `config/keycloak/realm-init.json` üzerinden `GameGaraj` realm'ini, Google OAuth sağlayıcısını ve `.env` içindeki admin kullanıcısını otomatik oluşturur.

### 4. Uygulama Servislerini Başlatın

**macOS (11 .NET + 2 Go servisi — interaktif menü veya CLI):**
```bash
# 11 .NET servisi (WebUI HTTPS :7050, diğerleri HTTP)
./dotnet-services.sh          # İnteraktif menü
./dotnet-services.sh start    # Tümünü başlat
./dotnet-services.sh stop     # Tümünü durdur

# 2 Go servisi (Search :5082, Notification :5025)
./go-services.sh              # İnteraktif menü
./go-services.sh start        # Tümünü başlat
./go-services.sh stop         # Tümünü durdur
```

**Windows (PowerShell):**
```powershell
.\go-services.ps1 start
```

### 5. Erişim Adresleri
- **WebUI:** `https://localhost:7050`
- **YARP Gateway:** `http://localhost:5000`
- **Keycloak Admin:** `http://localhost:8080`
- **RabbitMQ Management:** `http://localhost:15672`
- **Jaeger Tracing:** `http://localhost:16686`

---

## Mimari Özeti ve Temel Prensipler

Bu proje; yüksek trafik, veri tutarlılığı (consistency), yatay ölçeklenebilirlik (scalability) ve gözlemlenebilirlik gereksinimlerine yönelik modern mikroservis desenlerini uygular.

```
                         ┌─────────────────────────┐
                         │   WebUI (MVC) :7050     │
                         └────────────┬────────────┘
                                      │
                         ┌────────────▼────────────┐
                         │   YARP Gateway :5000    │
                         └──────┬───────────┬──────┘
         /api/search/*          │           │          /api/catalog/*
      ┌─────────────────────────┘           └──────────────────────────┐
      ▼                                                                ▼
┌──────────────────────────────┐                         ┌──────────────────────────────┐
│  Search API (Go + Gin)       │                         │  Catalog API (.NET 8)        │
│  Port: 5082                  │                         │  Port: 5011                  │
│                              │                         │                              │
│  • Full-Text Search (ES)     │                         │  • Product / Category CRUD   │
│  • Autocomplete Suggestions  │      RabbitMQ Events    │  • Dynamic Attributes        │
│  • Facets & Aggregations     │◄────────────────────────┤  • Primary Source of Truth   │
│  • Multi-Level Redis Cache   │  (ProductCreated/       │  • EF Core + PostgreSQL      │
└───────┬──────────────┬───────┘   Updated/Deleted)      └──────────────┬───────────────┘
        │              │                                                │
        ▼              ▼                                                ▼
┌──────────────┐ ┌──────────────┐                             ┌───────────────────┐
│Elasticsearch │ │ Redis Cache  │                             │ PostgreSQL (JSONB)│
│:9201         │ │ :6380        │                             │ :5434             │
└──────────────┘ └──────────────┘                             └───────────────────┘
```

---

## Dağıtık İşlem Mimarisi: Saga Choreography & Transactional Outbox

Sipariş akışında dağıtık veri tutarlılığı (**Eventual Consistency**) ve sıfır veri kaybı garantisi sağlamak için **Saga Choreography Pattern** ve **Transactional Outbox Pattern** uygulanmıştır.

```
[ Kullanıcı Sipariş Verdi ]
            │
            ▼ (Faz 1: Senkron Doğrulama & Ödeme)
 ┌─────────────────────────────────────────────────────────────┐
 │ 1. Catalog.API  ──► Stok Doğrulama                         │
 │ 2. Order.API    ──► Sipariş Kaydı + Outbox (OrderStarted)   │
 │ 3. Payment.API  ──► İyzico Kredi Kartı Tahsilatı            │
 └──────────────────────────────┬──────────────────────────────┘
                                │
                                ▼ (Faz 2: Asenkron Event Dağıtımı)
                     [ MassTransit / RabbitMQ ]
                                │
         ┌──────────────────────┼──────────────────────┐
         ▼                      ▼                      ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│   Invoice.API    │  │ Notification.API │  │   Campaign.API   │
│   (.NET 8)       │  │ (Go + Gin)       │  │   (.NET 8)       │
│                  │  │                  │  │                  │
│ • PDF Üretimi    │  │ • Gmail SMTP     │  │ • Hediye Kupon   │
│ • MinIO / Disk   │  │ • PDF Eki E-posta│  │ • Sadakat Puanı  │
└────────┬─────────┘  └──────────────────┘  └──────────────────┘
         │ (Fatura Hazır Event)
         └──────────────────────► Notification.API (Fatura Maili)
```

### 1. Transactional Outbox Pattern
**MassTransit EF Core Outbox** kullanılarak sipariş kaydı ve domain event'leri `OutboxMessages` tablosuna aynı SQL transaction'ı içinde atomik olarak yazılır. RabbitMQ geçici olarak erişilemez olsa bile mesaj kaybolmaz; kuyruk hazır olduğunda arka plan worker'ı mesajları iletir (At-least-once delivery).

### 2. Chaos Engineering & Admin Pipeline Simülatörü
Admin paneldeki **Sipariş Pipeline & Chaos Senaryoları** (`/Admin/OrderPipelineDemo`) üzerinden mikroservis dayanıklılığı senaryo bazlı test edilebilir:
- **Senaryo Kartları:** Normal Akış, Polly Retry (Geçici Hata Kurtarma), Saga Compensate / Rollback, Stok Yok Koruması (Fail-Fast), 3sn Gecikme ve Serbest Chaos Lab.
- **Canlı Vana Kontrolleri:** Her mikroservis için `Aktif`, `Retry`, `3sn` ve `Uykuda (503)` modları.
- **Canlı Veritabanı Durumu:** Her adımdan sonra Catalog DB (`Stok`, `Rezerve`, `Kullanılabilir`) ve Order DB (`Pending`, `Completed`, `Failed`) durumları gerçek veritabanından okunarak gösterilir.

---

## Catalog API & Search API Ayrımı (CQRS)

### 1. Catalog API (.NET 8 & EF Core) — Write & Domain Model
- Ürün, kategori, dinamik özellikler ve stok rezervasyonu **PostgreSQL** üzerinde tutulur.
- Ürün ve stok değişikliklerinde MassTransit aracılığıyla RabbitMQ'ya event yayınlanır (`ProductCreatedForSearch`, `ProductUpdatedForSearch`, `ProductDeletedForSearch`).

### 2. Search API (Go + Gin + Elasticsearch) — Read & Arama Modeli
- Çoklu alan araması (`Multi-match`), typo toleransı, `Edge-NGram` otomatik tamamlama ve facet/filtreleme sorgularını **Elasticsearch** üzerinden sunar.
- RabbitMQ event'lerini dinleyerek Elasticsearch indeksini ve Redis önbelleğini asenkron günceller.

---

## Redis Dağıtık Önbellekleme & Invalidation

- **Search API:** `search-cache:query_{hash}`, `search-cache:sugg_{hash}`, `search-cache:featured`
- **Catalog API:** `product_{id}`, `catalog-cache:category_{id}`, `catalog-cache:tree`
- **Basket API:** `basket_{userId}`

Ürün güncellendiğinde `ProductUpdatedForSearch` / `ProductDeletedForSearch` event'leri ile Elasticsearch güncellenir ve ilgili Redis anahtarları non-blocking `SCAN` + `DEL` ile geçersiz kılınır.

---

## Servisler ve Port Listesi

### Uygulama Servisleri

| Servis | Dil / Framework | Port | Swagger / OpenAPI Adresi | Veritabanı / Altyapı |
| :--- | :--- | :--- | :--- | :--- |
| **Yarp Gateway** | .NET 8 / YARP | `5000` | - | Reverse Proxy |
| **WebUI** | ASP.NET Core MVC | `7050` (HTTPS) / `5050` | - | MVC / Razor View |
| **Catalog API** | .NET 8 / WebAPI | `5011` | `http://localhost:5011/swagger` | PostgreSQL (`5434`), Redis |
| **PhotoStock API** | .NET 8 / WebAPI | `5012` | `http://localhost:5012/swagger` | MinIO / Local Storage |
| **Basket API** | .NET 8 / WebAPI | `5013` | `http://localhost:5013/swagger` | Redis Sentinel (`6380`) |
| **Order API** | .NET 8 / EF Core | `5015` | `http://localhost:5015/swagger` | SQL Server (`1433`), Outbox |
| **Payment API** | .NET 8 / MassTransit | `5016` | `http://localhost:5016/swagger` | Iyzico Sandbox |
| **Invoice API** | .NET 8 / MassTransit | `5017` | `http://localhost:5017/swagger` | MinIO / Local Storage |
| **Campaign API** | .NET 8 / Dapper | `5018` | `http://localhost:5018/swagger` | SQL Server (`1434`) |
| **Review API** | .NET 8 / EF Core | `5221` | `http://localhost:5221/swagger` | PostgreSQL (`5435`), Redis |
| **Discussion API** | .NET 8 / EF Core | `5023` | `http://localhost:5023/swagger` | PostgreSQL (`5436`), Redis |
| **Search API** | Go / Gin | `5082` | `http://localhost:5082/swagger/index.html` | Elasticsearch (`9201`), Redis |
| **Notification API** | Go / Gin | `5025` | `http://localhost:5025/swagger/index.html` | RabbitMQ, MinIO, SMTP |

### Altyapı ve Destek Servisleri

| Servis | Port(lar) | Açıklama |
| :--- | :--- | :--- |
| **Keycloak (IAM)** | `8080` | OIDC / OAuth2 Kimlik Doğrulama ve Yetkilendirme |
| **RabbitMQ** | `5672`, `15672` | Servisler arası asenkron mesajlaşma broker'ı |
| **Elasticsearch** | `9201` | Ürün arama indeksi ve analitik veriler |
| **Kibana** | `5601` | Elasticsearch görselleştirme ve indeks yönetimi |
| **Jaeger** | `16686`, `4317`, `4318` | Dağıtık tracing (OpenTelemetry OTLP) |
| **Redis Master** | `6380` | Dağıtık önbellek ve sepet veritabanı |
| **Redis Sentinels** | `26379`, `26380`, `26381` | Yüksek erişilebilirlik (HA) ve otomatik failover |
| **Prometheus** | `9090` | Mikroservislerden metrik toplama (`/metrics`) |
| **Grafana** | `30300` | Sistem performans ve metrik dashboard'ları |
| **ArgoCD** | `30580` | GitOps tabanlı Continuous Deployment |

---

## Güvenlik Notları

- `.env` ve tüm `appsettings.json` dosyaları `.gitignore` içindedir; Git'e commit edilmez.
- `.env.example` ve `appsettings.example.json` dosyaları yalnızca şablon olarak kullanılır, gerçek şifre veya API anahtarı içermez.
- Production ve staging ortamları için gizli anahtarlar GitHub Secrets / Kubernetes Secrets üzerinden yönetilir.
