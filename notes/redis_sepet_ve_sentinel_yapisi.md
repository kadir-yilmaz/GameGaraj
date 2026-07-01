# Redis Sentinel & Sepet Senkronizasyon (Sync) Yapısı

Bu doküman, GameGaraj projesinde kullanılan yüksek erişilebilir (High Availability) Redis Sentinel kümesini, sepet veri yapısını, port yönlendirmelerini ve misafir-üye sepet birleştirme (Sync) mekanizmasının işleyişini kod referanslarıyla birlikte açıklamaktadır.

---

## 1. Redis Sentinel & Master-Replica Altyapısı

Proje genelinde sepet ve favori verileri, Redis üzerinde tutulmaktadır. Docker Compose ortamındaki dağıtım mimarisi ve port eşleşmeleri şu şekildedir:

### Düğüm Rolleri ve Port Eşleşmeleri
Detaylı tanımlamaları [docker-compose.yml](file:///d:/Kadir/Projeler/GameGaraj/docker-compose.yml#L16-L119) dosyasında inceleyebilirsiniz:

| Container Adı | Rolü | İç Port | Dış Port (Host) | Görevi |
| :--- | :--- | :--- | :--- | :--- |
| `gamegaraj-redis-master` | **Master** | 6379 | **6380** | Okuma ve yazma işlemlerinin yapıldığı ana veritabanı. |
| `gamegaraj-redis-replica-1` | **Replica** | 6379 | **6381** | Master'ı anlık olarak kopyalayan salt-okunur (read-only) kopya. |
| `gamegaraj-redis-replica-2` | **Replica** | 6379 | **6382** | Master'ı anlık olarak kopyalayan ikinci salt-okunur kopya. |
| `gamegaraj-redis-sentinel-1` | **Sentinel 1** | 26379 | **26379** | Sağlık izleyicisi ve karar verici (Quorum oylamasına katılır). |
| `gamegaraj-redis-sentinel-2` | **Sentinel 2** | 26379 | **26380** | Sağlık izleyicisi ve karar verici (Quorum oylamasına katılır). |
| `gamegaraj-redis-sentinel-3` | **Sentinel 3** | 26379 | **26381** | Sağlık izleyicisi ve karar verici (Quorum oylamasına katılır). |

### Failover (Otomatik Lider Değişimi) ve Oylama Süreci
1. **İzleme:** Sentinel'ler ([sentinel1.conf](file:///d:/Kadir/Projeler/GameGaraj/config/redis/sentinel/sentinel1.conf)) yapılandırmasına göre 5 saniyede bir Master'a ping atar.
2. **Oylama (Quorum = 2):** Master çöktüğünde en az 2 Sentinel çökmeyi onaylarsa sistem failover başlatır.
3. **Promote (Terfi):** Sentinel'ler replikalardan birini (örneğin `redis-replica-1`) yeni Master olarak seçer. Düğümün portu değişmez (içte 6379, dışta 6381 kalır), ancak Sentinel rolünü Master olarak günceller ve veri yazma kilidini açar.
4. **Eski Master'ın Durumu:** Çöken eski master (`redis-master`) tekrar ayağa kalktığında, Sentinel'ler tarafından otomatik olarak replica konumuna çekilir ve yeni master'dan veri senkronize etmeye başlar.

---

## 2. Sepet Veri Yapısı (Redis HASH)

`Microsoft.Extensions.Caching.StackExchangeRedis` paketi (`IDistributedCache` aracılığıyla) Redis'teki verileri **HASH** tipinde saklar.

* **Anahtar (Key):** Kullanıcının ID bilgisi (Giriş yaptıysa Keycloak `UserId` GUID'si, misafir ise `guest-[GUID]` stringi).
* **Alan (Field):** Standart olarak `data` alanında binary/JSON formatında tutulur.
* **Değer (Value - JSON formatı):**
  ```json
  {
    "UserId": "96d2cfe3-00a5-4a23-80e5-4e7413e465d6",
    "Items": [
      {
        "Id": "187cf495-ccdc-4ce1-a3c3-57aead692481",
        "Name": "Beyerdynamic MMX 300",
        "ProductSlug": "beyerdynamic-mmx-300",
        "CategoryId": "c6f6f51c-73e5-497b-9c65-b588b188b488",
        "Price": 9500,
        "PictureUrl": "https://images.unsplash.com/...jpg",
        "Quantity": 1
      }
    ],
    "TotalPrice": 9500
  }
  ```

---

## 3. Sepet Senkronizasyon (Sync) Mekanizması

Kullanıcı siteye giriş yapmadan önce sepetine ürün ekleyebilir. Giriş yaptığı anda, tarayıcıdaki misafir sepeti ile üye sepetinin birleştirilmesi gerekir.

### A. Giriş Sonrası Tetiklenme Noktaları
Senkronizasyon işlemi, kullanıcının sisteme başarıyla giriş yaptığı iki farklı noktada tetiklenir:

1. **Standart Giriş (E-posta / Şifre):** 
   [AuthController.cs:L50-54](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Controllers/AuthController.cs#L50-L54) metodunda, Keycloak doğrulaması başarılı olduktan sonra `SyncBasketAsync` tetiklenir ve başarılı olursa misafir çerezi (cookie) silinir.
   ```csharp
   if (!string.IsNullOrEmpty(guestId) && !string.IsNullOrEmpty(signInResult.UserId))
   {
       await _basketService.SyncBasketAsync(guestId, signInResult.UserId);
       HttpContext.Response.Cookies.Delete(guestCookieName);
   }
   ```

2. **Google ile Giriş (OAuth):**
   [AuthController.cs:L135-144](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Controllers/AuthController.cs#L135-L144) metodunda (Callback aşamasında) benzer şekilde senkronizasyon çalıştırılır.
   ```csharp
   if (HttpContext.Request.Cookies.TryGetValue(guestCookieName, out var guestId) && !string.IsNullOrEmpty(guestId))
   {
       var loggedInUserId = _identityService.GetUserId();
       if (!string.IsNullOrEmpty(loggedInUserId) && loggedInUserId != "anonymous-user")
       {
           await _basketService.SyncBasketAsync(guestId, loggedInUserId);
           HttpContext.Response.Cookies.Delete(guestCookieName);
       }
   }
   ```

---

### B. Senkronizasyon Akışı ve Kod Detayı (`SyncBasketAsync`)

Birleştirme mantığı [BasketService.cs:L174-275](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Services/Concrete/BasketService.cs#L174-L275) metodu içerisinde asenkron olarak gerçekleştirilir. İşlem adımları sırasıyla şöyledir:

1. **Misafir Sepetini Çekme:** `X-User-Id` başlığına (header) `guestId` değeri yazılarak misafir sepeti API'den istenir. Eğer misafir sepeti boşsa işlem sonlandırılır.
2. **Üye Sepetini Çekme:** Benzer şekilde `X-User-Id` başlığına giriş yapmış üyenin `userId` değeri yazılarak mevcut sepet çekilir (varsa).
3. **Ürünleri Birleştirme (Merge):**
   * Misafir sepetindeki ürünler tek tek dönülür.
   * Eğer aynı ürün (`Id` bazlı) üye sepetinde zaten varsa, adetler toplanır (`existingItem.Quantity += guestItem.Quantity`).
   * Eğer ürün üye sepetinde yoksa, doğrudan yeni bir eleman olarak listeye eklenir.
4. **Veriyi Kaydetme:** Birleştirilmiş yeni sepet, üye ID'si ile Basket API'ye gönderilerek Redis'te güncellenir.
5. **Eski Misafir Sepetini Silme:** Birleştirme başarılı olduğunda, eski misafir sepetini temizlemek için Basket API'ye `DELETE` isteği gönderilir ve Redis'ten kaldırılır.

```csharp
// BasketService.cs içindeki birleştirme algoritması (Özet)
foreach (var guestItem in guestApiResponse.Items)
{
    var existingItem = mergedItems.FirstOrDefault(x => x.Id == guestItem.Id);
    if (existingItem != null)
    {
        existingItem.Quantity += guestItem.Quantity; // Adet birleştirme
    }
    else
    {
        mergedItems.Add(guestItem); // Yeni ürün ekleme
    }
}
```

---

## 4. Sepet TTL (Time-To-Live) Yönetimi

Sepet verileri Redis üzerinde saklanırken, kullanıcı tipine (giriş yapmış üye veya misafir) göre farklı ömür (TTL) süreleri atanır:

* **Giriş Yapmış Üye (Logged-in User):** Sepet verisi Redis üzerinde **30 gün (1 ay)** boyunca saklanır. Kullanıcı tekrar geldiğinde sepetini kaldığı yerden bulabilir.
* **Misafir Kullanıcı (Guest User):** Sepet verisi Redis üzerinde **1 gün** boyunca saklanır. Misafir kullanıcılar sisteme üye olmadıkları için sepetlerinin uzun süre bellekte tutulması önlenerek Redis bellek kullanımı optimize edilir.

### TTL Karar Mekanizması

Bu ayrım, `IIdentityService` arayüzüne eklenen `IsGuest` özelliği aracılığıyla yapılır. Bu özellik, kullanıcının kimlik bilgisini analiz eder:

* Kullanıcı ID'si boşsa, `"anonymous-user"` fallback değerine eşitse veya WebUI'nin misafirler için ürettiği `"guest-"` ön ekiyle başlıyorsa kullanıcı **Misafir (Guest)** kabul edilir.
* Aksi halde kullanıcı **Giriş Yapmış Üye (Logged-in)** kabul edilir.

```csharp
// IdentityService.cs (Basket.API)
public bool IsGuest
{
    get
    {
        var userId = UserId;
        return string.IsNullOrEmpty(userId) || userId.StartsWith("guest-") || userId == "anonymous-user";
    }
}
```

Sepet kaydedilirken `BasketService` içerisindeki `SaveBasketAsync` metodu bu bilgiyi okuyarak uygun TTL süresini atar:

```csharp
// BasketService.cs (Basket.API)
public async Task SaveBasketAsync(Data.Basket basket, CancellationToken cancellationToken = default)
{
    basket.UserId = identityService.UserId;
    var basketString = JsonSerializer.Serialize(basket);
    
    var expiration = identityService.IsGuest ? TimeSpan.FromDays(1) : TimeSpan.FromDays(30);
    var options = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = expiration
    };
    await distributedCache.SetStringAsync(identityService.UserId, basketString, options, cancellationToken);
}
```
