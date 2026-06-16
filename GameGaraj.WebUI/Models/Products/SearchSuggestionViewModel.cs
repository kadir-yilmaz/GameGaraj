namespace GameGaraj.WebUI.Models.Products
{
    public class SearchSuggestionViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? Price { get; set; }
    }
}
