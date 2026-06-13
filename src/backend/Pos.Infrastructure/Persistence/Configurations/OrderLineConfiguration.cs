using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.ProductId).IsRequired();
        builder.Property(l => l.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(l => l.UnitPriceCents).IsRequired();
        builder.Property(l => l.Quantity).IsRequired();
        builder.Ignore(l => l.LineTotalCents); // computed
    }
}
