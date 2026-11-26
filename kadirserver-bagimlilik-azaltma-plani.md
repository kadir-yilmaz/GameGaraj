# `kadirserver` Bağımlılığını Azaltma ve Tam HA Planı

**Tarih:** 26.11.2026  
**Hedef:** `kadirserver` kapandığında bile `gamegaraj` web sitesi, `argocd`, `headlamp` ve DNS (`coredns`) servislerinin AWS (`aws-cp1`, `aws-cp2`, `aws-wk1`) üzerinden kesintisiz çalışmaya devam etmesi.

---

## Mevcut Mimari Özeti

- **Control Plane (3'lü HA Quorum):**
  - `kadirserver` (`100.100.32.3`)
  - `aws-cp1` (`100.65.253.16`, 2 GB RAM, `NoSchedule` taint)
  - `aws-cp2` (`100.83.144.93`, 2 GB RAM, `NoSchedule` taint)
- **Worker (AWS):**
  - `aws-wk1` (`100.114.224.67`, 8 GB RAM, taint yok — tüm uygulama ve sistem podlarını alabilir)

---

## Tespit Edilen Bağımlılıklar (Bulgular)

### Bulgu 1: Cloudflare Tunnel (`cloudflared`) Tek Nokta Bağımlılığı
- **Sorun:** `local-server` isimli Cloudflare Tunnel şu an `kadirserver` host'u üzerinde çalışıyor. `kadirserver` kapandığında `gamegaraj.kadiryilmaz.online`, `gateway.kadiryilmaz.online` vb. tüm dış erişimler kesiliyor.
- **Çözüm:** `cloudflared`'ı host yerine Kubernetes içinde **`replicas: 2`** olan bir `Deployment` olarak çalıştırmak (`kadirserver` + `aws-wk1` üzerine dağıtarak). Aynı tunnel token ile iki pod paralel çalışır; `kadirserver` kapanınca `aws-wk1`'deki pod trafiği kesintisiz taşır.
- **Not:** Tunnel rotalarının node IP'si yerine cluster içi Kubernetes servis adreslerine (`http://<servis>.<namespace>.svc.cluster.local:<port>`) yönlendirilmesi gerekir.

### Bulgu 2: Sistem & Yönetim Podlarının Tek Node'da (`kadirserver`) Olması
- **Sorun:** `coredns` (1 replika), `metrics-server`, `headlamp` ve `argocd` (7 pod) şu an `kadirserver` üzerinde. `kadirserver` kapandığında Kubernetes varsayılan `300s` (5 dk) node eviction süresini beklediği için bu servisler ~5 dakika boyunca erişilemez kalıyor.
- **Çözüm:**
  - `coredns`'i **2 replikaya** çıkarıp `kadirserver` + `aws-wk1` arasında dağıtmak (DNS kesintisini 0'a indirmek).
  - `headlamp` ve `argocd` podlarını `aws-wk1` üzerinde (veya nodeAffinity/spread ile) konumlandırmak.

### Bulgu 3: `local-path` Kalıcı Disk (PVC) Bağımlılığı
- **Sorun:** `prometheus` ve `alertmanager` gibi PVC kullanan podlar `kadirserver` diskine (`local-path`) bağlıysa, `kadirserver` kapandığında başka node'a taşınamaz (`Pending` kalır).
- **Çözüm:** `kadirserver` kapandığında da açık kalması istenen stateful araçların PVC'lerini `aws-wk1` üzerinde oluşturmak (veya stateless mod kullanmak).

### Bulgu 4: Dokploy ve Veritabanları (Bilinen Sınır)
- **Durum:** Elasticsearch, Redis ve diğer DB'ler `kadirserver` üzerindeki Dokploy içinde çalışıyor. `kadirserver` kapandığında veritabanı katmanı durur; ancak `webui` / `gateway` podları ve Cloudflare Tunnel `aws-wk1` üzerinde ayakta olduğu sürece site HTTP yanıtı vermeye devam eder.

---

## Aşamalı Uygulama Planı

