# Local K8s Deploy - Kurulum ve Çalıştırma Rehberi (GameGaraj)

Bu doküman, GameGaraj projesinin yerel makinede GitHub Actions ve Kubernetes (K8s) kullanılarak nasıl otomatik olarak ayağa kaldırılacağını açıklar. Udemy projesindeki yapının aynısı uyarlanmıştır.

---

## 1. GitHub Actions Workflow
Sürecin merkezi, `.github/workflows/deploy-local.yml` dosyasıdır. Bu workflow sadece manuel olarak tetiklendiğinde çalışan otomasyon adımlarını içerir.

- **Dosya Yolu:** `.github/workflows/deploy-local.yml`
- **Tetikleyici:** Yalnızca manuel tetikleme (`workflow_dispatch`).
- **Hedef:** `runs-on: self-hosted` (Yerel makinedeki runner).

---

## 2. Self-Hosted Runner Kurulumu (Yerel Makine Tanımlama)
GitHub'ın yerel makinenize komut gönderebilmesi için "Runner" kurmanız gerekir.

### Kurulum Adımları:
1. GitHub deponuzda **Settings > Actions > Runners** yolunu izleyin.
2. **New self-hosted runner** butonuna tıklayın ve işletim sistemi olarak "Windows" seçeneğini seçin.
3. Proje dizininizin dışında (veya projede gitignore edilecek) `actions-runner-k8s` adında bir klasör oluşturun (Örn: `d:\Kadir\Projeler\GameGaraj\actions-runner-k8s`).
4. GitHub'ın verdiği indirme ve konfigürasyon komutlarını bu klasör içinde çalıştırın.
   - Örn: `config.cmd --url https://github.com/kadir-yilmaz/GameGaraj --token [TOKEN]`
5. Kurulum bittiğinde klasör yapınız şöyle olacaktır: `d:\Kadir\Projeler\GameGaraj\actions-runner-k8s`.

---

## 3. Altyapı ve Hazırlık (Infrastructure)
Uygulama K8s üzerinde ayağa kalkarken önce veritabanları, Redis, Keycloak, RabbitMQ ve ElasticSearch gibi servisler çalışır durumda olmalıdır.

- **Otomasyondaki Komut:** `docker compose -f docker-compose.yml up -d`
- **İçerik:** GameGaraj altyapısı bu komutla Docker üzerinden çalışır. K8s'teki API'ler `host.docker.internal` üzerinden bu servislere ulaşacak şekilde yapılandırılmıştır.

---

## 4. Uygulamayı Yayına Alma (Deployment)
Runner aktif olduğunda, workflow şu adımları otomatik gerçekleştirir:

1. **Build:** Tüm mikroservisler (Catalog, Basket, Discount, WebUI vb.) için yeni eklenen `Dockerfile`'lar kullanılarak imajlar oluşturulur.
2. **K8s Apply:** `k8s/` klasöründeki manifest dosyaları (`gateway.yaml`, `webui.yaml` vb.) Kubernetes kümesine uygulanır.
3. **Rollout Restart:** Değişikliklerin anında yansıması için deployment'lar yeniden başlatılır.

---

## 5. Sistemi Çalıştırma (Kritik Komutlar)

Projenin yerelinizde GitHub Actions ile senkronize çalışması için runner'ın açık olması gerekir.

### Runner'ı Başlatma:
Terminali açın ve şu komutları çalıştırın:
```powershell
cd d:\Kadir\Projeler\GameGaraj\actions-runner-k8s
./run.cmd
```
*Bu komut çalıştıktan sonra GitHub'daki workflow "Waiting for a runner..." durumundan "Running" durumuna geçecektir.*

### Kubernetes Durumunu Kontrol Etme:
Her şeyin yolunda olduğunu doğrulamak için:
```powershell
kubectl get pods
kubectl get services
```

### Tarayıcı Erişimi (K8s):
Tüm servisler NodePort ile dışarı açılmıştır:
- **Gateway:** `http://localhost:30000`
- **WebUI:** `http://localhost:30075`
- **Catalog API:** `http://localhost:30010`
- **Basket API:** `http://localhost:30011`
- **Discount API:** `http://localhost:30012`

---

## 6. Önemli Notlar
- **Docker Desktop:** Kubernetes özelliğinin aktif olduğundan emin olun.
- K8s API'leri altyapı servislerine erişirken `host.docker.internal` adresini kullanır. Ortam değişkenleri `k8s/*.yaml` dosyalarında ayarlanmıştır.
