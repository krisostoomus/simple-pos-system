using Pos.Application.Abstractions;
using Pos.Application.Payments;
using Pos.Domain.Exceptions;
using Pos.Domain.Payments;

namespace Pos.Infrastructure.Payments;

/// <summary>Fake payment service: validates cash and computes change. The seam where a real
/// card/PSP integration would plug in.</summary>
public sealed class CashPaymentService : IPaymentService
{
    public PaymentResult AcceptCash(int totalCents, int cashPaidCents)
    {
        if (cashPaidCents < totalCents)
            throw new InsufficientPaymentException(totalCents, cashPaidCents);
        var change = cashPaidCents - totalCents;
        return new PaymentResult(change, ChangeCalculator.Calculate(change));
    }
}
