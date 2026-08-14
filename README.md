# GameGaraj - Microservices E-Commerce Platform

GameGaraj, modern mikroservis mimarisi, polyglot yaklaşımı (.NET 8 & Go) ve dağıtık sistem prensipleri üzerine inşa edilmiş, yüksek performanslı bir e-ticaret platformudur.

---

## 🚀 Hızlı Başlangıç

### 1. Konfigürasyon Dosyalarını Hazırlayın

Proje, hassas bilgileri korumak için `appsettings.example.json` dosyaları kullanır. Başlamadan önce:

```bash
# Her API klasöründe:
cp appsettings.example.json appsettings.json

# Veya PowerShell ile:
Get-ChildItem -Recurse -Filter "appsettings.example.json" | ForEach-Object {
    Copy-Item $_.FullName ($_.FullName -replace ".example.json", ".json")
}
```

### 2. Environment Variables (.env)

`.env` dosyasını oluşturun ve aşağıdaki değişkenleri doldurun:

```env
# Admin User Settings
ADMIN_EMAIL=your-email@example.com
ADMIN_PASSWORD=YourSecurePassword123
GOOGLE_CLIENT_ID=your-google-client-id
GOOGLE_CLIENT_SECRET=your-google-client-secret

# Email Settings
EmailSettings__SmtpUsername=your-email@gmail.com
EmailSettings__SmtpPassword=your-app-password

# Iyzico Settings
Iyzipay__ApiKey=your-iyzipay-api-key
Iyzipay__SecretKey=your-iyzipay-secret-key
```

### 3. Docker Containerları Başlatın

```bash
docker-compose up -d
```

### 4. Uygulamayı Çalıştırın

Keycloak ilk kez realm import ederken `.env` dosyasındaki `ADMIN_EMAIL` ve `ADMIN_PASSWORD` değerleriyle admin kullanıcısını oluşturur.

---

## 🏛️ Mimari Özeti ve Temel Prensipler

Bu proje, sadece bir e-ticaret uygulaması değil; yüksek trafik, veri tutarlılığı (consistency), yatay ölçeklenebilirlik (scalability) ve SEO gereksinimlerine getirilmiş modern mimari çözümler bütünüdür.

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

## 🔄 Catalog API & Search API Ayrımı (CQRS & Read/Write Mimarisi)

Sistemde okuma ve yazma operasyonları net bir sorumluluk ayrımı (Separation of Concerns) ile iki farklı servise paylaştırılmıştır:

### 1. Catalog API (.NET 8 & EF Core) — *Yazma (Write) ve Domain Okuma Modeli*
- **Ana Doğruluk Kaynağı (Single Source of Truth):** Ürün, kategori, dinamik kategori özellikleri (Attributes) ve ilişkisel veriler **PostgreSQL** üzerinde `JSONB` destekli olarak tutulur.
- **Transactional Bütünlük:** Ürün oluşturma, güncelleme, silme, stok rezervasyonu ve kategori hiyerarşisi işlemleri bu servis üzerinden yürütülür.
- **Event Yayınlama (Publishing):** Bir ürün üzerinde CRUD işlemi yapıldığında veya stok durumu değiştiğinde, MassTransit aracılığıyla RabbitMQ'ya domain event'leri fırlatır (`ProductCreatedForSearch`, `ProductUpdatedForSearch`, `ProductDeletedForSearch`).

### 2. Search API (Go + Gin + Elasticsearch) — *Yüksek Performanslı Arama & Read Modeli*
- **Polyglot Yaklaşım:** Düşük bellek tüketimi, yüksek eşzamanlılık (concurrency) ve mikro-saniye seviyesinde yanıt süreleri için **Go (Golang)** ve **Gin Web Framework** ile geliştirilmiştir.
- **Arama ve İndeksleme:** Çoklu alan araması (`Multi-match`), typo toleransı (Fuzziness), `Edge-NGram` tabanlı otomatik tamamlama (`Autocomplete/Suggestions`), fiyat/marka kırılımları (`Facets/Aggregations`) ve öne çıkan ürünler (`Featured`) sorgularını **Elasticsearch** üzerinden yönetir.
- **Asenkron Senkronizasyon (Consumer):** RabbitMQ üzerinden gelen ürün olaylarını dinleyerek Elasticsearch indeksini ve Redis önbelleğini eşzamanlı olarak günceller (Eventually Consistent).
- **Admin Reindex & İzleme:** Admin panelden veya doğrudan API'den tek tıkla PostgreSQL'deki tüm ürünleri Elasticsearch'e aktarma (`POST /api/search/reindex`), doküman önizleme ve indeks sağlık durumunu (`/api/search/status`) sunar.

---

## ⚡ Redis Dağıtık Önbellekleme (Distributed Cache) & Invalidation Stratejisi

Tüm read akışlarında veritabanı ve arama motoru yükünü minimuma indirmek amacıyla **Cache-Aside (Lazy Loading)** ve **Event-Driven Cache Invalidation** mimarisi uygulanmıştır.

### 1. Cache İzolasyonu ve Key Yapısı
Farklı servislerin aynı Redis kümesini anahtar çakışması olmadan güvenle kullanabilmesi için prefix bazlı izolasyon uygulanır:
- **Search API:** `search-cache:query_{hash}`, `search-cache:sugg_{hash}`, `search-cache:featured`
- **Catalog API:** `catalog-cache:category_{id}`, `catalog-cache:tree`
- **Basket API:** `basket_{userId}`

