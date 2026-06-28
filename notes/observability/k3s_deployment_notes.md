# k3s Cluster Deployment Notes for Observability

k3s Cluster'ındaki mikroservislerin Dokploy'da kurulu olan observability stack'ine veri gönderebilmesi için gereken yapılandırma adımları aşağıda açıklanmıştır.

## 1. Environment Variables (Ortam Değişkenleri)

Her mikroservis deploy edildiğinde aşağıdaki environment değişkenlerini almalıdır. Bu değişkenler Helm Chart aracılığıyla `values.yaml` üzerinden yönetilir.

### values.yaml Üzerindeki Ayarlar

`helm/gamegaraj/values.yaml` içinde, `global` sekmesi altında altyapı adresleri tanımlanmıştır:

```yaml
global:
  aspnetEnvironment: Production
  aspnetUrls: "http://+:8080"
  containerPort: 8080

  # Altyapı adresleri (192.168.1.56 = Dokploy host IP'si)
  rabbitmqUrl: "192.168.1.56"
  identityAuthority: "http://192.168.1.56:8080/realms/GameGaraj"
  
  # Observability Endpoints
  elasticUri: "http://192.168.1.56:9201"
  otlpEndpoint: "http://192.168.1.56:4317"
```

### Microservice Pod'larındaki Enjeksiyon

[microservice.yaml](file:///d:/Kadir/Projeler/GameGaraj/helm/gamegaraj/templates/microservice.yaml) içinde bu değişkenler pod'lara şu şekilde enjekte edilir:

```yaml
            - name: ElasticSearchSettings__Uri
              value: {{ $.Values.global.elasticUri | quote }}
            - name: OpenTelemetry__OtlpEndpoint
              value: {{ $.Values.global.otlpEndpoint | quote }}
```

Bu sayede:
1. **Serilog logları** `ElasticSearchSettings__Uri` değerine (Dokploy ES) gönderilir.
2. **OTel trace'leri** `OpenTelemetry__OtlpEndpoint` değerine (Dokploy Jaeger gRPC alıcısı) push edilir.

---

## 2. Pod Annotations (Prometheus Scraping için)

Eğer Prometheus'u Dokploy yerine **k3s cluster içinde** çalıştırmayı seçerseniz (Helm Chart içindeki `monitoring.enabled: true` ayarı ile), Prometheus pod'ları otomatik keşfetmek için pod annotation'larını okur.

Şablonumuz her microservice deployment'ı için bu annotation'ları otomatik üretmektedir:

```yaml
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
```

Eğer Dokploy üzerindeki Prometheus'u kullanacaksanız, servislerin dışarıya açtığı **NodePort**'lar üzerinden scraping yapılır. Bu durumda k3s cluster içindeki Prometheus deployment'ını `values.yaml` üzerinden kapatabilirsiniz:

```yaml
monitoring:
  enabled: false # Dokploy stack'i kullanıldığı için k3s içi izleme kapatılabilir
```

---

## 3. Bağlantı Kontrolü ve Sorun Giderme

Mikroservis pod'larının Dokploy'a erişebildiğini doğrulamak için k3s üzerindeki herhangi bir pod içinden test gerçekleştirebilirsiniz:

```bash
# k3s worker/master shell'inden veya pod içerisinden Dokploy portlarına istek atma:
nc -zv 192.168.1.56 9201  # Elasticsearch Log Portu
nc -zv 192.168.1.56 4317  # Jaeger gRPC Trace Portu
```

Eğer `Connection refused` veya `Timeout` alıyorsanız:
1. Dokploy hostundaki firewall (ufw vb.) kurallarını kontrol edin ve `9201`, `4317` portlarına k3s cluster IP aralığından gelen istekler için izin verin.
2. docker-compose üzerinde container'ların sağlıklı bir şekilde çalıştığını `docker ps` ile doğrulayın.
