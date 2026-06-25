# PhotoStock - MinIO Resim Yukleme Akisi ve Dikkat Edilecekler

Bu not, urun resmi yukleme sirasinda DB kaydinin olusup resmin MinIO bucket'a dusmemesi sorunundan sonra hazirlandi. Amac, PhotoStock upload akisini, kimin kime istek attigini ve MinIO endpoint ayarlarinda nelere dikkat edilmesi gerektigini netlestirmektir.

## Olay Ozeti

Semptom:

- Admin panelinde urun resmi yukleniyor gibi gorunuyordu.
- Urun tarafinda DB kaydi veya image path bilgisi olusuyordu.
- Fakat MinIO console'da `gamegaraj` bucket altinda beklenen obje gorunmuyordu.

Canli ortamda yapilan kontrolde asil problem su cikti:

- Kubernetes secret icindeki MinIO endpoint degeri `http://192.168.1.56:9000` idi.
- K3s pod'u icinden bu adrese istek atildiginda `Connection refused` donuyordu.
- Ayni pod icinden `http://minio.kadiryilmaz.online/minio/health/live` adresi ise `200 OK` donuyordu.

Yani MinIO servisi domain uzerinden erisilebilir durumdaydi, fakat `192.168.1.56:9000` endpoint'i K3s pod'larindan ulasilabilir degildi. Bu nedenle PhotoStock API MinIO'ya obje yazamiyordu.

Canli duzeltme:

```text
MINIO_ENDPOINT=http://minio.kadiryilmaz.online
MINIO_SECURE=false
```

Bu degerler Kubernetes `gamegaraj-secrets` secret'inda guncellendi ve `photostock-api` deployment'i restart edildi.

Smoke test sonucu:

- Gateway uzerinden PhotoStock upload endpoint'ine test resmi gonderildi.
- PhotoStock loglarinda MinIO upload basladi.
- `StatObject` dogrulamasi basarili oldu.
- Test objesi sonrasinda silindi.

Logda gorulen basarili pattern:

```text
Uploading photo to MinIO bucket gamegaraj as object photos/...
Photo uploaded and verified in MinIO bucket gamegaraj as object photos/...
HTTP POST /api/photos responded 200
```

## Upload Akisi: Kim Kime Istek Atiyor?

Admin panelinden urun resmi yukleme akisi su sekildedir:

1. Tarayici, admin panelindeki urun create/edit formunu `WebUI`'a gonderir.
2. `WebUI`, gelen dosyalari `PhotoStockService` ile arka planda Gateway'e yollar.
3. `WebUI -> Gateway` istegi:

```text
POST http://gateway:8080/api/photostock/photos
Content-Type: multipart/form-data
Form fields:
  photos
  brand
  productName
```

4. `Gateway`, YARP route ile path'i donusturur.

```text
/api/photostock/{**catch-all} -> /api/{**catch-all}
```

5. Gateway hedef olarak Kubernetes service DNS'ini kullanir:

```text
http://photostock-api:8080/api/photos
```

6. `PhotoStock API`, dosya validasyonunu yapar.

- Maksimum dosya sayisi: 5
- Maksimum dosya boyutu: 5 MB
- Izin verilen uzantilar: `.jpg`, `.jpeg`, `.png`, `.webp`

7. `PhotoStock API`, dosya adini brand ve productName bilgisine gore slug'lar.

Ornek obje path:

```text
photos/amd-ryzen-7-9800x3d-xxxxxxxxxx.png
```

8. `PhotoStock API -> MinIO` istegi:

```text
PUT object
Bucket: gamegaraj
Object: photos/...
Endpoint: http://minio.kadiryilmaz.online
```

9. Upload bittikten sonra PhotoStock API `StatObject` cagirir.

Bu adim onemlidir: PhotoStock API basarili cevap donmeden once objenin MinIO'da gercekten var oldugunu dogrular.

10. PhotoStock API WebUI'a su formatta cevap doner:

```json
{
  "urls": [
    "photos/ornek-dosya.png"
  ],
  "errors": null
}
```

11. WebUI bu path'i urun modeline yazar ve Catalog API'ye urun create/update istegi atar.

12. Catalog API image path bilgisini DB'ye kaydeder.

