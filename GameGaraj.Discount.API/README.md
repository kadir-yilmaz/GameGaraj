# GameGaraj Discount API

Bu proje, Udemy projesindeki Discount API'sinden örnek alınarak oluşturulmuştur.

## Özellikler

- PostgreSQL veritabanı kullanımı (Dapper ORM)
- JWT Bearer Authentication
- RESTful API endpoints
- Otomatik veritabanı migration

## Endpoints

- `GET /api/discounts` - Tüm indirimleri listele
- `GET /api/discounts/{id}` - ID'ye göre indirim getir
- `GET /api/discounts/user/{userId}` - Kullanıcıya özel indirimleri getir
- `GET /api/discounts/code/{code}` - Koda göre indirim getir
- `GET /api/discounts/code/{code}/user/{userId}` - Kod ve kullanıcıya göre indirim getir
- `POST /api/discounts` - Yeni indirim oluştur
- `PUT /api/discounts` - İndirimi güncelle
- `DELETE /api/discounts/{id}` - İndirimi sil

## Yapılandırma

`appsettings.json` dosyasında:
- PostgreSQL bağlantı dizesi
- IdentityServer URL'i

## Çalıştırma

```bash
dotnet restore
dotnet build
dotnet run
```

API Swagger UI: http://localhost:5165/swagger