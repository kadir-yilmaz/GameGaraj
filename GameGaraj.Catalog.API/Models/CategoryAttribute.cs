
namespace GameGaraj.Catalog.API.Models
{
    public class CategoryAttribute
    {
        public string Id { get; set; } = string.Empty;

        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AttributeType Type { get; set; }
        public List<string>? Options { get; set; } // For Dropdown type
        public bool IsRequired { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public enum AttributeType
    {
        Text,
        Number,
        Boolean,
        Dropdown
    }
}
