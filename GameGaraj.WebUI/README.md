# GameGaraj WebUI

GamingStore görünümü + Udemy microservice yapısı ile oluşturulmuş web uygulaması.

## Özellikler

- **Microservice Mimarisi**: Her servis için ayrı HttpClient servisleri
- **Tokensiz**: Şu an için authentication yok (Keycloak ile eklenecek)
- **GamingStore Görünümü**: Modern, responsive tasarım
- **Bootstrap 5**: UI framework
- **Font Awesome**: İkonlar

## Yapı

### Models
- **Products**: ProductViewModel, CategoryViewModel
- **Baskets**: BasketViewModel, BasketItemViewModel

### Services
- **ICatalogService**: Ürün ve kategori işlemleri
- **IBasketService**: Sepet işlemleri

### Controllers
- **HomeController**: Ana sayfa
- **ProductController**: Ürün listesi ve detay
- **BasketController**: Sepet işlemleri

### Views
- **Home/Index**: Ana sayfa - öne çıkan ürünler
- **Product/Index**: Tüm ürünler
- **Product/Detail**: Ürün detayı
- **Basket/Index**: Sepet

## Yapılacaklar

- [ ] Order servisi entegrasyonu
- [ ] Payment servisi entegrasyonu
- [ ] Discount servisi entegrasyonu
- [ ] Keycloak authentication
- [ ] Kategori filtreleme
- [ ] Ürün arama
- [ ] Sipariş geçmişi
- [ ] Kullanıcı profili

## Çalıştırma

```bash
dotnet restore
dotnet build
dotnet run
```

URL: http://localhost:5013
