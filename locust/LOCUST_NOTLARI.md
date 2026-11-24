# GameGaraj Locust Yük Testi Notları ve Sonuç Özeti
./locust/.venv/bin/locust -f locust/locustfile.py --processes -1
http://localhost:8089

**Tarih:** 23 Kasım 2026  
**Ortam:** k3s 3-Node Homelab (`kadirserver` bare-metal + `wk1`/`wk2` VirtualBox VM) — Yük Üretici: MacBook Air (Ofisten Tailscale / Cloudflare üzerinden)

hedefler:
Hedefleri **maksimum yükte değil, hedef yükte** koymak lazım. 3.857 RPS bir stres/tavan testi; SLO testini bunun yaklaşık %50-70'inde (~2-2,5k RPS) yapıp o seviyede hedefleri tutturmaya çalış.

| Metrik | Hedef |
|---|---|
| **p50** | < 100-150 ms |
| **p95 (okuma: catalog, basket, gateway genel)** | < 500 ms |
| **p95 (search)** | < 300 ms |
| **p95 (yazma: sipariş, ödeme, auth)** | < 1 s |
| **p99** | < 1-2 s |
| **5xx hata oranı** | < %0,1 |
| **Timeout / 499 / 530** | < %0,5 |
| **Pod CPU (hedef yükte)** | < %70 limit |
| **Node CPU** | < %70, dengeli dağılım (node'lar arası fark < 15 puan) |
| **Throttling (CFS)** | ~0'a yakın |
| **Recovery** | Yük kalkınca 1-2 dk içinde p95 normale dönmeli |

**Nasıl ölç?**

1. **Server-side (Grafana, `http_server_request_duration_seconds`) asıl SLO kaynağın olsun.** Ofis interneti + Tailscale gecikmesi Locust'ın p95'ine eklendiği için client-side rakam kötü görünür. Senin Test 8'de de bu fark var: Grafana 1,8 s, Locust 30 s.
2. **Locust'ı sadece "tavan bulma" için değil, sabit RPS'te (ör. 1k, 2k, 3k basamaklı) koşup** her basamakta p95'in hedefte kalıp kalmadığına bak. Hedefi tutturduğun en yüksek basamak senin "güvenli kapasiten".
3. **Endpoint bazlı ayrı hedef koy.** Tek bir genel p95 rakamı search'teki sorunu catalog'un iyi rakamıyla gizleyebilir.

**Gerçekçi beklenti:** Homelab + VM'lerle p95 < 500 ms'yi 3,8k RPS'te tutturmak zor; ama 1-2k RPS'te çok makul. Bu yüzden "2k RPS'te p95 < 500 ms, %0,1 altı hata" gibi tek net bir cümle, "4k RPS" demekten hem daha dürüst hem de portfolyoda daha güçlü durur.
---

## 1. Trendyol Tech Referans Değerleri ve GameGaraj Karşılaştırması

Sektördeki büyük ölçekli e-ticaret sistemlerini kıyaslayabilmek için **Trendyol Tech**'in Medium yayınlarında paylaştığı RPM (*Requests Per Minute*, `1 RPS = 60 RPM`) değerleri ve GameGaraj'ın bugünkü homelab sonuçları aşağıdadır:

| Sistem / Servis | Ortam / Mimari | RPM | RPS |
|---|---|---:|---:|
| **Trendyol Mobil BFF** | Go (*1.5M Throughput with Golang*) | `1,500,000+ RPM` | `~25,000 RPS` |
| **Trendyol Promotion / Search API** | Tek DC, Elasticsearch optimizasyonlu | `1,600,000 RPM` | `~26,600 RPS` |
| **Trendyol İçerik Servisi** | Java → Go geçişi (*Scaling to 5M RPM*) | `5,000,000 RPM` | `~83,300 RPS` |
| **Trendyol Content Eligibility** | KEDA auto-scaling | `8,100,000 RPM` | `~135,000 RPS` |
| **Trendyol Ana Gateway (Kasım Zirvesi)** | Çoklu DC, yüzlerce sunucu / 1000+ pod | `~100,000,000 RPM` | `~1,660,000 RPS` |
| **GameGaraj (Bugünkü Homelab Zirvesi)** | **3 replika (1 PC + 2 VM), Tailscale üzerinden** | **`~244,086 RPM`** | **`4,068.1 RPS` (Ort.)** |

> **Temel Çıkarım:** Trendyol milyonluk RPS'lere yüzlerce sunucu ve binlerce pod ile yatay ölçeklenerek ulaşır; GameGaraj'ın 3 replikalı ev donanımında (üstelik ofisten Tailscale tüneliyle) **replika başına `~1,356 RPS`** görmesi servis mimarisi açısından güçlü bir taban verir.

### Trendyol'da Neden Aşım Olmuyor?
1. **Tek Tünel / Ev Modemi Yok (10G/40G Veri Merkezi + Dağıtık Trafik):** Prodüksiyonda trafik yüz binlerce farklı kullanıcı IP'sinden doğrudan BGP/Anycast Load Balancer'lara ve 10Gbps+ veri merkezi hatlarına dağılır; arada tek bir `tailscaled` şifrelemesi veya ev modemi yoktur.
2. **Aynı Veri Merkezinden Dağıtık Yük Testi (Ares):** Yük testi tek bir laptop yerine hedef cluster ile **aynı veri merkezindeki onlarca load-generator podundan** (`<0.5 ms` RTT) atılır.
3. **Yatay Otomatik Ölçekleme (HPA / KEDA):** Yük arttıkça pod sayısı sabit `3`'te kalmaz; otomatik olarak `10 → 100 → 500` replikaya çıkarılır.
4. **Kısa Timeout + Edge Cache + Circuit Breaker:** Sıcak trafik CDN/Edge Cache'ten döner; içeride istekler 30 saniye bekletilmez, `500 ms – 2s` içinde kesilip fallback dönülür.

---

## 2. Ne Yaptık? (Altyapı ve Araç İyileştirmeleri)

1. **Locust Web UI & Profil Sistemi ([locust/locustfile.py](locust/locustfile.py)):**
   - `FastHttpUser` ile düşük overhead'li Web UI kuruldu (`locust -f locustfile.py --processes -1`).
   - Web UI'dan seçilebilir **`Target Env`** (`wk1-tailscale`, `wk2-tailscale`, `wk1+wk2-tailscale`, `all3-tailscale`, `wk1+wk2-lan`, `all3-lan`, `cloudflare`), **`Target RPS`** (`500`–`10000` otomatik kullanıcı profili) ve **`Intensity`** (`realistic`, `fast`, `max`) eklendi.
   - Her test durduğunda `locust/results/latest_summary.txt` ve zaman damgalı klasöre otomatik **node bazlı + endpoint bazlı** rapor yazma eklendi.
2. **Prometheus & Grafana RPS Gözlemlenebilirliği:**
   - Tüm **11 .NET servisine** [PrometheusMetricsExtensions.cs](GameGaraj.Shared/Observability/Metrics/PrometheusMetricsExtensions.cs) ve **Go `search-api`** servisine `promhttp` + `http_server_request_duration_seconds` eklendi (`notification-api` zaten açıktı → 13/13 servis kapsandı).
   - [servicemonitor.yaml](helm/gamegaraj/templates/servicemonitor.yaml) ve [grafana-rps-dashboard.yaml](helm/apps/grafana-rps-dashboard.yaml) oluşturuldu.
3. **CPU Limitleri Artırıldı ([CPU_LIMITS.md](CPU_LIMITS.md)):**
   - `global.defaultResources.limits.cpu`: `500m` → **`1500m`**
   - `catalog-api.resources.limits.cpu`: `1000m` → **`2000m`**
4. **`kadirserver` Kernel ve Otomatik Güncelleme Müdahalesi:**
   - `unattended-upgrade` tarafından kurulan `7.0.0-43-generic` kerneli `7.0.0-38-generic` ile değiştirildi ve otomatik güncellemeler kapatıldı.

---

## 3. Test Sonuçları Özeti

| Test Senaryosu | Kullanıcı / Hedef | Ort. RPS | Anlık Pik RPS | `p50` | `p95` | Hata (%) | Öne Çıkan Bulgu |
|---|---|---:|---:|---:|---:|---:|---|
| **1. `wk1` Tek Node (Isınma)** | 100 user | `~127` | `~127` | `170 ms` | `990 ms` | `%0.00` | İlk bağlantı stabil |
| **2. `wk1` Tek Node (Tailscale)** | 2500 user / 5K | `~958` | `~1,550` | `1,500 ms` | `7–8 s` | `%0.00` | `wk1` `tailscaled` tek başına boğuldu (`Load 24.86`) |
| **3. Cloudflare (`gateway.kadiryilmaz.online`)** | 2500 user / 5K | `587.7` | `~600` | `~1,000 ms` | `~30 s` | `%4.00` | Milan POP + `cloudflared` tüneli (`499`/`530`/`403`) |
| **4. `wk1+wk2-tailscale` (`500m` limit)** | 2500 user / 5K | `~1,490` | `~1,600` | — | `~3–4 s` | `%0.00` | Yük iki VM'e dağıldı; podlar `0.5` CPU limitine takıldı |
| **5. `all3-tailscale` (Önceki, 14:41)** | 2500 user / 5K | `1,170.6` | `~1,300` | `210 ms` | `9.4 s` | `%0.90` | `kadirserver`: `661.7`, `wk1`: `256.7`, `wk2`: `252.1 RPS` |
| **6. `all3-tailscale` (`1500m` limit, 14:52)** | 2500 user / 5K | `1,830.9` | `2,084.3` | `180 ms` | `5.0 s` | `%0.19` | `basket`/`catalog` `p95` `1.2–2.9s`, `search` `10–11s` |
| **7. `all3-tailscale` (15:02)** | 2500 user / 5K | **`1,730.2`** | **`2,345.4`** | **`83 ms`** | `5.1 s` | `%1.31` | **`kadirserver`: `1,236.2 RPS` (`%0.44` fail, `450 ms avg`)** |
| **8. `all3-tailscale` (ES 2GB, 15:15)** | 5000 user / 10K | `~1,982` | **`2.8K` (Grafana) / `~3.1K` pik** | — | **`1.8 s` (Grafana)** / `30s` (Locust) | **`%0.00` 5xx** / `%3` timeout | **`search-api p95`: `22.8 ms` (`max 269 ms`); `catalog`: `940 req/s`, `basket`: `805 req/s`** |
| **9. `kadirserver-tailscale` (15:35)** | 5000 user / 10K | **`3,857.1`** | `3,857+` (`~231K RPM`) | `190 ms` | `2.7 s` | `%0.53` | `kadirserver` girişte `tailscaled` sadece `%13.6` CPU ile tüm trafiği 3 node'a (%55 / %64 / %65 CPU) dengeli dağıttı |
| **10. `kadirserver-tailscale` (16:06, Sade UI)** | 5000 user / `User RPS=2` | **`4,068.1`** | **`4,187.3` (`~244K RPM`)** | `210 ms` | **`2.2 s`** | `%1.25` | **Yeni Rekor: `kadirserver` `%48`, `wk1` `%63`, `wk2` `%65` CPU; ES `%1.49`, Postgres `%0.57`, Redis `%1.0` CPU ile yarım güçte 4.06K RPS** |

### Son Koşuda Node Bazlı Dağılım (`447,541` toplam istek / `258.7 sn`)
- **`kadirserver` (bare-metal):** `1,236.2 RPS` (`avg: 450.8 ms`, hata `%0.44`) — `wk1 + wk2` toplamının **2.5 katı** throughput.
- **`wk1` (VirtualBox VM):** `244.5 RPS` (`avg: 3,092.4 ms`, hata `%3.54`)
- **`wk2` (VirtualBox VM):** `249.5 RPS` (`avg: 3,010.0 ms`, hata `%3.45`)

---

## 4. Tespit Edilen Darboğazlar ve Sonraki Adımlar

1. **Dış Taşıma Borusu (İnternet + Tailscale + Mac Port Sınırı):** `kadirserver-tailscale` ile `4,068.1 RPS` (`4,187` anlık) görülürken bile cluster sadece `%48–65` CPU'da, DB'ler ise `%0.5–1.5` CPU'da kaldı. Asıl tavan:
   - **Ofis ↔ Ev İnterneti & Tailscale RTT (`avg = 977 ms`):** Ev upload hattı ve tünel gecikmesi `5,000` kullanıcıyı `~4.1K RPS`'te tutuyor.
   - **Mac Ephemeral Port Limiti (`16,383` port):** `5,000` user üstünde (`manual + max`) `TIME_WAIT` yüzünden `OSError(49, "Can't assign requested address")` oluşuyor (`sudo sysctl -w net.inet.ip.portrange.first=16384` veya `all3-lan` ile aşılır).
   - **Çözüm:** Evde kablolu LAN'dan **`kadirserver-lan` / `all3-lan`** ile test atıldığında dış internet ve Tailscale kalkacağı için cluster gerçek %90–100 yük altında **`5,000+ RPS`**'e çıkacaktır.
2. **VirtualBox VM Overhead'i:** `wk1`/`wk2` üzerinde VirtualBox Bridged NIC + Windows E-Core zamanlaması gecikmeyi artırıyor. Tek güçlü VM + **`virtio-net`** + **kablolu Ethernet** performansı ciddi oranda yükseltir.
3. **Elasticsearch RAM Sınırı (Çözüldü):** `gamegaraj-elasticsearch` RAM limiti `1 GiB → 2 GiB` yapılarak `1.09 GiB / 2 GiB (%54.52)` seviyesine rahatlatıldı; `search-api` p95 gecikmesi **`10–11s` → `22.8 ms`**'ye düştü.
4. **CoreDNS IPv6 / Modem Timeout:** Ev modemi yoğun yükte DNS timeout verdiğinde CoreDNS upstream'i `1.1.1.1 8.8.8.8` (yalnızca IPv4) olarak sabitlenebilir.
