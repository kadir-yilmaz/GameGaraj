using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Repositories.Postgres
{
    public class PostgresProductRepository : IProductRepository
    {
        private readonly CatalogDbContext _context;

        public PostgresProductRepository(CatalogDbContext context)
        {
            _context = context;
        }

        public async Task<long> CountAsync()
        {
            return await _context.Products.CountAsync();
        }

        public async Task<Product> CreateAsync(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return false;

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Product>> GetAllAsync()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task<List<Product>> GetByCategoryIdsAsync(List<string> categoryIds)
        {
            return await _context.Products
                .Where(p => categoryIds.Contains(p.CategoryId))
                .ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(string id)
        {
            return await _context.Products.FindAsync(id);
        }

        public async Task<List<string>> GetDistinctValuesForAttributeAsync(List<string> categoryIds, string attributeName)
        {
            // PostgreSQL implementation for extracting distinct keys from JSONB column
            var products = await _context.Products
                .Where(p => categoryIds.Contains(p.CategoryId))
                .Select(p => p.Specs)
                .ToListAsync();

            var distinctValues = products
                .Where(specs => specs != null && specs.ContainsKey(attributeName))
                .Select(specs => specs[attributeName])
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            return distinctValues;
        }

        public async Task<List<Product>> GetFeaturedAsync(int limit = 10)
        {
            return await _context.Products
                .Where(p => p.IsFeatured && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<bool> UpdateAsync(Product product)
        {
            _context.Products.Update(product);
            var result = await _context.SaveChangesAsync();
            return result > 0;
        }
    }
}
