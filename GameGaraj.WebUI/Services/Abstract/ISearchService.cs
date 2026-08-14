using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Models.Common;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface ISearchService
    {
        Task<List<ProductViewModel>> SearchProductsAsync(string keyword);
        Task<List<SearchSuggestionViewModel>> SearchSuggestionsAsync(string keyword);
        Task<List<string>> SearchBrandsAsync(string keyword);
        Task<List<ProductViewModel>> GetFeaturedProductsAsync();
        Task<ProductViewModel?> GetProductByIdAsync(string id);
        Task<ProductViewModel?> GetProductBySlugAsync(string slug);
        Task<List<ProductViewModel>> GetFilteredProductsAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string[]>? specs = null, string? brand = null);
        Task<SearchIndexStatusViewModel?> GetSearchIndexStatusAsync();
        Task<SearchIndexDocumentPageViewModel> GetSearchIndexDocumentPreviewsAsync(int page = 1, int pageSize = 100);
        Task<ReindexResultViewModel?> ReindexSearchIndexAsync();
    }
}
