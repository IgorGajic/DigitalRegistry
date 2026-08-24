namespace DigitalRegistry.Domain.ValueObjects;

/// <summary>
/// A monetary amount together with its currency.
/// </summary>
/// <remarks>
/// Used to total orders and compute payment amounts. Entities persist plain <see cref="decimal"/>
/// columns per the schema specification; this type guards the arithmetic that happens in between,
/// in particular preventing amounts in different currencies from being silently added.
/// </remarks>
public readonly record struct Money
{
    /// <summary>
    /// Currency assumed when none is stated, and the default for a newly created restaurant.
    /// </summary>
    /// <remarks>
    /// Each restaurant carries its own <c>CurrencyCode</c>, so this is only the fallback; change it
    /// to re-denominate venues created from here on, not existing ones.
    /// </remarks>
    public const string DefaultCurrencyCode = "RSD";

    public Money(decimal amount, string currencyCode = DefaultCurrencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new ArgumentException("Currency code is required.", nameof(currencyCode));
        }

        Amount = amount;
        CurrencyCode = currencyCode.ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public static Money Zero => new(0m);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.CurrencyCode);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.CurrencyCode);
    }

    public static Money operator *(Money money, int quantity) => new(money.Amount * quantity, money.CurrencyCode);

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor, money.CurrencyCode);

    /// <summary>Rounds to the currency's minor unit using banker's rounding.</summary>
    public Money Round(int decimals = 2) => new(Math.Round(Amount, decimals, MidpointRounding.ToEven), CurrencyCode);

    public override string ToString() => $"{Amount:0.00} {CurrencyCode}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.CurrencyCode, right.CurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in {left.CurrencyCode} and {right.CurrencyCode}.");
        }
    }
}
