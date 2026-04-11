using GameGaraj.Catalog.API.Data;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Services.Seed
{
    public static class PostgresDbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

            if (await context.Products.AnyAsync())
            {
                Console.WriteLine("[Seed-Postgres] Database already contains products. Skipping seed.");
                return;
            }

            Console.WriteLine("[Seed-Postgres] Starting fresh database seed...");
            var now = DateTime.UtcNow;

            var categories = SeedData.GenerateCategories(now, out var categoryIds);
            var attributes = SeedData.GenerateCategoryAttributes(categoryIds, now);
            var products = SeedData.GenerateProducts(categoryIds, now);

            context.Categories.AddRange(categories);
            context.CategoryAttributes.AddRange(attributes);
            context.Products.AddRange(products);

            await context.SaveChangesAsync();

            Console.WriteLine($"[Seed-Postgres] ✅ Completed: {categories.Count} categories, {attributes.Count} attributes, {products.Count} products");
        }
    }
}
