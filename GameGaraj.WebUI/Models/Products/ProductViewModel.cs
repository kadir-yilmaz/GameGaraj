namespace GameGaraj.WebUI.Models.Products
{
    public class ProductViewModel
    {
        public const string DefaultImageUrl = "/default.jpg";

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
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
        public IReadOnlyList<string> DisplayImageUrls
        {
            get
            {
                var validImageUrls = ImageUrls?
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToList();

                return validImageUrls is { Count: > 0 }
                    ? validImageUrls
                    : new List<string> { DefaultImageUrl };
            }
        }

        public string FirstImageUrl => DisplayImageUrls[0];

        public Dictionary<string, string> Specs { get; set; } = new();
    }
}
