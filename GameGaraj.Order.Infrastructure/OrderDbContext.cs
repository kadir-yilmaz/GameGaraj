using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Domain.Entities;

namespace GameGaraj.Order.Infrastructure
{
    public class OrderDbContext : DbContext
    {
        public const string DEFAULT_SCHEMA = "ordering";

        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Domain.Entities.Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Domain.Entities.Order>().ToTable("Orders", DEFAULT_SCHEMA);
            modelBuilder.Entity<OrderItem>().ToTable("OrderItems", DEFAULT_SCHEMA);
            modelBuilder.Entity<Address>().ToTable("Addresses", DEFAULT_SCHEMA);
            modelBuilder.Entity<UserAddress>().ToTable("UserAddresses", DEFAULT_SCHEMA);

            // Decimal precision ayarı
            modelBuilder.Entity<OrderItem>().Property(x => x.Price).HasColumnType("decimal(18,2)");

            // Order - Address ilişkileri (1 Order : 2 Address - Delivery + Invoice)
            modelBuilder.Entity<Domain.Entities.Order>()
                .HasOne(o => o.DeliveryAddress)
                .WithOne()
                .HasForeignKey<Domain.Entities.Order>(o => o.DeliveryAddressId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Domain.Entities.Order>()
                .HasOne(o => o.InvoiceAddress)
                .WithOne()
                .HasForeignKey<Domain.Entities.Order>(o => o.InvoiceAddressId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Domain.Entities.Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId);

            // UserAddress index'leri
            modelBuilder.Entity<UserAddress>()
                .HasIndex(a => new { a.UserId, a.Type })
                .HasDatabaseName("IX_UserAddresses_UserId_Type");

            modelBuilder.Entity<UserAddress>()
                .HasIndex(a => new { a.UserId, a.Type, a.IsDefault })
                .HasDatabaseName("IX_UserAddresses_UserId_Type_IsDefault");

            base.OnModelCreating(modelBuilder);
        }
    }
}
