using GameGaraj.Catalog.API.Dtos;

namespace GameGaraj.Catalog.API.Services.Abstract
{
    public interface IProductQueryService
    {
        Task<List<ProductDto>> GetAllAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string>? specs = null, string? brand = null);
        Task<List<ProductDto>> GetFeaturedProductsAsync();
        Task<ProductDto?> GetByIdAsync(string id);
        Task<ProductDto?> GetBySlugAsync(string slug);
        Task<List<ProductDto>> GetByCategoryIdAsync(string categoryId);
        Task<List<ProductDto>> SearchAsync(string keyword);
        Task<List<string>> GetBrandsByKeywordAsync(string keyword);
        Task<List<SearchSuggestionDto>> GetSuggestionsAsync(string keyword);
        Task<SearchFacetResultDto> GetSearchFacetsAsync(string? keyword);
    }
}
