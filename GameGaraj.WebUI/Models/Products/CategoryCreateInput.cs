namespace GameGaraj.WebUI.Models.Products
{
    public class CategoryCreateInput
    {
        public string Name { get; set; } = string.Empty;
        public string? ParentId { get; set; }
    }
}
