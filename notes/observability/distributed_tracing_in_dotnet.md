# 🎓 Distributed Tracing (Dağıtık İzleme) Masterclass - Bölüm 2: .NET ve C# Alt Yapısı

Kadir, birinci bölümde teoriyi hallettik. Şimdi işin mutfağına, yani **.NET Core** tarafına inelim. 

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

### Bu kod çalışınca Jaeger'da ne olur?
*   Jaeger arayüzünde **`GenerateInvoicePdf`** adında bir kutucuk oluşur.
*   Bu kutucuğun süresi tam olarak `Task.Delay(150)` süresi kadar (~150ms) görünür.
*   Üzerine tıkladığında sağ panelde `invoice.order_id`, `invoice.total_amount` ve `invoice.output_path` gibi etiketleri görürsün.
*   Eğer kod hata alırsa kutucuk **kırmızı** renge boyanır ve hatanın detayı içine yazılır.

---

## 3. Bizim Projede (`GameGaraj.Shared`) Observability Entegrasyonu Nasıl Çalışıyor?

Bizim projede her servisin `Program.cs` dosyasında `AddObservability` ve `UseObservability` metodunu çağırıyoruz. Peki bu metotların arkasında ne dönüyor?

`GameGaraj.Shared/Observability/ObservabilityExtensions.cs` dosyasını açıp baktığında şuna benzer bir yapılandırma görürsün:

```csharp
public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)
{
    services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                // 1. .NET bileşenlerinin otomatik izlenmesini sağlıyoruz
                .AddAspNetCoreInstrumentation()   // Gelen HTTP isteklerini yakala
                .AddHttpClientInstrumentation()   // Giden HTTP (HttpClient) isteklerini yakala
                .AddEntityFrameworkCoreInstrumentation() // EF Core SQL sorgularını yakala
                .AddSqlClientInstrumentation()    // Ham SQL bağlantılarını yakala
                // 2. Kendi yazdığımız özel ActivitySource'ları dinleme listesine ekliyoruz
                .AddSource("GameGaraj.*") 
                // 3. Verileri OTLP (OpenTelemetry Protocol) üzerinden Jaeger'a yolla
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(configuration["OpenTelemetry:OtlpEndpoint"] 
                        ?? "http://192.168.1.56:4317");
                });
        });

    return services;
}
```

### Bu Yapılandırmanın Sihri:
*   **`AddAspNetCoreInstrumentation`:** WebUI veya API'lerinize gelen tüm HTTP istekleri için otomatik bir `Parent Span` açar.
*   **`AddHttpClientInstrumentation`:** Bir servis içinden `HttpClient` ile dışarıya (örneğin Gateway'e veya başka API'ye) istek attığınızda, mevcut `TraceId`'yi otomatik tespit eder ve giden HTTP istek başlığına `traceparent` olarak ekler.
*   **`AddEntityFrameworkCoreInstrumentation`:** Entity Framework Core üzerinden veritabanına sorgu atıldığında bunu yakalar, SQL sorgusunun kendisini alıp span tag'ine yazar ve Jaeger'a yollar.

Bir sonraki bölümde, **yaşayabileceğimiz gerçek dünya problemlerini Jaeger ve dağıtık izleme kullanarak nasıl çözeceğimizi** göreceğiz. Hazırsan [Bölüm 3: Gerçek Dünya Senaryoları ve Hata Çözümü](file:///d:/Kadir/Projeler/GameGaraj/notes/observability/distributed_tracing_real_world.md) dosyasına geçelim.
