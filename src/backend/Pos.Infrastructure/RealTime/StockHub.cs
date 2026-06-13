using Microsoft.AspNetCore.SignalR;

namespace Pos.Infrastructure.RealTime;

/// <summary>Clients subscribe here to receive live stock changes. No server-to-client calls are
/// invoked by clients; the hub is broadcast-only via <see cref="SignalRStockNotifier"/>.</summary>
public sealed class StockHub : Hub;
