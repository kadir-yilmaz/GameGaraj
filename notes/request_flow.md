# GameGaraj İstek ve Kimlik Doğrulama İşleyişi (Request Flow & Auth Architecture)

Bu doküman, GameGaraj mikroservis mimarisindeki bir kullanıcının WebUI üzerindeki eylemlerinden, Keycloak kimlik doğrulamasına, YARP API Gateway yönlendirmesine ve backend mikroservislerine uzanan tüm istek döngüsünü açıklamaktadır.

---

## 1. Genel Mimari ve Adım Adım İstek Akış Tablosu

Bir kullanıcının sepet sayfasına gitmesi veya sepeti listelemesi senaryosundaki istek akışı ve kimlik doğrulama adımları sırasıyla şu şekildedir:

| Adım | Kaynak (Kimden) | Hedef (Kime) | İşlem / İstek | Açıklama |
| :---: | :--- | :--- | :--- | :--- |
| **1** | Kullanıcı (Tarayıcı) | **WebUI (MVC)** | `POST /Auth/SignIn` | Kullanıcı e-posta ve şifresini girerek giriş formunu gönderir. |
| **2** | **WebUI (MVC)** | **Keycloak** | `POST /protocol/openid-connect/token` | WebUI, arka planda Keycloak master/realm servislerine "password" grant_type isteği atarak JWT (Access & Refresh) token'larını alır. |
| **3** | **WebUI (MVC)** | Kullanıcı (Tarayıcı) | `Set-Cookie` | WebUI JWT token'ı parse eder. Kullanıcının benzersiz ID'sini (`sub`) ve rollerini (`roles`) local cookie olan `GameGarajWebCookie` içerisine şifreli yazar. Giriş tamamlanır. |
| **4** | Kullanıcı (Tarayıcı) | **WebUI (MVC)** | `GET /basket` | Tarayıcı, sepet sayfasına gitmek için istek atar. |
| **5** | **WebUI (MVC)** | **YARP Gateway** | `GET /api/basket/baskets` | WebUI'ın HTTP Client katmanında `UserIdDelegatingHandler` devreye girer. Çerezden aldığı JWT'yi `Authorization: Bearer <token>` ve Keycloak ID'sini `X-User-Id` olarak ekleyip Gateway poduna gönderir. |
| **6** | **YARP Gateway** | **Keycloak** | JWKS İmza Kontrolü | Gateway, token'ın güvenli olduğunu doğrulamak için Keycloak'un public imza anahtarlarını (`/certs`) sorgular (performans için bu anahtarlar önbelleğe alınır). |
| **7** | **YARP Gateway** | **Basket API** | `GET /api/v1/baskets` | Gateway, path transformasyonu (/api/basket -> /api/v1) uygulayarak doğrulanmış isteği K8s DNS üzerinden `http://basket-api:8080/api/v1/baskets` adresine yönlendirir. |
| **8** | **Basket API** | **Redis Sentinel** | Sepet Sorgusu | Basket API, header'dan gelen `X-User-Id` bilgisine göre kullanıcının sepet verilerini Redis'ten çeker. |
| **9** | **Basket API / Gateway**| **WebUI (MVC)** | `200 OK (JSON)` | Çekilen sepet verileri Gateway üzerinden geçerek WebUI'a JSON formatında geri döner. |
| **10**| **WebUI (MVC)** | Kullanıcı (Tarayıcı) | `200 OK (HTML)` | WebUI sunucu tarafında JSON verilerini Razor View kullanarak HTML'e dönüştürür (render eder) ve tarayıcıya nihai sayfayı servis eder. |

---

## 2. Detaylı İstek Adımları (Adım Adım Açıklama)

### Aşama A: Kullanıcı Giriş İşlemi (Authentication / Sign-In)
1. **İstek Başlatma:** Kullanıcı tarayıcıdan giriş yapmak istediğinde, WebUI'daki `SignIn` formunu doldurur.
2. **Keycloak POST İsteği:** WebUI sunucu tarafında (`IdentityService.cs`), Keycloak'un token alma endpoint'ine (`/protocol/openid-connect/token`) arkadan (Server-to-Server) bir POST isteği gönderir:
   - **Client ID:** `web-ui`
   - **Grant Type:** `password`
   - **Credentials:** Kullanıcının girmiş olduğu E-posta (`username`) ve Şifre (`password`).
