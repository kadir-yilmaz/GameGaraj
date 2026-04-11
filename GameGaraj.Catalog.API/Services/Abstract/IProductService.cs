using GameGaraj.Catalog.API.Dtos;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface IProductService
    {
        Task<List<ProductDto>> GetAllAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string>? specs = null);
        Task<List<ProductDto>> GetFeaturedProductsAsync();
        Task<ProductDto?> GetByIdAsync(string id);
        Task<List<ProductDto>> GetByCategoryIdAsync(string categoryId);
        Task<ProductDto> CreateAsync(ProductCreateDto dto);
        Task<bool> UpdateAsync(ProductUpdateDto dto);
        Task<bool> DeleteAsync(string id);
        Task<List<ProductDto>> SearchAsync(string keyword);
        Task<List<string>> GetBrandsByKeywordAsync(string keyword);
    }
}