### 2. Okuma Akışı (Cache-Aside Pattern)
1. İstemci bir arama veya öne çıkan ürün isteği attığında Search API ilk olarak **Redis Cache** kontrolü yapar.
2. **Cache HIT:** Veri Redis'te mevcutsa, doğrudan çözümlenerek mikro-saniyeler içinde istemciye dönülür (Elasticsearch'e hiçbir sorgu atılmaz).
3. **Cache MISS:** Veri Redis'te yoksa, Elasticsearch'ten sorgulanır, sonuç istemciye iletilirken arka planda belirlenen TTL (örn. 5 dakika) ile Redis'e kaydedilir.

### 3. Akıllı Önbellek Temizleme (Event-Driven Invalidation)
Ürün verisi güncellendiğinde bayat veri (stale data) sunulmaması için şu akış işletilir:
```
[Admin / Catalog API] ──(Ürün Güncellendi)──► [PostgreSQL]
                                │
                                └──► [RabbitMQ Event: ProductUpdatedForSearch]
                                             │
                                             ▼
                             [Search API Consumer (Go)]
                                  │                │
                                  ▼                ▼
                         [Elasticsearch]    [Redis Cache]
                         (Update Document)  (SCAN search-cache:* & DEL)
```
- Go Search API, RabbitMQ'dan `ProductUpdatedForSearch` veya `ProductDeletedForSearch` event'ini aldığı anda:
  1. Elasticsearch üzerindeki ilgili dokümanı anında günceller / siler.
  2. Redis üzerindeki ilgili sorgu ve öne çıkanlar önbelleklerini non-blocking `SCAN` + `DEL` mekanizmasıyla anında geçersiz kılar (Invalidate).

---

## 🛠️ Servisler ve Port Listesi

### Uygulama Servisleri

| Servis | Dil / Framework | Port | Swagger / OpenAPI Adresi | Veritabanı / Altyapı |
| :--- | :--- | :--- | :--- | :--- |
| **Yarp Gateway** | .NET 8 / YARP | `5000` | - | Reverse Proxy |
| **WebUI** | ASP.NET Core MVC | `7050` | - | MVC / Razor View |
| **Catalog API** | .NET 8 / WebAPI | `5011` | `http://localhost:5011/swagger` | PostgreSQL (`5434`) |
| **Search API** | Go 1.23 / Gin | `5082` | `http://localhost:5082/swagger/index.html` | Elasticsearch (`9201`), Redis (`6380`) |
| **Notification API** | Go 1.23 / Gin | `5025` | `http://localhost:5025/swagger/index.html` | RabbitMQ, MinIO, SMTP |
| **PhotoStock API** | .NET 8 / WebAPI | `5012` | `http://localhost:5012/swagger` | Local Storage |
| **Basket API** | .NET 8 / WebAPI | `5013` | `http://localhost:5013/swagger` | Redis Sentinel Cluster (`6380`) |
| **Discount API** | .NET 8 / Dapper | `5014` | `http://localhost:5014/swagger` | PostgreSQL (`5432`) |
| **Order API** | .NET 8 / EF Core | `5015` | `http://localhost:5015/swagger` | SQL Server (`1433`), RabbitMQ |
| **Payment API** | .NET 8 / MassTransit| `5016` | `http://localhost:5016/swagger` | Iyzico Entegrasyonu |
| **Invoice API** | .NET 8 / MassTransit| `5017` | `http://localhost:5017/swagger` | RabbitMQ |
| **Campaign API** | .NET 8 / Dapper | `5018` | `http://localhost:5018/swagger` | SQL Server (`1434`) |
| **Review API** | .NET 8 / EF Core | `5221` | `http://localhost:5221/swagger` | PostgreSQL (`5435`) |

### Altyapı ve Destek Servisleri

| Servis | Port(lar) | Açıklama |
| :--- | :--- | :--- |
| **Keycloak (IAM)** | `8080` | OIDC / OAuth2 Kimlik Doğrulama ve Yetkilendirme |
| **RabbitMQ** | `5672`, `15672` | Servisler arası asenkron mesajlaşma broker'ı |
| **Elasticsearch** | `9201` | Ürün arama indeksi ve analitik veriler |
| **Kibana** | `5601` | Elasticsearch görselleştirme ve indeks yönetimi |
| **Redis Master** | `6380` | Dağıtık önbellek ve sepet veritabanı |
| **Redis Sentinels**| `26379, 26380, 26381` | Yüksek erişilebilirlik (HA) ve otomatik failover |
| **Prometheus** | `9090` | Mikroservislerden metrik toplama (`/metrics`) |
| **Grafana** | `30300` | Sistem performans ve metrik dashboard'ları |
| **ArgoCD** | `30580` | GitOps tabanlı Continuous Deployment |

---

## 🔒 Güvenlik Notları

- **Hassas bilgiler** `.env` dosyasında saklanır ve Git'e commit edilmez.
- **appsettings.json** dosyaları `.gitignore`'da listelenmiştir.
- **Example dosyaları** yeni geliştiriciler için şablon olarak kullanılır.
- **Admin kullanıcısı** Keycloak realm import sırasında otomatik oluşturulur.
- **GitHub Secrets** tarafında prod/stage için gerekli connection string ve anahtarlar saklanır.
