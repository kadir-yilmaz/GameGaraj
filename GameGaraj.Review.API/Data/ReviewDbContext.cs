using GameGaraj.Review.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Review.API.Data;

public class ReviewDbContext : DbContext
{
    public ReviewDbContext(DbContextOptions<ReviewDbContext> options) : base(options)
    {
    }

    public DbSet<ProductReview> ProductReviews { get; set; } = null!;
    public DbSet<ProductReviewReaction> ProductReviewReactions { get; set; } = null!;
    public DbSet<ModerationTerm> ModerationTerms { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(review => review.Id);
            entity.Property(review => review.ProductId).HasMaxLength(128).IsRequired();
            entity.Property(review => review.ProductName).HasMaxLength(256);
            entity.Property(review => review.ProductImageUrl).HasMaxLength(1024);
            entity.Property(review => review.UserId).HasMaxLength(128).IsRequired();
            entity.Property(review => review.UserName).HasMaxLength(160);
            entity.Property(review => review.UserEmail).HasMaxLength(256);
            entity.Property(review => review.Comment).HasMaxLength(1000).IsRequired();
            entity.Property(review => review.AdminNote).HasMaxLength(500);
            entity.Property(review => review.Status).HasConversion<int>();
            entity.HasQueryFilter(review => !review.IsDeleted);
            entity.HasIndex(review => new { review.ProductId, review.Status, review.CreatedAt });
            entity.HasIndex(review => new { review.UserId, review.CreatedAt });
            entity.HasIndex(review => new { review.ProductId, review.UserId })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProductReviews_Rating_Range", "\"Rating\" >= 1 AND \"Rating\" <= 5");
            });
        });

        modelBuilder.Entity<ProductReviewReaction>(entity =>
        {
            entity.HasKey(reaction => reaction.Id);
            entity.Property(reaction => reaction.ReviewId).HasMaxLength(128).IsRequired();
            entity.Property(reaction => reaction.UserId).HasMaxLength(128).IsRequired();
            entity.HasIndex(reaction => new { reaction.ReviewId, reaction.UserId }).IsUnique();
            entity.HasQueryFilter(reaction => !reaction.Review.IsDeleted);

            entity.HasOne(reaction => reaction.Review)
                .WithMany(review => review.Reactions)
                .HasForeignKey(reaction => reaction.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModerationTerm>(entity =>
        {
            entity.HasKey(term => term.Id);
            entity.Property(term => term.Type).HasMaxLength(32).IsRequired();
            entity.Property(term => term.Term).HasMaxLength(128).IsRequired();
            entity.HasIndex(term => new { term.Type, term.Term }).IsUnique();
            entity.HasIndex(term => new { term.Type, term.IsActive });
        });
    }
}
