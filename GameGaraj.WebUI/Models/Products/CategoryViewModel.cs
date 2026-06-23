namespace GameGaraj.WebUI.Models.Products
{
    public class CategoryViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public string? ParentName { get; set; }
        public bool IsShowOnHome { get; set; }
        public int ProductCount { get; set; }
        public List<CategoryAttributeViewModel> Attributes { get; set; } = new();
        public List<CategoryViewModel> Children { get; set; } = new();
    }

    public class CategoryDropdownViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
