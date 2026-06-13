using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Catalog;

namespace Pos.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.PriceCents).IsRequired();
        builder.Property(p => p.StockQuantity).IsRequired();
        builder.Property(p => p.ImageKey).IsRequired().HasMaxLength(100);
        builder.Property(p => p.IsActive).IsRequired();

        // Optimistic concurrency via the Postgres system column xmin.
        // Npgsql convention maps a uint shadow property named "xmin" with IsRowVersion() to the xmin system column.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasMany(p => p.Translations)
            .WithOne()
            .HasForeignKey(t => t.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Product.Translations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
