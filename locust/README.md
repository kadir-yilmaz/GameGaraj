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

## Önerilen Canlı Senaryo

Canlı sistemde gerçek kullanıcı davranışına en yakın ve test makinesini daha az yoran seçenek:

* **Scenario:** `realistic_shopper`
* **Host:** `https://gateway.kadiryilmaz.online`
* **Users:** 100
* **Spawn rate:** 2 veya 5
* **Start spread:** 60

Bu senaryoda her kullanıcı:

1. Arama yapar.
2. Ürün detayına girer.
3. Farklı 3 ürünü sepete ekler.
4. Sepeti açar.
5. Sepette 2 ürünün adedini artırır.
6. 1 ürünü sepetten siler.
7. Sepeti tekrar kontrol eder.
8. Kısa bir ürün gezintisi daha yapıp bekler.

İnsan davranışı taklidi için ilk aksiyon 0-60 saniyeye yayılır ve akış içinde 1-6 saniye arası beklemeler vardır. Bu yüzden aynı kullanıcı sayısında eski rastgele endpoint testine göre daha düşük ama daha gerçekçi RPS üretir.

Yerel makinede Locust worker CPU'su yükselirse distributed script'i 2 worker ile başlatın:

```powershell
./run-locust-distributed.ps1 -WorkerCount 2
```
