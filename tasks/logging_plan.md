# GameGaraj Merkezi Loglama Mimarisi ve İş Listesi (Serilog + Elasticsearch + Console)

Bu dokümanda, GameGaraj mikroservis ekosistemine entegre edilecek merkezi loglama yapısının mimarisi, teknik tasarımı ve uygulanacak adımların iş listesi yer almaktadır.

---

## 1. Mimari Tasarım

Sistemde halihazırda PostgreSQL, Redis, MSSQL gibi servislerin yanında **Elasticsearch** (`192.168.1.56:9201`) aktif olarak çalışmaktadır. Bu altyapıyı kullanarak merkezi loglama mimarisini aşağıdaki gibi kuracağız:

```
[Mikroservisler & Gateway & WebUI]
         │
         │ (Serilog Asenkron Log Akışı)
         ▼
 ┌───────────────┬─────────────────┬──────────────┐
 │               │                 │              │
 ▼               ▼                 ▼              ▼
[Console]     [File Sinks]    [Elasticsearch]  [Kibana / Dashboard]
(kubectl logs) (Yerel Dosya)   (gamegaraj-logs) (Görsel Analiz)
```

### Log Hedefleri (Sinks):
1. **Console Sink:** Kubernetes üzerinde `kubectl logs -f` ile anlık log izleme için standart konsol çıktısı.
2. **File Sink (Rolling File):** Yerel hata ayıklama ve yedekleme amacıyla günlük olarak dönen (`ConsoleLogs/serilog-servisname-yyyyMMdd.txt`) yapı.
3. **Elasticsearch Sink:** Tüm servislerden gelen logların indekslenerek merkezi olarak aratılabilmesi için ES veri tabanına doğrudan asenkron gönderim.

---

## 2. Teknik Tasarım ve Ortak Yapı

Loglama mantığını her servise tek tek yazmak yerine, **[GameGaraj.Shared](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.Shared)** ortak kütüphanesine taşıyacağız.

### A. Gerekli NuGet Paketleri (Shared Projesine Eklenecek):
- `Serilog.AspNetCore` (Serilog entegrasyonu ve request logging için)
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`
- `Serilog.Sinks.Elasticsearch`
- `Serilog.Exceptions` (Exception detaylarını otomatik ayıklamak için)

### B. Serilog Yapılandırma Metodu:
`GameGaraj.Shared` projesinde `SerilogConfiguration.cs` adında bir sınıf oluşturularak tüm servislerin tek satırda çağırabileceği bir extension metot (`builder.AddSerilogLogging("ServisAdi")`) yazılacaktır.

### C. İstek Loglama & Kullanıcı Bilgisi (Request Logging):
HTTP isteklerini loglarken, istek yapan kullanıcının kim olduğu otomatik olarak log context'ine eklenecektir:
- **Kullanıcı Login ise:** E-posta adresi veya `sub` ID'si (`UserIdentity` olarak).
- **Ziyaretçi ise (Gateway X-User-Id header'ı varsa):** `Guest-<UserId>` olarak.
- **Anonim ise:** `Anonymous` olarak.

---

## 3. Yapılacak İşler Listesi (Tasks)

### Adım 1: Shared Projesinin Güncellenmesi
- [ ] [GameGaraj.Shared.csproj](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.Shared/GameGaraj.Shared.csproj) dosyasına gerekli Serilog paketlerini ekle.
- [ ] [NEW] `GameGaraj.Shared/Logging/SerilogConfiguration.cs` sınıfını oluştur ve Serilog extension metodunu yaz.
- [ ] [NEW] `GameGaraj.Shared/Logging/SerilogRequestLoggingExtensions.cs` sınıfını oluştur ve isteklerin loglanması sırasında kullanıcı bilgilerini (`UserIdentity`, `UserAgent` vb.) diagnostic context'e ekleyen middleware sarmalayıcısını kodla.
- [ ] Eski ve senkron olarak dosyayı kilitleyen `FileLogger.cs` kodlarını temizle.

### Adım 2: Gateway ve Mikroservislerin Entegrasyonu
Her servisin (toplam 10 servis) `Program.cs` dosyasını güncelle:
- [ ] **API Gateway:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **WebUI:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Catalog API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Basket API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Discount API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Order API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Payment API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Invoice API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **Campaign API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.
- [ ] **PhotoStock API:** `Program.cs`'e Serilog'u ekle ve request logging'i aktif et.

### Adım 3: CI/CD ve Test
- [ ] Değişiklikleri push et ve K3s üzerinde deploy sürecini izle.
- [ ] Kubernetes loglarını izleyerek Serilog ve Elasticsearch entegrasyonunun sorunsuz başladığını doğrula.
