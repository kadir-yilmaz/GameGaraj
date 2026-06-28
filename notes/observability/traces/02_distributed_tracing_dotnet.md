# 🎓 Distributed Tracing (Dağıtık İzleme) - Bölüm 2: .NET ve C# Alt Yapısı

Microsoft, OpenTelemetry standartlarını .NET runtime'ının içine birinci sınıf vatandaş (first-class citizen) olarak gömdü. Bu yüzden .NET dünyasında OpenTelemetry terimleri ile C# sınıf isimleri biraz farklılık gösterir. Öncelikle bu isim sözlüğünü oturtalım.

---

## 1. .NET ve OpenTelemetry Terim Sözlüğü

| OpenTelemetry Terimi | .NET Core Karşılığı (C# Sınıfı) | Açıklama |
| :--- | :--- | :--- |
| **Tracer** | `ActivitySource` | Span'leri oluşturan ve başlatan fabrika sınıfı. |
| **Span** | `Activity` | Tek bir iş adımını temsil eden nesne. Zaman aralığını ölçer. |
| **Span Attributes** | `Tags` | Span'e eklenen anahtar-değer (metadata) çiftleri. |
| **Span Events** | `ActivityEvent` | Span içine atılan anlık zaman damgalı küçük loglar. |

---

## 2. C# Kodunda Trace Nasıl Oluşturulur ve Yönetilir?

Hadi örnek bir iş mantığı (business logic) üzerinden gidelim. Sipariş onaylandığında fatura PDF'i üreten ve bunu diske kaydeden bir servisimiz olsun. Bu işlemleri izlenebilir hale getirelim:

```csharp
using System.Diagnostics;

public class InvoiceService
{
    // 1. Servise özel benzersiz bir ActivitySource tanımlıyoruz (Bu bizim tracer fabrikamız)
    private static readonly ActivitySource InvoiceActivitySource = 
        new("GameGaraj.Order.API.InvoiceService");

    public async Task<string> GenerateInvoiceAsync(Guid orderId, decimal totalAmount)
    {
        // 2. Bir Activity (Span) başlatıyoruz. using bloğu bittiğinde span otomatik sonlanır.
        using Activity? activity = InvoiceActivitySource.StartActivity("GenerateInvoicePdf");

        try
        {
            // 3. Span'e filtreleme ve arama için anlamlı etiketler (Tags) ekliyoruz
            activity?.SetTag("invoice.order_id", orderId.ToString());
            activity?.SetTag("invoice.total_amount", totalAmount);
            activity?.SetTag("invoice.format", "PDF");

            // 4. Span içinde önemli anlık olayları kaydetmek için Event atıyoruz (Mikro-log)
            activity?.AddEvent(new ActivityEvent("Starting PDF rendering engine"));
            
            // PDF oluşturma simülasyonu
            await Task.Delay(150); 
            
            activity?.AddEvent(new ActivityEvent("PDF render completed successfully"));

            string pdfPath = $"/invoices/{orderId}.pdf";
            
            // 5. Çıktıyı span etiketlerine yazıyoruz
            activity?.SetTag("invoice.output_path", pdfPath);

            return pdfPath;
        }
        catch (Exception ex)
        {
            // 6. Hata durumunda span'in durumunu ERROR olarak işaretliyoruz
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Hatayı tag olarak ekliyoruz
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.stacktrace", ex.StackTrace);
            
            throw;
        }
    }
}
```

---

## 3. Bizim Projede (`GameGaraj.Shared`) Observability Entegrasyonu Nasıl Çalışıyor?

Bizim projede her servisin `Program.cs` dosyasında `AddObservability` ve `UseObservability` metodunu çağırıyoruz.

`GameGaraj.Shared/Observability/OpenTelemetryConfiguration.cs` dosyasındaki yapılandırma şöyledir:

```csharp
public static WebApplicationBuilder AddObservability(
    this WebApplicationBuilder builder,
    string serviceName,
    string serviceVersion = "1.0.0")
{
    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]
                       ?? "http://192.168.1.56:4317";

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(resourceBuilder)
                // 1. .NET bileşenlerinin otomatik izlenmesini sağlıyoruz
                .AddAspNetCoreInstrumentation()          // Gelen HTTP isteklerini yakala
                .AddHttpClientInstrumentation()          // Giden HTTP isteklerini yakala
                .AddEntityFrameworkCoreInstrumentation() // EF Core SQL sorgularını yakala
                .AddSqlClientInstrumentation()           // Ham SQL bağlantılarını yakala
                .AddSource($"{serviceName}.*")           // Servise ait özel kaynakları yakala
                .AddSource("GameGaraj.*")                // Ortak kaynakları yakala
                .AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(otlpEndpoint);
                    opts.Protocol = OtlpExportProtocol.Grpc;
                });
        });
    return builder;
}
```

### Bu Yapılandırmanın Sihri:
*   **`AddAspNetCoreInstrumentation`:** WebUI veya API'lerinize gelen tüm HTTP istekleri için otomatik bir `Parent Span` açar.
*   **`AddHttpClientInstrumentation`:** Bir servis içinden `HttpClient` ile dışarıya istek attığınızda, mevcut `TraceId`'yi otomatik tespit eder ve giden HTTP istek başlığına `traceparent` olarak ekler.
*   **`AddEntityFrameworkCoreInstrumentation`:** EF Core üzerinden veritabanına sorgu atıldığında bunu yakalar, SQL sorgusunun kendisini alıp span tag'ine yazar.
