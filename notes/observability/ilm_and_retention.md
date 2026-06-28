# Elasticsearch ILM (Index Lifecycle Management) & Retention Setup

Elasticsearch üzerindeki log birikmesini önlemek, disk doluluğunu engellemek ve yasal/operasyonel gereksinimleri karşılamak amacıyla **log retention (saklama) politikası** uygulanmalıdır.

## 1. Retention Politikası Detayları

Oluşturduğumuz [ilm-policy.json](file:///d:/Kadir/Projeler/GameGaraj/config/elasticsearch/ilm-policy.json) dosyası ile logların ömrü 4 aşamada yönetilir:

*   **Hot Phase (0 - 7 Gün):** Loglar bu aşamada yazılır ve hızlıca sorgulanır. Günlük olarak ya da indeks boyutu **5GB**'a ulaştığında yeni indekse geçiş (rollover) yapılır.
*   **Warm Phase (7 - 30 Gün):** İndeksler salt okunur hale getirilir, shard sayısı 1'e düşürülerek küçültülür (`shrink`) ve disk alanı kazanmak için birleştirilir (`forcemerge`).
*   **Cold Phase (30 - 180 Gün):** Loglar seyrek sorgular için saklanır. Arama öncelikleri düşürülür.
*   **Delete Phase (180 Gün Sonrası):** Loglar kalıcı olarak diskten silinir.

---

## 2. Politikayı ve İndeks Şablonunu Uygulama

Hazırladığımız [setup-elasticsearch-ilm.sh](file:///d:/Kadir/Projeler/GameGaraj/scripts/setup-elasticsearch-ilm.sh) betiğini çalıştırarak bu politikayı Elasticsearch'e tek komutla yükleyebilirsiniz.

### Yerel Ortamda Uygulama
```bash
# Proje kök dizininde terminali açın:
bash scripts/setup-elasticsearch-ilm.sh http://localhost:9201
```

### Canlı Ortamda (Dokploy Elasticsearch için) Uygulama
```bash
# Dokploy üzerindeki Elasticsearch URL'sini parametre olarak geçin:
bash scripts/setup-elasticsearch-ilm.sh http://192.168.1.56:9201
```

---

## 3. Yapılandırmanın Doğrulanması

Politikanın düzgün yüklendiğini teyit etmek için aşağıdaki komutları kullanabilirsiniz:

```bash
# ILM Politikasını sorgulama:
curl -s http://192.168.1.56:9201/_ilm/policy/gamegaraj-logs-policy

# Index Template'ini sorgulama:
curl -s http://192.168.1.56:9201/_index_template/gamegaraj-logs-template
```

Bu adımlardan sonra, `gamegaraj-logs-*` deseniyle eşleşen tüm yeni log indeksleri (Serilog'un oluşturduğu) bu yaşam döngüsü kurallarına otomatik olarak tabi tutulacaktır.
