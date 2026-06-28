# GameGaraj Observability Architecture Overview

Bu belgede, GameGaraj mikroservis mimarisinin canlı ortam (production) gözlemlenebilirlik (observability) yapısı, veri akışları ve bileşenlerin rolleri açıklanmaktadır.

## Genel Mimari Şema

Sistem, **k3s Kubernetes Cluster** üzerinde çalışan mikroservisler ile **Dokploy (Homeserver)** üzerinde çalışan ortak gözlemlenebilirlik (observability) bileşenlerinden oluşur.

```
                  k3s Kubernetes Cluster (Pods)
┌──────────────────────────────────────────────────────────────┐
│                                                              │
│    Gateway Pod        Catalog Pod        Order Pod   ...     │
│  (Serilog + OTel)   (Serilog + OTel)  (Serilog + OTel)       │
│         │                  │                  │              │
└─────────┼──────────────────┼──────────────────┼──────────────┘
          │ (Logs)           │ (Traces: OTLP)   │ (Metrics: Scrape)
          ▼                  ▼                  ▼
┌──────────────────────────────────────────────────────────────┐
│                                                              │
│   Elasticsearch          Jaeger          Prometheus          │
│     Port 9200          Port 4317          Port 9090          │
│         │                  │                  │              │
│         ▼                  ▼                  ▼              │
│      Kibana            Jaeger UI           Grafana           │
│     Port 5601          Port 16686         Port 3000          │
│                                                              │
│                      Dokploy (Infra Server)                  │
└──────────────────────────────────────────────────────────────┘
```

---

## Bileşenlerin Rolleri ve Port Bilgileri

### 1. Dokploy Üzerinde Çalışan Yapılar (Observability Backend)

| Bileşen | Görevi | Protokol / Port | Dışarıya Açık Port (Dokploy) |
| :--- | :--- | :--- | :--- |
| **Elasticsearch** | Logları indeksler ve saklar. | HTTP / 9200 | `9201` (Güvenlik için sadece k3s ve Kibana erişimine açık olmalı) |
| **Kibana** | Logları aramak, filtrelemek ve analiz etmek için arayüz sağlar. | HTTP / 5601 | `5601` (Şifre korumalı / VPN arkasında olmalı) |
| **Jaeger** | Distributed tracing verilerini toplar, birleştirir ve arayüz sunar. | OTLP gRPC / 4317<br>OTLP HTTP / 4318<br>UI HTTP / 16686 | `4317` (gRPC Telemetry alıcı)<br>`16686` (Jaeger UI Arayüzü) |
| **Prometheus** | Mikroservislerden gelen metrikleri belirli aralıklarla çekip (scraping) depolar. | HTTP / 9090 | `9090` (Opsiyonel / Sadece Grafana erişse yeterlidir) |
| **Grafana** | Prometheus metriklerini görselleştirir, dashboard'ları sunar ve alert üretir. | HTTP / 3000 | `3000` (Grafana Arayüzü - SSL/TLS ile açılmalı) |

### 2. k3s Cluster Üzerinde Çalışan Yapılar (Mikroservisler)

Tüm mikroservisler `GameGaraj.Shared` kütüphanesini kullanır:
- **Logs:** Serilog üzerinden doğrudan Dokploy'daki Elasticsearch (`http://192.168.1.56:9201`) sunucusuna gönderilir.
- **Traces:** OpenTelemetry SDK, oluşturduğu trace'leri OTLP gRPC protokolü üzerinden Dokploy'daki Jaeger'a (`http://192.168.1.56:4317`) push eder.
- **Metrics:** Her servis içinde bir Prometheus Exporter barındırır. Prometheus, k3s pod'larının `/metrics` endpoint'ini periyodik olarak okur (scrape eder).

---

## Kurulum ve Yapılandırma Yol Haritası

1. **Dokploy Tarafı:**
   - [dokploy_compose.md](file:///d:/Kadir/Projeler/GameGaraj/notes/observability/dokploy_compose.md) dosyasındaki docker-compose kodunu Dokploy üzerinde yeni bir application / stack olarak oluşturun ve başlatın.
   - Elasticsearch ve Kibana'nın çalıştığından emin olun.
   - `/scripts/setup-elasticsearch-ilm.sh` betiğini çalıştırarak indeks şablonunu ve log tutma (ILM) politikasını uygulayın.

2. **k3s Cluster Tarafı:**
   - Mikroservis deployment'larındaki `OpenTelemetry__OtlpEndpoint` environment değişkeninin Dokploy sunucusunun IP adresini (`http://192.168.1.56:4317`) gösterdiğinden emin olun.
   - Detaylar için [k3s_deployment_notes.md](file:///d:/Kadir/Projeler/GameGaraj/notes/observability/k3s_deployment_notes.md) belgesini inceleyin.
