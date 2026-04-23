using GameGaraj.Catalog.API.Models;
namespace GameGaraj.Catalog.API.Repositories.Abstract
{
    public interface ICategoryRepository
    {
        Task<List<Category>> GetAllAsync();
        Task<Category?> GetByIdAsync(string id);
        Task<Category?> GetBySlugAsync(string slug);
        Task<List<Category>> GetByParentIdAsync(string? parentId);
        Task<Category> CreateAsync(Category category);
        Task<bool> UpdateAsync(Category category);
        Task<bool> DeleteAsync(string id);
    }
}
