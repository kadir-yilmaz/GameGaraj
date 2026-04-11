# GameGaraj Order API

Bu proje, Udemy projesindeki Order API'sinden örnek alınarak oluşturulmuştur.

## Mimari

Clean Architecture ve CQRS pattern kullanılarak 4 katmanlı yapı:

- **Domain**: Entity'ler, Enum'lar, Business Logic
- **Infrastructure**: DbContext, EF Core, Migrations
- **Application**: Commands, Queries, Handlers, DTOs, Mapping, **RabbitMQ Consumers**
- **API**: Controllers, Program.cs, Configuration

## Teknolojiler

- Entity Framework Core (SQL Server)
- MediatR (CQRS)
- AutoMapper
- JWT Bearer Authentication
- **MassTransit + RabbitMQ** (Event-driven)

## RabbitMQ Consumers

Order API 3 event'i dinler:

### 1. ProductNameChangedConsumer
**Queue**: `product-name-changed-order-service`
**Görev**: Catalog'da ürün ismi değiştiğinde OrderItem'ları günceller (Eventual Consistency)

### 2. PaymentCompletedConsumer
**Queue**: `payment-completed-order-service`
**Görev**: Ödeme başarılı olduğunda Order.Status = Completed

### 3. PaymentFailedConsumer
**Queue**: `payment-failed-order-service`
**Görev**: Ödeme başarısız olduğunda Order.Status = Failed

## Endpoints

- `GET /api/orders/{userId}` - Kullanıcıya ait siparişleri listele
- `GET /api/orders/{userId}/owned-products` - Kullanıcının sahip olduğu ürünleri listele
- `GET /api/orders/{userId}/owns/{productId}` - Kullanıcının belirli bir ürüne sahip olup olmadığını kontrol et
- `POST /api/orders` - Yeni sipariş oluştur

## Veritabanı

SQL Server kullanılmaktadır. Docker Compose ile:

```bash
docker-compose up -d orderdb
```

Connection String: `Server=localhost,1433;Database=GameGarajOrderDb;User Id=sa;Password=Password12*;TrustServerCertificate=True`

## RabbitMQ

```bash
docker-compose up -d rabbitmq
```

RabbitMQ Management: http://localhost:15672 (guest/guest)

## Migration

Migration otomatik olarak uygulama başlangıcında çalışır. Manuel çalıştırmak için:

```bash
dotnet ef database update --project ../GameGaraj.Order.Infrastructure --startup-project .
```

## Çalıştırma

```bash
# RabbitMQ ve SQL Server'ı başlat
docker-compose up -d orderdb rabbitmq

# Order API'yi çalıştır
dotnet restore
dotnet build
dotnet run
```

API Swagger UI: http://localhost:5166/swagger

## Test

1. Sipariş oluştur (POST /api/orders)
2. Payment API'den ödeme yap
3. RabbitMQ event'leri dinle
4. Order status'ün güncellendiğini gör
5. Catalog'da ürün ismini değiştir
6. Order'daki ürün isminin güncellendiğini gör
