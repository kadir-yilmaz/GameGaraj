namespace GameGaraj.Catalog.API.Dtos
{
    public class CategoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public int ProductCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<CategoryDto> Children { get; set; } = new();
        public List<CategoryAttributeDto> Attributes { get; set; } = new();
    }

    public class CategoryCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? ParentId { get; set; }
    }

    public class CategoryAttributeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
    }

    public class CategoryAttributeCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = "String";
        public List<string>? Options { get; set; }
    }
}
