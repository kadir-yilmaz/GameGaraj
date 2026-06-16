# GameGaraj - Microservices E-Commerce Platform

GameGaraj, .NET 8 mimarisi üzerine inşa edilmiş, modern mikroservis yaklaşımlarını kullanan kapsamlı bir e-ticaret platformudur.

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

## Proje Özeti ve Öne Çıkan Mimari Özellikler

Bu proje, sadece bir e-ticaret uygulaması değil; karmaşık dağıtık sistem sorunlarına (consistency, scalability, SEO) getirilmiş modern çözümlerin bir bütünüdür. Bir "Senior Developer" gözüyle incelendiğinde öne çıkan temel yetkinlikler şunlardır:

### 1. Advanced SEO & Dynamic Routing
Standart GUID tabanlı URL yapıları yerine, tüm sistem **Slug-based** bir mimari üzerine kurulmuştur. Ürün ve kategori ağacı için `/product/c/{category}` ve `/product/p/{slug}` formatında, arama motoru dostu ve kullanıcı deneyimi yüksek bir yönlendirme katmanı sunar.

### 2. Catalog CQRS: PostgreSQL JSONB Write Model + Elasticsearch Read Model
Katalog servisinde okuma ve yazma sorumlulukları MediatR kullanmadan, sade servis katmanları ile ayrılmıştır. Ürün oluşturma/güncelleme/silme işlemleri **PostgreSQL** üzerinde tutulur; ürünün kategoriye göre değişebilen teknik özellikleri ise `Specs` alanında **JSONB** olarak saklanır.

Bu tercih write tarafında bilinçlidir:
- **PostgreSQL ana doğruluk kaynağıdır:** Ürün fiyatı, stok, kategori, aktiflik ve teknik özellikler transactional olarak tek yerde tutulur.
- **JSONB esneklik sağlar:** Mouse, ekran kartı, RAM veya laptop gibi farklı kategoriler aynı ürün tablosunda farklı attribute setleriyle yönetilebilir. EAV modeliyle oluşacak çok sayıda join ve bakım maliyeti azaltılır.
- **Admin operasyonları daha güvenlidir:** Kategori attribute değişikliklerinde ürün spec temizliği/güncellemesi write modelde kontrollü yapılır.

Read tarafında ise listeleme, arama, öneri ve facet senaryoları için **Elasticsearch** kullanılır. Böylece typo toleransı, autocomplete, marka/kategori önerileri ve hızlı filtreleme gibi kullanıcıya dönük okuma ihtiyaçları PostgreSQL sorgularını karmaşıklaştırmadan, ayrı bir read model üzerinden karşılanır.

### 3. Event-Driven Architecture & SAGA Pattern
Mikroservisler arası iletişim, **MassTransit** ve **RabbitMQ** üzerinden asenkron olarak yönetilir. Sipariş süreçleri (Ordering), ödeme (Payment) ve fatura (Invoice) işlemleri arasındaki veri tutarlılığı, dağıtık sistemlerdeki en kritik konulardan biri olan **SAGA Pattern** yaklaşımları ile koordine edilir.

### 4. Identity Management (Keycloak)
Güvenlik katmanı, endüstri standardı olan **Keycloak** (OIDC/OAuth2) ile merkezi olarak yönetilir. JWT tabanlı yetkilendirme mimarisi, gateway ve mikroservisler arasında tam güvenli bir ekosistem sağlar.

### 5. High Performance Search & Smart Suggestions (Elasticsearch)
Ürün aramaları, autocomplete, marka/kategori önerileri ve arama facetleri için **Elasticsearch** kullanılır. Catalog API, ürün write işlemlerinden sonra Elasticsearch indeksini günceller; ihtiyaç halinde admin panelden indeks yeniden oluşturulabilir. Bu yapı arama deneyimini hızlandırırken PostgreSQL'i transactional write model olarak temiz tutar.

### 6. Comprehensive Campaign & Coupon Management
Projede iki farklı indirim mekanizması bir arada sunulmuştur:
- **Campaign Engine:** İş mantığının esnekliğini korumak adına **Strategy Pattern** ile kurgulanmıştır. "3 Al 2 Öde", "X TL Üzeri Sabit İndirim" gibi karmaşık kampanya türleri, mevcut koda dokunmadan (Open/Closed Principle) sisteme dahil edilebilir.
- **Coupon Service:** Kullanıcı bazlı indirim kuponlarını yönetir. Yüksek trafik altında hızlı cevap verebilmesi adına **Dapper (Micro ORM)** ile optimize edilmiştir.

### 7. CI/CD Pipeline & Home Server Deployment (K3s)
Deployment süreci artık home server üzerinde çalışan **self-hosted GitHub Actions runner** ile yönetilir. Ana hedef ortam hafif Kubernetes dağıtımı olan **K3s**'tir.

- **Self-hosted runner:** Workflow, home server üzerindeki Linux/X64 runner'da çalışır ve `/home/kadirserver/.kube/config` üzerinden K3s cluster'a bağlanır.
- **Config sync:** `k8s/config/**` altındaki dosyalar Kubernetes ConfigMap olarak senkronize edilir.
- **Secret yönetimi:** Hassas bilgiler GitHub Secrets üzerinden alınır. Tek tek secret değerleri veya `K3S_SECRETS_JSON` ile toplu secret aktarımı desteklenir.
- **K8s rollout:** Manifestler `k8s/**` altından uygulanır, deployment'lar restart edilir ve rollout status kontrolü yapılır.
- **Manuel kurulum opsiyonu:** Workflow dispatch ile istenirse home server üzerinde K3s eksikse otomatik kurulabilir.

