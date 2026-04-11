# GameGaraj Payment API

Iyzico entegrasyonu ile ödeme işlemlerini yöneten mikroservis.

## Özellikler

- ✅ Iyzico Sandbox entegrasyonu
- ✅ Kredi kartı ile ödeme
- ✅ Ücretsiz satın alma desteği (0 TL bypass)
- ✅ RabbitMQ event publishing
- ✅ JWT Authentication (şimdilik AllowAnonymous)

## RabbitMQ Event'leri

### Publish Edilen Event'ler

1. **PaymentCompleted** - Ödeme başarılı olduğunda
   - OrderId gönderilir
   - Order API tarafından consume edilir
   - Sipariş durumu "Completed" olur

2. **PaymentFailed** - Ödeme başarısız olduğunda
   - OrderId ve Reason gönderilir
   - Order API tarafından consume edilir
   - Sipariş durumu "Failed" olur

3. **InvoiceRequested** - Ödeme başarılı olduğunda fatura için
   - Sipariş detayları gönderilir
   - Invoice API tarafından consume edilir
   - Müşteriye email gönderilir

## API Endpoints

### POST /api/payments

Ödeme işlemi yapar.

**Request Body**:
```json
{
  "cardName": "JOHN DOE",
  "cardNumber": "5528790000000008",
  "expireMonth": "12",
  "expireYear": "2030",
  "cvv": "123",
  "totalPrice": 299.99,
  "orderId": 1,
  "customerName": "John",
  "customerSurname": "Doe",
  "customerEmail": "john@example.com",
  "customerPhone": "+905551234567",
  "customerIdentityNumber": "11111111111",
  "customerIp": "85.34.78.112",
  "addressDetail": "Nidakule Göztepe, Merdivenköy Mah. Bora Sok. No:1",
  "city": "Istanbul",
  "country": "Turkey",
  "zipCode": "34732",
  "items": [
    {
      "productName": "Gaming Mouse X1",
      "price": 299.99
    }
  ]
}
```

**Success Response** (200):
```json
{
  "success": true,
  "message": "Ödeme başarılı",
  "paymentId": "12345678"
}
```

**Error Response** (400):
```json
{
  "success": false,
  "message": "Ödeme başarısız.",
  "error": "Kart bilgileri hatalı",
  "errorCode": "5001"
}
```

## Iyzico Test Kartları

### Başarılı Test Kartları

| Kart Numarası | Açıklama |
|---------------|----------|
| 5528790000000008 | Master Card (Non-3DS) |
| 5526080000000006 | Master Card (3DS) |
| 4766620000000001 | Visa (Non-3DS) |
| 4603450000000000 | Visa (3DS) |

**Test Kart Bilgileri**:
- CVV: Herhangi 3 haneli sayı (örn: 123)
- Son Kullanma Tarihi: Gelecekteki herhangi bir tarih
- Kart Sahibi: Herhangi bir isim

### Başarısız Test Senaryoları

| Kart Numarası | Sonuç |
|---------------|-------|
| 5406670000000009 | Yetersiz bakiye |
| 4111111111111129 | Kart doğrulama hatası |

## Ücretsiz Satın Alma

TotalPrice = 0 olduğunda:
- Iyzico API'sine istek gönderilmez
- Direkt olarak PaymentCompleted event'i publish edilir
- PaymentId: "FREE-{OrderId}" formatında döner

## Konfigürasyon

### appsettings.json

```json
{
  "RabbitMQUrl": "localhost",
  "IdentityServerURL": "http://localhost:5001",
  "Iyzipay": {
    "ApiKey": "sandbox-dpqO6rwbOIPECHFjBs32VyoN5KlR6oWP",
    "SecretKey": "sandbox-0uIV2XDxyFxgxo04uQ8eyowp0rC79tUr",
    "BaseUrl": "https://sandbox-api.iyzipay.com"
  }
}
```

## Docker Compose

RabbitMQ container'ı gereklidir:

```bash
docker-compose up -d rabbitmq
```

## Çalıştırma

```bash
cd GameGaraj.Payment.API
dotnet run
```

API: http://localhost:5013
Swagger: http://localhost:5013/swagger

