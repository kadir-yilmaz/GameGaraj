namespace GameGaraj.Shared.Events
{
    public class ProductCreatedForSearch
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int ReservedStock { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public string CategoryId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string CategorySlug { get; set; } = string.Empty;
        public Dictionary<string, string> Specs { get; set; } = new();
    }

    public class ProductUpdatedForSearch : ProductCreatedForSearch
    {
    }

    public class ProductDeletedForSearch
    {
        public string Id { get; set; } = string.Empty;
    }
}
