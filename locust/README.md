# GameGaraj Locust Load Tests

Bu klasör, GameGaraj mikroservisleri için modüler ve yüksek performanslı yük testlerini içerir.

## Mimari
* **`locustfile.py`**: Ana giriş noktasıdır. Arayüz konfigürasyonlarını ve ana `User` sınıfını içerir.
* **`common/setup.py`**: Test başlamadan önce Catalog API'den gerçek ürünleri çeker ve RAM'de önbellekler.
* **`common/api_tasks.py`**: Locust sanal kullanıcılarının (VU) atacağı isteklerin kodlandığı görev fonksiyonlarıdır.
* **`scenarios/weights.py`**: Arayüzden seçilebilen test senaryolarının (`standard`, `black_friday`, `heavy_basket`) işlem ağırlıklarını (olasılıklarını) tanımlar.

## Çalıştırma
Locust klasörü içerisinde şu betiği çalıştırın:
```powershell
./run-locust.ps1
```
Sonrasında tarayıcınızdan `http://localhost:8089` adresine girerek senaryoyu, kullanıcı sayısını ve hedef gateway adresinizi girip testi ateşleyebilirsiniz.
