using Pos.Domain.Payments;

namespace Pos.Domain.Tests;

public class ChangeCalculatorTests
{
    [Fact]
    public void Calculate_ZeroChange_ReturnsNoPieces()
    {
        var pieces = ChangeCalculator.Calculate(0);
        Assert.Empty(pieces);
    }

    [Fact]
    public void Calculate_270Cents_ReturnsFewestPieces()
    {
        var pieces = ChangeCalculator.Calculate(270);
        Assert.Equal(
            new[] { new ChangePiece(200, 1), new ChangePiece(50, 1), new ChangePiece(20, 1) },
            pieces);
    }

    [Fact]
    public void Calculate_99Cents_UsesCoinsGreedily()
    {
        var pieces = ChangeCalculator.Calculate(99);
        Assert.Equal(
            new[]
            {
                new ChangePiece(50, 1), new ChangePiece(20, 2),
                new ChangePiece(5, 1), new ChangePiece(2, 2)
            },
            pieces);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(270)]
    [InlineData(9999)]
    [InlineData(123_45)]
    public void Calculate_PiecesSumBackToAmount(int amount)
    {
        var total = ChangeCalculator.Calculate(amount).Sum(p => p.DenominationCents * p.Count);
        Assert.Equal(amount, total);
    }

    [Fact]
    public void Calculate_EveryAmountUpTo1000_SumsBack()
    {
        for (var amount = 0; amount <= 1000; amount++)
        {
            var total = ChangeCalculator.Calculate(amount).Sum(p => p.DenominationCents * p.Count);
            Assert.Equal(amount, total);
        }
    }

    [Fact]
    public void Calculate_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangeCalculator.Calculate(-1));
    }
}
