using GameGaraj.Catalog.API.Models;
using MongoDB.Bson;

namespace GameGaraj.Catalog.API.Services.Seed
{
    public static class SeedData
    {
        public static List<Category> GenerateCategories(DateTime now, out Dictionary<string, string> categoryIds)
        {
            var categoryList = new List<Category>();
            categoryIds = new Dictionary<string, string>();

            var mainCategories = new[]
            {
                ("bilgisayar_parcalari", "Bilgisayar Parçaları"),
                ("cevre_birimleri", "Çevre Birimleri"),
                ("laptop", "Laptop")
            };

            foreach (var (key, name) in mainCategories)
            {
                var id = ObjectId.GenerateNewId().ToString();
                categoryIds[key] = id;
                categoryList.Add(new Category { Id = id, Name = name, ParentId = null, CreatedAt = now, UpdatedAt = now });
            }

            var subCategories = new[]
            {
                ("islemci", "İşlemci", "bilgisayar_parcalari"),
                ("ekran_karti", "Ekran Kartı", "bilgisayar_parcalari"),
                ("ram", "RAM", "bilgisayar_parcalari"),
                ("anakart", "Anakart", "bilgisayar_parcalari"),
                ("kasa", "Kasa", "bilgisayar_parcalari"),
                ("guc_kaynagi", "Güç Kaynağı", "bilgisayar_parcalari"),

                ("klavye", "Klavye", "cevre_birimleri"),
                ("mouse", "Mouse", "cevre_birimleri"),
                ("kulaklik", "Kulaklık", "cevre_birimleri"),
                ("monitor", "Monitör", "cevre_birimleri"),
                ("webcam", "Webcam", "cevre_birimleri"),

                ("gaming_laptop", "Gaming Laptop", "laptop"),
                ("is_laptop", "İş Laptopu", "laptop"),
                ("ogrenci_laptop", "Öğrenci Laptopu", "laptop")
            };

            foreach (var (key, name, parentKey) in subCategories)
            {
                var id = ObjectId.GenerateNewId().ToString();
                categoryIds[key] = id;
                categoryList.Add(new Category { Id = id, Name = name, ParentId = categoryIds[parentKey], CreatedAt = now, UpdatedAt = now });
            }

            return categoryList;
        }