## Test Senaryosu

### 1. Başarılı Ödeme Testi

```bash
curl -X POST http://localhost:5013/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardName": "JOHN DOE",
    "cardNumber": "5528790000000008",
    "expireMonth": "12",
    "expireYear": "2030",
    "cvv": "123",
    "totalPrice": 299.99,
    "orderId": 1,
    "customerName": "John",
    "customerSurname": "Doe",
    "customerEmail": "john@example.com",
    "customerPhone": "+905551234567",
    "addressDetail": "Test Address",
    "city": "Istanbul",
    "country": "Turkey",
    "items": [
      {
        "productName": "Gaming Mouse",
        "price": 299.99
      }
    ]
  }'
```

**Beklenen Sonuç**:
- ✅ HTTP 200 OK
- ✅ PaymentCompleted event → Order API (Status: Completed)
- ✅ InvoiceRequested event → Invoice API (Email gönderilir)

### 2. Başarısız Ödeme Testi

```bash
# Yetersiz bakiye kartı kullan
curl -X POST http://localhost:5013/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "5406670000000009",
    ...
  }'
```

**Beklenen Sonuç**:
- ❌ HTTP 400 Bad Request
- ✅ PaymentFailed event → Order API (Status: Failed)

### 3. Ücretsiz Satın Alma Testi

```bash
curl -X POST http://localhost:5013/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "totalPrice": 0,
    "orderId": 2,
    ...
  }'
```

**Beklenen Sonuç**:
- ✅ HTTP 200 OK
- ✅ PaymentId: "FREE-2"
- ✅ PaymentCompleted event → Order API
- ✅ InvoiceRequested event → Invoice API

## Monitoring

### RabbitMQ Management UI

http://localhost:15672
- Username: guest
- Password: guest

**Kontrol Edilecek Queue'lar**:
- `payment-completed-order-service`
- `payment-failed-order-service`
- `invoice-requested-service`

### Console Logs

Payment API console'unda şu logları göreceksiniz:

```
[PaymentsController] POST ReceivePayment called.
[PaymentsController] Card: 5528790000000008, Amount: 299.99
[PaymentsController] ✅ Payment SUCCESS - PaymentId: 12345678
[PaymentsController] 📤 PaymentCompleted event published for OrderId: 1
[PaymentsController] 📧 InvoiceRequested event published for OrderId: 1
```

## Troubleshooting

### Iyzico API Hatası

**Hata**: "An error occurred while sending the request"

**Çözüm**: İnternet bağlantınızı kontrol edin. Sandbox API'ye erişim gereklidir.

### RabbitMQ Bağlantı Hatası

**Hata**: "None of the specified endpoints were reachable"

**Çözüm**:
```bash
docker-compose up -d rabbitmq
```

### Event Publish Edilmiyor

**Kontrol**:
1. RabbitMQ çalışıyor mu? → `docker ps`
2. appsettings.json'da RabbitMQUrl doğru mu?
3. Consumer'lar çalışıyor mu? (Order API, Invoice API)

## Güvenlik

- JWT Authentication aktif (şimdilik AllowAnonymous)
- Kart bilgileri loglanmıyor
- Iyzico PCI-DSS uyumlu
- HTTPS kullanımı önerilir (production)

## Production Notları

### Iyzico Production Geçişi

1. Iyzico'dan production API key alın
2. appsettings.Production.json oluşturun:

```json
{
  "Iyzipay": {
    "ApiKey": "production-api-key",
    "SecretKey": "production-secret-key",
    "BaseUrl": "https://api.iyzipay.com"
  }
}
```

3. HTTPS zorunlu hale getirin
4. JWT Authentication'ı aktif edin (AllowAnonymous kaldırın)

### Monitoring

- Application Insights ekleyin
- Payment success/failure rate izleyin
- Iyzico API response time izleyin
- RabbitMQ queue depth izleyin

## Bağımlılıklar

- GameGaraj.Shared (Events)
- MassTransit.RabbitMQ
- Iyzipay SDK
- Microsoft.AspNetCore.Authentication.JwtBearer

## Port

**5013** (HTTP)
