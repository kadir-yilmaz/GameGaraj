# Catalog API Seed System v2.0

## Özellikler

### ✅ Temiz ve Tutarlı Yapı
- Tüm kategori ve ürün ID'leri `ObjectId.GenerateNewId()` ile dinamik oluşturuluyor
- Parent-child ilişkileri Dictionary ile yönetiliyor
- Kod tekrarı minimize edildi

### ✅ Akıllı Seed Kontrolü
- `_seed_metadata` koleksiyonu ile versiyon takibi
- Eğer veritabanı zaten seed edilmişse, tekrar seed etmez
- Seed versiyonu değiştirildiğinde otomatik yeniden seed

### ✅ Atomic Operations
- Tüm koleksiyonlar önce tamamen siliniyor
- Sonra sıfırdan oluşturuluyor
- Yarım kalmış seed durumu yok

### ✅ Kategori Hiyerarşisi
```
Level 1 (Main Categories)
├── Bilgisayar Parçaları
│   ├── İşlemci
│   ├── Ekran Kartı
│   ├── RAM
│   ├── Anakart
│   ├── Kasa
│   └── Güç Kaynağı
├── Çevre Birimleri
│   ├── Klavye
│   ├── Mouse
│   ├── Kulaklık
│   ├── Monitör
│   └── Webcam
└── Laptop
    ├── Gaming Laptop
    ├── İş Laptopu
    └── Öğrenci Laptopu
```

## Nasıl Çalışır?

### 1. Startup (Program.cs)
```csharp
await DatabaseSeedHelper.SeedAsync(app.Services);
```

### 2. Seed Kontrolü
- `_seed_metadata` koleksiyonunda `v2.0` versiyonu var mı?
- Varsa: "Already seeded, skipping"
- Yoksa: Tüm koleksiyonları sil ve yeniden oluştur

### 3. Seed Sırası
1. **DropAllCollectionsAsync()** - Tüm koleksiyonları sil
2. **SeedCategoriesAsync()** - Kategori hiyerarşisi oluştur
3. **SeedCategoryAttributesAsync()** - Her kategoriye attribute'lar ekle
4. **SeedProductsAsync()** - Ürünleri oluştur
5. **Save Metadata** - Seed versiyonunu kaydet

## Ürün Oluşturma

Her ürün için helper metod:
```csharp
CreateProduct(
    name: "AMD Ryzen 7 7800X3D",
    description: "3D V-Cache teknolojisi...",
    price: 12500,
    stock: 25,
    categoryId: categoryIds["islemci"],
    isFeatured: true,
    specs: new() { ["marka"] = "AMD", ["soket"] = "AM5" },
    createdAt: now
)
```

## Category Filtreleme

ProductService'de:
```csharp
// 1. Get all descendant categories (recursive)
var allCategoryIds = await GetCategoryDescendants(objectId);

// 2. Add parent category itself
allCategoryIds.Add(objectId);

// 3. Filter products
query = query.Where(p => allCategoryIds.Contains(p.CategoryId));
```

Bu sayede:
- "Bilgisayar Parçaları" seçildiğinde → İşlemci, Ekran Kartı, RAM vb. tüm alt kategorilerdeki ürünler gelir
- "İşlemci" seçildiğinde → Sadece işlemci kategorisindeki ürünler gelir

## Database Reset

### Development Endpoint (Önerilen)
```bash
POST http://localhost:5001/api/dev/reset-database
```

### Manuel Reset
1. Tüm koleksiyonları sil
2. API'yi yeniden başlat
3. Seed otomatik çalışır

## Seed Versiyonunu Güncelleme

`DatabaseSeedHelper.cs` içinde:
```csharp
var seedVersion = "v2.1"; // Increment this to force re-seed
```

Versiyon değiştirildiğinde, API başlatıldığında otomatik olarak yeniden seed edilir.

## Veri İstatistikleri

- **Ana Kategoriler**: 3
- **Alt Kategoriler**: 15
- **Toplam Kategori**: 18
- **Ürünler**: ~45
- **Category Attributes**: ~20
- **Featured Products**: ~5

## Sorun Giderme

### Ürünler gelmiyor
1. MongoDB çalışıyor mu? → `mongosh` ile test et
2. Connection string doğru mu? → `appsettings.json` kontrol et
3. Seed başarılı oldu mu? → Console loglarına bak

### Category filtreleme çalışmıyor
1. CategoryId doğru mu? → Browser URL'sini kontrol et
2. Parent-child ilişkisi doğru mu? → MongoDB Compass'ta kontrol et
3. GetCategoryDescendants çalışıyor mu? → Loglara bak

### Seed tekrar çalışmıyor
1. `_seed_metadata` koleksiyonunu sil
2. API'yi yeniden başlat
