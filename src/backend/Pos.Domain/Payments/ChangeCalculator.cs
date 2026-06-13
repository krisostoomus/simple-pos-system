namespace Pos.Domain.Payments;

/// <summary>
/// Computes the smallest number of physical pieces for a change amount.
/// Greedy is provably optimal for the canonical euro denomination set, and the
/// drawer is assumed to hold an unlimited supply of every denomination.
/// </summary>
public static class ChangeCalculator
{
    public static IReadOnlyList<ChangePiece> Calculate(int changeCents)
    {
        if (changeCents < 0)
            throw new ArgumentOutOfRangeException(nameof(changeCents), "Change cannot be negative.");

        var pieces = new List<ChangePiece>();
        var remaining = changeCents;

        foreach (var denomination in EuroDenominations.InCents)
        {
            if (remaining < denomination)
                continue;

            var count = remaining / denomination;
            remaining -= count * denomination;
            pieces.Add(new ChangePiece(denomination, count));
        }

        return pieces;
    }
}
