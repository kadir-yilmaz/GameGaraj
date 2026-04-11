using GameGaraj.Catalog.API.Models;
using MongoDB.Bson;

namespace GameGaraj.Catalog.API.Repositories.Abstract
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();
        Task<Category?> GetByIdAsync(string id);
        Task<List<Category>> GetByParentIdAsync(string? parentId);
        Task<Category> CreateAsync(Category category);
        Task<bool> UpdateAsync(Category category);
        Task<bool> DeleteAsync(string id);
    }
}
