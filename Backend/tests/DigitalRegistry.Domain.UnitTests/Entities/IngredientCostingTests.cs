using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Exceptions;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// What stock costs, and what a stocktake does to it.
/// </summary>
public class IngredientCostingTests
{
    private static Ingredient Gin(decimal stock = 0m, decimal averagePrice = 0m) => new()
    {
        Name = "Gin",
        Unit = UnitOfMeasure.Milliliters,
        StockQuantity = stock,
        AveragePurchasePrice = averagePrice,
        LowStockThreshold = 500m
    };

    [Fact]
    public void Receive_OntoAnEmptyShelfSimplySetsThePrice()
    {
        var gin = Gin();

        gin.Receive(1000m, 2.00m);

        Assert.Equal(1000m, gin.StockQuantity);
        Assert.Equal(2.00m, gin.AveragePurchasePrice);
    }

    [Fact]
    public void Receive_WeightsTheNewPriceAgainstWhatIsAlreadyOnTheShelf()
    {
        var gin = Gin(stock: 1000m, averagePrice: 2.00m);

        // 1000 at 2.00 plus 1000 at 3.00 averages to 2.50, not 3.00.
        gin.Receive(1000m, 3.00m);

        Assert.Equal(2000m, gin.StockQuantity);
        Assert.Equal(2.50m, gin.AveragePurchasePrice);
    }

    [Fact]
    public void Receive_WeightsBySizeNotByCount()
    {
        var gin = Gin(stock: 100m, averagePrice: 1.00m);

        // A large delivery at a new price should pull the average most of the way to it.
        gin.Receive(900m, 2.00m);

        Assert.Equal(1.90m, gin.AveragePurchasePrice);
    }

    [Fact]
    public void Receive_RejectsANonPositiveQuantityOrNegativePrice()
    {
        Assert.Throws<DomainException>(() => Gin().Receive(0m, 2m));
        Assert.Throws<DomainException>(() => Gin().Receive(-1m, 2m));
        Assert.Throws<DomainException>(() => Gin().Receive(100m, -1m));
    }

    [Fact]
    public void Restock_LeavesThePriceAlone()
    {
        var gin = Gin(stock: 1000m, averagePrice: 2.00m);

        // Stock coming back from a cancellation was already bought and already priced; only a
        // delivery changes what a unit has cost.
        gin.Restock(50m);

        Assert.Equal(1050m, gin.StockQuantity);
        Assert.Equal(2.00m, gin.AveragePurchasePrice);
    }

    [Fact]
    public void StockValue_IsTheQuantityAtWhatItCost()
    {
        var gin = Gin(stock: 1500m, averagePrice: 2.40m);

        Assert.Equal(3600m, gin.StockValue);
    }

    [Fact]
    public void AdjustTo_ReportsTheDifferenceAndLeavesThePriceAlone()
    {
        var gin = Gin(stock: 1000m, averagePrice: 2.00m);

        // A count finding less than expected does not change what the missing stock cost.
        var difference = gin.AdjustTo(940m);

        Assert.Equal(-60m, difference);
        Assert.Equal(940m, gin.StockQuantity);
        Assert.Equal(2.00m, gin.AveragePurchasePrice);
    }

    [Fact]
    public void AdjustTo_CanAlsoFindMoreThanExpected()
    {
        var gin = Gin(stock: 1000m, averagePrice: 2.00m);

        Assert.Equal(120m, gin.AdjustTo(1120m));
        Assert.Equal(1120m, gin.StockQuantity);
    }

    [Fact]
    public void AdjustTo_AMatchingCountIsNoMovement()
    {
        var gin = Gin(stock: 1000m, averagePrice: 2.00m);

        Assert.Equal(0m, gin.AdjustTo(1000m));
    }

    [Fact]
    public void AdjustTo_RefusesANegativeCount()
    {
        Assert.Throws<DomainException>(() => Gin(stock: 100m).AdjustTo(-1m));
    }

    [Fact]
    public void AdjustTo_WarnsWhenACountTakesStockBelowTheThreshold()
    {
        var gin = Gin(stock: 1000m, averagePrice: 2.00m);
        gin.ClearDomainEvents();

        gin.AdjustTo(100m);

        Assert.Single(gin.DomainEvents.OfType<Domain.Events.IngredientLowStockDomainEvent>());
    }

    [Fact]
    public void AdjustTo_DoesNotWarnWhenACountFindsMore()
    {
        // Already below the threshold, but the count found more than the books said. Warning here
        // would tell the manager something they are in the middle of fixing.
        var gin = Gin(stock: 100m, averagePrice: 2.00m);
        gin.ClearDomainEvents();

        gin.AdjustTo(300m);

        Assert.Empty(gin.DomainEvents);
    }
}
