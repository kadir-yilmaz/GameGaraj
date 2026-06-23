
namespace GameGaraj.Catalog.API.Models
{
    public class Category
    {
        public string Id { get; set; } = string.Empty;
        
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string? ParentId { get; set; }
        
        public bool IsShowOnHome { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
