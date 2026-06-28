# 🎓 Metrics (Metrikler) - Bölüm 2: .NET Core Metrik Entegrasyonu ve Püf Noktaları

Bu bölümde, GameGaraj projelerinde kendi iş metriklerimizi (sipariş, sepet, ödeme vb.) .NET 8 kullanarak nasıl oluşturduğumuzu ve OpenTelemetry'ye nasıl kaydettiğimizi öğreneceğiz.

---

## 1. C# Kodunda Custom Metrik Sınıfı Oluşturma

.NET Core'da metrik oluşturmak için standart `System.Diagnostics.Metrics` kütüphanesini kullanırız. Örnek olarak `OrderMetrics.cs` sınıfımızın yapısını inceleyelim:

```csharp
using System.Diagnostics.Metrics;

public sealed class OrderMetrics
{
    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _ordersCancelled;
    private readonly Histogram<double> _orderProcessingDuration;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        // 1. Servise özel bir Meter tanımlıyoruz.
        // Bu isim OpenTelemetry'ye tanıtacağımız anahtar isimdir!
        var meter = meterFactory.Create("GameGaraj.Order");

        // 2. Counter (Sayaç) tanımları
        _ordersCreated = meter.CreateCounter<long>(
            "orders.created.total", "orders", "Total orders created");

        _ordersCancelled = meter.CreateCounter<long>(
            "orders.cancelled.total", "orders", "Total orders cancelled");

        // 3. Histogram (Süre ölçümü) tanımı
        _orderProcessingDuration = meter.CreateHistogram<double>(
            "orders.processing.duration", "ms", "Order processing duration");
    }

    // 4. Metriği arttıracak metotlarımız
    public void OrderCreated(string? userId = null)
    {
        var tags = userId != null
            ? new KeyValuePair<string, object?>("user.id", userId)
            : default;
            
        _ordersCreated.Add(1, tags); // Değeri 1 arttır
    }
}
```

---

## 2. Püf Noktası: OpenTelemetry Metrik Kaydı (Kritik Hata Sebebi)

C# kodunda bir metrik oluşturup `.Add(1)` demek tek başına yeterli değildir. OpenTelemetry SDK'sına bu oluşturduğumuz metre ismini dinlemesini söylememiz gerekir.

### Yaşadığımız "No Data" Hatası ve Çözümü:
OpenTelemetry ayarlarımızda (`OpenTelemetryConfiguration.cs`) eskiden şu satır vardı:
```csharp
metrics.AddMeter($"{serviceName}.*")
```
*   `serviceName` değeri `"GameGaraj.Order"` idi.
*   Eski kod OpenTelemetry'ye sadece `"GameGaraj.Order.*"` (sonunda nokta olan) isimleri dinlemesini söylüyordu.
*   Ancak bizim C# kodundaki metremiz doğrudan `"GameGaraj.Order"` (noktasız tam isim) idi.
*   Eşleşme sağlanamadığı için OpenTelemetry, topladığımız tüm sipariş metriklerini **sessizce çöpe atıyordu.** Grafana'da bu yüzden "No Data" görüyorduk.

### Çözüm Olarak Yapılandırmayı Şöyle Güncelledik:
```csharp
metrics
    .AddMeter(serviceName)          // GameGaraj.Order gibi tam isimli metreyi dinle
    .AddMeter($"{serviceName}.*")   // Alt metrikleri dinle
    .AddMeter("GameGaraj.*")        // GameGaraj ile başlayan her şeyi garantiye al
```

Artık OpenTelemetry SDK'sı bu isimleri başarıyla yakalar, `/metrics` endpoint'inde yayına açar ve Prometheus bu sayede veriyi toplayabilir.

---

## 3. Servis İçinde Metrik Sınıfının Çağrılması

Oluşturduğumuz metrik sınıflarını Dependency Injection (DI) aracılığıyla controller veya handler sınıflarımıza enjekte edip kullanırız.

### Program.cs Kaydı:
```csharp
builder.Services.AddSingleton<OrderMetrics>();
```

### Kullanım Örneği (Sipariş Oluşturma Controller'ı):
```csharp
public class OrderController : ControllerBase
{
    private readonly OrderMetrics _metrics;

    public OrderController(OrderMetrics metrics)
    {
        _metrics = metrics;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        // 1. Sipariş oluşturma işlemleri...
        var orderId = await _orderService.SaveAsync(dto);

        // 2. Metriği tetikle
        _metrics.OrderCreated(dto.UserId);

        return Ok(orderId);
    }
}
```
 Sitede her sipariş verildiğinde, bu kod çalışır ve Prometheus sayacını 1 arttırır. Grafana'da anında grafiklerde yükselişi görürüz!
