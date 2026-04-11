using GameGaraj.Catalog.API.Models;
using MongoDB.Bson;

namespace GameGaraj.Catalog.API.Repositories.Abstract
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(string id);
        Task<List<Product>> GetByCategoryIdsAsync(List<string> categoryIds);
        Task<List<Product>> GetFeaturedAsync(int limit = 10);
        Task<Product> CreateAsync(Product product);
        Task<bool> UpdateAsync(Product product);
        Task<bool> DeleteAsync(string id);
        Task<long> CountAsync();
        Task<List<string>> GetDistinctValuesForAttributeAsync(List<string> categoryIds, string attributeName);
    }
}
