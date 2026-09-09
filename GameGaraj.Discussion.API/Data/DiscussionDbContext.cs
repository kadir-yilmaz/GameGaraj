using GameGaraj.Discussion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Discussion.API.Data;

public class DiscussionDbContext : DbContext
{
    public DiscussionDbContext(DbContextOptions<DiscussionDbContext> options) : base(options)
    {
    }

    public DbSet<Question> Questions { get; set; } = null!;
    public DbSet<Answer> Answers { get; set; } = null!;
    public DbSet<AnswerVote> AnswerVotes { get; set; } = null!;
    public DbSet<ModerationTerm> ModerationTerms { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.ProductId).HasMaxLength(128).IsRequired();
            entity.Property(q => q.ProductName).HasMaxLength(256);
            entity.Property(q => q.ProductImageUrl).HasMaxLength(1024);
            entity.Property(q => q.UserId).HasMaxLength(128).IsRequired();
            entity.Property(q => q.UserName).HasMaxLength(160);
            entity.Property(q => q.QuestionText).HasMaxLength(500).IsRequired();
            entity.Property(q => q.AdminNote).HasMaxLength(500);
            entity.Property(q => q.IdempotencyKey).HasMaxLength(64);
            entity.Property(q => q.Status).HasConversion<int>();

            // Soft delete global query filter
            entity.HasQueryFilter(q => !q.IsDeleted);

            // Indexes
            entity.HasIndex(q => q.ProductId);
            entity.HasIndex(q => new { q.ProductId, q.Status, q.CreatedAt });
            entity.HasIndex(q => new { q.UserId, q.CreatedAt });
            entity.HasIndex(q => q.Status);

            // Idempotency: aynı key ile birden fazla soru oluşturulamaz
            entity.HasIndex(q => q.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");

            // 1 Question -> 0..1 Answer (navigation property)
            entity.HasOne(q => q.Answer)
                .WithOne(a => a.Question)
                .HasForeignKey<Answer>(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.QuestionId).HasMaxLength(128).IsRequired();
            entity.Property(a => a.ResponderId).HasMaxLength(128).IsRequired();
            entity.Property(a => a.ResponderName).HasMaxLength(160);
            entity.Property(a => a.AnswerText).HasMaxLength(1000).IsRequired();
            entity.Property(a => a.IdempotencyKey).HasMaxLength(64);
            entity.Property(a => a.HelpfulCount).HasDefaultValue(0);
            entity.Property(a => a.DislikeCount).HasDefaultValue(0);

            // Soft delete global query filter
            entity.HasQueryFilter(a => !a.Question.IsDeleted);

            // DB seviyesinde 1 soru -> max 1 cevap garantisi (Madde 6 & 16)
            entity.HasIndex(a => a.QuestionId).IsUnique();

            // Idempotency
            entity.HasIndex(a => a.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        });

        modelBuilder.Entity<AnswerVote>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.AnswerId).HasMaxLength(128).IsRequired();
            entity.Property(v => v.UserId).HasMaxLength(128).IsRequired();

            // Soft delete global query filter
            entity.HasQueryFilter(v => !v.Answer.Question.IsDeleted);

            // 1 Kullanıcı 1 Cevaba sadece 1 oy verebilir (Unique Index)
            entity.HasIndex(v => new { v.AnswerId, v.UserId }).IsUnique();

            entity.HasOne(v => v.Answer)
                .WithMany(a => a.Votes)
                .HasForeignKey(v => v.AnswerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ModerationTerm>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Type).HasMaxLength(32).IsRequired();
            entity.Property(t => t.Term).HasMaxLength(128).IsRequired();
            entity.HasIndex(t => new { t.Type, t.Term }).IsUnique();
            entity.HasIndex(t => new { t.Type, t.IsActive });
        });
    }
}
