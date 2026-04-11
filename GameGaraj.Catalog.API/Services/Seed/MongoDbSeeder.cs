using GameGaraj.Catalog.API.Data;
using MongoDB.Driver;

namespace GameGaraj.Catalog.API.Services.Seed
{
    public static class MongoDbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

            var seedVersion = "v2.2"; 
            var db = context.Database;

            try
            {
                var metaCollection = db.GetCollection<SeedMetadata>("_seed_metadata");
                var currentMeta = await metaCollection.Find(m => m.Version == seedVersion).FirstOrDefaultAsync();

                if (currentMeta != null)
                {
                    var existingProductCount = await context.Products.CountDocumentsAsync(_ => true);
                    Console.WriteLine($"[Seed-Mongo] Database already seeded with version {seedVersion} ({existingProductCount} products). Skipping.");
                    return;
                }
            }
            catch { }

            Console.WriteLine($"[Seed-Mongo] Starting fresh database seed (version {seedVersion})...");
            await DropCollectionsAsync(db);

            var now = DateTime.UtcNow;

            var categories = SeedData.GenerateCategories(now, out var categoryIds);
            var attributes = SeedData.GenerateCategoryAttributes(categoryIds, now);
            var products = SeedData.GenerateProducts(categoryIds, now);

            await context.Categories.InsertManyAsync(categories);
            await context.CategoryAttributes.InsertManyAsync(attributes);
            await context.Products.InsertManyAsync(products);

            var seedMetaCollection = db.GetCollection<SeedMetadata>("_seed_metadata");
            await seedMetaCollection.InsertOneAsync(new SeedMetadata { Version = seedVersion, SeededAt = DateTime.UtcNow });

            Console.WriteLine($"[Seed-Mongo] ✅ Completed: {categories.Count} categories, {attributes.Count} attributes, {products.Count} products");
        }

        private static async Task DropCollectionsAsync(IMongoDatabase db)
        {
            Console.WriteLine("[Seed-Mongo] Dropping all collections...");

            var collections = new[] { "products", "categories", "categoryAttributes", "_seed_metadata" };
            foreach (var collectionName in collections)
            {
                try
                {
                    await db.DropCollectionAsync(collectionName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Seed-Mongo] Warning: Could not drop {collectionName}: {ex.Message}");
                }
            }
        }
    }

    public class SeedMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime SeededAt { get; set; }
    }
}
