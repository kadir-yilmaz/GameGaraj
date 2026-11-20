namespace GameGaraj.WebUI.Areas.Admin.Models
{
    public class PipelineScenarioViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "bg-primary";
        public string Icon { get; set; } = "fa-diagram-project";
        public string Summary { get; set; } = string.Empty;
        public string Importance { get; set; } = string.Empty;
        public List<string> Expectations { get; set; } = new();
        public bool ShowValveControlsByDefault { get; set; } = false;

        public static List<PipelineScenarioViewModel> GetAll() => new()
        {
            new PipelineScenarioViewModel
            {
                Key = "normal",
                Title = "Normal Sipariş Akışı",
                Category = "Happy Path",
                BadgeClass = "bg-success",
                Icon = "fa-circle-check",
                Summary = "Tüm servisler sağlıklı iken stok rezervasyonu, sipariş oluşturma, ödeme ve event dağıtımını uçtan uca test eder.",
                Importance = "Mikroservis mimarisinde senkron (HTTP) ve asenkron (RabbitMQ / Outbox) adımların temel referans akışını doğrular.",
                Expectations = new()
                {
                    "Adım 1 (Catalog.API): Ürün stoğu doğrulanır ve Rezerve +1 artar.",
                    "Adım 2 (Order.API): Sipariş MSSQL'e Pending (0) statüsünde yazılır.",
                    "Adım 3 (Payment.API): Geçerli karttan ödeme çekilir ve PaymentCompleted eventi yayınlanır.",
                    "Adım 4 (RabbitMQ): Rezerve stok kalıcı stoktan düşer, sipariş Completed (1) olur."
                }
            },
            new PipelineScenarioViewModel
            {
                Key = "polly_retry",
                Title = "Polly Retry (Geçici Hata Kurtarma)",
                Category = "Resilience",
                BadgeClass = "bg-warning text-dark",
                Icon = "fa-rotate-right",
                Summary = "Catalog.API ilk 2 istekte HTTP 503 döndürür; Polly Exponential Backoff ile 3. denemede isteği otomatik kurtarır.",
                Importance = "Ağ dalgalanmaları, container restart veya anlık yük artışlarında sistemin kullanıcıya hata yansıtmadan kendini toparlamasını sağlar.",
                Expectations = new()
                {
                    "Adım 1 başlatıldığında Catalog.API ilk 2 istekte 503 döner (yaklaşık 2sn + 4sn bekleme).",
                    "Konsolda Polly Retry 1/3 ve 2/3 uyarıları canlı olarak görünür.",
                    "3. denemede HTTP 200 alınır ve sipariş akışı kesintisiz tamamlanır."
                }
            },
            new PipelineScenarioViewModel
            {
                Key = "compensate",
                Title = "Saga Compensate / Rollback",
                Category = "Saga Pattern",
                BadgeClass = "bg-danger",
                Icon = "fa-shield-halved",
                Summary = "Stok rezerve edilip sipariş açıldıktan sonra ödeme adımında hata alınır; önceki adımlar otomatik geri alınır.",
                Importance = "Dağıtık sistemlerde tek bir veritabanı transaction'ı (2PC) yoktur. Geç bir adımda hata olursa yarım kalan stok ve sipariş kayıtlarının tutarlı biçimde telafi edilmesi zorunludur.",
                Expectations = new()
                {
                    "Adım 1: Catalog DB'de Rezerve +1 olur.",
                    "Adım 2: Order DB'de sipariş Pending (0) olarak açılır.",
                    "Adım 3: Yetersiz bakiye kartı nedeniyle Payment.API hata döner ve PaymentFailed eventi yayınlanır.",
                    "Telafi (1-4 sn içinde): Catalog DB'de rezerve stok iade edilir, Order DB'de sipariş statüsü Failed (2) olur."
                }
            },
            new PipelineScenarioViewModel
            {
                Key = "nostock",
                Title = "Stok Yok Koruması (Fail-Fast)",
                Category = "Validation",
                BadgeClass = "bg-secondary",
                Icon = "fa-ban",
                Summary = "Stoğu 0 olan veya silinmiş bir ürünle sipariş başlatılır; sistem akışı daha ilk adımda güvenli şekilde keser.",
                Importance = "Geçersiz veya stoksuz işlemlerde boşuna sipariş kaydı açılmasını ve ödeme gateway'ine istek atılmasını engeller.",
                Expectations = new()
                {
                    "Adım 1 (Catalog.API): HTTP 400 döner ve boru hattı durdurulur.",
                    "Order.API ve Payment.API adımlarına hiç geçilmez.",
                    "DB'de yeni sipariş kaydı oluşturulmaz, karttan çekim yapılmaz."
                }
            },
            new PipelineScenarioViewModel
            {
                Key = "latency",
                Title = "Yüksek Gecikme Testi (3sn)",
                Category = "Observability",
                BadgeClass = "bg-primary",
                Icon = "fa-clock",
                Summary = "Catalog.API yanıtlarına 3 saniye yapay gecikme eklenerek yavaşlayan servis davranışı ve süre metrikleri gözlemlenir.",
                Importance = "Yavaş çalışan bir bağımlılığın uçtan uca işlem süresini nasıl etkilediğini ve timeout sınırları içinde kalınıp kalınmadığını ölçer.",
                Expectations = new()
                {
                    "Adım 1 (Catalog.API): 3000ms+ sürede tamamlanır.",
                    "Timeout aşılmadığı için Polly retry tetiklenmez ve akış başarıyla devam eder."
                }
            },
            new PipelineScenarioViewModel
            {
                Key = "custom",
                Title = "Serbest Chaos Lab",
                Category = "Manual Control",
                BadgeClass = "bg-dark",
                Icon = "fa-sliders",
                Summary = "Tüm mikroservis vanalarını (Aktif, Retry, 3sn, Uykuda) manuel yöneterek kendi hata senaryonuzu oluşturun.",
                Importance = "Farklı servis kombinasyonlarının (örneğin Order.API uykuda veya Payment.API retry modunda) sisteme etkisini serbestçe test etmenizi sağlar.",
                Expectations = new()
                {
                    "Servis vanalarından istediğiniz kuralı seçip Otomatik veya Adım ile çalıştırabilirsiniz.",
                    "Her adımdan sonra Catalog DB ve Order DB durumları güncellenir."
                },
                ShowValveControlsByDefault = true
            }
        };

        public static PipelineScenarioViewModel GetByKey(string? key) =>
            GetAll().FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? GetAll().First();
    }
}
