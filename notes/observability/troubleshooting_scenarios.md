# Observability Platform — Sorun Giderme ve Kullanım Senaryoları

Bu kılavuzda, kurduğumuz gözlemlenebilirlik (observability) platformunu kullanarak günlük yazılım geliştirme ve operasyonel süreçlerde karşılaşabileceğiniz sorunları adım adım nasıl çözeceğinizi gösteren örnek senaryolar yer almaktadır.

---

## 🛠️ Senaryo 1: Yavaş Çalışan Bir API İsteğinin Analizi (Jaeger ile SQL İzleme)

**Problem:** Müşterilerden "Katalog sayfasında ürünler çok geç yükleniyor" şeklinde şikayetler geliyor. Hangi servisin veya hangi SQL sorgusunun yavaşlığa sebep olduğunu bulmamız gerekiyor.

### Çözüm Adımları:
1.  **Jaeger UI Arayüzüne Giriş Yapın:**
    *   Tarayıcınızdan `http://192.168.1.56:16686` adresini açın.
2.  **Arama Parametrelerini Girin:**
    *   **Service:** `GameGaraj.Catalog` seçin.
    *   **Operation:** `POST /api/products` (veya yavaş olduğundan şüphelenilen endpoint).
    *   **Max Duration:** `1s` yazarak 1 saniyeden uzun süren istekleri filtreleyin.
    *   **Find Traces** butonuna tıklayın.
3.  **Trace Detayını İnceleyin:**
    *   Gelen sonuçlarda en uzun süren çizgiye (görsel olarak en uzun bar) tıklayın.
    *   Uçtan uca çağrı ağacı açılacaktır (Gateway -> Catalog -> DB).
4.  **SQL Sorgusunu Tespit Edin:**
    *   Trace ağacında alt kırılımlara inip `GameGarajCatalogDb` yazan veritabanı span'ına tıklayın.
    *   **Tags** veya **Attributes** sekmesine bakın:
        *   `db.statement` alanında çalıştırılan ham SQL sorgusunu göreceksiniz.
        *   `db.query.duration` (veya span duration) kısmında bu sorgunun kaç milisaniye sürdüğünü görerek yavaşlığın veritabanından kaynaklanıp kaynaklanmadığını kesinleştirebilirsiniz.

---

## 🔍 Senaryo 2: Hata Alan Bir Akışın Analizi (Kibana Log ↔️ Jaeger Trace Korelasyonu)

**Problem:** Ödeme servisinde başarısız olan bir işlem var. Hatayı Kibana'da gördük ama bu hataya yol açan önceki servis çağrılarında (Gateway, Basket, Order) ne yaşandığını anlamak istiyoruz.

### Çözüm Adımları:
1.  **Kibana'da Log Arama:**
    *   Kibana arayüzünü açın (`http://192.168.1.56:5601`).
    *   Arama kısmına `level: "Error" AND Service: "Payment.API"` yazarak ödeme servisindeki hataları listeleyin.
2.  **TraceId Değerini Alın:**
    *   Bulduğunuz hata logunun detayını açın.
    *   Log alanlarındaki **`TraceId`** değerini (örneğin: `45d6a782b9c1d0e4f3a5b6c7d8e9f0a1`) kopyalayın.
3.  **Jaeger'da Arama Yapın:**
    *   Jaeger UI'a (`http://192.168.1.56:16686`) gidin.
    *   Sağ üst köşedeki **"Lookup by Trace ID"** kutucuğuna kopyaladığınız TraceId değerini yapıştırıp Enter'a basın.
4.  **Uçtan Uca Akışı Görün:**
    *   Karşınıza sadece o istek esnasında gerçekleşen tüm mikroservis çağrıları gelecektir:
        *   *Kullanıcı Gateway'e istek attı -> Basket API okundu -> Order API sipariş oluşturdu -> Payment API hata verdi.*
    *   Burada hataya yol açan asıl sebebin ödeme servisinin kendisi mi, yoksa Order API'den Payment API'ye geç gelen eksik parametreler mi olduğunu çağrı parametreleri üzerinden görebilirsiniz.

---

## ⚡ Senaryo 3: Canlı Ortamda Geçici Olarak Detaylı (Debug/Trace) Loglama Açma

**Problem:** Canlı ortamda sadece `Information` ve üzeri loglar yazılıyor. Ancak sepet güncellenirken nadir görülen bir hata oluşuyor ve bunu yakalamak için geçici olarak `Debug` seviyesinde log yazmamız gerekiyor. Deploy etmeden bunu nasıl yaparız?

### Çözüm Adımları:
1.  **API İsteği Gönderin:**
    *   İlgili mikroservise (örneğin `Basket.API`'ye) `/api/observability/log-level` endpoint'i üzerinden bir `PUT` isteği gönderin:
    ```http
    PUT http://<k3s-node-ip>:30011/api/observability/log-level
    Content-Type: application/json

    {
      "level": "Debug",
      "durationMinutes": 15,
      "reason": "Debugging rare basket update issue",
      "changedBy": "Kadir YILMAZ"
    }
    ```
2.  **Sistemi İzleyin:**
    *   `durationMinutes: 15` parametresi sayesinde, servis 15 dakika boyunca tüm detaylı debug loglarını Elasticsearch'e yazacaktır.
    *   Kibana'dan hataları debug loglarıyla birlikte analiz edin.
3.  **Otomatik Geri Dönüş:**
    *   15 dakika dolduğunda sistem, CPU ve disk sağlığını korumak adına log seviyesini otomatik olarak tekrar `Information` seviyesine geri çekecektir. Sizin ekstra bir şey yapmanıza gerek kalmaz.

---

## 🚨 Senaryo 4: Grafana Alarmları ile Sorun Erken Teşhisi

**Problem:** Sistem kaynakları dolmadan veya müşteriler fark etmeden önce olası bir tıkanıklığı anlamamız gerekiyor.

### Çözüm Adımları:
1.  **Grafana Dashboard'larını İnceleyin:**
    *   Grafana arayüzünden (`http://<k3s-node-ip>:30300`) **"GameGaraj - Service Health"** dashboard'unu açın.
2.  **Metrikleri İzleyin:**
    *   **RPS & Latency:** Anlık istek sayısı artarken Latency P95 barının yükseldiğini görürseniz darboğaz başlıyor demektir.
    *   **ThreadPool Queue Length:** Eğer bu metrik 0'dan yukarı doğru tırmanıyorsa, servis gelen istekleri işlemekte yetersiz kalıyor demektir (Thread starvation).
3.  **Alarm Tetiklenmesi:**
    *   Eğer hata oranı 5 dakikada %3'ü geçerse veya ödeme başarısızlıkları 10 dakikada 10 adedi aşarsa, oluşturduğumuz `alerts.yaml` kuralları tetiklenir ve tanımlı olan kanala (Slack, E-posta vb.) otomatik bildirim düşer.
