namespace Pos.Application.Abstractions;

public interface IStockNotifier
{
    Task NotifyStockChangedAsync(int productId, int newQuantity, CancellationToken ct = default);
}
