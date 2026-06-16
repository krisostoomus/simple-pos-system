using Pos.Web.Models;

namespace Pos.Web.Services;

/// <summary>Client-side cart. Quantity equals clicks on a product; totals are display-only and the
/// server re-computes authoritatively at checkout.</summary>
public sealed class CartService
{
    private readonly Dictionary<int, int> _quantities = new();
    public event Action? Changed;

    public IReadOnlyDictionary<int, int> Quantities => _quantities;
    public int Count => _quantities.Values.Sum();
    public bool IsEmpty => _quantities.Count == 0;

    public int QuantityOf(int productId) => _quantities.GetValueOrDefault(productId);

    public void Add(int productId)
    {
        _quantities[productId] = QuantityOf(productId) + 1;
        Changed?.Invoke();
    }

    public void Remove(int productId)
    {
        if (!_quantities.TryGetValue(productId, out var q)) return;
        if (q <= 1) _quantities.Remove(productId);
        else _quantities[productId] = q - 1;
        Changed?.Invoke();
    }

    public int TotalCents(IReadOnlyDictionary<int, int> priceByProductId)
        => _quantities.Sum(kv => priceByProductId.GetValueOrDefault(kv.Key) * kv.Value);

    public IReadOnlyList<CheckoutLineModel> ToLines()
        => _quantities.Select(kv => new CheckoutLineModel(kv.Key, kv.Value)).ToList();

    public void Reset()
    {
        _quantities.Clear();
        Changed?.Invoke();
    }
}
