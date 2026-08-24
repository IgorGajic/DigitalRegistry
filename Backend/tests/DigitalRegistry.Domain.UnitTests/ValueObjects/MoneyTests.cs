using DigitalRegistry.Domain.ValueObjects;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Addition_SumsAmountsOfTheSameCurrency()
    {
        Assert.Equal(15.10m, (new Money(11.90m) + new Money(3.20m)).Amount);
    }

    [Fact]
    public void Addition_RefusesToMixCurrencies()
    {
        // Silently adding these would produce a number that means nothing.
        Assert.Throws<InvalidOperationException>(() => new Money(10m, "EUR") + new Money(10m, "USD"));
    }

    [Fact]
    public void Multiplication_ScalesALineByItsQuantity()
    {
        Assert.Equal(23.80m, (new Money(11.90m) * 2).Amount);
    }

    [Fact]
    public void Zero_IsAUsableStartingPointForSummingLines()
    {
        var total = Money.Zero + new Money(2.20m) + new Money(11.90m);

        Assert.Equal(14.10m, total.Amount);
        Assert.Equal(Money.DefaultCurrencyCode, total.CurrencyCode);
    }

    [Fact]
    public void CurrencyCode_IsNormalisedToUpperCase()
    {
        Assert.Equal("EUR", new Money(1m, "eur").CurrencyCode);
    }

    [Fact]
    public void Constructor_RequiresACurrencyCode()
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, " "));
    }

    [Fact]
    public void Round_UsesBankersRoundingToTheMinorUnit()
    {
        Assert.Equal(2.22m, new Money(2.225m).Round().Amount);
        Assert.Equal(2.24m, new Money(2.235m).Round().Amount);
    }

    [Fact]
    public void Equality_ComparesAmountAndCurrency()
    {
        Assert.Equal(new Money(5m, "EUR"), new Money(5m, "EUR"));
        Assert.NotEqual(new Money(5m, "EUR"), new Money(5m, "USD"));
        Assert.NotEqual(new Money(5m, "EUR"), new Money(6m, "EUR"));
    }
}
