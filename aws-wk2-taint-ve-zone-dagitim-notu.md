# `aws-wk2` İzolasyon (Taint) ve Zone Tabanlı Pod Dağıtım Notu

**Tarih:** 26.11.2026

---

## 1. `aws-wk2` Kurulumu ve A/B Testi Sonucu
- **Sunucu:** AWS `m7i-flex.large` (8 GB RAM, 15 GB disk), **`Ubuntu 24.04.5 LTS`**, Tailscale IP: `100.79.150.26`.
- **A/B Testi:** `aws-wk1` (`Ubuntu 26.04.1`) üzerinde `Exit 139` (`SIGSEGV`) veren servislerin tamamı, `aws-wk2` (`Ubuntu 24.04.5 LTS`) üzerinde **0 hata** ile yeşil (`1/1 Running`) çalıştı. Ardından `aws-wk1` cluster'dan temizlendi.

---

## 2. `aws-wk2`'yi Sadece `gamegaraj` Podlarına Ayırma (Taint & Labels)
**Uygulanan Komutlar:**
```bash
sudo k3s kubectl label node aws-wk2 gamegaraj-node=worker topology.kubernetes.io/zone=aws --overwrite
sudo k3s kubectl label node kadirserver topology.kubernetes.io/zone=home --overwrite
sudo k3s kubectl taint nodes aws-wk2 gamegaraj=only:NoSchedule --overwrite
```

- **`gamegaraj=only:NoSchedule` taint'i:** `argocd`, `headlamp`, `cloudflared`, `prometheus`, `alertmanager` gibi sistem podlarının `aws-wk2`'ye düşmesini engeller (sadece `node-exporter` DaemonSet'i hariç).
- **`helm/gamegaraj/values.yaml`:** Zaten `gamegaraj=only:NoSchedule` toleration'ına ve `gamegaraj-node: "worker"` seçicisine sahip olduğu için `gamegaraj` podları `aws-wk2`'de çalışabilir.

---

## 3. Her Servisten `aws-wk2`'ye Tam 1 Pod (`home`: 2, `aws`: 1) Dağıtımı
[helm/gamegaraj/templates/microservice.yaml](file:///c:/Users/kadir/OneDrive/Desktop/Projeler/GameGaraj/helm/gamegaraj/templates/microservice.yaml) dosyasına eklenen kurallar:

1. **`topologySpreadConstraints` (`topology.kubernetes.io/zone`, `maxSkew: 1`):**
   - Her servisin `3` replikasını `home` ve `aws` bölgeleri arasında en fazla 1 farkla böler (`2` + `1`).
2. **`nodeAffinity` (`preferred`: `topology.kubernetes.io/zone: home`, `weight: 100`):**
   - Çoğunluk (`2` pod) **`home`** (`kadirserver` + ev worker'ları) bölgesinde kalır, **`aws-wk2`** ise 13 servisin her birinden **tam 1'er pod (toplam 13 pod)** alır.
3. **`trafficDistribution: PreferClose`:**
   - İstekler öncelikle aynı zone içindeki podlara yönlenir (evdeki trafik `<1 ms` hızında kalır; sadece zone'da pod yoksa diğer zone'a geçer).
