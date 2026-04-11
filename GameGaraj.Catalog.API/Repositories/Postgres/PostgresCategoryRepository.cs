using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Repositories.Postgres
{
    public class PostgresCategoryRepository : ICategoryRepository
    {
        private readonly CatalogDbContext _context;

        public PostgresCategoryRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<Category> CreateAsync(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return false;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _context.Categories.ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(string id)
        {
            return await _context.Categories.FindAsync(id);
        }

        public async Task<List<Category>> GetByParentIdAsync(string? parentId)
        {
            return await _context.Categories
                .Where(c => c.ParentId == parentId)
                .ToListAsync();
        }

        public async Task<bool> UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
    }
}