3. **Token Yanıtı:** Keycloak kimlik bilgilerini doğrularsa, WebUI'a şifrelenmiş bir JWT **Access Token** ve **Refresh Token** döner.
4. **Cookie Oluşturma:** WebUI, gelen Access Token'ı çözümler (parse eder):
   - JWT içindeki `sub` claim'ini kullanıcının benzersiz ID'si (`NameIdentifier`) olarak belirler.
   - `realm_access.roles` içindeki rolleri (örn. `admin`, `instructor`) MVC yetkilendirme mekanizmasına enjekte eder.
   - Tarayıcıya `GameGarajWebCookie` adında şifrelenmiş bir oturum cookie'si yazar ve token'ları bu cookie'nin içinde saklar (`SaveTokens = true`).

---

### Aşama B: WebUI'dan API Gateway'e İstek Gönderme
1. **HTTP Client Çağrısı:** Kullanıcı sepete ekleme, ürün arama veya sipariş verme gibi bir eylem yaptığında, WebUI backend'den veri çekmek için kendi servislerini çağırır (örn. `BasketService.cs`).
2. **Delege Edici Handler (Interceptors):** WebUI Http client'ları oluşturulurken devreye [UserIdDelegatingHandler](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Handlers/UserIdDelegatingHandler.cs) girer. Bu handler:
   - O anki HTTP oturumunun cookie'sinden **Access Token**'ı alır.
   - İstek başlığına (Headers) `Authorization: Bearer <Access_Token>` değerini ekler.
   - Ayrıca kullanıcının Keycloak `sub` ID'sini `X-User-Id` başlığı ile ekler.
3. **Gateway'e Gönderim:** İstek, K3s içindeki API Gateway servis IP'sine/DNS adına gönderilir. Örneğin:
   `GET http://gateway:8080/api/basket/baskets`

---

### Aşama C: YARP API Gateway İşlemleri (Yönlendirme ve Doğrulama)
Gateway podunda (`GameGaraj.Gateway`), istek karşılandığında sırasıyla şu ASP.NET Core middleware'leri çalıştırılır:

1. **Routing (`UseRouting`):** İstek path'ine göre YARP üzerindeki rotalar eşleştirilir.
   - İstek `/api/basket/baskets` ise, [appsettings.json](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.Gateway/appsettings.json) içerisindeki `basket-route` eşleşir.
   - Bu rota `basket-cluster` kümesine bağlıdır.
2. **Kimlik Doğrulama & Yetkilendirme (`UseAuthentication` & `UseAuthorization`):**
   - Rota eğer bir yetki veya kimlik doğrulama politikası içeriyorsa (Gateway JWT doğrulaması), Gateway gelen Bearer token'ı doğrular.
   - Doğrulama işlemi için Gateway Keycloak'un public imza anahtarlarını (JWKS) kullanır (K8s içinden `IdentityOption.Address` yani `http://192.168.1.56:8080/realms/GameGaraj` adresine sorgu atılır).
3. **Yönlendirme ve Path Dönüşümü (Transforms):**
   - YARP rotadaki kurala göre URL'i dönüştürür.
   - Örneğin `basket-route` için transform kuralı şudur: `/api/basket/{**catch-all}` -> `/api/v1/{**catch-all}`.
   - URL `/api/basket/baskets` iken `/api/v1/baskets` haline gelir.
4. **Backend Servise Proxy Etme (`MapReverseProxy`):**
   - Gateway, hedef cluster adresini okur. `basket-cluster` için adresi Kubernetes DNS çözümü ile `http://basket-api:8080` olarak bulur.
   - Yeni dönüştürülmüş URL ile isteği doğrudan hedef pod'a yönlendirir:
     `GET http://basket-api:8080/api/v1/baskets`

---

