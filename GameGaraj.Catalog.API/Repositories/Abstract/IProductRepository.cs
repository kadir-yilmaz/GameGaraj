using GameGaraj.Catalog.API.Models;

namespace GameGaraj.Catalog.API.Repositories.Abstract
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(string id);
        Task<Product?> GetBySlugAsync(string slug);
        Task<List<Product>> GetByCategoryIdsAsync(List<string> categoryIds);
        Task<List<Product>> GetFeaturedAsync(int limit = 10);
        Task<Product> CreateAsync(Product product);
        Task<bool> UpdateAsync(Product product);
        Task<bool> DeleteAsync(string id);
        Task<long> CountAsync();
        Task<List<string>> GetDistinctValuesForAttributeAsync(List<string> categoryIds, string attributeName);
    }
}
