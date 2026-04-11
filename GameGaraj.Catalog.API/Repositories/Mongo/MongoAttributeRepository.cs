using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Repositories.Mongo
{
    /// <summary>
    /// MongoDB implementation of IAttributeRepository using MongoDB.Driver
    /// </summary>
    public class MongoAttributeRepository : IAttributeRepository
    {
        private readonly IMongoCollection<CategoryAttribute> _attributes;

        public MongoAttributeRepository(MongoDbContext context)
        {
            _attributes = context.CategoryAttributes;
        }

        public async Task<List<CategoryAttribute>> GetAllAsync()
        {
            return await _attributes.Find(_ => true).ToListAsync();
        }

        public async Task<List<CategoryAttribute>> GetByCategoryIdAsync(string categoryId)
        {
            var filter = Builders<CategoryAttribute>.Filter.Eq(a => a.CategoryId, categoryId);
            var sort = Builders<CategoryAttribute>.Sort
                .Ascending(a => a.DisplayOrder)
                .Ascending(a => a.Name);

            return await _attributes.Find(filter).Sort(sort).ToListAsync();
        }

        public async Task<CategoryAttribute?> GetByIdAsync(string id)
        {
            return await _attributes.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        public async Task<CategoryAttribute> CreateAsync(CategoryAttribute attribute)
        {
            attribute.CreatedAt = DateTime.UtcNow;
            await _attributes.InsertOneAsync(attribute);
            return attribute;
        }

        public async Task<CategoryAttribute?> UpdateAsync(string id, CategoryAttribute attribute)
        {
            var existing = await GetByIdAsync(id);
            if (existing == null)
                return null;

            // Update fields
            existing.DisplayName = attribute.DisplayName;
            existing.Type = attribute.Type;
            existing.Options = attribute.Options;
            existing.IsRequired = attribute.IsRequired;
            existing.DisplayOrder = attribute.DisplayOrder;

            var result = await _attributes.ReplaceOneAsync(a => a.Id == id, existing);
            return result.ModifiedCount > 0 ? existing : null;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _attributes.DeleteOneAsync(a => a.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<bool> DeleteByCategoryIdAsync(string categoryId)
        {
            var filter = Builders<CategoryAttribute>.Filter.Eq(a => a.CategoryId, categoryId);
            var result = await _attributes.DeleteManyAsync(filter);
            return result.DeletedCount > 0;
        }

        public async Task<bool> ExistsAsync(string categoryId, string name)
        {
            var filter = Builders<CategoryAttribute>.Filter.And(
                Builders<CategoryAttribute>.Filter.Eq(a => a.CategoryId, categoryId),
                Builders<CategoryAttribute>.Filter.Eq(a => a.Name, name)
            );
            var count = await _attributes.CountDocumentsAsync(filter);
            return count > 0;
        }
    }
}
