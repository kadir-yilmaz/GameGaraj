using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Repositories.Mongo
{
    public class MongoCategoryRepository : ICategoryRepository
    {
        private readonly IMongoCollection<Category> _categories;

        public MongoCategoryRepository(MongoDbContext context)
        {
            _categories = context.Categories;
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _categories.Find(_ => true).ToListAsync();
        }

        public async Task<Category?> GetByIdAsync(string id)
        {
            return await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Category>> GetByParentIdAsync(string? parentId)
        {
            return await _categories.Find(c => c.ParentId == parentId).ToListAsync();
        }

        public async Task<Category> CreateAsync(Category category)
        {
            await _categories.InsertOneAsync(category);
            return category;
        }

        public async Task<bool> UpdateAsync(Category category)
        {
            var result = await _categories.ReplaceOneAsync(c => c.Id == category.Id, category);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _categories.DeleteOneAsync(c => c.Id == id);
            return result.DeletedCount > 0;
        }
    }
}
