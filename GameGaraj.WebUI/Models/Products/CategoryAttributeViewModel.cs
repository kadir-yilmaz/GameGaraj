namespace GameGaraj.WebUI.Models.Products
{
    public class CategoryAttributeViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
    }
}
