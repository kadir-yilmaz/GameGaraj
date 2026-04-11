using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Repositories.Mongo
{
    public class MongoProductRepository : IProductRepository
    {
        private readonly IMongoCollection<Product> _products;
        private readonly ILogger<MongoProductRepository> _logger;

        public MongoProductRepository(MongoDbContext context, ILogger<MongoProductRepository> logger)
        {
            _products = context.Products;
            _logger = logger;
        }

        public async Task<List<Product>> GetAllAsync()
        {
            return await _products.Find(_ => true).ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(string id)
        {
            return await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Product>> GetByCategoryIdsAsync(List<string> categoryIds)
        {
            _logger.LogInformation($"[ProductRepository] GetByCategoryIdsAsync called with {categoryIds.Count} category IDs");
            _logger.LogInformation($"[ProductRepository] Category IDs: {string.Join(", ", categoryIds)}");

            // First, let's check total products in database
            var totalCount = await _products.CountDocumentsAsync(_ => true);
            _logger.LogInformation($"[ProductRepository] Total products in database: {totalCount}");

            // Get a sample product to see its CategoryId
            var sampleProduct = await _products.Find(_ => true).Limit(1).FirstOrDefaultAsync();
            if (sampleProduct != null)
            {
                _logger.LogInformation($"[ProductRepository] Sample product CategoryId: {sampleProduct.CategoryId}, Name: {sampleProduct.Name}");
            }

            // Build and execute the filter
            var filter = Builders<Product>.Filter.In(p => p.CategoryId, categoryIds);
            var results = await _products.Find(filter).ToListAsync();

            _logger.LogInformation($"[ProductRepository] Filter returned {results.Count} products");

            if (results.Any())
            {
                _logger.LogInformation($"[ProductRepository] First result: {results[0].Name} (CategoryId: {results[0].CategoryId})");
            }

            return results;
        }

        public async Task<List<Product>> GetFeaturedAsync(int limit = 10)
        {
            return await _products
                .Find(p => p.IsFeatured && p.IsActive)
                .SortByDescending(p => p.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<Product> CreateAsync(Product product)
        {
            await _products.InsertOneAsync(product);
            return product;
        }

        public async Task<bool> UpdateAsync(Product product)
        {
            var result = await _products.ReplaceOneAsync(p => p.Id == product.Id, product);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _products.DeleteOneAsync(p => p.Id == id);
            return result.DeletedCount > 0;
        }

        public async Task<long> CountAsync()
        {
            return await _products.CountDocumentsAsync(_ => true);
        }

        public async Task<List<string>> GetDistinctValuesForAttributeAsync(List<string> categoryIds, string attributeName)
        {
            // MongoDB Aggregation to find all unique values for a specific spec key
            // 1. Filter by category
            // 2. Filter by products that HAVE this spec key
            // 3. Project only that spec value
            // 4. Group by value to get distinct ones

            var pipeline = new[]
            {
                // Match categories
                new BsonDocument("$match", new BsonDocument("CategoryId", new BsonDocument("$in", new BsonArray(categoryIds)))),
                
                // Match products that have the specific spec
                new BsonDocument("$match", new BsonDocument($"Specs.{attributeName}", new BsonDocument("$exists", true))),
                
                // Project the spec value
                new BsonDocument("$project", new BsonDocument("val", $"$Specs.{attributeName}")),
                
                // Group by value to get unique ones
                new BsonDocument("$group", new BsonDocument("_id", "$val")),
                
                // Sort alphabetically
                new BsonDocument("$sort", new BsonDocument("_id", 1))
            };

            var results = await _products.Aggregate<BsonDocument>(pipeline).ToListAsync();

            return results
                .Where(r => r["_id"] != BsonNull.Value && r["_id"].IsBsonNull == false)
                .Select(r => r["_id"].AsString)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
        }
    }
}
