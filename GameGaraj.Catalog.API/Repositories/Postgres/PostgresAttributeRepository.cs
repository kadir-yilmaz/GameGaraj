using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Repositories.Postgres
{
    public class PostgresAttributeRepository : IAttributeRepository
    {
        private readonly CatalogDbContext _context;

        public PostgresAttributeRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<CategoryAttribute> CreateAsync(CategoryAttribute attribute)
        {
            _context.CategoryAttributes.Add(attribute);
            await _context.SaveChangesAsync();
            return attribute;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var attribute = await _context.CategoryAttributes.FindAsync(id);
            if (attribute == null) return false;

            _context.CategoryAttributes.Remove(attribute);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteByCategoryIdAsync(string categoryId)
        {
            var attributes = await _context.CategoryAttributes
                .Where(a => a.CategoryId == categoryId)
                .ToListAsync();

            if (!attributes.Any()) return false;

            _context.CategoryAttributes.RemoveRange(attributes);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }

        public async Task<bool> ExistsAsync(string categoryId, string name)
        {
            return await _context.CategoryAttributes
                .AnyAsync(a => a.CategoryId == categoryId && a.Name == name);
        }

        public async Task<List<CategoryAttribute>> GetAllAsync()
        {
            return await _context.CategoryAttributes.ToListAsync();
        }

        public async Task<List<CategoryAttribute>> GetByCategoryIdAsync(string categoryId)
        {
            return await _context.CategoryAttributes
                .Where(a => a.CategoryId == categoryId)
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<CategoryAttribute?> GetByIdAsync(string id)
        {
            return await _context.CategoryAttributes.FindAsync(id);
        }

        public async Task<CategoryAttribute?> UpdateAsync(string id, CategoryAttribute attribute)
        {
            var existing = await _context.CategoryAttributes.FindAsync(id);
            if (existing == null) return null;

            existing.DisplayName = attribute.DisplayName;
            existing.Type = attribute.Type;
            existing.Options = attribute.Options;
            existing.IsRequired = attribute.IsRequired;
            existing.DisplayOrder = attribute.DisplayOrder;

            await _context.SaveChangesAsync();
            return existing;
        }
    }
}