### Aşama D: Backend Mikroservis ve Veritabanı Süreci
1. **İstek Karşılama:** `basket-api` servisi isteği alır, isteğin header'ındaki `X-User-Id` değerine göre kullanıcının sepetini veri tabanından (Redis Sentinel) sorgular.
2. **Cevap Dönüşü:** Veri tabanından dönen sepet nesnesini JSON formatında Gateway'e iletir.
3. **Gateway'den WebUI'a İletim:** Gateway bu cevabı şeffaf bir şekilde WebUI'a aktarır.
4. **WebUI HTML Rendering:** WebUI gelen JSON verisini alıp HTML şablonuna (Razor View) bağlayarak kullanıcı tarayıcısına nihai HTML sayfasını döner.

---

## 3. Servislerin Gateway Üzerindeki Path ve Cluster Haritası

Gateway'in hangi istekleri hangi servis DNS'ine yönlendirdiği aşağıdaki tabloda özetlenmiştir:

| Dış İstek Path'i (WebUI veya Mobil) | Eşleşen Rota | Dönüştürülmüş Hedef Path | Hedef Cluster K8s Adresi | Servisin Kullandığı Altyapı |
|---|---|---|---|---|
| `/api/catalog/{**catch-all}` | `catalog-route` | `/api/{**catch-all}` | `http://catalog-api:8080` | PostgreSQL & Elasticsearch |
| `/api/basket/{**catch-all}` | `basket-route` | `/api/v1/{**catch-all}` | `http://basket-api:8080` | Redis Sentinel |
| `/api/favorites/{**catch-all}`| `favorites-route`| `/api/v1/favorites/{**catch-all}`| `http://basket-api:8080` | Redis Sentinel |
| `/api/order/{**catch-all}` | `order-route` | `/api/{**catch-all}` | `http://order-api:8080` | MS SQL Server |
| `/api/payment/{**catch-all}` | `payment-route` | `/api/payments/{**catch-all}` | `http://payment-api:8080` | Iyzico API |
| `/api/photostock/{**catch-all}`| `photostock-route`| `/api/{**catch-all}` | `http://photostock-api:8080` | MinIO Obje Deposu |
| `/api/campaign/{**catch-all}` | `campaign-route`| `/api/{**catch-all}` | `http://campaign-api:8080` | MS SQL Server |
| `/api/discount/{**catch-all}` | `discount-route`| `/api/{**catch-all}` | `http://discount-api:8080` | PostgreSQL |

---

## 4. Asenkron Mesajlaşma Akışı (MassTransit & RabbitMQ)

Senkron HTTP isteklerinin haricinde, servisler arasında **gevşek bağlı (loosely coupled)** iletişimi sağlamak için MassTransit ve RabbitMQ kullanılır:

