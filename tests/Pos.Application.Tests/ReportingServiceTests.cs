using NSubstitute;
using Pos.Application.Abstractions;
using Pos.Application.Reporting;

namespace Pos.Application.Tests;

public class ReportingServiceTests
{
    private readonly IReportQueries _queries = Substitute.For<IReportQueries>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();

    private ReportingService CreateSut() => new(_queries, _products);

    [Fact]
    public async Task GetSummary_JoinsLocalizedNamesOntoTotals()
    {
        _queries.GetTotalsAsync().Returns(new ReportTotals(
            TotalFundsCents: 500, OrderCount: 3,
            Items: [new ItemSold(ProductId: 1, QuantitySold: 4, RevenueCents: 260)]));

        var brownie = TestData.Product(1, "Brownie");
        brownie.AddTranslation("et", "Brauni");
        _products.GetAllActiveAsync().Returns([brownie]);

        var summary = await CreateSut().GetSummaryAsync("et");

        Assert.Equal(500, summary.TotalFundsCents);
        Assert.Equal(3, summary.OrderCount);
        var item = summary.Items.Single();
        Assert.Equal("Brauni", item.Name);
        Assert.Equal(4, item.QuantitySold);
        Assert.Equal(260, item.RevenueCents);
    }

    [Fact]
    public async Task GetSummary_UnknownProductId_FallsBackToPlaceholder()
    {
        _queries.GetTotalsAsync().Returns(new ReportTotals(
            100, 1, [new ItemSold(99, 1, 100)]));
        _products.GetAllActiveAsync().Returns([]);

        var summary = await CreateSut().GetSummaryAsync("en");

        Assert.Equal("#99", summary.Items.Single().Name);
    }
}
