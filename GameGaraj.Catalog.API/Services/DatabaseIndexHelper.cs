using GameGaraj.Catalog.API.Models;
using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Services
{
    public static class DatabaseIndexHelper
    {
        public static async Task CreateIndexesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            
            var connectionString = configuration["MongoDbSettings:ConnectionString"];
            var databaseName = configuration["MongoDbSettings:DatabaseName"];
            
            var mongoClient = new MongoClient(connectionString);
            var database = mongoClient.GetDatabase(databaseName);
            
            Console.WriteLine("[Index] Creating MongoDB indexes...");
            
            // Categories Collection Indexes
            var categoriesCollection = database.GetCollection<Category>("categories");
            
            // Index on ParentId for hierarchical queries
            var parentIdIndex = Builders<Category>.IndexKeys.Ascending(c => c.ParentId);
            await categoriesCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Category>(
                    parentIdIndex,
                    new CreateIndexOptions { Name = "idx_categories_parentId" }
                )
            );
            Console.WriteLine("[Index] ✅ Created index on Categories.ParentId");
            
            // Attributes Collection Indexes
            var attributesCollection = database.GetCollection<CategoryAttribute>("categoryAttributes");
            
            // Unique compound index on (CategoryId, Name)
            var categoryIdNameIndex = Builders<CategoryAttribute>.IndexKeys
                .Ascending(a => a.CategoryId)
                .Ascending(a => a.Name);
            await attributesCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CategoryAttribute>(
                    categoryIdNameIndex,
                    new CreateIndexOptions 
                    { 
                        Name = "idx_attributes_categoryId_name_unique",
                        Unique = true
                    }
                )
            );
            Console.WriteLine("[Index] ✅ Created unique compound index on Attributes (CategoryId, Name)");
            
            // Products Collection Indexes
            var productsCollection = database.GetCollection<Product>("products");
            
            // Index on CategoryId for filtering
            var categoryIdIndex = Builders<Product>.IndexKeys.Ascending(p => p.CategoryId);
            await productsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Product>(
                    categoryIdIndex,
                    new CreateIndexOptions { Name = "idx_products_categoryId" }
                )
            );
            Console.WriteLine("[Index] ✅ Created index on Products.CategoryId");
            
            // Text index on (Name, Description) for search
            var textIndex = Builders<Product>.IndexKeys
                .Text(p => p.Name)
                .Text(p => p.Description);
            await productsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Product>(
                    textIndex,
                    new CreateIndexOptions 
                    { 
                        Name = "idx_products_text_search",
                        DefaultLanguage = "turkish"
                    }
                )
            );
            Console.WriteLine("[Index] ✅ Created text index on Products (Name, Description)");
            
            Console.WriteLine("[Index] All indexes created successfully.");
        }
    }
}
