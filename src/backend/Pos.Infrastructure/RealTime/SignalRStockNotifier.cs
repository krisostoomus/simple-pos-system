using Microsoft.AspNetCore.SignalR;
using Pos.Application.Abstractions;

namespace Pos.Infrastructure.RealTime;

public sealed class SignalRStockNotifier(IHubContext<StockHub> hub) : IStockNotifier
{
    public Task NotifyStockChangedAsync(int productId, int newQuantity, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("StockChanged", new { productId, newQuantity }, ct);
}
