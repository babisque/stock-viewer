namespace StockViewer.Shared.Money;

/// <summary>An amount tagged with its currency. <c>+</c> and <c>-</c> throw <see cref="CurrencyMismatchException"/> when the currencies differ.</summary>
public readonly record struct Money(decimal Amount, Currency Currency)
{
    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new CurrencyMismatchException(left.Currency, right.Currency);
        }
    }
}

public sealed class CurrencyMismatchException(Currency left, Currency right)
    : InvalidOperationException($"Can't combine {left} and {right} amounts without an explicit conversion")
{
    public Currency Left { get; } = left;

    public Currency Right { get; } = right;
}
