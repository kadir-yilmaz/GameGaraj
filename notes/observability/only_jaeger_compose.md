# Dokploy üzerinde Sadece Jaeger Kurulumu (Docker Compose)

Eğer Dokploy üzerinde **Elasticsearch** ve **Kibana** halihazırda kuruluysa ve **Prometheus + Grafana** ikilisini k3s cluster içinde (K8s pod'ları olarak) tutmaya devam edecekseniz, Dokploy'a **sadece Jaeger** kurmanız yeterlidir.

## 1. Sadece Jaeger için Docker Compose (`docker-compose.yml`)

Dokploy üzerinde sadece Jaeger için yeni bir Compose projesi oluşturup aşağıdaki yapılandırmayı kullanabilirsiniz.

```yaml
version: '3.8'

services:
  jaeger:
    image: jaegertracing/all-in-one:1.56
    container_name: gamegaraj-jaeger
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    ports:
      - "16686:16686"   # Jaeger UI Arayüzü (Tarayıcıdan erişim için)
      - "4317:4317"     # OTLP gRPC Alıcı Portu (k3s pod'ları buraya push edecek)
      - "4318:4318"     # OTLP HTTP Alıcı Portu
    restart: unless-stopped
    volumes:
      - jaeger_data:/badger

volumes:
  jaeger_data:
```

---

## 2. Bu Senaryoda Telemetri Akışı Nasıl Olacak?

Bu kurulum modelinde 3 ana sinyal şu şekilde dağıtılır:

```
                            k3s Cluster
                      ┌──────────────────────┐
                      │    Mikroservisler    │
                      └─┬──────┬───────────┬─┘
     1. Logs (Serilog)  │      │           │  3. Metrics (Scrape)
       (Direct Push)    │      │           │    (Internal Scrape)
                        ▼      │           ▼
           ┌────────────────┐  │  ┌─────────────────┐
           │ Elasticsearch  │  │  │   Prometheus    │
           │    (Dokploy)   │  │  │     (k3s)       │
           └────────────────┘  │  └────────┬────────┘
                               │           │
            2. Traces (OTLP)   │           ▼
              (Direct Push)    │  ┌─────────────────┐
                               ▼  │     Grafana     │
           ┌────────────────┐  └───►    (k3s)       │
           │     Jaeger     │     │ (Jaeger Link)   │
           │    (Dokploy)   │     └─────────────────┘
           └────────────────┘
```

1.  **Logs:** Mikroservisleriniz doğrudan Dokploy'daki mevcut Elasticsearch'e (`http://192.168.1.56:9201`) logları yollamaya devam eder.
2.  **Traces:** Mikroservisleriniz `OpenTelemetry__OtlpEndpoint` env var değeri üzerinden Dokploy'daki Jaeger'a (`http://192.168.1.56:4317`) trace'leri gönderir.
3.  **Metrics:** k3s cluster'ınız içinde çalışan Prometheus, k3s içindeki pod'ları yerel IP'leri üzerinden `/metrics` endpoint'inden tarar (scrape eder). Grafana da k3s içinde çalışır.

---

## 3. k3s içindeki Grafana ile Jaeger Bağlantısı

k3s üzerinde kurulu olan Grafana'ya girip Jaeger'ı veri kaynağı (Datasource) olarak eklemek için:

1.  Grafana Arayüzüne gidin (**Connections -> Data sources -> Add new data source**).
2.  **Jaeger**'ı seçin.
3.  URL alanına Dokploy üzerindeki Jaeger adresini girin:
    `http://192.168.1.56:16686` (veya Jaeger Query API adresi).
4.  **Save & test** butonuna tıklayarak bağlantıyı doğrulayın.

Bu sayede k3s cluster içindeki Grafana dashboard'larınızdan Dokploy üzerindeki Jaeger trace verilerine doğrudan geçiş yapabilirsiniz.
