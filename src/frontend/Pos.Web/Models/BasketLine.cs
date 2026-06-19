namespace Pos.Web.Models;

/// <summary>A single editable cart line for the basket panel: a product joined with its current
/// quantity. Line total is derived from the product's unit price.</summary>
public sealed record BasketLine(ProductModel Product, int Quantity)
{
    public int LineTotalCents => Product.PriceCents * Quantity;

    /// <summary>Units currently available to sell (never negative).</summary>
    public int Available => Math.Max(0, Product.StockQuantity);

    /// <summary>True when the line cannot grow further because quantity has reached available stock.</summary>
    public bool AtStockCap => Quantity >= Product.StockQuantity;

    /// <summary>The product sold out (elsewhere) while it sat in this basket.</summary>
    public bool IsOutOfStock => Product.StockQuantity <= 0;

    /// <summary>The basket holds more of this product than is still available — checkout would be rejected.</summary>
    public bool ExceedsStock => Quantity > Product.StockQuantity;
}
