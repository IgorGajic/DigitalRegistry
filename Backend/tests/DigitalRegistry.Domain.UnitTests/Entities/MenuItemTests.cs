using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// Tests the availability toggle and the "can this still be made?" check that drives it.
/// </summary>
public class MenuItemTests
{
    private static MenuItem Cheeseburger(params (decimal Required, decimal InStock)[] recipe)
    {
        var menuItem = new MenuItem
        {
            Name = "Cheeseburger",
            Category = "Food",
            UnitPrice = 11.90m,
            IsAvailable = true
        };

        foreach (var (required, inStock) in recipe)
        {
            menuItem.Recipe.Add(new RecipeItem
            {
                QuantityRequired = required,
                Ingredient = new Ingredient
                {
                    Name = $"Ingredient {menuItem.Recipe.Count + 1}",
                    StockQuantity = inStock,
                    Unit = UnitOfMeasure.Units
                }
            });
        }

        return menuItem;
    }

    [Fact]
    public void SetAvailability_RaisesAnEventWhenTheFlagChanges()
    {
        var menuItem = Cheeseburger();

        var changed = menuItem.SetAvailability(false);

        Assert.True(changed);
        Assert.False(menuItem.IsAvailable);
        var domainEvent = Assert.IsType<MenuItemAvailabilityChangedDomainEvent>(
            Assert.Single(menuItem.DomainEvents));
        Assert.False(domainEvent.IsAvailable);
        Assert.Equal("Cheeseburger", domainEvent.Name);
    }

    [Fact]
    public void SetAvailability_StaysSilentWhenNothingChanges()
    {
        var menuItem = Cheeseburger();

        var changed = menuItem.SetAvailability(true);

        // Availability is re-evaluated on every order, so repeating the current value must not
        // produce a stream of identical pushes to the displays.
        Assert.False(changed);
        Assert.Empty(menuItem.DomainEvents);
    }

    [Fact]
    public void SetAvailability_AnnouncesComingBackOnTheMenu()
    {
        var menuItem = Cheeseburger();
        menuItem.SetAvailability(false);
        menuItem.ClearDomainEvents();

        var changed = menuItem.SetAvailability(true);

        Assert.True(changed);
        var domainEvent = Assert.IsType<MenuItemAvailabilityChangedDomainEvent>(
            Assert.Single(menuItem.DomainEvents));
        Assert.True(domainEvent.IsAvailable);
    }

    [Fact]
    public void CanBePreparedFromStock_RequiresEveryIngredient()
    {
        // Patties in stock, buns exhausted.
        var menuItem = Cheeseburger((1m, 10m), (1m, 0m));

        Assert.False(menuItem.CanBePreparedFromStock());
    }

    [Fact]
    public void CanBePreparedFromStock_IsTrueWhenAllIngredientsCover()
    {
        var menuItem = Cheeseburger((1m, 10m), (1m, 10m), (30m, 2000m));

        Assert.True(menuItem.CanBePreparedFromStock());
    }

    [Fact]
    public void CanBePreparedFromStock_AccountsForTheNumberOfServings()
    {
        var menuItem = Cheeseburger((1m, 3m));

        Assert.True(menuItem.CanBePreparedFromStock(servings: 3));
        Assert.False(menuItem.CanBePreparedFromStock(servings: 4));
    }

    [Fact]
    public void CanBePreparedFromStock_IsTrueForAnItemWithNoRecipe()
    {
        // Something bought in ready to serve consumes no tracked stock.
        Assert.True(Cheeseburger().CanBePreparedFromStock());
    }

    [Fact]
    public void Price_ExposesTheUnitPriceAsMoney()
    {
        Assert.Equal(11.90m, Cheeseburger().Price.Amount);
    }
}