13. Frontend tarafinda resim gosterilirken public base URL ile birlestirilir.

Ornek:

```text
ServiceApiSettings__PhotoBaseUrl=https://minio.kadiryilmaz.online/gamegaraj
Image path=photos/ornek-dosya.png
Final URL=https://minio.kadiryilmaz.online/gamegaraj/photos/ornek-dosya.png
```

## Kritik Ayrim: API Endpoint ile Public Photo URL Ayni Sey Degil

Iki farkli URL turu vardir:

### 1. PhotoStock'un MinIO'ya Yazmak Icin Kullandigi Endpoint

Kubernetes secret:

```text
MINIO_ENDPOINT=http://minio.kadiryilmaz.online
MINIO_BUCKET_NAME=gamegaraj
MINIO_SECURE=false
```

Bu endpoint, `photostock-api` pod'unun MinIO S3 API'ye ulasmasi icindir.

### 2. WebUI'nin Resimleri Gostermek Icin Kullandigi Public Base URL

Helm values:

```text
ServiceApiSettings__PhotoBaseUrl=https://minio.kadiryilmaz.online/gamegaraj
```

Bu URL, tarayicinin bucket icindeki objeyi okumasina yarar.

Bu iki ayar birbirine benzer gorunse de ayni amaca hizmet etmez. Biri server-to-server upload endpoint'idir, digeri public image URL base'idir.

## Neden `192.168.1.56:9000` Calismadi?

Dokploy tarafinda MinIO domain ile yayinlaniyor olabilir, ama bu `192.168.1.56:9000` portunun host uzerinden veya K3s pod network'unden erisilebilir oldugu anlamina gelmez.

Canli test sonucu:

```text
curl http://192.168.1.56:9000/minio/health/live
Connection refused
```

Pod icinden domain testi:

```text
curl http://minio.kadiryilmaz.online/minio/health/live
HTTP/1.1 200 OK
```

Bu nedenle PhotoStock icin dogru endpoint domain oldu.

## Kubernetes Secret Kaliciligi

Canli ortamda `gamegaraj-secrets` patch edilerek sorun duzeltildi. Ancak kalici kaynak GitHub Secrets oldugu icin su deger de GitHub tarafinda guncellenmelidir:

```text
MINIO_ENDPOINT=http://minio.kadiryilmaz.online
```

Eger GitHub Actions `k3s-secret-sync.yml` tekrar eski secret ile calisirsa Kubernetes secret tekrar eski degere donebilir.

Bu yuzden dikkat:

- Canli Kubernetes secret duzeltmesi acil mudahaledir.
- Kalici duzeltme GitHub Secrets / `K3S_SECRETS_JSON` tarafinda yapilmalidir.
- Secret sync workflow'u calistiktan sonra `photostock-api` pod'u restart edilmelidir.

## Kontrol Komutlari

### Pod'larin durumunu kontrol et

```bash
sudo kubectl get pods -o wide
```

### PhotoStock env mapping kontrolu

Gizli degerleri yazdirmadan env kaynaklarini kontrol etmek icin:

```bash
sudo kubectl get deploy photostock-api \
  -o jsonpath='{range .spec.template.spec.containers[0].env[*]}{.name}{"="}{.value}{" secret="}{.valueFrom.secretKeyRef.key}{"\n"}{end}' \
  | grep -E 'Minio|ASPNETCORE|IdentityOption'
```

Beklenenler:

```text
Minio__UseLocalStorage=false
Minio__Endpoint secret=minio-endpoint
Minio__BucketName secret=minio-bucket-name
Minio__Secure secret=minio-secure
```

### Secret icindeki endpoint'i kontrol et

```bash
sudo kubectl get secret gamegaraj-secrets \
  -o jsonpath='{.data.minio-endpoint}' | base64 -d
```

Beklenen:

```text
http://minio.kadiryilmaz.online
```

### Pod icinden MinIO health kontrolu

```bash
sudo kubectl run minio-domain-check --rm -i --restart=Never \
  --image=curlimages/curl --command -- \
  sh -c 'curl -sv --max-time 8 http://minio.kadiryilmaz.online/minio/health/live'
```

Beklenen:

```text
HTTP/1.1 200 OK
```

