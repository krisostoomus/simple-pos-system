using Pos.Domain.Payments;

namespace Pos.Application.Payments;

/// <summary>Outcome of accepting cash: the change owed and its denomination breakdown.</summary>
public sealed record PaymentResult(int ChangeCents, IReadOnlyList<ChangePiece> Breakdown);
