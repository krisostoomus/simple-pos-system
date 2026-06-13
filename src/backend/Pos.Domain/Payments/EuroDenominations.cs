namespace Pos.Domain.Payments;

/// <summary>Standard euro denominations in cents, descending (notes then coins).</summary>
public static class EuroDenominations
{
    public static readonly IReadOnlyList<int> InCents =
    [
        50_000, 20_000, 10_000, 5_000, 2_000, 1_000,
        500, 200, 100,
        50, 20, 10, 5, 2, 1
    ];
}
