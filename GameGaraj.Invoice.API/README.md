# GameGaraj Invoice API

RabbitMQ ile event-driven fatura email servisi.

## Özellikler

- **RabbitMQ Consumer**: InvoiceRequested event'ini dinler
- **Email Service**: SMTP ile fatura emaili gönderir
- **MassTransit**: RabbitMQ entegrasyonu
- **HTML Email Template**: Profesyonel fatura tasarımı

## Event Flow

1. Payment API ödeme başarılı olduğunda `InvoiceRequested` event'i publish eder
2. Invoice API bu event'i consume eder
3. Email servisi fatura emailini müşteriye gönderir

## Yapılandırma

`appsettings.json` dosyasında:
- RabbitMQ URL
- SMTP ayarları (Gmail, Outlook, vb.)

## Gmail SMTP Ayarları

Gmail kullanmak için:
1. Google hesabınızda 2FA'yı aktifleştirin
2. App Password oluşturun
3. appsettings.json'da SmtpUsername ve SmtpPassword'u güncelleyin

## Çalıştırma

```bash
# RabbitMQ'yu başlat
docker-compose up -d rabbitmq

# Invoice API'yi çalıştır
dotnet restore
dotnet build
dotnet run
```

API: http://localhost:5014
RabbitMQ Management: http://localhost:15672 (guest/guest)

## Test

Payment API'den ödeme yapıldığında otomatik olarak fatura emaili gönderilir.