        public static List<CategoryAttribute> GenerateCategoryAttributes(Dictionary<string, string> categoryIds, DateTime now)
        {
            var attributes = new List<CategoryAttribute>
            {
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["islemci"], Name = "soket", DisplayName = "Soket", Type = AttributeType.Dropdown, Options = new() { "AM5", "LGA1700", "AM4" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["islemci"], Name = "cekirdek_sayisi", DisplayName = "Çekirdek Sayısı", Type = AttributeType.Dropdown, Options = new() { "6", "8", "12", "16", "24" }, CreatedAt = now },

                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["ekran_karti"], Name = "vram", DisplayName = "VRAM", Type = AttributeType.Dropdown, Options = new() { "8GB", "12GB", "16GB", "24GB" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["ekran_karti"], Name = "cip", DisplayName = "Çip", Type = AttributeType.Text, CreatedAt = now },

                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["ram"], Name = "kapasite", DisplayName = "Kapasite", Type = AttributeType.Dropdown, Options = new() { "8GB", "16GB", "32GB", "64GB" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["ram"], Name = "hiz", DisplayName = "Hız", Type = AttributeType.Dropdown, Options = new() { "3200MHz", "3600MHz", "5200MHz", "6000MHz", "6400MHz" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["ram"], Name = "tip", DisplayName = "Tip", Type = AttributeType.Dropdown, Options = new() { "DDR4", "DDR5" }, CreatedAt = now },

                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["klavye"], Name = "switch_tipi", DisplayName = "Switch Tipi", Type = AttributeType.Dropdown, Options = new() { "Cherry MX Red", "Cherry MX Blue", "Cherry MX Brown", "Cherry MX Speed", "Optik", "Mekanik Olmayan" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["klavye"], Name = "layout", DisplayName = "Layout", Type = AttributeType.Dropdown, Options = new() { "Full Size", "TKL", "60%", "65%" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["klavye"], Name = "baglanti", DisplayName = "Bağlantı", Type = AttributeType.Dropdown, Options = new() { "Kablolu", "Kablosuz", "Bluetooth" }, CreatedAt = now },

                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["mouse"], Name = "dpi", DisplayName = "DPI", Type = AttributeType.Dropdown, Options = new() { "16000", "20000", "25600", "30000", "32000" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["mouse"], Name = "baglanti", DisplayName = "Bağlantı", Type = AttributeType.Dropdown, Options = new() { "Kablolu", "Kablosuz" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["mouse"], Name = "agirlik", DisplayName = "Ağırlık", Type = AttributeType.Dropdown, Options = new() { "Hafif (<70g)", "Orta (70-90g)", "Ağır (>90g)" }, CreatedAt = now },

                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["kulaklik"], Name = "driver", DisplayName = "Driver Boyutu", Type = AttributeType.Dropdown, Options = new() { "40mm", "50mm", "53mm" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["kulaklik"], Name = "baglanti", DisplayName = "Bağlantı", Type = AttributeType.Dropdown, Options = new() { "Kablolu", "Kablosuz", "Bluetooth" }, CreatedAt = now },
                new() { Id = ObjectId.GenerateNewId().ToString(), CategoryId = categoryIds["kulaklik"], Name = "mikrofon", DisplayName = "Mikrofon", Type = AttributeType.Dropdown, Options = new() { "Var", "Yok" }, CreatedAt = now },
            };

            return attributes;
        }

        public static List<Product> GenerateProducts(Dictionary<string, string> categoryIds, DateTime now)
        {
            var products = new List<Product>();

            // İşlemci
            products.AddRange(new[]
            {
                CreateProduct("AMD Ryzen 7 7800X3D", "AMD", "3D V-Cache teknolojisi ile oyun parmak izi", 12500, 25, categoryIds["islemci"], true, new() { ["soket"] = "AM5", ["cekirdek_sayisi"] = "8" }, now),
                CreateProduct("AMD Ryzen 9 7950X", "AMD", "16 çekirdek yüksek performans", 18000, 15, categoryIds["islemci"], false, new() { ["soket"] = "AM5", ["cekirdek_sayisi"] = "16" }, now),
                CreateProduct("Intel Core i9-14900K", "Intel", "24 çekirdek amiral gemisi", 22000, 10, categoryIds["islemci"], false, new() { ["soket"] = "LGA1700", ["cekirdek_sayisi"] = "24" }, now),
                CreateProduct("Intel Core i7-14700K", "Intel", "20 çekirdek güç", 15500, 20, categoryIds["islemci"], false, new() { ["soket"] = "LGA1700", ["cekirdek_sayisi"] = "20" }, now),
                CreateProduct("AMD Ryzen 5 7600X", "AMD", "6 çekirdek bütçe dostu", 7500, 30, categoryIds["islemci"], false, new() { ["soket"] = "AM5", ["cekirdek_sayisi"] = "6" }, now),
            });

            // Ekran Kartı
            products.AddRange(new[]
            {
                CreateProduct("NVIDIA RTX 4090", "NVIDIA", "24GB VRAM amiral gemisi", 65000, 5, categoryIds["ekran_karti"], true, new() { ["vram"] = "24GB", ["cip"] = "AD102" }, now),
                CreateProduct("NVIDIA RTX 4080 Super", "NVIDIA", "16GB VRAM üst segment", 42000, 10, categoryIds["ekran_karti"], false, new() { ["vram"] = "16GB", ["cip"] = "AD103" }, now),
                CreateProduct("NVIDIA RTX 4070 Ti Super", "NVIDIA", "16GB VRAM üst-orta segment", 32000, 15, categoryIds["ekran_karti"], false, new() { ["vram"] = "16GB", ["cip"] = "AD104" }, now),
                CreateProduct("AMD RX 7900 XTX", "AMD", "24GB VRAM AMD amiral gemisi", 38000, 8, categoryIds["ekran_karti"], false, new() { ["vram"] = "24GB", ["cip"] = "Navi 31" }, now),
                CreateProduct("NVIDIA RTX 4060 Ti", "NVIDIA", "8GB VRAM orta segment", 15000, 25, categoryIds["ekran_karti"], false, new() { ["vram"] = "8GB", ["cip"] = "AD106" }, now),
            });

            // RAM
            products.AddRange(new[]
            {
                CreateProduct("Corsair Vengeance DDR5 32GB", "Corsair", "6000MHz yüksek hız", 4500, 40, categoryIds["ram"], false, new() { ["kapasite"] = "32GB", ["hiz"] = "6000MHz", ["tip"] = "DDR5" }, now),
                CreateProduct("G.Skill Trident Z5 RGB 32GB", "G.Skill", "6400MHz RGB hız canavarı", 5200, 30, categoryIds["ram"], false, new() { ["kapasite"] = "32GB", ["hiz"] = "6400MHz", ["tip"] = "DDR5" }, now),
                CreateProduct("Kingston Fury Beast 16GB", "Kingston", "5200MHz giriş DDR5", 2200, 50, categoryIds["ram"], false, new() { ["kapasite"] = "16GB", ["hiz"] = "5200MHz", ["tip"] = "DDR5" }, now),
                CreateProduct("Corsair Dominator Platinum 64GB", "Corsair", "6000MHz 64GB kit", 9500, 10, categoryIds["ram"], false, new() { ["kapasite"] = "64GB", ["hiz"] = "6000MHz", ["tip"] = "DDR5" }, now),
                CreateProduct("TeamGroup T-Force Delta 32GB", "TeamGroup", "5600MHz RGB tasarım", 3800, 35, categoryIds["ram"], false, new() { ["kapasite"] = "32GB", ["hiz"] = "5600MHz", ["tip"] = "DDR5" }, now),
            });

            // Klavye
            products.AddRange(new[]
            {
                CreateProduct("Corsair K100 RGB", "Corsair", "Premium optomekanik klavye", 8500, 20, categoryIds["klavye"], true, new() { ["switch_tipi"] = "Cherry MX Speed", ["layout"] = "Full Size", ["baglanti"] = "Kablolu" }, now),
                CreateProduct("Logitech G Pro X", "Logitech", "Esports profesyonel klavye", 4200, 30, categoryIds["klavye"], false, new() { ["switch_tipi"] = "Cherry MX Blue", ["layout"] = "TKL", ["baglanti"] = "Kablolu" }, now),
                CreateProduct("Razer Huntsman V3 Pro", "Razer", "Analog optik switch klavye", 7500, 15, categoryIds["klavye"], false, new() { ["switch_tipi"] = "Optik", ["layout"] = "Full Size", ["baglanti"] = "Kablosuz" }, now),
                CreateProduct("SteelSeries Apex Pro", "SteelSeries", "Ayarlanabilir OmniPoint switch", 6500, 18, categoryIds["klavye"], false, new() { ["switch_tipi"] = "Mekanik Olmayan", ["layout"] = "Full Size", ["baglanti"] = "Kablolu" }, now),
                CreateProduct("Ducky One 3", "Ducky", "Mekanik klavye klasiği", 3500, 25, categoryIds["klavye"], false, new() { ["switch_tipi"] = "Cherry MX Brown", ["layout"] = "TKL", ["baglanti"] = "Kablolu" }, now),
            });

            // Mouse
            products.AddRange(new[]
            {
                CreateProduct("Logitech G Pro X Superlight 2", "Logitech", "60g ultralight kablosuz", 5500, 25, categoryIds["mouse"], false, new() { ["dpi"] = "32000", ["baglanti"] = "Kablosuz", ["agirlik"] = "Hafif (<70g)" }, now),
                CreateProduct("Razer DeathAdder V3 Pro", "Razer", "Ergonomik kablosuz şampiyon", 5000, 30, categoryIds["mouse"], false, new() { ["dpi"] = "30000", ["baglanti"] = "Kablosuz", ["agirlik"] = "Orta (70-90g)" }, now),
                CreateProduct("Zowie EC2-CW", "Zowie", "Esports profesyonel kablosuz", 4500, 20, categoryIds["mouse"], false, new() { ["dpi"] = "16000", ["baglanti"] = "Kablosuz", ["agirlik"] = "Orta (70-90g)" }, now),
                CreateProduct("Finalmouse UltralightX", "Finalmouse", "Dünyanın en hafif mouseu", 6500, 10, categoryIds["mouse"], false, new() { ["dpi"] = "25600", ["baglanti"] = "Kablosuz", ["agirlik"] = "Hafif (<70g)" }, now),
                CreateProduct("Pulsar X2 Mini", "Pulsar", "Küçük el için ultralight", 3200, 35, categoryIds["mouse"], false, new() { ["dpi"] = "25600", ["baglanti"] = "Kablosuz", ["agirlik"] = "Hafif (<70g)" }, now),
            });

            // Kulaklık
            products.AddRange(new[]
            {
                CreateProduct("SteelSeries Arctis Nova Pro", "SteelSeries", "Hi-Fi multi-system kablosuz", 12000, 15, categoryIds["kulaklik"], false, new() { ["driver"] = "40mm", ["baglanti"] = "Kablosuz", ["mikrofon"] = "Var" }, now),
                CreateProduct("Logitech G Pro X 2 Lightspeed", "Logitech", "Esports amiral gemisi kulaklık", 7500, 20, categoryIds["kulaklik"], false, new() { ["driver"] = "50mm", ["baglanti"] = "Kablosuz", ["mikrofon"] = "Var" }, now),
                CreateProduct("HyperX Cloud III Wireless", "HyperX", "Konfor odaklı kablosuz", 5500, 25, categoryIds["kulaklik"], false, new() { ["driver"] = "53mm", ["baglanti"] = "Kablosuz", ["mikrofon"] = "Var" }, now),
                CreateProduct("Razer BlackShark V2 Pro", "Razer", "THX Spatial Audio desteği", 6000, 22, categoryIds["kulaklik"], false, new() { ["driver"] = "50mm", ["baglanti"] = "Kablosuz", ["mikrofon"] = "Var" }, now),
                CreateProduct("Beyerdynamic MMX 300", "Beyerdynamic", "Audiophile kalite oyun kulaklığı", 9500, 8, categoryIds["kulaklik"], false, new() { ["driver"] = "40mm", ["baglanti"] = "Kablolu", ["mikrofon"] = "Var" }, now),
            });

            // Gaming Laptop
            products.AddRange(new[]
            {
                CreateProduct("ASUS ROG Strix G18", "ASUS", "RTX 4090 18 inç canavar", 95000, 5, categoryIds["gaming_laptop"], true, new() { ["gpu"] = "RTX 4090", ["cpu"] = "i9-14900HX", ["ram"] = "32GB" }, now),
                CreateProduct("MSI Titan 18 HX", "MSI", "RTX 4090 masaüstü alternatifi", 120000, 3, categoryIds["gaming_laptop"], false, new() { ["gpu"] = "RTX 4090", ["cpu"] = "i9-14900HX", ["ram"] = "64GB" }, now),
                CreateProduct("Lenovo Legion Pro 7i", "Lenovo", "RTX 4080 güç ve değer dengesi", 75000, 8, categoryIds["gaming_laptop"], false, new() { ["gpu"] = "RTX 4080", ["cpu"] = "i9-14900HX", ["ram"] = "32GB" }, now),
                CreateProduct("Razer Blade 16", "Razer", "RTX 4090 ince şık tasarım", 110000, 4, categoryIds["gaming_laptop"], false, new() { ["gpu"] = "RTX 4090", ["cpu"] = "i9-14900HX", ["ram"] = "32GB" }, now),
                CreateProduct("ASUS TUF Gaming A15", "ASUS", "RTX 4060 bütçe dostu oyun laptopu", 42000, 15, categoryIds["gaming_laptop"], false, new() { ["gpu"] = "RTX 4060", ["cpu"] = "Ryzen 9 7940HS", ["ram"] = "16GB" }, now),
            });

            // İş Laptopu
            products.AddRange(new[]
            {
                CreateProduct("Lenovo ThinkPad X1 Carbon", "Lenovo", "İş dünyasının tercihi ultrabook", 65000, 10, categoryIds["is_laptop"], false, new() { ["cpu"] = "i7-1365U", ["ram"] = "32GB", ["ekran"] = "14 inch" }, now),
                CreateProduct("Dell XPS 15", "Dell", "Üretkenlik odaklı premium laptop", 58000, 12, categoryIds["is_laptop"], false, new() { ["cpu"] = "i7-13700H", ["ram"] = "32GB", ["ekran"] = "15.6 inch" }, now),
                CreateProduct("HP EliteBook 860", "HP", "Kurumsal güvenlik ve performans", 52000, 15, categoryIds["is_laptop"], false, new() { ["cpu"] = "i7-1365U", ["ram"] = "16GB", ["ekran"] = "16 inch" }, now),
                CreateProduct("Apple MacBook Pro 16", "Apple", "M3 Max çip olağanüstü performans", 145000, 6, categoryIds["is_laptop"], false, new() { ["cpu"] = "M3 Max", ["ram"] = "48GB", ["ekran"] = "16 inch" }, now),
                CreateProduct("Microsoft Surface Laptop 6", "Microsoft", "Şık tasarım üretkenlik laptopu", 48000, 18, categoryIds["is_laptop"], false, new() { ["cpu"] = "i7-1365U", ["ram"] = "16GB", ["ekran"] = "15 inch" }, now),
            });

            // Öğrenci Laptopu
            products.AddRange(new[]
            {
                CreateProduct("Lenovo IdeaPad Slim 5", "Lenovo", "Öğrenci favorisi hafif laptop", 22000, 30, categoryIds["ogrenci_laptop"], false, new() { ["cpu"] = "Ryzen 5 7530U", ["ram"] = "16GB", ["ekran"] = "15.6 inch" }, now),
                CreateProduct("HP Pavilion 15", "HP", "Giriş seviye çok yönlü laptop", 18000, 35, categoryIds["ogrenci_laptop"], false, new() { ["cpu"] = "i5-1335U", ["ram"] = "8GB", ["ekran"] = "15.6 inch" }, now),
                CreateProduct("ASUS VivoBook 15", "ASUS", "Taşınabilir günlük kullanım laptopu", 16000, 40, categoryIds["ogrenci_laptop"], false, new() { ["cpu"] = "Ryzen 5 5500U", ["ram"] = "8GB", ["ekran"] = "15.6 inch" }, now),
                CreateProduct("Acer Aspire 5", "Acer", "Performans odaklı öğrenci laptopu", 14500, 45, categoryIds["ogrenci_laptop"], false, new() { ["cpu"] = "i5-1235U", ["ram"] = "8GB", ["ekran"] = "15.6 inch" }, now),
                CreateProduct("Dell Inspiron 15", "Dell", "Güvenilir günlük kullanım laptopu", 19500, 28, categoryIds["ogrenci_laptop"], false, new() { ["cpu"] = "i5-1335U", ["ram"] = "16GB", ["ekran"] = "15.6 inch" }, now),
            });

            return products;
        }

        private static Product CreateProduct(
            string name, string brand, string description, decimal price, int stock,
            string categoryId, bool isFeatured, Dictionary<string, string> specs, DateTime createdAt)
        {
            return new Product
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = name,
                Brand = brand,
                Description = description,
                Price = price,
                Stock = stock,
                ReservedStock = 0,
                CategoryId = categoryId,
                IsActive = true,
                IsFeatured = isFeatured,
                Specs = specs,
                ImageUrls = new List<string> { "https://images.unsplash.com/photo-1587202372634-32705e3bf49c?auto=format&fit=crop&q=80&w=800" },
                CreatedAt = createdAt,
                UpdatedAt = null
            };
        }
    }
}
