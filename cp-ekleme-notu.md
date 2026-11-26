# K3s HA Control Plane (AWS EC2) Ekleme Notları

**Tarih:** 26.11.2026  
**Amaç:** Mevcut tekli `kadirserver` Control Plane yapısına AWS EC2 (2 GB RAM, Ubuntu) üzerinde yeni bir Control Plane node'u eklemek (ileride 2. AWS CP ile toplam 3 CP'lik HA yapıya geçmek).

---

## Mevcut Cluster Bilgisi (Başlangıç Durumu)

- **Cihaz 1 (Desktop PC - `kadirserver`):**
  - Donanım: 16 GB RAM, R5 1600 AF, GTX 1650 Super
  - İşletim Sistemi: Ubuntu Server (`192.168.1.56`)
  - Firewall: `nftables` (UFW kullanılmıyor)
  - Roller: Dokploy (ES, Redis, diğer DB'ler) + K3s Control Plane (`gamegaraj` 13 pod)
- **Cihaz 2 (Asus Laptop - `wk1` & `wk2`):**
  - Donanım: 32 GB RAM, i7 13650HX, RTX 4060
  - `wk1`: AlmaLinux 10 (`192.168.1.5`), 8 GB RAM, 8 çekirdek, VirtualBox Bridged Adapter, `firewalld`
  - `wk2`: AlmaLinux 10 (`192.168.1.200`), 8 GB RAM, 8 çekirdek, VirtualBox Bridged Adapter, `firewalld`
- **Yeni Eklenecek Node (`aws-cp1`):**
  - AWS EC2, 2 GB RAM, Ubuntu

---

## Aşama 1: `kadirserver` Yapılandırma ve Veritabanı Kontrolü

### 1.1. Systemd Servis Dosyası Kontrolü
**Çalıştırılan Komut:**
```bash
sudo cat /etc/systemd/system/k3s.service
```

**Önemli Çıktı:**
```ini
ExecStart=/usr/local/bin/k3s \
    server \
```
**Bulgu:** `k3s.service` içinde `--cluster-init` parametresi görünmüyordu. Bu yüzden `/etc/rancher/k3s/` altında `config.yaml` olup olmadığını ve DB klasörünü kontrol etmeye geçtik.

---

### 1.2. K3s Config ve Veritabanı Klasörü Kontrolü
**Çalıştırılan Komut:**
```bash
sudo ls -la /etc/rancher/k3s/ /var/lib/rancher/k3s/server/db/
```

**Önemli Çıktı:**
- `/etc/rancher/k3s/` içinde:
  - `config.yaml` (40 byte, Nov 18)
  - `k3s.yaml` (2941 byte, Nov 26)
- `/var/lib/rancher/k3s/server/db/` içinde:
  - `etcd/` klasörü
  - `snapshots/` klasörü
  - `state.db.migrated` (Oct 17)

**Bulgu:** `kadirserver` daha önce SQLite'tan **embedded `etcd`** yapısına geçirilmiş (`state.db.migrated`, `etcd/` ve `snapshots/` mevcut). Yani `kadirserver` tarafında yeniden kurulum veya DB taşıma işlemine gerek yok.

---

## Aşama 2: `config.yaml` ve Mevcut Node Kontrolü

### 2.1. `config.yaml` İçeriği
**Çalıştırılan Komut:**
```bash
sudo cat /etc/rancher/k3s/config.yaml
```

**Çıktı:**
```yaml
cluster-init: true
disable:
  - traefik
```

**Bulgu:**
- `cluster-init: true` zaten tanımlı (embedded `etcd` aktif).
- `traefik` devre dışı (`disable: [traefik]`). **Önemli:** AWS EC2'ye kurulacak yeni CP'lerin `config.yaml` dosyasında da `disable: [traefik]` yer almalı; aksi halde diğer serverlarla çakışır.

---

### 2.2. Mevcut Node Listesi ve K3s Sürümü
**Çalıştırılan Komut:**
```bash
sudo k3s kubectl get nodes -o wide
```

**Çıktı:**
- `kadirserver` — `Ready` — `control-plane,etcd` — `v1.37.2+k3s1` — `192.168.1.56` (Ubuntu 26.04 LTS)
- `wk1` — `NotReady` — `v1.37.2+k3s1` — `192.168.1.5` (AlmaLinux 10.2)
- `wk2` — `NotReady` — `v1.37.2+k3s1` — `192.168.1.200` (AlmaLinux 10.3)
- `wk3` — `NotReady` — `v1.37.2+k3s1` — `192.168.1.199` (Rocky Linux 10.3)

**Bulgu:**
- Tüm cluster **`v1.37.2+k3s1`** sürümünde. AWS EC2 kurulumunda sürüm uyumu için `INSTALL_K3S_VERSION="v1.37.2+k3s1"` sabitlenmeli.
- `wk1`, `wk2` ve `wk3` şu an `NotReady` (muhtemelen laptop VM'leri kapalı).

---

## Aşama 3: Ağ Bağlantısı ve Tailscale IP Tespiti

- **Karar:** Evdeki node'lar (`kadirserver`, `wk1`, `wk2`, `wk3`) ile AWS EC2 arasındaki haberleşme **Tailscale** üzerinden yapılacak.
- **Tailscale IP Listesi (Admin Paneli):**
  - `aws-cp1`: `100.65.253.16` (Connected)
  - `kadirserver`: `100.100.32.3` (Connected)
  - `wk1`: `100.67.37.25`
  - `wk2`: `100.120.194.97`

---

## Aşama 4: `aws-cp1` -> `kadirserver` Bağlantı Testi

**Çalıştırılan Komut (`aws-cp1` üzerinde):**
```bash
curl -vk --connect-timeout 5 https://100.100.32.3:6443/ping
```

**Çıktı:**
- `Established connection to 100.100.32.3 (100.100.32.3 port 6443) from 100.65.253.16`
- `< HTTP/2 200` -> `pong`

**Bulgu:** `aws-cp1`, Tailscale üzerinden `kadirserver:6443` API portuna sorunsuz ulaşıyor (`nftables` engeli yok).

---

## Aşama 5: `etcd` Peer IP Kontrolü (`192.168.1.56` Erişimi)

**Çalıştırılan Komut (`aws-cp1` üzerinde):**
```bash
ping -c 2 192.168.1.56
```

**Çıktı:**
- `2 packets transmitted, 0 received, 100% packet loss`

**Bulgu:** `aws-cp1`, evdeki yerel `192.168.1.56` IP'sine doğrudan ulaşamıyor (subnet routing yok). Bu nedenle `kadirserver` ve `aws-cp1` üzerindeki K3s node IP'lerinin ve Flannel ağının Tailscale (`100.x.y.z` ve `tailscale0`) üzerinden çalışacak şekilde ayarlanması gerekiyor.

---

## Aşama 6: `kadirserver` Yedeği ve Tailscale IP Ayarı

### 6.1. `etcd` Snapshot (Yedek) Alınması
**Çalıştırılan Komut (`kadirserver` üzerinde):**
```bash
sudo k3s etcd-snapshot save --name pre-tailscale-ha
```

**Çıktı:**
- `INFO[0000] Snapshot pre-tailscale-ha-kadirserver-1795690073 saved.`
*(Not: `etcd-snapshot` alt komutu `server` flag'lerini tanımadığı için `Unknown flag --cluster-init / --disable` uyarısı vermesi normaldir.)*

### 6.2. `config.yaml` Güncellemesi
**Çalıştırılan Komut (`kadirserver` üzerinde):**
```bash
sudo tee /etc/rancher/k3s/config.yaml <<EOF
cluster-init: true
disable:
  - traefik
node-ip: 100.100.32.3
flannel-iface: tailscale0
tls-san:
  - 100.100.32.3
  - 192.168.1.56
EOF
```
**Durum:** Dosya güncellendi.

## Aşama 7: `kadirserver` `etcd` Peer IP Güncellemesi (`--cluster-reset`)

**Çalıştırılan Komutlar (`kadirserver` üzerinde):**
```bash
sudo systemctl stop k3s
sudo k3s server --cluster-reset
sudo systemctl start k3s
sudo k3s kubectl get nodes -o wide
```

**Önemli Çıktı:**
- `updated-remote-peer-urls: ["https://100.100.32.3:2380"]`
- `Managed etcd cluster membership has been reset, restart without --cluster-reset flag now.`
- `k3s` servisi sorunsuz açıldı ve `kadirserver` `Ready` durumuna geçti.

---

## Aşama 8: `aws-cp1` Kurulumu ve Cluster'a Katılım

### 8.1. `aws-cp1` Üzerinde `config.yaml` Oluşturulması
**Çalıştırılan Komut (`aws-cp1` üzerinde):**
```bash
sudo mkdir -p /etc/rancher/k3s
sudo tee /etc/rancher/k3s/config.yaml <<EOF
server: https://100.100.32.3:6443
token: "<K3S_SERVER_TOKEN>"
disable:
  - traefik
node-ip: 100.65.253.16
flannel-iface: tailscale0
tls-san:
  - 100.65.253.16
node-taint:
  - "node-role.kubernetes.io/control-plane=true:NoSchedule"
EOF
```
*(Not: Güvenlik amacıyla token not dosyasında maskelenmiştir; gerçek token `kadirserver` üzerindeki `/var/lib/rancher/k3s/server/token` dosyasından alınmıştır.)*

**Durum:** `aws-cp1` üzerinde `/etc/rancher/k3s/config.yaml` dosyası oluşturuldu.

### 8.2. `aws-cp1` K3s Kurulumu
**Çalıştırılan Komut (`aws-cp1` üzerinde):**
```bash
curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="v1.37.2+k3s1" sh -s - server
```
**Çıktı:**
- `v1.37.2+k3s1` indirildi ve kuruldu.
- `[INFO] systemd: Starting k3s` aşamasını tamamlayıp servisi başlattı (WAN üzerinden ilk `etcd` senkronizasyonu nedeniyle birkaç dakika sürdü).

---

### 8.3. `kadirserver` `nftables` Kontrolü
**Çalıştırılan Komut (`kadirserver` üzerinde):**
```bash
sudo cat /etc/nftables.conf
```
**Bulgu:** `iifname "tailscale0" accept` kuralı ve `policy accept` zaten mevcut. Yani `kadirserver` `nftables` tarafında Tailscale trafiğine engel yok.

---

## Aşama 9: Doğrulama (`kubectl get nodes -o wide`)

**Çalıştırılan Komut (`kadirserver` üzerinde):**
```bash
sudo k3s kubectl get nodes -o wide
```

**Çıktı:**
- `aws-cp1` — `Ready` — `control-plane,etcd` — `v1.37.2+k3s1` — `100.65.253.16` (Ubuntu 26.04.1 LTS)
- `kadirserver` — `Ready` — `control-plane,etcd` — `v1.37.2+k3s1` — `100.100.32.3` (Ubuntu 26.04 LTS)
- `wk1` — `NotReady` — `v1.37.2+k3s1` — `192.168.1.5`
- `wk2` — `NotReady` — `v1.37.2+k3s1` — `192.168.1.200`
- `wk3` — `NotReady` — `v1.37.2+k3s1` — `192.168.1.199`

---

## Önemli Notlar ve İleride 3. CP (`aws-cp2`) Ekleme Rehberi

1. **Geçici 2 CP Quorum Uyarısı:** Şu an 2 adet `etcd` üyesi (`kadirserver` + `aws-cp1`) var. `etcd`'de 2 node'un quorum'u 2'dir; yani 3. CP eklenene kadar ikisinden biri kapanırsa cluster geçici olarak kilitlenir. 3. CP (`aws-cp2`) eklendiğinde 3 node'dan 1'i düşse bile cluster çalışmaya devam edecektir (tam HA).
2. **İleride `aws-cp2` Eklerken Yapılacaklar:**
   - **Otomatik Yol (Terraform):** `infra/terraform/` klasöründeki şablon hazır. `terraform.tfvars` dosyasını doldurup `terraform init && terraform apply` çalıştırmak yeterli (Tailscale + K3s `v1.37.2+k3s1` + `NoSchedule` taint otomatik kurulur).
   - **Manuel Yol:**
     - Tailscale kurup `100.x.y.z` IP'sini al.
     - `/etc/rancher/k3s/config.yaml` dosyasını `aws-cp1` ile aynı şablonda oluştur (`node-ip` ve `tls-san` kısmına `aws-cp2`'nin Tailscale IP'sini yaz; `node-taint` eklemeyi unutma).
     - Aynı sürümle kur: `curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="v1.37.2+k3s1" sh -s - server`
3. **Worker Node'lar (`wk1`, `wk2`, `wk3`) Açıldığında:**
   - `kadirserver` artık `100.100.32.3` (`tailscale0`) üzerinden haberleştiği için, worker'lar açıldığında Tailscale üzerinden sorunsuz konuşabilmeleri adına gerekirse worker `/etc/rancher/k3s/config.yaml` dosyalarına da kendi Tailscale IP'leri (`node-ip` + `flannel-iface: tailscale0`) eklenebilir.

---

## Aşama 10: Terraform ile `aws-cp2` Sunucusunun Oluşturulması

**Çalıştırılan Komutlar (PowerShell - `infra/terraform/`):**
```powershell
terraform init
terraform apply
```

**Çıktı:**
- `Apply complete! Resources: 2 added, 0 changed, 0 destroyed.`
- `instance_id = "i-0f3ec986df0a1d399"`
- `public_ip = "13.53.186.123"`

**Durum:** `aws-cp2` (`t3.small`, 15 GB gp3, Ubuntu 24.04 LTS, `eu-north-1`) AWS üzerinde oluşturuldu ve SSH (`13.53.186.123`) üzerinden erişime hazır.

---

## Aşama 11: `aws-cp2` Tailscale, Swap ve K3s Kurulumu (3'lü HA Tamamlandı)

1. **Tailscale:** `aws-cp2` üzerinde `sudo tailscale up` ile bağlanıldı -> IP: `100.83.144.93`.
2. **Swap:** 1 GB kalıcı `/swapfile` oluşturuldu (`free -h`: Mem 1.9Gi / Swap 1.0Gi).
3. **`config.yaml` ve K3s Kurulumu (`paramiko` üzerinden otomatik):**
   - `/etc/rancher/k3s/config.yaml` (`server: https://100.100.32.3:6443`, `disable: [traefik]`, `node-ip: 100.83.144.93`, `flannel-iface: tailscale0`, `tls-san: [100.83.144.93]`, `node-taint: ["node-role.kubernetes.io/control-plane=true:NoSchedule"]`).
   - `curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="v1.37.2+k3s1" sh -s - server` -> `EXIT: 0`.

### 11.1. 3'lü HA Cluster Doğrulaması
**Çalıştırılan Komutlar (`aws-cp2` üzerinde):**
```bash
sudo k3s kubectl get nodes -o wide
curl -s http://127.0.0.1:2381/metrics | grep -E '^etcd_server_(has_leader|is_leader) '
sudo k3s kubectl get --raw='/readyz?verbose' | grep etcd
```

**Çıktı:**
- `aws-cp1` — `Ready` — `control-plane,etcd` — `v1.37.2+k3s1` — `100.65.253.16`
- `aws-cp2` — `Ready` — `control-plane,etcd` — `v1.37.2+k3s1` — `100.83.144.93`
- `kadirserver` — `Ready` — `control-plane,etcd` — `v1.37.2+k3s1` — `100.100.32.3`
- `etcd_server_has_leader 1`
- `[+]etcd ok` / `[+]etcd-readiness ok`

**Sonuç:** 3 Control Plane'li (`kadirserver`, `aws-cp1`, `aws-cp2`) tam High Availability (HA) `etcd` kümesi aktif. Artık 3 CP'den 1'i kapansa bile quorum (2/3) korunur ve cluster/ArgoCD kesintisiz çalışmaya devam eder.

---

## Aşama 12: AWS Worker Node (`aws-wk1` - 8 GB RAM) Kurulumu

1. **Sunucu:** AWS `m7i-flex.large` (8 GB RAM), Ubuntu 26.04.1 LTS, Tailscale IP: `100.114.224.67`.
2. **Ayarlar (`/etc/rancher/k3s/config.yaml`):**
   - `server: https://100.100.32.3:6443`
   - `token: "<K3S_SERVER_TOKEN>"`
   - `node-name: aws-wk1`
   - `node-ip: 100.114.224.67`
   - `flannel-iface: tailscale0`
   - *(Taint eklenmedi: hem `gamegaraj` hem `argocd`/`headlamp`/`coredns`/`monitoring` podlarını alabilir.)*
3. **Kurulum:** `curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION="v1.37.2+k3s1" sh -s - agent`
4. **Doğrulama (`sudo k3s kubectl get nodes -o wide`):**
   - `aws-cp1` — `Ready` (`control-plane,etcd`, `100.65.253.16`)
   - `aws-cp2` — `Ready` (`control-plane,etcd`, `100.83.144.93`)
   - `aws-wk1` — `Ready` (`worker`, `100.114.224.67`)
   - `kadirserver` — `Ready` (`control-plane,etcd`, `100.100.32.3`)
