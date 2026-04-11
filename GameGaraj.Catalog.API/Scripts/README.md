# Database Reset Scripts

Bu klasör MongoDB veritabanını sıfırlamak için scriptler içerir.

## Kullanım

### PowerShell (Windows)
```powershell
cd GameGaraj.Catalog.API/Scripts
.\reset-database.ps1
```

### MongoDB Shell (Cross-platform)
```bash
mongosh "mongodb://localhost:27017" drop-database.js
```

## Ne Yapar?

Bu scriptler şu koleksiyonları siler:
- `products`
- `categories`
- `categoryAttributes`
- `_seed_metadata`

Catalog API'yi yeniden başlattığınızda, DatabaseSeedHelper otomatik olarak veritabanını yeniden oluşturur.

## Seed Versiyonu

Seed helper `v2.0` versiyonunu kullanır. Eğer veritabanı zaten bu versiyonla seed edilmişse, tekrar seed etmez.

Yeni bir seed zorlamak için:
1. Bu scriptlerden birini çalıştırın
2. Catalog API'yi yeniden başlatın
