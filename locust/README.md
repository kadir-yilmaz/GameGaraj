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

Tek process ile daha düşük overhead isteyen canlı testlerde headless mod kullanılabilir:

```powershell
./run-locust.ps1 -Headless -Users 10000 -SpawnRate 20 -RunTime 30m -StartSpread 600 -PaceMultiplier 20 -SkipInstall
```

Bu mod web arayüzü açmaz ve tek Python process içinde çalışır. `PaceMultiplier` yükseldikçe aynı kullanıcı sayısı daha sakin istek üretir.

## Önerilen Canlı Senaryo

Canlı sistemde gerçek kullanıcı davranışına en yakın ve test makinesini daha az yoran seçenek:

* **Scenario:** `realistic_shopper`
* **Host:** `https://gateway.kadiryilmaz.online`
* **Users:** 100
* **Spawn rate:** 2 veya 5
* **Start spread:** 60
* **Pace multiplier:** 1

Bu senaryoda her kullanıcı:

1. Arama yapar.
2. Ürün detayına girer.
3. Farklı 3 ürünü sepete ekler.
4. Sepeti açar.
5. Sepette 2 ürünün adedini artırır.
6. 1 ürünü sepetten siler.
7. Sepeti tekrar kontrol eder.
8. Kısa bir ürün gezintisi daha yapıp bekler.

İnsan davranışı taklidi için ilk aksiyon 0-60 saniyeye yayılır ve akış içinde beklemeler vardır. `PaceMultiplier` bu beklemeleri çarpar; 10k kullanıcı ile sakin test için 15-25 aralığı iyi başlangıçtır.

Yerel makinede Locust worker CPU'su yükselirse distributed script'i 2 worker ile başlatın:

```powershell
./run-locust-distributed.ps1 -WorkerCount 2
```
