using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Models.Common;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface ICatalogService
    {
        Task<List<ProductViewModel>> GetAllProductsAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string[]>? specs = null, string? brand = null);
        Task<List<ProductViewModel>> GetFeaturedProductsAsync();
        Task<ProductViewModel?> GetProductByIdAsync(string id);
        Task<ProductViewModel?> GetProductBySlugAsync(string slug);
        Task<List<CategoryViewModel>> GetAllCategoriesAsync();
        Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId);
        Task<List<ProductViewModel>> SearchProductsAsync(string keyword);
        Task<List<CategoryViewModel>> SearchCategoriesAsync(string keyword);
        Task<List<string>> SearchBrandsAsync(string keyword);
        Task<List<SearchSuggestionViewModel>> SearchSuggestionsAsync(string keyword);
        Task<SearchIndexStatusViewModel?> GetSearchIndexStatusAsync();
        Task<SearchIndexDocumentPageViewModel> GetSearchIndexDocumentPreviewsAsync(int page = 1, int pageSize = 100);
        Task<ReindexResultViewModel?> ReindexSearchIndexAsync();
        Task<PagedResultViewModel<ProductViewModel>> GetAdminProductsPageAsync(string? query = null, string? categoryId = null, bool? isFeatured = null, bool? isActive = null, string? stockState = null, int page = 1, int pageSize = 20);

        // Admin Methods
        Task<CategoryViewModel?> CreateCategoryAsync(CategoryCreateInput model);
        Task<CategoryViewModel?> UpdateCategoryAsync(string id, CategoryCreateInput model);
        Task<bool> DeleteCategoryAsync(string id);
        Task<bool> ToggleCategoryShowOnHomeAsync(string id);
        Task<bool> AddAttributeAsync(string categoryId, CategoryAttributeInput model);
        Task<bool> UpdateAttributeAsync(string categoryId, string attributeId, CategoryAttributeInput model);
        Task<bool> DeleteAttributeAsync(string categoryId, string attributeId);
        Task<CategoryViewModel?> GetCategoryByIdAsync(string id);
        Task<CategoryViewModel?> GetCategoryBySlugAsync(string slug);
        Task<ProductViewModel?> CreateProductAsync(ProductCreateInput model);
        Task<bool> UpdateProductAsync(ProductUpdateInput model);
        Task<bool> DeleteProductAsync(string id);
    }
}
