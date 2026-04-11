using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Services
{
    /// <summary>
    /// Helper class to verify MongoDB indexes are created correctly
    /// </summary>
    public static class IndexVerificationHelper
    {
        public static async Task<bool> VerifyIndexesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            
            var connectionString = configuration["MongoDbSettings:ConnectionString"];
            var databaseName = configuration["MongoDbSettings:DatabaseName"];
            
            var mongoClient = new MongoClient(connectionString);
            var database = mongoClient.GetDatabase(databaseName);
            
            Console.WriteLine("[Verification] Verifying MongoDB indexes...");
            
            bool allIndexesExist = true;
            
            // Verify Categories indexes
            var categoriesCollection = database.GetCollection<Models.Category>("categories");
            var categoriesIndexes = await (await categoriesCollection.Indexes.ListAsync()).ToListAsync();
            var hasParentIdIndex = categoriesIndexes.Any(idx => 
                idx.GetValue("name").AsString == "idx_categories_parentId");
            
            if (hasParentIdIndex)
                Console.WriteLine("[Verification] ✅ Categories.ParentId index exists");
            else
            {
                Console.WriteLine("[Verification] ❌ Categories.ParentId index NOT found");
                allIndexesExist = false;
            }
            
            // Verify Attributes indexes
            var attributesCollection = database.GetCollection<Models.CategoryAttribute>("categoryAttributes");
            var attributesIndexes = await (await attributesCollection.Indexes.ListAsync()).ToListAsync();
            var hasUniqueCompoundIndex = attributesIndexes.Any(idx => 
                idx.GetValue("name").AsString == "idx_attributes_categoryId_name_unique" &&
                idx.Contains("unique") && idx.GetValue("unique").AsBoolean == true);
            
            if (hasUniqueCompoundIndex)
                Console.WriteLine("[Verification] ✅ Attributes (CategoryId, Name) unique compound index exists");
            else
            {
                Console.WriteLine("[Verification] ❌ Attributes unique compound index NOT found");
                allIndexesExist = false;
            }
            
            // Verify Products indexes
            var productsCollection = database.GetCollection<Models.Product>("products");
            var productsIndexes = await (await productsCollection.Indexes.ListAsync()).ToListAsync();
            
            var hasCategoryIdIndex = productsIndexes.Any(idx => 
                idx.GetValue("name").AsString == "idx_products_categoryId");
            
            if (hasCategoryIdIndex)
                Console.WriteLine("[Verification] ✅ Products.CategoryId index exists");
            else
            {
                Console.WriteLine("[Verification] ❌ Products.CategoryId index NOT found");
                allIndexesExist = false;
            }
            
            var hasTextIndex = productsIndexes.Any(idx => 
                idx.GetValue("name").AsString == "idx_products_text_search");
            
            if (hasTextIndex)
                Console.WriteLine("[Verification] ✅ Products (Name, Description) text index exists");
            else
            {
                Console.WriteLine("[Verification] ❌ Products text index NOT found");
                allIndexesExist = false;
            }
            
            if (allIndexesExist)
                Console.WriteLine("[Verification] All indexes verified successfully! ✅");
            else
                Console.WriteLine("[Verification] Some indexes are missing! ❌");
            
            return allIndexesExist;
        }
    }
}
