# 🎓 Structured Logging (Yapılandırılmış Günlükleme) - Bölüm 1: Serilog ve Elasticsearch

Geleneksel loglama düz metinler yazar:
`[2026-06-28 19:40] [INFO] User 12 bought product 5 for 150000 TL.`
Düz metin logları aramak zordur. Bu logu Elasticsearch'e atarsan kelimeleri indeksler ama "Fiyatı 100.000 TL'den büyük olan satın alımları getir" sorgusunu atamazsın.

**Structured Logging (Yapılandırılmış Loglama)** ise logu bir JSON objesi gibi kaydeder:
```json
{
  "Timestamp": "2026-06-28T19:40:00Z",
  "Level": "Information",
  "MessageTemplate": "User {UserId} bought product {ProductId} for {Price} TL.",
  "Properties": {
    "UserId": 12,
    "ProductId": 5,
    "Price": 150000.00,
    "LogType": "BusinessTransaction",
    "TraceId": "666f7ad052b44d492f93f1"
  }
}
```
Artık Elasticsearch üzerinde `Properties.Price > 100000` şeklinde mükemmel filtrelemeler yapabilirsin!

---

## 1. GameGaraj Log Yapısı ve İndeks Ayrımı

GameGaraj sisteminde çok fazla log birikir. Performans ve arama kolaylığı için logları **iki farklı Elasticsearch indeksine** böldük:

### A. `gamegaraj-logs-*` (Sistem ve Operasyonel Loglar)
*   **İçerik:** Uygulama hataları (Error), veritabanı sorgu detayları, servislerin çalışma durumları (Information), RabbitMQ kuyruk bildirimleri vb.
*   **Amaç:** Sistemde ters giden bir şeyler olduğunda teknik detayları araştırmak.

### B. `gamegaraj-requests-*` (HTTP Arama ve İstek Logları)
*   **İçerik:** Sadece API Gateway ve WebUI'a gelen HTTP istek detayları.
*   **Amaç:** Kullanıcının site içindeki gezinme akışını izlemek. (Örn: Hangi sayfalara girdi, ne kadar sürdü, istek misafir kullanıcıdan mı yoksa giriş yapmış üyeden mi geldi vb.).

---

## 2. Koddaki Ayrım Nasıl Yapılıyor?

Bu ayrımı `GameGaraj.Shared/Logging/SerilogConfiguration.cs` dosyasında **`WriteTo.Conditional`** özelliğini kullanarak yaptık.

*   `SerilogRequestLoggingExtensions` sınıfında, HTTP istek loglarını yazarken bunlara otomatik olarak **`LogType = "HttpRequest"`** etiketini (tag) ekliyoruz.
*   Serilog konfigurasyonunda ise şu kuralı işletiyoruz:

```csharp
// Eğer logun içinde LogType = "HttpRequest" etiketlenmişse gamegaraj-requests indeksine yaz
.WriteTo.Conditional(
    logEvent => logEvent.Properties.TryGetValue("LogType", out var typeVal) 
                && typeVal.ToString().Contains("HttpRequest"),
    writeTo => writeTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
    {
        IndexFormat = "gamegaraj-requests-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = false
    })
)
// Geri kalan tüm operasyonel logları gamegaraj-logs indeksine yaz
.WriteTo.Conditional(
    logEvent => !(logEvent.Properties.TryGetValue("LogType", out var typeVal) 
                  && typeVal.ToString().Contains("HttpRequest")),
    writeTo => writeTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
    {
        IndexFormat = "gamegaraj-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = false
    })
);
```

Bu sayede, gereksiz metrik logları veya sistem hataları arama loglarımızı kirletmez. Sadece HTTP isteklerini temiz bir indeks üzerinden analiz edebiliriz.
