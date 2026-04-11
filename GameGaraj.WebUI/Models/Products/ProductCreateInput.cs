namespace GameGaraj.WebUI.Models.Products
{
    public class ProductCreateInput
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public IFormFileCollection? Photos { get; set; }
        public Dictionary<string, string> Specs { get; set; } = new();
    }
}
