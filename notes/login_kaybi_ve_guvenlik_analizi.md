# Her Deploy Sonrası Login Kaybı (Oturum Düşmesi) ve Güvenlik Analizi

Uygulamada her yeni sürüm çıkıldığında (deploy) veya sunucu/konteyner yeniden başlatıldığında kullanıcıların (ve adminlerin) sistemden çıkış yapmış (logout) duruma düşmesinin temel nedeni ASP.NET Core'un **Data Protection (Veri Koruma)** mekanizmasıdır. Aşağıda bu sorunun analizi, güvenlik endişeleriniz (XSS/CSRF) ve çözüm yolları detaylıca açıklanmıştır.

---

## 1. Data Protection Key (Veri Koruma Anahtarı) Nedir? Ne İşe Yarar?

ASP.NET Core, tarayıcıya gönderdiği Çerezleri (Cookies) düz metin olarak göndermez. Kullanıcı giriş yaptığında (OIDC veya Cookie Auth ile), kullanıcının kimlik bilgileri, **Access Token** ve **Refresh Token** gibi kritik veriler bir araya getirilir ve sunucu tarafında kriptografik bir **Anahtar (Key)** ile şifrelenir. 

Şifrelenmiş bu anlamsız metin (cookie) tarayıcıya gönderilir. Tarayıcı her istek yaptığında bu cookie'yi sunucuya yollar, sunucu kendi elindeki "Anahtar" ile şifreyi çözer ve "Tamam, bu Kadir'in isteği ve tokenları bunlar" der.

### Neden Deploy Sonrası Oturum Düşüyor?
Eğer sisteme özel bir kalıcı "Anahtar Deposu" (Key Storage) tanımlamazsanız, ASP.NET Core her çalıştığında hafızada (veya sunucudaki geçici bir temp dosyasında) rastgele **yeni bir şifreleme anahtarı** üretir. 

1. Eski anahtarla şifrelenmiş cookie tarayıcıda duruyordur.
2. Siz sistemi güncelleyip yeniden başlattığınızda (deploy), sunucu yeni bir anahtar üretir.
3. Tarayıcı eski şifreli cookie'yi gönderdiğinde, sunucu yeni anahtarla bunu çözemez.
4. Çözemediği için cookie'yi "Geçersiz/Bozuk" kabul eder ve reddeder. Kullanıcı sistemden anında düşer.

---

## 2. Access ve Refresh Tokenlar Nasıl Güvenle Tutuluyor?

`SaveTokens = true` ayarı açık olduğunda, OIDC (Keycloak) üzerinden gelen Access Token ve Refresh Token, ASP.NET Core tarafından yukarıda bahsettiğimiz **Veri Koruma Anahtarı** ile güçlü bir şekilde (AES-256-GCM gibi algoritmalarla) şifrelenerek `GameGarajWebCookie` adındaki çerezin (cookie'nin) tam içine gömülür.

Yani tokenlar tarayıcıda asla **düz metin olarak (Local Storage vs.) bulunmaz**. Tarayıcı sadece şifrelenmiş büyük bir metin bloğu görür. Bu blok sadece sunucu tarafından çözülebilir.

---

## 3. XSS ve CSRF Koruması Nasıl Sağlanır?

Gelelim bahsettiğiniz **XSS (Cross-Site Scripting)** ve **CSRF (Cross-Site Request Forgery)** güvenlik zafiyetlerine. Tokenların şifrelenmesi tek başına yeterli değildir, çerezin (cookie'nin) nasıl taşındığı da önemlidir.

### XSS (Cross-Site Scripting) ve Çözümü (`HttpOnly`)
- **Tehlike:** Eğer sitenizde bir açık varsa ve kötü niyetli biri sayfaya zararlı bir JavaScript kodu çalıştırabilirse (XSS), tarayıcıdaki cookie'leri okuyup çalabilir.
- **Koruma:** Cookie oluşturulurken `HttpOnly = true` bayrağı eklenir. Bu bayrak tarayıcıya şu emri verir: *"Bu cookie sadece HTTP sunucu isteklerinde gönderilir. Tarayıcıdaki hiçbir JavaScript kodu (`document.cookie` dahil) bu veriye erişemez!"* Böylece hacker sayfaya JS kodu enjekte etse bile cookie'nizi çalamaz.

### Ağ Üzerinden Dinleme (Man-in-the-Middle) ve Çözümü (`Secure`)
- **Tehlike:** İnternet kafedeki biri Wi-Fi ağını dinleyip HTTP üzerinden giden cookie'yi çalabilir.
- **Koruma:** `SecurePolicy = Always` bayrağı ile cookie'nin **sadece HTTPS** (şifreli bağlantı) üzerinden gönderilmesi garanti altına alınır.

### CSRF (Cross-Site Request Forgery) ve Çözümü (`SameSite`)
- **Tehlike:** Başka bir sekmede kötü niyetli bir site (ornegin: `hacker.com`) açıkken, bu site arkaplanda sizin adınıza GameGaraj'a bir POST isteği (örneğin: sipariş ver, ürünü sil) yapabilir. Tarayıcı, istek GameGaraj'a gittiği için sizin login cookie'nizi de o isteğe otomatik ekler.
- **Koruma:** `SameSite = SameSiteMode.Lax` (veya `Strict`) bayrağı kullanılır. Bu bayrak sayesinde tarayıcı, farklı bir domainden gelen isteklerin içerisine GameGaraj cookie'sini eklemez. Ek olarak `app.UseAntiforgery()` kullanılarak POST formlarının içine tek kullanımlık bir token konularak %100 koruma sağlanır.

---

## 4. Kalıcı Çözüm: Anahtarları (Keys) Redis'te Tutmak

Login kaybını engellemek için, yeniden başlatmalarda silinmeyen merkezi bir kasaya (Vault/Redis/DB) ihtiyacımız var. Altyapımızda zaten **Redis** kurulu olduğu için en mantıklı ve en hızlı çözüm Data Protection anahtarlarını Redis'e yazmaktır.

```csharp
// Örnek Çözüm Kodu
builder.Services.AddDataProtection()
    .SetApplicationName("GameGarajApp") // Farklı mikroservisler aynı cookie'yi okuyabilsin diye
    .PersistKeysToStackExchangeRedis(redisConnection, "DataProtection-Keys");
```

**Bu kod uygulandığında:**
1. Sunucu ayağa kalkarken şifreleme anahtarını Redis'e yazar.
2. Sistem kapanıp (deploy) geri açıldığında, anahtar üretmek yerine Redis'teki eski anahtarı alır.
3. Kullanıcının tarayıcısındaki eski cookie hala geçerli sayılır ve **oturumu düşmez.**
4. Hem Access/Refresh Token'larınız cookie içinde `HttpOnly` olarak şifreli durur, hem XSS'ten korunur hem de deploy sonrası kimse mağdur olmaz.
