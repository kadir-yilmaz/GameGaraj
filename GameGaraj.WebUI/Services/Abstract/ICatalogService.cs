using GameGaraj.WebUI.Models.Products;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface ICatalogService
    {
        Task<List<ProductViewModel>> GetAllProductsAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string[]>? specs = null);
        Task<List<ProductViewModel>> GetFeaturedProductsAsync();
        Task<ProductViewModel?> GetProductByIdAsync(string id);
        Task<ProductViewModel?> GetProductBySlugAsync(string slug);
        Task<List<CategoryViewModel>> GetAllCategoriesAsync();
        Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId);
        Task<List<ProductViewModel>> SearchProductsAsync(string keyword);
        Task<List<CategoryViewModel>> SearchCategoriesAsync(string keyword);
        Task<List<string>> SearchBrandsAsync(string keyword);

        // Admin Methods
        Task<CategoryViewModel?> CreateCategoryAsync(CategoryCreateInput model);
        Task<CategoryViewModel?> UpdateCategoryAsync(string id, CategoryCreateInput model);
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
