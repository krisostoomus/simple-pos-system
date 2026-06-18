namespace Pos.Web.Models;

/// <summary>A single editable cart line for the basket panel: a product joined with its current
/// quantity. Line total is derived from the product's unit price.</summary>
public sealed record BasketLine(ProductModel Product, int Quantity)
{
    public int LineTotalCents => Product.PriceCents * Quantity;

    /// <summary>True when the line cannot grow further because quantity has reached available stock.</summary>
    public bool AtStockCap => Quantity >= Product.StockQuantity;
}
