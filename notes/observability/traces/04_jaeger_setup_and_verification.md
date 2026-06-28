# 🎓 Distributed Tracing (Dağıtık İzleme) - Bölüm 4: Jaeger Kurulumu ve Kontrol Adımları

Bu bölümde, Jaeger'ın sistemimizdeki fiziksel kurulumunu, portlarını ve trace'lerin akıp akmadığını nasıl kontrol edeceğimizi öğreneceğiz.

---

## 1. Fiziksel Kurulum ve Adresler

Jaeger'ı Dokploy üzerinde Docker Compose olarak ayağa kaldırdık. Dışarıya açık portları ve protokolleri şöyledir:

| Port | Protokol | Kullanım Amacı |
| :--- | :--- | :--- |
| **`4317`** | gRPC | **OTLP Telemetry Receiver.** Mikroservislerimizin trace verilerini push ettiği ana port. |
| **`4318`** | HTTP | **OTLP HTTP Receiver.** Web uygulamalarının veya HTTP tabanlı istemcilerin trace gönderdiği port. |
| **`16686`** | HTTP | **Jaeger UI (Arayüz).** Tarayıcıdan girip trace'leri sorguladığımız kullanıcı arayüzü. |

*   **Arayüz Adresi:** 👉 [http://192.168.1.56:16686](http://192.168.1.56:16686)

---

## 2. Trace'lerin Akıp Akmadığını Kontrol Etme (Adım Adım Doğrulama)

Eğer Jaeger arayüzünde aradığın trace'i bulamıyorsan veya hiçbir servis listelenmiyorsa, şu kontrol adımlarını izle:

### Adım 1: Pod Çevre Değişkenlerini Kontrol Et
Mikroservis pod'larının Jaeger gRPC adresini doğru aldığından emin ol. K3s cluster'ındaki bir pod'un environment değişkenlerine bak:
```bash
kubectl describe pod -l app=order-api -n gamegaraj
```
Çıktıda şu satırın olduğundan ve IP adresinin Dokploy host IP'si (`192.168.1.56`) olduğundan emin ol:
*   `OpenTelemetry__OtlpEndpoint: http://192.168.1.56:4317`

### Adım 2: Pod Loglarında OpenTelemetry Hatalarını Kontrol Et
Eğer OTLP bağlantısında bir sorun varsa (örneğin port kapalıysa veya firewall engelliyorsa), .NET uygulaması başlangıçta konsola hata basar:
```bash
kubectl logs -l app=order-api -n gamegaraj --tail=100
```
Loglarda `"Failed to export spans"` veya `"grpc: transient failure"` gibi OTLP export hataları görüyorsan, pod'un Dokploy host'una network erişimi yoktur.

### Adım 3: Pod İçinden Jaeger Bağlantısını Test Et
Mikroservis pod'unun içinden Jaeger portuna telnet veya curl ile TCP bağlantısı atarak network engeli olup olmadığını test et:
```bash
kubectl exec -it <pod-adi> -n gamegaraj -- nc -zv 192.168.1.56 4317
```
`open` veya `succeeded` yanıtını almalısın. Eğer bağlantı zaman aşımına uğruyorsa Dokploy firewall'unu kontrol et.
