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
        public DbSet<IndexingJob> IndexingJobs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Specs).HasColumnType("jsonb");

                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.CategoryId);
                entity.HasIndex(e => e.Brand);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.IsFeatured);
                entity.HasIndex(e => e.Specs).HasMethod("gin");

                entity.HasOne<Category>()
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.ToTable(table =>
                {
                    table.HasCheckConstraint("CK_Products_Price_NonNegative", "\"Price\" >= 0");
                    table.HasCheckConstraint("CK_Products_Stock_NonNegative", "\"Stock\" >= 0");
                    table.HasCheckConstraint("CK_Products_ReservedStock_NonNegative", "\"ReservedStock\" >= 0");
                    table.HasCheckConstraint("CK_Products_ReservedStock_LessOrEqualStock", "\"ReservedStock\" <= \"Stock\"");
                });
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.Slug).IsUnique();
                entity.HasIndex(e => e.ParentId);

                entity.HasOne<Category>()
                    .WithMany()
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CategoryAttribute>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Options).HasColumnType("jsonb");

                entity.HasIndex(e => new { e.CategoryId, e.Name }).IsUnique();

                entity.HasOne<Category>()
                    .WithMany()
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IndexingJob>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).HasMaxLength(64);
                entity.Property(e => e.EntityId).HasMaxLength(128);
                entity.Property(e => e.Operation).HasMaxLength(32);
                entity.Property(e => e.Status).HasMaxLength(32);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => new { e.EntityType, e.EntityId, e.Status });
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}
