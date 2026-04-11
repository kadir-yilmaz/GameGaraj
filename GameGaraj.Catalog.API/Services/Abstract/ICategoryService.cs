using GameGaraj.Catalog.API.Dtos;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface ICategoryService
    {
        Task<List<CategoryDto>> GetAllAsync();
        Task<CategoryDto?> GetByIdAsync(string id);
        Task<CategoryDto> CreateAsync(CategoryCreateDto dto);
        Task<CategoryDto?> UpdateAsync(string id, CategoryCreateDto dto);
        Task<List<CategoryAttributeDto>> GetAttributesAsync(string categoryId);
        Task<CategoryAttributeDto> AddAttributeAsync(string categoryId, CategoryAttributeCreateDto dto);
        Task<CategoryAttributeDto?> UpdateAttributeAsync(string categoryId, string attributeId, CategoryAttributeCreateDto dto);
        Task<bool> DeleteAttributeAsync(string categoryId, string attributeId);
    }
}
