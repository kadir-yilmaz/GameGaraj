# 🎓 Metrics (Metrikler) - Bölüm 1: Prometheus ve Grafana Temelleri

Metrikler, sistemin sağlığını ve iş performansını **sayısal verilerle** anlık olarak izlememizi sağlar. 

Traces ve Loglar "Ne oldu?" sorusuna yanıt ararken, Metrikler **"Şu anda ne kadar/nasıl oluyor?"** (istatistik, yoğunluk, oranlar) sorusuna yanıt verir.

---

## 1. Prometheus Pull (Çekme) Modeli Nasıl Çalışır?

Gözlemlenebilirlik dünyasında metrik toplamanın iki yolu vardır: *Push (Göndermek)* ve *Pull (Çekmek)*. Prometheus **Pull** modelini kullanır.

```
┌──────────────┐                  ┌──────────────┐
│  Prometheus  │ ──(Pull: /metrics)──> │  Catalog.API │
│  (Backend)   │                  └──────────────┘
│              │                  ┌──────────────┐
│              │ ──(Pull: /metrics)──> │   Order.API  │
└──────────────┘                  └──────────────┘
```

1.  Her mikroservisimiz kendi içinde ufak bir HTTP sunucusu barındırır. `Program.cs` içindeki `app.UseObservability()` çağrısı sayesinde dışarıya **`/metrics`** endpoint'ini açar.
2.  Mikroservis çalıştıkça kendi içinde CPU, RAM, Thread sayısı ve custom iş metriklerini belleğinde (RAM) toplar. `/metrics` adresine girildiğinde bu verileri düz metin formatında döner.
3.  **Prometheus Sunucusu** ise belirli aralıklarla (örneğin 15 saniyede bir) k3s cluster içindeki tüm servislerin `/metrics` adresini ziyaret eder, oradaki verileri okur (Scraping) ve kendi zaman serisi veritabanına kaydeder.

---

## 2. Temel Metrik Tipleri

OpenTelemetry ve Prometheus standartlarında 3 temel metrik tipiyle çalışırız:

### A. Counter (Sayaç)
Sadece **artan** veya **sıfırlanan** sayısal değerlerdir. Asla azalmazlar.
*   **Örnekler:** Toplam oluşturulan sipariş sayısı (`orders.created.total`), toplam alınan hata sayısı, sepetten çıkarılan ürün sayısı.
*   **Grafana Kullanımı:** Genelde anlık hızı görmek için PromQL'de `rate()` fonksiyonu ile birlikte kullanılır. (Örn: *Son 5 dakikadaki saniyelik sipariş hızı*).

### B. Gauge (Ölçer)
Anlık olarak **artıp azalabilen** değerlerdir.
*   **Örnekler:** Aktif bellek (RAM) kullanımı, CPU yüzdesi, Thread Pool'da bekleyen iş kuyruğu uzunluğu, veritabanı aktif bağlantı sayısı.
*   **Grafana Kullanımı:** Doğrudan anlık değeri göstermek veya zaman içindeki iniş çıkışı çizdirmek için kullanılır.

### C. Histogram (Dağılım)
Ölçülen değerlerin (genellikle sürelerin veya boyutların) dağılımını gösterir. Belirli aralıklara (buckets) göre sayım yapar.
*   **Örnekler:** Sayfa yüklenme süresi milisaniyesi, ödeme işlemi süresi milisaniyesi.
*   **Grafana Kullanımı:** Genelde yüzde 95'lik (P95) veya yüzde 99'luk (P99) gecikme (latency) sürelerini hesaplamak için `histogram_quantile()` fonksiyonu ile birlikte kullanılır.

---

## 3. Temel PromQL Sorguları

Grafana panellerinde grafikleri çizdirmek için kullandığımız PromQL (Prometheus Query Language) dilinden en sık kullanılan 2 örnek:

1.  **Saniye Başındaki Ortalama İstek Hızı (RPS):**
    `sum(rate(http_server_duration_count[5m])) by (service_name)`
    *(Son 5 dakikadaki verilere bakarak saniyede kaç istek geldiğini servislere göre grupla).*

2.  **Yüzde 95'lik HTTP Yanıt Süresi (P95 Latency):**
    `histogram_quantile(0.95, sum(rate(http_server_duration_bucket[5m])) by (le, service_name))`
    *(Tüm isteklerin %95'inin ne kadar sürede tamamlandığını milisaniye cinsinden servislere göre göster).*
    
Bir sonraki bölümde, **C# ile bu custom metrikleri nasıl yazdığımızı ve OpenTelemetry ile nasıl Prometheus'a sunduğumuzu** öğreneceğiz. [Bölüm 2: .NET Core Metrik Entegrasyonu](file:///d:/Kadir/Projeler/GameGaraj/notes/observability/metrics/02_dotnet_metrics_implementation.md) dosyasına geçelim.
