namespace GameGaraj.Catalog.API.Dtos
{
    public class ProductDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalStock { get; set; }
        public int ReservedStock { get; set; }
        public int AvailableStock { get; set; }
        public int Stock => TotalStock; // Legacy support for internal mapping
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public string CategoryId { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public Dictionary<string, string> Specs { get; set; } = new();
    }

    public class ProductCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalStock { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public string CategoryId { get; set; } = string.Empty;
        public Dictionary<string, string> Specs { get; set; } = new();
    }

    public class ProductUpdateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalStock { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public string CategoryId { get; set; } = string.Empty;
        public Dictionary<string, string> Specs { get; set; } = new();
    }
}
