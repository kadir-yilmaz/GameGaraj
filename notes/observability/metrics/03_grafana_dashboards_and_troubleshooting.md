# 🎓 Metrics (Metrikler) - Bölüm 3: Grafana Panelleri ve Sorun Giderme Kılavuzu

Canlı ortamda çalışırken Grafana panellerinde **"No Data"** uyarısı görmek en yaygın durumlardan biridir. Bu bölümde, "No Data" durumunda bir dedektif gibi sorunun kaynağını adım adım nasıl bulacağımızı öğreneceğiz.

---

## 1. Sorun Giderme Akış Şeması (Metrik Neden Gelmez?)

```
 ┌────────────────────────────────────────────────────────┐
 │          Grafana'da Panel "No Data" Diyor              │
 └───────────────────────────┬────────────────────────────┘
                             │
            1. Sitede hiç hareket var mı?
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │  Sitede sepet/sipariş işlemleri yapıldı mı? (Veri oluştu)
 └───────────────────────────┬────────────────────────────┘
                             │ Evet
            2. Servis metrik üretiyor mu?
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │  /metrics endpoint'ine istek atıldığında metrik var mı?
 └───────────────────────────┬────────────────────────────┘
                             │ Evet
            3. Prometheus servisi kazıyor mu (Scrape)?
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │  Prometheus Status -> Targets sayfasında pod "UP" mı?
 └───────────────────────────┬────────────────────────────┘
                             │ Evet
            4. Grafana PromQL sorgusu doğru mu?
                             ▼
 ┌────────────────────────────────────────────────────────┐
 │  Metrik ismi veya filtreler eşleşiyor mu?              │
 └────────────────────────────────────────────────────────┘
```

---

## 2. Sorun Giderme Adımları (Detaylı)

### Adım 1: Sitede Hareket Tetikleme (Data Initialization)
*   **Açıklama:** Prometheus'taki custom iş metrikleri (örn: `orders_created_total`) uygulama ilk ayağa kalktığında bellekte henüz sıfırdır. Sitede hiç sipariş verilmediyse, bu metrik Prometheus'a hiç yansımaz.
*   **Eylem:** Tarayıcıdan siteye gidin, sepetinize ürün ekleyin ve bir sipariş oluşturun. Sistemi tetikleyin.

### Adım 2: `/metrics` Endpoint'ini Elle Kontrol Etme
*   **Açıklama:** Uygulamanın metrikleri düzgün üretip üretmediğini tarayıcıdan veya pod içinden kontrol edebiliriz.
*   **Eylem:** K3s cluster içindeki bir pod'un `/metrics` endpoint'ine curl ile istek atın:
    ```bash
    kubectl exec -it <order-api-pod-adi> -n gamegaraj -- curl http://localhost:8080/metrics
    ```
*   **Kontrol:** Çıktıda bizim custom metriğimizi aratın:
    ```text
    # HELP gamegaraj_orders_created_total Total orders created
    # TYPE gamegaraj_orders_created_total counter
    gamegaraj_orders_created_total 12
    ```
    *   Eğer bu metin çıktıda **yoksa**, C# kodundaki OpenTelemetry yapılandırması (Meter ismi eşleşmesi) veya metrik tetikleme kodu çalışmıyordur. (Bölüm 2'deki eşleşme konusuna bakın).

### Adım 3: Prometheus Targets Durumunu Kontrol Etme
*   **Açıklama:** Servis metriği üretiyor olabilir ama Prometheus bu pod'u kazımıyor (scrape etmiyor) olabilir.
*   **Eylem:** Prometheus arayüzünü açın (`http://192.168.1.56:9090`).
*   Üst menüden **Status ➔ Targets** sayfasına gidin.
*   `gamegaraj-services` veya ilgili scraping grubunu bulun.
*   Hedef pod'unuzun yanında yeşil **"UP"** ibaresini görmelisiniz. 
    *   Eğer **"DOWN"** veya **"UNKNOWN"** yazıyorsa; port tanımı yanlıştır veya pod çökmüştür.

### Adım 4: Grafana PromQL Sorgu Kontrolü
*   **Açıklama:** Prometheus veriyi başarıyla alıyor olabilir ama Grafana panelinde yazdığımız sorgu hatalı olabilir.
*   **Eylem:** Grafana'da "No Data" veren panelin sağ üstündeki üç noktaya tıklayıp **Edit** deyin.
*   Sorgu alanındaki PromQL ifadesine bakın. 
*   **Kritik Kural:** C# kodundaki metrik isimleri Prometheus'a aktarılırken noktalar (`.`) alt çizgiye (`_`) dönüşür. 
    *   C# Kodu: `orders.created.total`
    *   PromQL Sorgusu: `gamegaraj_orders_created_total`
*   Filtrelerde (örn: `service_name="GameGaraj.Order"`) büyük/küçük harf uyuşmazlığı olup olmadığını kontrol edin.
