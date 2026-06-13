namespace Pos.Domain.Exceptions;

public sealed class InsufficientPaymentException : DomainException
{
    public int TotalCents { get; }
    public int CashPaidCents { get; }

    public InsufficientPaymentException(int totalCents, int cashPaidCents)
        : base($"Insufficient payment: total {totalCents}c, paid {cashPaidCents}c.")
    {
        TotalCents = totalCents;
        CashPaidCents = cashPaidCents;
    }
}
