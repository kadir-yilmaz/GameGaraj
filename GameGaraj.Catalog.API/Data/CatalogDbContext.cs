using GameGaraj.Catalog.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Data
{
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<CategoryAttribute> CategoryAttributes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Map the Specs dictionary to jsonb in PostgreSQL
                entity.Property(e => e.Specs).HasColumnType("jsonb");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<CategoryAttribute>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Map the Options list to jsonb safely
                entity.Property(e => e.Options).HasColumnType("jsonb");
            });
        }
    }
}
