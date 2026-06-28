# 🎓 Structured Logging (Yapılandırılmış Günlükleme) - Bölüm 2: Kibana, ILM ve Saklama Politikaları

Bu bölümde, Elasticsearch'e akan logları Kibana arayüzünden nasıl inceleyeceğimizi ve veritabanımızın şişmesini önlemek için uyguladığımız **Index Lifecycle Management (ILM - İndeks Yaşam Döngüsü)** politikalarını öğreneceğiz.

---

## 1. Kibana Üzerinden Arama ve Filtreleme (KQL)

Elasticsearch'teki loglarımızı görselleştirmek için **Kibana** arayüzünü kullanırız.
*   **Adres:** 👉 [http://192.168.1.56:5601](http://192.168.1.56:5601)

### Yararlı KQL (Kibana Query Language) Arama Örnekleri:
*   **Sadece Hataları Listele:** `level: "Error" OR level: "Fatal"`
*   **Belirli Bir Servise Ait Loglar:** `fields.Service: "Catalog.API"`
*   **Giriş Yapmış Üye İsteklerini Filtrele (Arama İndeksinde):** `LogType: "HttpRequest" AND fields.IsAuthenticated: true`
*   **Sepete Eklenen POST İsteklerini Filtrele:** `LogType: "HttpRequest" AND fields.Method: "POST" AND fields.RequestPath: "*basket*"`

---

## 2. Elasticsearch İndeks Şablonları ve ILM Nedir?

Canlı ortamlarda günde milyonlarca log oluşabilir. Eğer logları tek bir devasa indeks içine kontrolsüzce yazarsak, Elasticsearch bir süre sonra disk yetersizliğinden çöker veya aramalar aşırı yavaşlar. 

Bunun önüne geçmek için **ILM (Index Lifecycle Management)** politikası uygularız.

### A. Sıcak ve Soğuk Aşama (Hot / Delete Phases)
Bizim kurduğumuz ILM politikasında loglar şu döngüden geçer:
1.  **Hot (Sıcak) Aşama:** Loglar aktif olarak yazılır. İndeks boyutu **10 GB**'a ulaştığında veya **7 gün** geçtiğinde indeks "Rollover" olur (kapatılır ve yeni boş bir indeks açılır).
2.  **Delete (Silme) Aşama:** İndeks kapatıldıktan **30 gün** sonra otomatik olarak diskten tamamen silinir. Böylece diskte asla 30 günden eski log tutulmaz, disk dolma riski engellenir.

---

## 3. WebUI "ILM Eşitle" (Sync ILM) Butonu Nasıl Çalışır?

Kendi yazdığımız admin panelinde (`ObservabilityAdminController.cs` altında) bir **"ILM Eşitle"** butonu bulunur. Bu buton arka planda şu işlemleri tetikler:

1.  **ILM Politikasını Yükler:** `gamegaraj-ilm-policy` adında bir kural setini Elasticsearch API'sine gönderir. (Max age: 7d, Max size: 10gb, Delete after: 30d).
2.  **İndeks Şablonlarını (Templates) Oluşturur:** `gamegaraj-logs-template` ve `gamegaraj-requests-template` şablonlarını kaydeder.
    *   Bu şablonlar, Elasticsearch'e yeni bir `gamegaraj-logs-*` veya `gamegaraj-requests-*` indeksi geldiğinde otomatik olarak hangi ayarlarla (shard sayısı, replica sayısı ve ILM rollover alias'ı) oluşturulacağını söyler.
3.  **Bootstrap İndekslerini Başlatır:** Eğer daha önce hiç indeks oluşmadıysa, yazma işleminin başlayabilmesi için ilk tetikleyici alias indeksini oluşturur (`gamegaraj-logs-000001` ve `gamegaraj-requests-000001`).

Eğer Elasticsearch'ü sıfırlarsanız veya yeni bir sunucu kurarsanız, Admin paneline gidip **"ILM Eşitle"** butonuna basarak tüm bu Elasticsearch altyapısını tek bir tıkla yeniden kurabilirsiniz.
