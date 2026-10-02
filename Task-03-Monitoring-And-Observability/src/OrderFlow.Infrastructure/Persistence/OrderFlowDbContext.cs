using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.ReadModels;

namespace OrderFlow.Infrastructure.Persistence;

public class OrderFlowDbContext(DbContextOptions<OrderFlowDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderDashboard> OrderDashboards => Set<OrderDashboard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);

            entity.Property(o => o.CustomerName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Ignore(o => o.Total);

            entity.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(o => o.Status);

            entity.HasIndex(o => o.CreatedAt);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(i => i.Id);

            entity.Property(i => i.ProductName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(i => i.UnitPrice)
                .HasPrecision(18, 2);

            entity.Ignore(i => i.LineTotal);
        });

        modelBuilder.Entity<OrderDashboard>(entity =>
        {
            entity.ToTable("OrderDashboard");
            entity.HasKey(d => d.OrderId);

            entity.Property(d => d.CustomerName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(d => d.Total)
                .HasPrecision(18, 2);

            entity.Property(d => d.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

        });
    }
}
