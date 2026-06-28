# 🎓 Structured Logging (Yapılandırılmış Günlükleme) - Bölüm 3: Canlıda Dinamik Log Seviyesi Kontrolü

**Problem:** Canlı ortamda disk ve CPU sağlığı için varsayılan olarak sadece `Information` ve üzeri seviyedeki logları yazarız. Ancak sepet güncellenirken nadir görülen kritik bir hata oluşuyor. Hatayı yakalamak için geçici olarak `Debug` seviyesinde log yazmamız gerekiyor ama uygulamayı kapatıp açmak veya tekrar deploy etmek istemiyoruz.

Uygulamayı yeniden başlatmadan canlı ortamda log seviyesini nasıl değiştiririz?

---

## 1. Dinamik Log Seviyesi Özelliği Nasıl Çalışır?

GameGaraj projesinde geliştirdiğimiz sistem sayesinde, her mikroservisin içinde Serilog'un `LoggingLevelSwitch` sınıfını barındırıyoruz. 
Ayrıca admin panelinden tetiklenebilen bir API endpoint'i bulunuyor:
*   `/api/observability/log-level`

### Değişiklik İsteği Gönderme (API):
Bir mikroservise (örneğin `Basket.API`'ye) şu şekilde bir `PUT` isteği gönderilir:

```http
PUT http://<k3s-node-ip>:<node-port>/api/observability/log-level
Content-Type: application/json

{
  "level": "Debug",
  "durationMinutes": 15,
  "reason": "Debugging rare basket update issue",
  "changedBy": "Kadir YILMAZ"
}
```

### Arka Plan Süreci:
1.  **Log Seviyesi Yükseltilir:** Servis isteği alır almaz loglama seviyesini anında `Information`'dan `Debug` seviyesine çeker. Artık en ufak SQL veya kod detayı dahi Elasticsearch'e yazılmaya başlar.
2.  **Zamanlayıcı (Timer) Başlatılır:** Servis hafızasında `durationMinutes` (örneğin 15 dakika) kadar sürecek bir arka plan görevi başlatır.
3.  **Audit Log Yazılır:** Kimin, hangi sebeple log seviyesini değiştirdiği güvenlik ve takip amacıyla loglanır.
4.  **Otomatik Geri Dönüş:** 15 dakika süre dolduğunda, arka plan görevi tetiklenir ve log seviyesini otomatik olarak güvenli varsayılan değer olan `Information` seviyesine geri çeker. Böylece diskin dolması veya sistemin yorulması engellenir.

---

## 2. Admin Panelinden Kullanım

Bu işlemi yapmak için Curl veya Postman kullanmana gerek yoktur.
*   Admin paneline girip **Gözlemlenebilirlik ➔ Log Ayarları** sayfasına gidin.
*   Listeden hedef servisi seçin (örn: `Catalog.API`).
*   İstediğiniz yeni log seviyesini (`Debug`, `Warning`, `Error` vb.) ve süreyi seçip **"Güncelle"** butonuna basın.
*   Sistem arka planda bu API çağrısını yapacak ve değişikliği anında devreye alacaktır.
