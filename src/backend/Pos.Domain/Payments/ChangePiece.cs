namespace Pos.Domain.Payments;

/// <summary>A quantity of a single denomination, e.g. 2 × 50c.</summary>
public sealed record ChangePiece(int DenominationCents, int Count);
