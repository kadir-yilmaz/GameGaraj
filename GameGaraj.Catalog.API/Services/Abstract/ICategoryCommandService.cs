using GameGaraj.Catalog.API.Dtos;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface ICategoryCommandService
    {
        Task<CategoryDto> CreateAsync(CategoryCreateDto dto);
        Task<CategoryDto?> UpdateAsync(string id, CategoryCreateDto dto);
        Task<CategoryAttributeDto> AddAttributeAsync(string categoryId, CategoryAttributeCreateDto dto);
        Task<CategoryAttributeDto?> UpdateAttributeAsync(string categoryId, string attributeId, CategoryAttributeCreateDto dto);
        Task<bool> DeleteAttributeAsync(string categoryId, string attributeId);
        Task<bool> ToggleShowOnHomeAsync(string id);
        Task<bool> DeleteAsync(string id);
    }
}