- **Örnek Senaryo (Sipariş Tamamlama):**
  1. Kullanıcı sepeti onaylar, `order-api` veritabanına siparişi yazar.
  2. `order-api` siparişin alındığına dair bir `OrderCreatedEvent` mesajını RabbitMQ (`192.168.1.56`) kuyruğuna yayınlar (Publish).
  3. `invoice-api` ve `basket-api` bu kuyruğu dinlemektedir.
  4. Mesaj geldiğinde:
     - `basket-api` kullanıcının sepetini temizler (Redis'ten siler).
     - `invoice-api` müşteriye SMTP üzerinden e-posta faturası gönderir.
  5. Bu işlemler arka planda asenkron olarak gerçekleştiği için kullanıcı siparişin tamamlanması için faturanın oluşturulmasını ve gönderilmesini beklemek zorunda kalmaz.

---

## 5. Token Çeşitleri ve Güvenlik Seviyeleri (Token Types & Access Control)

GameGaraj sisteminde endpoint'ler ve servisler, güvenlik gereksinimlerine göre iki temel kategoriye ayrılır:

### A. Giriş Gerektiren İstekler (User Authentication - Login Required)
Kullanıcının kimliğiyle doğrudan ilişkili olan ve veri güvenliği gerektiren işlemlerdir.
- **Kapsanan Servisler:**
  - `GameGaraj.Order.API`: `Program.cs` içerisinde global bir filtre olarak `options.Filters.Add(new AuthorizeFilter())` tanımlıdır. Sipariş oluşturma, adres yönetimi vb. işlemler kesinlikle token gerektirir.
  - `GameGaraj.Payment.API`: `Program.cs` içerisinde global filtre olarak `options.Filters.Add(new AuthorizeFilter())` eklenmiştir. Ödeme işlemleri güvenli JWT gerektirir.
  - `GameGaraj.Review.API`: Controller seviyesinde bazı endpoint'ler `[Authorize]` ile korunurken, onaylama/yönetim endpoint'leri `[Authorize(Roles = "admin, editor")]` ile role tabanlı olarak korunmaktadır.
- **Token Kullanımı:**
  - Kullanıcı giriş yaptığında Keycloak'tan alınan kişisel **User JWT Access Token**'ı kullanılır.
  - Bu token kullanıcının e-posta, roller (`realm_access.roles`) ve benzersiz kullanıcı ID'sini (`sub`) taşır.
  - `UserIdDelegatingHandler` bu token'ı her HTTP çağrısında `Authorization: Bearer <token>` olarak ekler.
  - **Eğer Token Yoksa/Geçersizse:** API doğrudan **HTTP 401 Unauthorized** hatası döndürür.

### B. Giriş Gerektirmeyen İstekler (Public / Guest Access)
Kullanıcının sisteme üye veya giriş yapmış olmasını gerektirmeyen; ürün arama, katalog inceleme ve misafir sepeti gibi süreçlerdir.
- **Kapsanan Servisler:**
  - `GameGaraj.Catalog.API`: Kategorileri ve ürünleri listeler. Tamamen halka açıktır. Herhangi bir `[Authorize]` filtresi içermez.
  - `GameGaraj.PhotoStock.API`: Resimlerin okunması/indirilmesi için koruma bulunmaz.
  - `GameGaraj.Campaign.API`: Kampanyaların/kuponların listelenmesi gibi süreçler anonim erişime açıktır.
  - `GameGaraj.Basket.API`: HTTP rotalarında global bir `[Authorize]` filtresi bulunmaz. Hem giriş yapmış kullanıcılar hem de misafir (guest) kullanıcılar sepet oluşturabilir.
- **Misafir (Guest) Kullanıcı Takip Mekanizması:**
  - Kullanıcı giriş yapmamışsa, WebUI tarafındaki [IdentityService.cs](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Services/Concrete/IdentityService.cs) kullanıcının tarayıcısına `GameGarajGuestId` adında 30 gün geçerli bir cookie yazar. Bu cookie'nin değeri `guest-<GUID>` şeklindedir.
  - WebUI'ın [UserIdDelegatingHandler.cs](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Handlers/UserIdDelegatingHandler.cs) sınıfı, isteğe `X-User-Id` başlığı (header) olarak bu `guest-<GUID>` değerini ekler ancak `Authorization` header'ı eklemez.
  - `Basket API`, istek geldiğinde `X-User-Id` başlığını okur. Eğer token veya header yoksa varsayılan olarak `"anonymous-user"` değerine düşer.
  - Redis Sentinel üzerinde sepet anahtarları bu `X-User-Id` değerine göre (örn. `basket:guest-abcd-1234`) tutulur. Böylece loginsiz sepet akışı JWT/Keycloak trafiği oluşturmadan verimli bir şekilde çalışır.

---

## 6. Keycloak Kimlik Doğrulama Akışları (Keycloak Auth Flows)

Mülakatlarda gelebilecek Keycloak akış tipleri sorusu için projede yapılandırılmış olan mekanizmalar şunlardır:

1. **Resource Owner Password Credentials Grant (Direct Access Grant / "password" flow):**
   - **Kullanım Yeri:** WebUI'ın login sayfası üzerinden gerçekleştirilen giriş işleminde ([IdentityService.cs](file:///d:/Kadir/Projeler/GameGaraj/GameGaraj.WebUI/Services/Concrete/IdentityService.cs#L32)).
   - **Açıklama:** Kullanıcı tarayıcıda kullanıcı adı ve şifresini girer. WebUI sunucusu (Server-to-Server) Keycloak'ın `/protocol/openid-connect/token` endpoint'ine `grant_type=password` ile bu bilgileri göndererek doğrudan JWT (Access + Refresh Token) alır.
   - **Avantajı:** WebUI kendi login arayüzünü (Razor View) özelleştirerek kullanabilir, Keycloak'ın varsayılan login sayfasına yönlendirme yapılması gerekmez.

2. **Authorization Code Flow (Standard Flow):**
   - **Kapsam:** Keycloak tarafındaki `web-ui` client konfigürasyonunda (`realm-init.json` içinde `"standardFlowEnabled": true`) aktiftir.
   - **Açıklama:** Kullanıcıyı Keycloak'ın kendi login sayfasına yönlendirip başarılı giriş sonrası bir `code` (kod) alarak, bu kod ile arka planda token takası yapmayı sağlayan standart, en güvenli OIDC akışıdır.

3. **Client Credentials Flow (Service Accounts):**
   - **Kapsam:** Gateway tarafındaki `gateway-client` konfigürasyonunda (`realm-init.json` içinde `"serviceAccountsEnabled": true` ve `"secret": "gateway-secret"`) aktiftir.
   - **Açıklama:** Bir kullanıcının doğrudan müdahalesi olmadan, iki servisin (Machine-to-Machine) birbirleriyle güvenli bir şekilde konuşması için kullanılır. İstemci kendi `client_id` ve `client_secret` bilgilerini Keycloak'a göndererek uygulama adına bir token alır. Projede `AuthenticationExt.cs` içinde `"ClientCredentialSchema"` olarak tanımlanmış ve altyapısı hazır durumdadır.

---

## 7. Mülakat Soruları ve Cevap Anahtarı (Interview QA)

### Soru 1: Catalog API neden tokensiz erişilebilir durumda? `/api/catalog/categories` adresine JWT olmadan istek atabiliyorum, bu bir güvenlik açığı mıdır?
**Cevap:** Hayır, bu bilinçli bir mimari tercihtir. E-ticaret platformlarında katalog (kategoriler, ürün listeleri, ürün detayları) arama motoru optimizasyonu (SEO), web tarayıcıları (crawlers) ve üye olmayan misafir ziyaretçiler için tamamen açık olmak zorundadır. Bu nedenle Catalog API üzerindeki okuma endpoint'lerinde global veya lokal `[Authorize]` filtresi bulunmamaktadır.
*(Not: Gerçek üretim ortamında ürün ekleme, güncelleme ve silme (POST/PUT/DELETE) gibi admin işlemleri içeren controller metotları `[Authorize(Roles = "admin")]` ile korunur. Okuma metotları ise anonim erişime açık bırakılır).*

### Soru 2: Loginsiz (misafir) akışlarda, WebUI veya API'ler arka planda Keycloak'tan otomatik olarak JWT mi talep ediyordu?
**Cevap:** Hayır. Loginsiz akışlarda Keycloak'a hiçbir token isteği atılmaz. Bu sayede gereksiz Keycloak trafiği önlenir ve sistem performansı artırılır.
- Ziyaretçi siteye girdiğinde WebUI, tarayıcıya benzersiz bir `GameGarajGuestId` çerezi (cookie) yazar (`guest-<GUID>`).
- API Gateway'e ve backend servislere atılan isteklere `Authorization` başlığı eklenmez, sadece `X-User-Id` başlığına bu guest ID değeri eklenir.
- `Basket API` gibi servisler bu ID'yi alarak veritabanında (Redis) misafir sepetini yönetir. Kullanıcı giriş yapmadığı sürece Keycloak sunucusuyla herhangi bir iletişim kurulmaz.

### Soru 3: Projede Keycloak üzerinde kaç farklı Client ve Flow tipi tanımladınız?
**Cevap:** Keycloak üzerinde 2 adet client tanımlanmıştır:
1. `web-ui` (Public Client): Kullanıcıların giriş yapması için kullanılır. Hem **Authorization Code Flow** hem de **Direct Access Grant (Password Flow)** desteklemektedir. Giriş işlemi backend sunucudan yapıldığı için pratiklik açısından Password Flow tercih edilmiştir.
2. `gateway-client` (Confidential Client): Client Secret korumalıdır. Machine-to-Machine (M2M) iletişimi için **Client Credentials Flow (Service Accounts)** yeteneğine sahiptir. Gateway'in iç servislerle konuşurken veya gelecekteki servisler arası doğrudan entegrasyonlarda kullanılmak üzere altyapısı oluşturulmuştur.

### Soru 4: Projede hangi servisler Resource Owner Password Credentials (Kullanıcı JWT) gerektirir, hangileri Client Credentials (M2M) token'ı gerektirir?
**Cevap:**
*   **Resource Owner Password Credentials (Kullanıcı Token'ı) Gerektiren Servisler:**
    *   Kullanıcıyla doğrudan ilişkili olan ve kullanıcı bazlı yetkilendirme / veri yönetimi yapan servislerdir.
    *   **Order API:** Siparişlerin listelenmesi, yeni sipariş oluşturma ve adres işlemleri tamamen giriş yapmış kullanıcının token'ı (`sub` ID'si) ile yürütülür.
    *   **Payment API:** Kullanıcının sepetindeki ürünleri ödemesi için kullanıcı JWT'si ve kimliği doğrulanmış olmalıdır.
    *   **Review API:** Yorum yazma, düzenleme ve silme işlemleri doğrudan kullanıcının kendi token'ı ile doğrulanır.
    *   **Basket API:** Loginsiz misafir sepetini desteklese de, kullanıcı giriş yaptığı an sepet işlemleri kullanıcının kendi JWT token'ı ile ilişkilendirilir.
*   **Client Credentials (Makine/Uygulama Token'ı) Gerektiren Servisler:**
    *   **Mevcut Çalışma Durumu:** Aktif çalışma döngüsünde (runtime flow) **hiçbir servis** zorunlu olarak Client Credentials token'ı talep etmemektedir. Halka açık olan Catalog, PhotoStock ve Campaign servislerine WebUI doğrudan token eklemeden istek atabilmektedir.
    *   **Altyapı Durumu:** API Gateway üzerinde `ClientCredentialSchema` ve `ClientCredential` politikası yapılandırılmıştır. Gelecekte, tamamen halka açık olan servislerin (örn. Catalog API) dış dünyadan doğrudan çağrılmasını engellemek, yalnızca kendi WebUI veya Mobil uygulamamızın erişebilmesini sağlamak amacıyla bu politika devreye alınabilir. Bu senaryoda WebUI, Keycloak'tan `gateway-client` (veya `web-ui-m2m`) aracılığıyla istemci token'ı alarak isteği imzalayacaktır. Mevcut kodda bu altyapı hazır olup, esneklik sağlamak amacıyla Catalog doğrudan anonim erişime açık bırakılmıştır.

### Soru 5: Client Credentials akışını Catalog API gibi halka açık servisler için zorunlu hale getiremez miydik? Bunu yapmak istesek sistem mimarisini ve kod akışını nasıl değiştirmemiz gerekirdi?
**Cevap:** Evet, Catalog API'yi de Client Credentials gerektirecek şekilde yapılandırabilirdik ve bu e-ticaret sistemlerinde çok yaygın bir **"API Scraping / Bot Protection" (Veri Kazıma Engelleme)** güvenlik önlemidir. 

Eğer Catalog API'yi bu şekilde korumak isteseydik yapılması gereken adımlar şunlar olurdu:

1.  **Keycloak Yapılandırması:** Keycloak üzerinde `gateway-client` (veya `catalog-client-m2m`) adında bir confidential client oluşturup `serviceAccountsEnabled: true` yapar ve bir Client Secret (`gateway-secret` gibi) belirleriz (bu zaten `realm-init.json` üzerinde hazır durumda).
2.  **API Gateway (YARP) Yapılandırması:** Gateway üzerindeki `catalog-route` tanımına Authorization Policy olarak `"ClientCredential"` ekleriz:
    ```json
    "catalog-route": {
      "ClusterId": "catalog-cluster",
      "AuthorizationPolicy": "ClientCredential", // Politika buraya eklenir
      "Match": { "Path": "/api/catalog/{**catch-all}" }
    }
    ```
    Böylece Gateway, Catalog API'ye gelen her istekte geçerli bir Client Credentials token'ı arar. Token yoksa 401 Unauthorized döner.
3.  **WebUI (İstemci) Tarafında Token Yönetimi:**
    - WebUI, Keycloak client id ve secret bilgilerini kullanarak arka planda (kullanıcıdan bağımsız olarak) Keycloak'tan bir **Client Token** talep eder.
    - WebUI'daki `HttpClient` katmanına (örn. bir `ClientCredentialsDelegatingHandler` ekleyerek), Catalog API'ye atılacak isteklerde `Authorization: Bearer <client_token>` başlığını eklemesini söyleriz.
    - Bu token'ı hafızada (In-Memory Cache) tutarak expire olana kadar yeniden kullanırız (böylece her istekte Keycloak'a gitmemiş oluruz).

**Bu Yapının Avantaj ve Dezavantajları Nelerdir?**
-   **Avantajı:** Dışarıdan herhangi bir bot veya rakip firma, doğrudan API Gateway URL'imize (`/api/catalog/products`) istek atarak tüm ürün ve fiyat veritabanımızı kazıyamaz (scraping yapamaz). API'lerimiz sadece bizim resmi istemcilerimize (WebUI, Mobil Uygulama vb.) hizmet verir hale gelir.
-   **Dezavantajı:** WebUI üzerindeki guest (loginsiz) trafik de dahil olmak üzere atılan her catalog isteğinde token yönetimi, önbelleğe alma (caching) ve Gateway doğrulama yükü eklenir. Ayrıca SEO botlarının (Google Crawler vb.) ürün sayfalarını doğrudan tarayabilmesi için Gateway'de özel istisnalar tanımlanması gerekir.

### Soru 6: Peki bu durumda (Catalog korunduğunda) bir Guest (misafir) ana sayfaya gelince kim kime nasıl token veriyor? WebUI arka planda Keycloak'a istek mi atıyor?
**Cevap:** Evet, tam olarak öyle. Bir misafir ana sayfaya girdiğinde, kullanıcının haberi olmadan **WebUI sunucusu arka planda (Server-to-Server) Keycloak'a istek atarak Client Credentials ile token alır.**

Adım adım akış şu şekilde gerçekleşirdi:

1.  **Ana Sayfa Talebi:** Misafir kullanıcı tarayıcısından `https://gateway.kadiryilmaz.online` (veya doğrudan WebUI) adresine girer. WebUI sunucusundaki `HomeController.Index` metodu tetiklenir.
2.  **WebUI Token Kontrolü (Cache):** WebUI, ana sayfadaki ürünleri listelemek için Catalog API'ye istek atmak zorundadır. İstek atmadan önce, kendi **belleğinde (Memory Cache)** geçerli bir **Client Credentials Token** olup olmadığını kontrol eder.
3.  **Keycloak'tan Token İsteme (Cache Miss):**
    - Eğer bellekte geçerli bir token yoksa, WebUI sunucusu Keycloak'ın `/protocol/openid-connect/token` endpoint'ine arka planda bir POST isteği gönderir:
      - `client_id=web-ui` (Confidential Client)
      - `client_secret=web-ui-secret-key`
      - `grant_type=client_credentials`
    - **Keycloak** bu istemci bilgilerini doğrular ve WebUI'a bir **Access Token (JWT)** döner.
    - WebUI, bu token'ı sonraki isteklerde de kullanabilmek için örneğin 50 dakikalığına belleğe (cache) yazar (böylece her gelen misafir için Keycloak'a tekrar gitmemiş olur).
4.  **Gateway'e İstek Atma:** WebUI, elindeki bu **Client Token**'ı ve misafir kullanıcının çerezindeki misafir ID'sini ekleyerek Gateway'e istek atar:
    - `Headers -> Authorization: Bearer <client_token>`
    - `Headers -> X-User-Id: guest-abcd-1234` (Kullanıcı loginsiz olduğu için)
5.  **Gateway Doğrulaması:** API Gateway (YARP), isteği karşılar. Rota üzerindeki `ClientCredential` politikasından dolayı token'ı doğrular (JWKS public anahtarları ile imza kontrolü yapar) ve isteğin kendi resmi WebUI uygulamasından geldiğinden emin olarak isteği K8s içindeki Catalog API poduna iletir.
6.  **Cevabın Dönmesi:** Catalog API veriyi Gateway üzerinden WebUI'a döner, WebUI Razor View ile sayfayı render edip nihai HTML'i misafir kullanıcının tarayıcısına servis eder.

**Özetle:** Kullanıcı (tarayıcı) Keycloak'ın varlığından bile habersizdir. Bütün Client Credentials token alma süreci sunucu tarafında (WebUI) gerçekleşir ve Keycloak trafiğini minimize etmek için bu token sunucu belleğinde (cache) saklanır.
