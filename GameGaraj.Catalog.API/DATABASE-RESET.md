# Database Reset Guide

## Hızlı Reset (Önerilen)

Catalog API çalışırken, tarayıcıdan veya Postman'den:

```
POST http://localhost:5001/api/dev/reset-database
```

Sonra Catalog API'yi yeniden başlatın.

## Alternatif Yöntemler

### 1. PowerShell Script
```powershell
cd Scripts
.\reset-database.ps1
```

### 2. MongoDB Shell
```bash
mongosh "mongodb://localhost:27017" Scripts/drop-database.js
```

### 3. MongoDB Compass
1. MongoDB Compass'ı aç
2. `catalogdb` veritabanına bağlan
3. Şu koleksiyonları sil:
   - products
   - categories
   - categoryAttributes
   - _seed_metadata

## Seed Sistemi

DatabaseSeedHelper otomatik olarak:
- Veritabanının seed edilip edilmediğini kontrol eder
- Eğer seed versiyonu (`v2.0`) mevcut değilse, tüm koleksiyonları siler ve yeniden oluşturur
- Tutarlı ObjectId'ler ve parent-child ilişkileri ile kategori hiyerarşisi oluşturur
- Her kategoriye uygun attribute'lar ekler
- Gerçekçi ürün verileri ile veritabanını doldurur

## Kategori Yapısı

```
Bilgisayar Parçaları
├── İşlemci
├── Ekran Kartı
├── RAM
├── Anakart
├── Kasa
└── Güç Kaynağı

Çevre Birimleri
├── Klavye
├── Mouse
├── Kulaklık
├── Monitör
└── Webcam

Laptop
├── Gaming Laptop
├── İş Laptopu
└── Öğrenci Laptopu
```

## Toplam Veri

- **Kategoriler**: 18 (3 ana + 15 alt)
- **Ürünler**: ~45
- **Attribute'lar**: ~20

## Sorun Giderme

Eğer seed çalışmazsa:
1. MongoDB'nin çalıştığından emin olun
2. `appsettings.json`'da connection string'i kontrol edin
3. Tüm koleksiyonları manuel olarak silin
4. API'yi yeniden başlatın