Aktif workflow'lar: `.github/workflows/k3s-config-sync.yml`, `.github/workflows/k3s-app-deploy.yml`, `.github/workflows/k3s-dashboard-install.yml`

`K3s App Deploy` workflow'u `service` input'u ile tum image'lari (`all`) veya tek bir servisi (`webui`, `catalog-api`, vb.) build edebilir.

---

## Client

### WebUI
- **Port:** `7050`
- **Teknoloji:** ASP.NET Core MVC
- **Önemli Bilgiler:** 
  - **SEO Mimari:** Dinamik slug-based yönlendirme sistemi (Ürünler için `/product/p/{slug}`, Kategoriler için `/product/c/{category}`).
  - **Admin Kullanıcısı:** Keycloak realm import sırasında `.env` dosyasındaki `ADMIN_EMAIL` ve `ADMIN_PASSWORD` ile oluşturulur.

---

## Gateway & Identity Management

### Yarp Gateway
- **Port:** `5000`
- **Teknoloji:** Microsoft YARP (Yet Another Reverse Proxy)
- **Önemli Bilgiler:** 
  - Tüm mikroservisler için merkezi giriş noktasıdır. 
  - İstekleri rotalarına göre ilgili API'ye yönlendirir.

### Keycloak (Identity Management)
- **Port:** `8080` (Admin: `8080`)
- **DB:** PostgreSQL (Port: `5433`)
- **Teknoloji:** OAuth2, OpenID Connect
- **Önemli Bilgiler:** 
  - Merkezi kimlik doğrulama ve yetkilendirme sağlar. 
  - Microservices ekosisteminde JWT tabanlı güvenlik sunar.

---

## Microservices (Port Sırasına Göre)

### Catalog API
- **Port:** `5011`
- **DB:** PostgreSQL (Port: `5434`)
- **Teknoloji:** Entity Framework Core (EF Core), Elasticsearch
- **Önemli Bilgiler:** 
  - **Write Model:** Ürün, kategori ve attribute verileri PostgreSQL'de tutulur. Kategoriye göre değişen ürün özellikleri `Specs` JSONB alanında saklanır.
  - **Read Model:** Ürün listeleme, arama, öneri ve facet ihtiyaçları Elasticsearch read modeli üzerinden karşılanır.
  - **CQRS:** MediatR kullanmadan sade query/command servisleri ile okuma ve yazma operasyonları ayrılmıştır.
  - **SEO Destekli:** Slug mekanizması ile ürün ve kategori URL'leri kullanıcı ve arama motoru dostudur.
  - **Admin Index Yönetimi:** Admin panelden Elasticsearch bağlantı durumu görülebilir ve ürün indeksi yeniden oluşturulabilir.

### PhotoStock API
- **Port:** `5012`
- **DB:** Local Storage
- **Teknoloji:** ASP.NET Core API

### Basket API
- **Port:** `5013`
- **DB:** Redis (Port: `6380`)
- **Teknoloji:** StackExchange.Redis
- **Önemli Bilgiler:** 
  - Hem user hem login olmuş kullanıcıların sepetlerini tutar.
  - Login sonrası sepet senkronizasyonu yapar.

### Discount API
- **Port:** `5014`
- **DB:** PostgreSQL (Port: `5432`)
- **Teknoloji:** Dapper (Micro ORM)
- **Önemli Bilgiler:** 
  - Kupon ve indirim tanımlama servisidir. 
  - Hız için Dapper kullanılarak doğrudan SQL sorguları ile çalışır.

### Order API
- **Port:** `5015`
- **DB:** SQL Server (Port: `1433`)
- **Teknoloji:** EF Core, MassTransit, RabbitMQ
- **Önemli Bilgiler:** 
  - Onion Architecture mimarisindedir.
  - **Event-Driven:** Sipariş tamamlandığında RabbitMQ üzerinden Payment ve Invoice servislerini tetikler.

### Payment API
- **Port:** `5016`
- **DB:** Yok (Iyzico panelinden yönetiliyor)
- **Teknoloji:** MassTransit, RabbitMQ
- **Önemli Bilgiler:** 
  - Ödeme işlemlerini simüle eder. (Iyzico)
  - Sonucu RabbitMQ üzerinden Order servisine bildirir.

### Invoice API
- **Port:** `5017`
- **DB:** Yok
- **Teknoloji:** MassTransit, Email Service
- **Önemli Bilgiler:** 
  - Sipariş sonrası fatura oluşturma ve e-posta gönderimini yönetir.

### Campaign API
- **Port:** `5018`
- **DB:** SQL Server (Port: `1434`)
- **Teknoloji:** Dapper, Strategy Pattern
- **Önemli Bilgiler:** 
  - **Strategy Pattern:** "3 Al 2 Öde", "X TL Üzeri İndirim" gibi karmaşık kampanya kurallarını esnek bir yapıda yönetir.

---

## Yardımcı Servisler

| Servis | Port | Görevi |
| :--- | :--- | :--- |
| **RabbitMQ** | `5672`, `15672` | Servisler arası asenkron iletişim (Message Broker). |
| **Redis** | `6380` | Dağıtık önbellekleme ve sepet yönetimi. |
| **Elasticsearch** | `9201` | Hızlı ürün arama ve katalog indeksleme. |
| **Kibana** | `5601` | Elastic verilerini görselleştirme ve log izleme. |

---

## 🔒 Güvenlik Notları

- **Hassas bilgiler** `.env` dosyasında saklanır ve Git'e commit edilmez
- **appsettings.json** dosyaları `.gitignore`'da listelenmiştir
- **Example dosyaları** yeni geliştiriciler için şablon olarak kullanılır
- **Admin kullanıcısı** Keycloak realm import sırasında oluşturulur

---
