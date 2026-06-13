using Pos.Domain.Exceptions;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderTests
{
    private static readonly DateTime At = new(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Key = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void OrderLine_ComputesLineTotal()
    {
        var line = new OrderLine(productId: 1, productName: "Brownie", unitPriceCents: 65, quantity: 3);
        Assert.Equal(195, line.LineTotalCents);
    }

    [Fact]
    public void OrderLine_NonPositiveQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderLine(1, "Brownie", 65, 0));
    }

    [Fact]
    public void Create_SumsLineTotalsAndStoresChange()
    {
        var lines = new[]
        {
            new OrderLine(1, "Brownie", 65, 2),
            new OrderLine(2, "Muffin", 100, 1),
        };

        var order = Order.Create(lines, cashPaidCents: 500, changeCents: 270, idempotencyKey: Key, createdAtUtc: At);

        Assert.Equal(230, order.TotalCents);
        Assert.Equal(500, order.CashPaidCents);
        Assert.Equal(270, order.ChangeCents);
        Assert.Equal(Key, order.IdempotencyKey);
        Assert.Equal(At, order.CreatedAtUtc);
        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Create_WithNoLines_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Order.Create([], 0, 0, Key, At));
    }

    [Fact]
    public void Create_WhenCashBelowTotal_ThrowsInsufficientPayment()
    {
        var lines = new[] { new OrderLine(1, "Brownie", 65, 2) };
        Assert.Throws<InsufficientPaymentException>(() =>
            Order.Create(lines, cashPaidCents: 100, changeCents: 0, idempotencyKey: Key, createdAtUtc: At));
    }

    [Fact]
    public void Create_WhenChangeInconsistent_Throws()
    {
        var lines = new[] { new OrderLine(1, "Brownie", 65, 2) };
        Assert.Throws<ArgumentException>(() =>
            Order.Create(lines, cashPaidCents: 200, changeCents: 99, idempotencyKey: Key, createdAtUtc: At));
    }
}