- [x] **Aşama 1: Durum Kontrolü (`kadirserver` açıldıktan sonra)**
  - Tüm node'lar `Ready`: `kadirserver`, `aws-cp1`, `aws-cp2`, `aws-wk1`.
  - Servis iç adresleri tespit edildi (`kubectl get svc -A`):
    - `gamegaraj.kadiryilmaz.online` → `http://webui.gamegaraj.svc.cluster.local:8080` (NodePort `30075`)
    - `gateway.kadiryilmaz.online` → `http://gateway.gamegaraj.svc.cluster.local:8080` (NodePort `30000`)
    - `argocd-server` → `http://argocd-server.argocd.svc.cluster.local:80` (NodePort `30080`)
    - `headlamp` → `http://headlamp.headlamp.svc.cluster.local:80` (NodePort `30444`)
- [x] **Aşama 2: Cloudflare Tunnel'ın Kubernetes'e Taşınması (GitOps Hazır)**
  - `cloudflared` namespace ve `cloudflared-token` Secret'ı `kadirserver` üzerinden cluster'da oluşturuldu (Git'e şifre konmadı).
  - `infra/k8s/cloudflared.yaml` (2 replika, `podAntiAffinity`) ve `helm/apps/cloudflared.yaml` (ArgoCD Application) oluşturuldu.
- [x] **Aşama 3: `headlamp`, `argocd`, `descheduler` `kadirserver` Kilidinin Kaldırılması**
  - `helm/apps/headlamp.yaml`, `helm/apps/argocd.yaml` ve `helm/apps/descheduler.yaml` içindeki `nodeSelector: kubernetes.io/hostname: kadirserver` sabitleri kaldırıldı.
  - *(Not: `prometheus-stack` `local-path` PVC'leri `kadirserver` diskine bağlı olduğu için şimdilik `prometheus.yaml` korundu.)*
- [x] **Aşama 4: `git push` + Cloudflare Routes Güncellemesi + Canlı Failover Hazırlığı**
  - [x] `git push` sonrası ArgoCD `cloudflared` (`aws-wk1` + `kadirserver`) ve `headlamp` (`aws-wk1`) podlarını otomatik açtı.
  - [x] `aws-wk1` node'una `gamegaraj-node=worker` etiketi eklendi ve `gamegaraj` podları `aws-wk1` üzerinde çalışmaya başladı.
  - [x] Tailscale Subnet Router (`kadirserver`: `--advertise-routes=192.168.1.0/24`, `aws-wk1`: `--accept-routes`) aktif edilerek AWS worker'ın `192.168.1.56` üzerindeki Dokploy/DB'lere erişimi sağlandı.
  - [x] Cloudflare Dashboard → Routes içinden `gamegaraj.kadiryilmaz.online` (`http://webui.gamegaraj.svc.cluster.local:8080`), `gateway.kadiryilmaz.online` (`http://gateway.gamegaraj.svc.cluster.local:8080`) ve diğer rotalar (`192.168.1.56`) güncellendi.
  - [x] Eski `kadirserver` host `cloudflared` servisi durduruldu (`sudo systemctl disable --now cloudflared`) ve `https://gamegaraj.kadiryilmaz.online` erişimi doğrulandı.
  - [x] `coredns` üzerindeki `kubernetes.io/hostname=kadirserver` kilidi kaldırıldı ve `replicas: 2` (`aws-cp1` + `aws-cp2`) olarak açıldı (502 Bad Gateway sebebi buydu).
  - [x] `helm/apps/headlamp.yaml` (`replicaCount: 2`, `podAntiAffinity`) ve `helm/apps/argocd.yaml` (`server.replicas: 2`, `repoServer.replicas: 2`, `syncPolicy.automated`) kalıcı 2 replika HA olarak güncellendi.
  - [x] `helm/gamegaraj/templates/microservice.yaml` içine `aws-wk1` Intel Xeon AVX-512 uyumluluğu için `DOTNET_EnableAVX512F: "0"` eklendi (`Exit 139` çözüldü).
  - [x] `helm/apps/prometheus.yaml` HA moduna alındı: Prometheus & Alertmanager `replicas: 2` (`kadirserver` + `aws-wk1`, `retentionSize: 2GB`), `kube-state-metrics` ve `prometheusOperator` üzerinden `kadirserver` kilidi kaldırıldı.
  - [ ] *(Opsiyonel Test)* `git push` sonrası `kadirserver` açıkken hem `kadirserver` hem `aws-wk1` üzerinde 2'şer podun doğrulanması.
