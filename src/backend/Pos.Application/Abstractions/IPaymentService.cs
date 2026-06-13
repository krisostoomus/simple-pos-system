using Pos.Application.Payments;

namespace Pos.Application.Abstractions;

public interface IPaymentService
{
    /// <summary>Accepts cash for a total and returns the change owed. MUST throw
    /// <see cref="Pos.Domain.Exceptions.InsufficientPaymentException"/> when cash is short.</summary>
    PaymentResult AcceptCash(int totalCents, int cashPaidCents);
}
