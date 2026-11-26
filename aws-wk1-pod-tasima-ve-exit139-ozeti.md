# `aws-wk1` Pod Taşınması ve `Exit 139` Hatası — Sade Anlatım

**Tarih:** 26.11.2026

---

## 1. Önce İyi Haber: Pod Taşınması Başarılı Oldu mu?

**Evet!** `kadirserver` üzerindeki `k3s` servisini durdurduğumuzda Kubernetes podları **`aws-wk1`** sunucusuna taşıdı.

* `aws-wk1` üzerinde toplam **26 adet `gamegaraj` podu** açıldı.
* Bunların **24 tanesi sorunsuz `1/1 Running` (yeşil)** çalıştı:
  - `webui` (3), `gateway` (3), `basket-api` (3), `catalog-api` (2), `campaign-api` (2), `discussion-api` (2), `review-api` (2), `search-api` (2), `invoice-api` (1), `notification-api` (1), `photostock-api` (1), `payment-api` (1).
* Tüm 13 mikroservisin en az 1 çalışan kopyası `aws-wk1` üzerinde olduğu için **site (`gamegaraj.kadiryilmaz.online`) çalışmaya devam etti.**

---

## 2. Peki O Kırmızı Görünen Podlar Neydi? (`Exit 139` Nedir?)

* Bir Linux programı bellek (RAM) içinde izin verilmeyen bir adrese dokunduğunda işletim sistemi o programı anında kapatır. Buna **`SIGSEGV` (Segmentation Fault)** denir ve çıkış kodu **`139`** olarak görünür.
* Yani `order-api` ve `payment-api` podları **RAM bittiği için değil (`OOMKilled` = 137 değildir)**, **ağ koptuğu için değil**, doğrudan programın içindeki çalışma anı (`.NET runtime`) Linux çekirdeğiyle çakıştığı için `Exit 139` verip kapandı.

---

## 3. Neden `kadirserver`'da Olmadı da `aws-wk1`'de Oldu?

İki sunucu arasında iki önemli donanım/sistem farkı var:

| Özellik | `kadirserver` (Evdeki PC) | `aws-wk1` (AWS EC2) |
|---|---|---|
| **İşlemci** | AMD Ryzen 5 1600 AF (6 çekirdek / 12 thread) | Intel Xeon Platinum 8488C (**2 vCPU**, AVX-512 destekli) |
| **İşletim Sistemi** | Ubuntu 26.04 (`Kernel 7.0.0-38-generic`) | Ubuntu 26.04.1 (`Kernel 7.0.0-1018-aws`) |

1. **Tek başına CPU yoğunluğu:** `kadirserver` kapanınca **26 podun tamamı aynı anda** `aws-wk1`'in sadece **2 çekirdekli** CPU'sunda açılmaya çalıştı.
2. **Yeni Kernel + .NET Uyumu:** `aws-wk1` üzerindeki `7.0.0-1018-aws` çekirdeği ve yeni Intel Xeon işlemci komut setleri, .NET uygulamasının JIT (anlık kod derleme) ve bellek koruma katmanıyla nadiren çakıştı.

---

## 4. Bizim Eklediğimiz İki Ayar Ne İşe Yaradı?

1. **`DOTNET_EnableAVX512F: "0"`**
   - .NET'e *"Yeni Intel işlemcinin `AVX-512` hızlandırma komutlarını kullanma"* dedik.
2. **`DOTNET_EnableWriteXorExecute: "0"`**
   - .NET'e *"Yeni Linux çekirdeğinin sıkı bellek izni (`Write^Execute`) kuralıyla çakışmayan klasik bellek haritalamasını kullan"* dedik.

**Sonuç:** Bu iki ayardan sonra `catalog-api`, `campaign-api`, `discussion-api`, `review-api`, `webui` gibi servislerdeki `Exit 139` çökmeleri **neredeyse tamamen durdu**; sadece aynı anda 26 pod birden ayağa kalktığında `order-api` ve `payment-api`'nin 1'er podu nadiren tekrar `Exit 139` verip birkaç saniye sonra yeniden `Running` oldu.

---

## 5. Kalıcı / Tertemiz Çözüm İstersen Ne Yapılır?

* **Ubuntu 24.04 LTS Kullanmak:** `aws-cp2` gibi **Ubuntu 24.04 LTS** imajıyla açılan worker'larda çekirdek ve .NET uyumu çok daha oturmuş olduğu için `Exit 139` yaşanmaz.
* **Normal Günlük Kullanım:** `kadirserver` açıkken yük zaten iki makineye bölündüğü için hem `kadirserver` hem `aws-wk1` tamamen yeşil ve stabil çalışır.
