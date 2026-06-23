namespace GameGaraj.WebUI.Models.Products
{
    public class ProductCreateInput
    {
        private string? _brand;
        private string? _slug;
        private string? _description;
        private string? _coverImageKey;

        public string Name { get; set; } = string.Empty;

        public string? Brand
        {
            get => _brand ?? string.Empty;
            set => _brand = value;
        }

        public string? Slug
        {
            get => _slug ?? string.Empty;
            set => _slug = value;
        }

        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; }

        public string? Description
        {
            get => _description ?? string.Empty;
            set => _description = value;
        }

        public string CategoryId { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();

        public string? CoverImageKey
        {
            get => _coverImageKey ?? string.Empty;
            set => _coverImageKey = value;
        }
        public List<string> ImageOrder { get; set; } = new();
        public IFormFileCollection? Photos { get; set; }
        public Dictionary<string, string> Specs { get; set; } = new();
    }
}
