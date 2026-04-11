# 🎯 Catalog API - Yeni Seed Sistemi (v2.0)

## ✅ Yapılan İyileştirmeler

### 1. Temiz ve Modüler Kod Yapısı
- **Önceki**: 300+ satır tek blok halinde seed kodu
- **Şimdi**: Modüler metodlar ile ayrılmış, okunabilir kod
  - `SeedCategoriesAsync()` - Kategori hiyerarşisi
  - `SeedCategoryAttributesAsync()` - Attribute'lar
  - `SeedProductsAsync()` - Ürünler
  - `CreateProduct()` - Helper metod

### 2. Akıllı Seed Yönetimi
- **Versiyon Kontrolü**: `_seed_metadata` koleksiyonu ile takip
- **Atomic Operations**: Tüm koleksiyonlar önce siliniyor, sonra oluşturuluyor
- **Idempotent**: Aynı versiyon tekrar seed edilmez

### 3. Tutarlı Parent-Child İlişkileri
- **Dictionary Tabanlı**: `categoryIds["islemci"]` şeklinde erişim
- **Dinamik ObjectId**: Her seed'de yeni ID'ler oluşturuluyor
- **Doğru İlişkiler**: ParentId'ler tutarlı şekilde atanıyor

### 4. Gerçekçi Veri
- **45+ Ürün**: Her kategoride yeterli ürün
- **18 Kategori**: 3 ana + 15 alt kategori
- **20+ Attribute**: Her kategoriye özel filtreler
- **Featured Products**: Öne çıkan ürünler işaretli

## 🚀 Kullanım

### Veritabanını Sıfırlama

#### Yöntem 1: API Endpoint (Önerilen)
```bash
# Catalog API çalışırken
curl -X POST http://localhost:5001/api/dev/reset-database

# Sonra API'yi yeniden başlat
```

#### Yöntem 2: PowerShell Script
```powershell
cd Scripts
.\reset-database.ps1
```

#### Yöntem 3: Manuel
1. MongoDB Compass'ta `catalogdb` veritabanını aç
2. Şu koleksiyonları sil:
   - products
   - categories
   - categoryAttributes
   - _seed_metadata
3. Catalog API'yi yeniden başlat

### Seed Versiyonunu Değiştirme

`DatabaseSeedHelper.cs` içinde:
```csharp
var seedVersion = "v2.1"; // Increment to force re-seed
```

## 📊 Veri Yapısı

### Kategori Hiyerarşisi
```
Bilgisayar Parçaları (6 alt kategori)
├── İşlemci (5 ürün)
├── Ekran Kartı (5 ürün)
├── RAM (5 ürün)
├── Anakart
├── Kasa
└── Güç Kaynağı

Çevre Birimleri (5 alt kategori)
├── Klavye (5 ürün)
├── Mouse (5 ürün)
├── Kulaklık (5 ürün)
├── Monitör
└── Webcam

Laptop (3 alt kategori)
├── Gaming Laptop (5 ürün)
├── İş Laptopu (5 ürün)
└── Öğrenci Laptopu (5 ürün)
```

### Örnek Ürün
```csharp
{
  "name": "AMD Ryzen 7 7800X3D",
  "description": "3D V-Cache teknolojisi ile gaming performansında lider",
  "price": 12500,
  "stock": 25,
  "categoryId": ObjectId("..."),
  "isFeatured": true,
  "specs": {
    "marka": "AMD",
    "soket": "AM5",
    "cekirdek_sayisi": "8"
  }
}
```

## 🔧 Sorun Giderme

### "Already seeded" mesajı alıyorum ama ürünler yok
```bash
# Metadata'yı sil
POST http://localhost:5001/api/dev/reset-database

# API'yi yeniden başlat
```

### Category filtreleme çalışmıyor
1. **URL'i kontrol et**: `/Product?categoryId=XXX` (doğru)
2. **ObjectId formatı**: 24 karakter hex string olmalı
3. **Parent-child ilişkisi**: MongoDB Compass'ta kontrol et

### Build hatası alıyorum
```bash
# Clean build
dotnet clean
dotnet build --no-incremental
```

## 📝 Değişiklik Listesi

### DatabaseSeedHelper.cs
- ✅ Modüler metod yapısı
- ✅ Dictionary ile kategori ID yönetimi
- ✅ Versiyon kontrolü
- ✅ Atomic drop operations
- ✅ Helper metodlar

### ProductService.cs
- ✅ Basitleştirilmiş category filtreleme
- ✅ Gereksiz if-else kaldırıldı
- ✅ Tutarlı Contains kullanımı

### Program.cs
- ✅ Development endpoint eklendi
- ✅ `/api/dev/reset-database` endpoint'i

## 🎉 Sonuç

Artık:
- ✅ Veritabanı tutarlı şekilde seed ediliyor
- ✅ Category filtreleme doğru çalışıyor
- ✅ Parent-child ilişkileri düzgün
- ✅ Kod temiz ve bakımı kolay
- ✅ Reset işlemi basit

**Catalog API'yi yeniden başlat ve test et!**
