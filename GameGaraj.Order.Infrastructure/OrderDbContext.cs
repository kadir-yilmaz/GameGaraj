using GameGaraj.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Order.Infrastructure
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Domain.Entities.Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<OrderPricingLedger> OrderPricingLedgers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("ordering");

            modelBuilder.Entity<Domain.Entities.Order>().ToTable("Orders");
            modelBuilder.Entity<OrderItem>().ToTable("OrderItems");
            modelBuilder.Entity<Address>().ToTable("Addresses");
            modelBuilder.Entity<UserAddress>().ToTable("UserAddresses");
            modelBuilder.Entity<OrderPricingLedger>().ToTable("OrderPricingLedgers");

            // Decimal precision ayarı
            modelBuilder.Entity<OrderItem>().Property(x => x.Price).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderItem>().Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<OrderPricingLedger>().Property(x => x.Amount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Domain.Entities.Order>().Property(x => x.OriginalTotalAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Domain.Entities.Order>().Property(x => x.CampaignDiscountAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Domain.Entities.Order>().Property(x => x.CouponDiscountAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Domain.Entities.Order>().Property(x => x.ShippingFee).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Domain.Entities.Order>().Property(x => x.TotalPaidAmount).HasColumnType("decimal(18,2)");

            // İlişkiler
            modelBuilder.Entity<Domain.Entities.Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId);

            modelBuilder.Entity<Domain.Entities.Order>()
                .HasMany(o => o.OrderPricingLedgers)
                .WithOne(ol => ol.Order)
                .HasForeignKey(ol => ol.OrderId);

            // Cascade delete çakışmasını önlemek için (SQL Server multiple cascade paths hatası)
            modelBuilder.Entity<Domain.Entities.Order>()
                .HasOne(o => o.DeliveryAddress)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Domain.Entities.Order>()
                .HasOne(o => o.InvoiceAddress)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}