### Gateway uzerinden PhotoStock upload smoke test

```bash
sudo kubectl run upload-check --rm -i --restart=Never \
  --image=curlimages/curl --command -- \
  sh -c "printf 'smoke' >/tmp/test.png; curl -sS --fail-with-body --max-time 20 \
  -F brand=codex \
  -F productName=minio-smoke \
  -F photos=@/tmp/test.png\;type=image/png \
  http://gateway:8080/api/photostock/photos"
```

Beklenen cevap:

```json
{"urls":["photos/codex-minio-smoke-....png"],"errors":null}
```

### PhotoStock loglarini kontrol et

```bash
sudo kubectl logs deploy/photostock-api --tail=100
```

Basarili upload icin beklenen loglar:

```text
Uploading photo to MinIO bucket gamegaraj as object photos/...
Photo uploaded and verified in MinIO bucket gamegaraj as object photos/...
```

## Kod Tarafinda Dikkat Edilecekler

### PhotoStock API basarili donmeden once MinIO'yu dogrular

`MinioStorageService.UploadFileAsync` akisi:

1. Bucket var mi kontrol eder.
2. Yoksa bucket olusturur.
3. Bucket policy ayarlar.
4. Dosyayi `photos/{fileName}` olarak yukler.
5. `StatObjectAsync` ile objeyi dogrular.
6. Ancak bundan sonra `photos/...` path'ini doner.

Bu nedenle mevcut kodda PhotoStock API'den 200 donuyorsa, normal sartlarda obje MinIO'da vardir.

### DB path'i obje degildir

Catalog DB'de tutulan deger genelde sadece path'tir:

```text
photos/dosya.png
```

Bu path'in DB'ye yazilmasi, MinIO'da obje oldugu anlamina tek basina gelmez. Dogru kaynak PhotoStock loglari ve MinIO bucket kontroludur.

### WebUI upload basarisizsa urun kaydini durdurmali

Product create/edit tarafinda PhotoStock bos liste donerse veya hata verirse urun kaydina devam edilmemelidir. Mevcut WebUI davranisi bu yonde olmalidir:

- `uploadedUrls` bos ise ModelState error eklenir.
- View tekrar dondurulur.
- Catalog API create/update cagrilmaz.

Bu davranis bozulursa yine "DB'de path var ama MinIO'da obje yok" benzeri tutarsizliklar gorulebilir.

## Operasyonel Dikkat Listesi

- `MINIO_ENDPOINT` pod icinden erisilebilir olmalidir, sadece tarayicidan aciliyor olmasi yetmez.
- Dokploy domain calisiyorsa ama host port calismiyorsa Kubernetes secret domain endpoint'i kullanmalidir.
- `MINIO_SECURE`, endpoint scheme'i ile uyumlu olmalidir:
  - `http://...` icin `false`
  - `https://...` icin `true`
- `ServiceApiSettings__PhotoBaseUrl` public image okuma icindir; PhotoStock upload endpoint'i yerine kullanilmamalidir.
- `Minio__UseLocalStorage=false` olmali; aksi halde PhotoStock dosyalari pod icindeki local `wwwroot/photos` klasorune yazar.
- Secret guncellendikten sonra ilgili pod restart edilmelidir; env var'lar calisan pod'a otomatik yansimaz.
- GitHub secret sync eski degerlerle calisirsa canli patch'i ezebilir.
- MinIO bucket adi hem secret'ta hem WebUI public URL'de ayni mantiga gore kullanilmalidir: `gamegaraj`.

## Kalici Cozum Checklist

- GitHub Secrets veya `K3S_SECRETS_JSON` icinde `MINIO_ENDPOINT=http://minio.kadiryilmaz.online` yap.
- `MINIO_SECURE=false` degerini koru, domain HTTP olarak kullaniliyorsa.
- `k3s-secret-sync.yml` workflow'unu calistir.
- `photostock-api` deployment'ini restart et veya rollout'un yeni pod olusturdugunu dogrula.
- Pod icinden MinIO health check yap.
- Gateway uzerinden PhotoStock smoke upload yap.
- MinIO console'da `gamegaraj/photos/...` altinda objeyi kontrol et.
- Test objesini sil.

