namespace GameGaraj.WebUI.Models.Products
{
    public class CategoryAttributeInput
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = "Text"; // Text, Number, Dropdown
        public List<string>? Options { get; set; }
    }
}
