namespace GameGaraj.WebUI.Models.Products
{
    public class ProductViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int ReservedStock { get; set; }
        public int AvailableStock { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsInBasket { get; set; }
        public bool IsFavorite { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string FirstImageUrl => (ImageUrls != null && ImageUrls.Any())
            ? ImageUrls.First()
            : "https://plus.unsplash.com/premium_photo-1682141882061-c7676602e111?w=800&auto=format&fit=crop&q=80";

        public Dictionary<string, string> Specs { get; set; } = new();
    }
}
