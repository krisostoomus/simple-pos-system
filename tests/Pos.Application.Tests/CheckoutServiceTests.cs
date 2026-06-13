using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pos.Application.Abstractions;
using Pos.Application.Checkout;
using Pos.Application.Exceptions;
using Pos.Application.Payments;
using Pos.Domain.Exceptions;
using Pos.Domain.Orders;
using Pos.Domain.Payments;

namespace Pos.Application.Tests;

public class CheckoutServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPaymentService _payment = Substitute.For<IPaymentService>();
    private readonly IStockNotifier _notifier = Substitute.For<IStockNotifier>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero));

    private CheckoutService CreateSut() => new(_products, _orders, _uow, _payment, _notifier, _clock);

    private void RealisticPayment() =>
        _payment.AcceptCash(Arg.Any<int>(), Arg.Any<int>()).Returns(ci =>
        {
            int total = ci.ArgAt<int>(0), cash = ci.ArgAt<int>(1);
            if (cash < total) throw new InsufficientPaymentException(total, cash);
            var change = cash - total;
            return new PaymentResult(change, ChangeCalculator.Calculate(change));
        });

    private static CheckoutRequest Request(int cash, params (int productId, int qty)[] lines) =>
        new(lines.Select(l => new CheckoutLine(l.productId, l.qty)).ToList(), cash, Guid.NewGuid());

    [Fact]
    public async Task Checkout_WithExactPayment_ReturnsZeroChange()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        var result = await CreateSut().CheckoutAsync(Request(cash: 130, (1, 2)));

        Assert.Equal(130, result.TotalCents);
        Assert.Equal(0, result.ChangeCents);
        Assert.Empty(result.Change);
        Assert.Equal(2, result.Lines.Single().Quantity);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WithOverpayment_ReturnsChangeBreakdown()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        var result = await CreateSut().CheckoutAsync(Request(cash: 200, (1, 2)));

        Assert.Equal(70, result.ChangeCents);
        Assert.Equal(new ChangePieceDto(50, 1), result.Change[0]);
        Assert.Equal(new ChangePieceDto(20, 1), result.Change[1]);
    }

    [Fact]
    public async Task Checkout_MergesDuplicateLinesForSameProduct()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        var result = await CreateSut().CheckoutAsync(Request(cash: 195, (1, 1), (1, 2)));

        Assert.Equal(195, result.TotalCents);
        Assert.Equal(3, result.Lines.Single().Quantity);
    }

    [Fact]
    public async Task Checkout_WhenStockInsufficient_ThrowsInsufficientStock()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 1)]);

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 130, (1, 2))));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WhenCashTooLow_ThrowsInsufficientPaymentAndDoesNotPersist()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);

        await Assert.ThrowsAsync<InsufficientPaymentException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 100, (1, 2))));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_ThrowsEmptyCart()
    {
        var request = new CheckoutRequest([], 0, Guid.NewGuid());
        await Assert.ThrowsAsync<EmptyCartException>(() => CreateSut().CheckoutAsync(request));
    }

    [Fact]
    public async Task Checkout_WithZeroQuantity_ThrowsInvalidQuantity()
    {
        await Assert.ThrowsAsync<InvalidQuantityException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 100, (1, 0))));
    }

    [Fact]
    public async Task Checkout_WithUnknownProduct_ThrowsUnknownProduct()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([]);

        await Assert.ThrowsAsync<UnknownProductException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 100, (99, 1))));
    }

    [Fact]
    public async Task Checkout_DecrementsStockAndNotifies()
    {
        RealisticPayment();
        var product = TestData.Product(1, "Brownie", priceCents: 65, stock: 10);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>()).Returns([product]);

        await CreateSut().CheckoutAsync(Request(cash: 130, (1, 2)));

        Assert.Equal(8, product.StockQuantity);
        await _notifier.Received(1).NotifyStockChangedAsync(1, 8, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WithExistingIdempotencyKey_ReturnsExistingWithoutCharging()
    {
        var key = Guid.NewGuid();
        var existing = Order.Create(
            [new OrderLine(1, "Brownie", 65, 2)], cashPaidCents: 200, changeCents: 70,
            idempotencyKey: key, createdAtUtc: _clock.GetUtcNow().UtcDateTime);
        TestData.SetId(existing, 555);
        _orders.GetByIdempotencyKeyAsync(key).Returns(existing);

        var request = new CheckoutRequest([new CheckoutLine(1, 2)], 200, key);
        var result = await CreateSut().CheckoutAsync(request);

        Assert.Equal(555, result.OrderId);
        Assert.Equal(70, result.ChangeCents);
        Assert.Equal(new ChangePieceDto(50, 1), result.Change[0]);
        await _products.DidNotReceive().GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_OnConcurrencyConflict_RetriesOnceThenSucceeds()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(_ => [TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new ConcurrencyConflictException(), _ => Task.CompletedTask);

        var result = await CreateSut().CheckoutAsync(Request(cash: 130, (1, 2)));

        Assert.Equal(130, result.TotalCents);
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checkout_WhenConflictPersists_Throws()
    {
        RealisticPayment();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(_ => [TestData.Product(1, "Brownie", priceCents: 65, stock: 10)]);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new ConcurrencyConflictException());

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            CreateSut().CheckoutAsync(Request(cash: 130, (1, 2))));

        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
