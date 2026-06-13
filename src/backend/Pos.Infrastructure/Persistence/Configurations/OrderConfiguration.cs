using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(o => o.TotalCents).IsRequired();
        builder.Property(o => o.CashPaidCents).IsRequired();
        builder.Property(o => o.ChangeCents).IsRequired();
        builder.Property(o => o.IdempotencyKey).IsRequired();
        builder.HasIndex(o => o.IdempotencyKey).IsUnique();

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Order.Lines))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
