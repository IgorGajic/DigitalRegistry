using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// Tests stock movement: that it cannot go negative, and that reaching the reorder threshold is
/// announced.
/// </summary>
public class IngredientTests
{
    private static Ingredient Ingredient(decimal stock, decimal threshold = 10m) => new()
    {
        Name = "Burger patty",
        StockQuantity = stock,
        Unit = UnitOfMeasure.Units,
        LowStockThreshold = threshold
    };

    [Fact]
    public void Deduct_ReducesStock()
    {
        var ingredient = Ingredient(60m);

        ingredient.Deduct(4m);

        Assert.Equal(56m, ingredient.StockQuantity);
    }

    [Fact]
    public void Deduct_RefusesToGoNegative()
    {
        var ingredient = Ingredient(3m);

        var exception = Assert.Throws<InsufficientStockException>(() => ingredient.Deduct(4m));

        Assert.Equal(4m, exception.Requested);
        Assert.Equal(3m, exception.Available);
        // The failed attempt must leave stock untouched.
        Assert.Equal(3m, ingredient.StockQuantity);
    }

    [Fact]
    public void Deduct_AllowsTakingTheLastOfTheStock()
    {
        var ingredient = Ingredient(4m);

        ingredient.Deduct(4m);

        Assert.Equal(0m, ingredient.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Deduct_RejectsANonPositiveQuantity(decimal quantity)
    {
        Assert.Throws<DomainException>(() => Ingredient(60m).Deduct(quantity));
    }

    [Fact]
    public void Deduct_RaisesLowStockOnlyOnceTheThresholdIsReached()
    {
        var ingredient = Ingredient(60m, threshold: 12m);

        ingredient.Deduct(40m);
        Assert.Empty(ingredient.DomainEvents);

        // 20 remaining, dropping to 10, which is at or below the threshold of 12.
        ingredient.Deduct(10m);
        var lowStock = Assert.Single(ingredient.DomainEvents);
        var domainEvent = Assert.IsType<IngredientLowStockDomainEvent>(lowStock);

        Assert.Equal(10m, domainEvent.StockQuantity);
        Assert.Equal(12m, domainEvent.LowStockThreshold);
        Assert.Equal("Burger patty", domainEvent.Name);
    }

    [Fact]
    public void Deduct_RaisesLowStockWhenStockExactlyMeetsTheThreshold()
    {
        var ingredient = Ingredient(15m, threshold: 12m);

        ingredient.Deduct(3m);

        Assert.Single(ingredient.DomainEvents);
    }

    [Fact]
    public void Restock_AddsStockAndDoesNotAnnounceAnything()
    {
        var ingredient = Ingredient(2m, threshold: 12m);

        ingredient.Restock(30m);

        Assert.Equal(32m, ingredient.StockQuantity);
        // Coming back up is not news in itself; whether menu items return is decided by re-checking
        // every recipe, not by this entity.
        Assert.Empty(ingredient.DomainEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restock_RejectsANonPositiveQuantity(decimal quantity)
    {
        Assert.Throws<DomainException>(() => Ingredient(10m).Restock(quantity));
    }

    [Theory]
    [InlineData(20, 12, false)]
    [InlineData(12, 12, true)]
    [InlineData(5, 12, true)]
    public void IsLowOnStock_ComparesStockAgainstTheThreshold(
        decimal stock,
        decimal threshold,
        bool expected)
    {
        Assert.Equal(expected, Ingredient(stock, threshold).IsLowOnStock);
    }

    [Fact]
    public void HasStockFor_AnswersWhetherAnAmountCanBeCovered()
    {
        var ingredient = Ingredient(10m);

        Assert.True(ingredient.HasStockFor(10m));
        Assert.True(ingredient.HasStockFor(9.999m));
        Assert.False(ingredient.HasStockFor(10.001m));
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheCollection()
    {
        var ingredient = Ingredient(13m, threshold: 12m);
        ingredient.Deduct(1m);
        Assert.NotEmpty(ingredient.DomainEvents);

        ingredient.ClearDomainEvents();

        Assert.Empty(ingredient.DomainEvents);
    }
}
