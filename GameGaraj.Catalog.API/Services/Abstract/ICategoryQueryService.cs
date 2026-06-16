using GameGaraj.Catalog.API.Dtos;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface ICategoryQueryService
    {
        Task<List<CategoryDto>> GetAllAsync();
        Task<CategoryDto?> GetByIdAsync(string id);
        Task<CategoryDto?> GetBySlugAsync(string slug);
        Task<List<CategoryAttributeDto>> GetAttributesAsync(string categoryId);
    }
}
