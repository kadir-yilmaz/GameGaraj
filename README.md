# GameGaraj - Microservices E-Commerce Platform

GameGaraj, .NET 8 mimarisi üzerine inşa edilmiş, modern mikroservis yaklaşımlarını kullanan kapsamlı bir e-ticaret platformudur.

---

## Proje Özeti ve Öne Çıkan Mimari Özellikler

Bu proje, sadece bir e-ticaret uygulaması değil; karmaşık dağıtık sistem sorunlarına (consistency, scalability, SEO) getirilmiş modern çözümlerin bir bütünüdür. Bir "Senior Developer" gözüyle incelendiğinde öne çıkan temel yetkinlikler şunlardır:

### 1. Advanced SEO & Dynamic Routing
Standart GUID tabanlı URL yapıları yerine, tüm sistem **Slug-based** bir mimari üzerine kurulmuştur. Ürün ve kategori ağacı için `/product/c/{category}` ve `/product/p/{slug}` formatında, arama motoru dostu ve kullanıcı deneyimi yüksek bir yönlendirme katmanı sunar.

### 2. PostgreSQL JSON Column Approach (Flexible Schema)
Katalog servisinde, her ürünün değişen teknik özelliklerini (Specs) yönetmek için geleneksel EAV (Entity-Attribute-Value) modeli yerine **PostgreSQL JSONB** sütun yaklaşımı tercih edilmiştir. Bu sayede ilişkisel bir veritabanı (RDBMS) içinde **şemasız (schema-less) veri esnekliği** elde edilmiş, karmaşık filtreleme operasyonları JSONB indeksleri üzerinden yüksek performansla sunulmuştur.

### 3. Event-Driven Architecture & SAGA Pattern
Mikroservisler arası iletişim, **MassTransit** ve **RabbitMQ** üzerinden asenkron olarak yönetilir. Sipariş süreçleri (Ordering), ödeme (Payment) ve fatura (Invoice) işlemleri arasındaki veri tutarlılığı, dağıtık sistemlerdeki en kritik konulardan biri olan **SAGA Pattern** yaklaşımları ile koordine edilir.

### 4. Identity Management (Keycloak)
Güvenlik katmanı, endüstri standardı olan **Keycloak** (OIDC/OAuth2) ile merkezi olarak yönetilir. JWT tabanlı yetkilendirme mimarisi, gateway ve mikroservisler arasında tam güvenli bir ekosistem sağlar.

### 5. High Performance Search (Elasticsearch)
Ürün aramaları ve indeksleme işlemleri için **Elasticsearch** kullanılarak, milisaniyeler seviyesinde arama ve auto-complete yetenekleri kazandırılmıştır.

### 6. Comprehensive Campaign & Coupon Management
Projede iki farklı indirim mekanizması bir arada sunulmuştur:
- **Campaign Engine:** İş mantığının esnekliğini korumak adına **Strategy Pattern** ile kurgulanmıştır. "3 Al 2 Öde", "X TL Üzeri Sabit İndirim" gibi karmaşık kampanya türleri, mevcut koda dokunmadan (Open/Closed Principle) sisteme dahil edilebilir.
- **Coupon Service:** Kullanıcı bazlı indirim kuponlarını yönetir. Yüksek trafik altında hızlı cevap verebilmesi adına **Dapper (Micro ORM)** ile optimize edilmiştir.

---

## Client

### WebUI
- **Port:** `7050`
- **Teknoloji:** ASP.NET Core MVC
- **Önemli Bilgiler:** 
  - **SEO Mimari:** Dinamik slug-based yönlendirme sistemi (Ürünler için `/product/p/{slug}`, Kategoriler için `/product/c/{category}`).

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
- **Teknoloji:** Entity Framework Core (EF Core)
- **Önemli Bilgiler:** 
  - **SEO Destekli:** PostgreSQL JSONB ve Slug mekanizması ile esnek ve hızlı kategori/ürün yapısı.
  - **Elasticsearch:** Arama sonuçlarını optimize etmek için Elasticsearch ile senkronize çalışır.

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
