using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// Tests the tab: how it totals, when it may be changed, and what it announces.
/// </summary>
public class OrderTests
{
    private static Table Table(bool isActive = true) => new()
    {
        TableNumber = 3,
        Capacity = 4,
        IsActive = isActive
    };

    private static MenuItem MenuItem(string name, decimal price, bool isAvailable = true) => new()
    {
        Name = name,
        Category = "Food",
        UnitPrice = price,
        IsAvailable = isAvailable
    };

    [Fact]
    public void OpenForTable_AnnouncesTheNewOrder()
    {
        var waiterId = Guid.NewGuid();

        var order = Order.OpenForTable(Table(), waiterId);

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(waiterId, order.WaiterId);
        Assert.False(order.PlacedByGuest);

        var created = Assert.IsType<OrderCreatedDomainEvent>(Assert.Single(order.DomainEvents));
        Assert.Equal(3, created.TableNumber);
        Assert.Equal(waiterId, created.WaiterId);
    }

    [Fact]
    public void OpenForTable_WithNoWaiter_AlsoAnnouncesAGuestQrOrder()
    {
        var order = Order.OpenForTable(Table(), waiterId: null);

        Assert.True(order.PlacedByGuest);
        // Both events fire: the kitchen needs the order, and the floor needs to know nobody took it.
        Assert.Equal(2, order.DomainEvents.Count);
        Assert.Contains(order.DomainEvents, domainEvent => domainEvent is OrderCreatedDomainEvent);
        Assert.Contains(order.DomainEvents, domainEvent => domainEvent is GuestQrOrderPlacedDomainEvent);
    }

    [Fact]
    public void OpenForTable_RefusesATableOutOfService()
    {
        Assert.Throws<DomainException>(() => Order.OpenForTable(Table(isActive: false), Guid.NewGuid()));
    }

    [Fact]
    public void AddItem_CapturesThePriceAtTheTimeOfOrdering()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var espresso = MenuItem("Espresso", 2.20m);

        var line = order.AddItem(espresso, quantity: 2);

        Assert.Equal(2.20m, line.UnitPrice);

        // Repricing the menu afterwards must not rewrite an existing tab.
        espresso.UnitPrice = 9.99m;

        Assert.Equal(2.20m, line.UnitPrice);
        Assert.Equal(4.40m, order.Total.Amount);
    }

    [Fact]
    public void Total_SumsEveryLine()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 2.20m), 2);
        order.AddItem(MenuItem("Cheeseburger", 11.90m), 1);

        Assert.Equal(16.30m, order.Total.Amount);
    }

    [Fact]
    public void Total_IsZeroForAnEmptyOrder()
    {
        Assert.Equal(0m, Order.OpenForTable(Table(), Guid.NewGuid()).Total.Amount);
    }

    [Fact]
    public void AddItem_RefusesAnUnavailableItem()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());

        Assert.Throws<DomainException>(() =>
            order.AddItem(MenuItem("Cheeseburger", 11.90m, isAvailable: false), 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_RejectsANonPositiveQuantity(int quantity)
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => order.AddItem(MenuItem("Espresso", 2.20m), quantity));
    }

    [Fact]
    public void IncreaseItemQuantity_ReturnsOnlyTheDifference()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 2.20m), 2);

        // The difference is what the caller uses to move exactly the right amount of stock.
        Assert.Equal(3, order.IncreaseItemQuantity(line, 5));
        Assert.Equal(5, line.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IncreaseItemQuantity_RefusesAnythingThatIsNotAnIncrease(int newQuantity)
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 2.20m), 2);

        // Reducing a line takes money off the bill, which has to leave a void record. Allowing it
        // here would be a way around that report.
        Assert.Throws<DomainException>(() => order.IncreaseItemQuantity(line, newQuantity));
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void EnsureOwnsItem_RejectsALineFromAnotherOrder()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var otherOrder = Order.OpenForTable(Table(), Guid.NewGuid());
        var foreignLine = otherOrder.AddItem(MenuItem("Espresso", 2.20m), 1);

        Assert.Throws<DomainException>(() => order.VoidItem(foreignLine, 1));
    }

    [Fact]
    public void Pay_RecordsTheTotalAndClosesTheOrder()
    {
        var waiterId = Guid.NewGuid();
        var order = Order.OpenForTable(Table(), waiterId);
        order.AddItem(MenuItem("Cheeseburger", 11.90m), 2);
        order.ClearDomainEvents();

        var transaction = order.Pay(waiterId, PaymentMethod.Card);

        Assert.Equal(23.80m, transaction.Amount);
        Assert.Equal(PaymentMethod.Card, transaction.PaymentMethod);
        Assert.Equal(waiterId, transaction.ProcessedByWaiterId);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.IsClosed);

        var paid = Assert.IsType<OrderPaidDomainEvent>(Assert.Single(order.DomainEvents));
        Assert.Equal(23.80m, paid.Amount);
    }

    [Fact]
    public void Pay_RefusesAnEmptyOrder()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => order.Pay(Guid.NewGuid(), PaymentMethod.Cash));
    }

    [Fact]
    public void Pay_RefusesAnOrderThatIsAlreadyPaid()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 2.20m), 1);
        order.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        Assert.Throws<DomainException>(() => order.Pay(Guid.NewGuid(), PaymentMethod.Card));
    }

    [Fact]
    public void AddItem_IsRefusedOnceTheOrderIsClosed()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 2.20m), 1);
        order.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        Assert.False(order.IsEditable);
        Assert.Throws<DomainException>(() => order.AddItem(MenuItem("Cheeseburger", 11.90m), 1));
    }

    [Fact]
    public void LinesRemainEditableWhileTheOrderIsBeingPrepared()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.MarkInPreparation();

        Assert.True(order.IsEditable);
        order.AddItem(MenuItem("Espresso", 2.20m), 1);
    }

    [Fact]
    public void MarkServed_IsReachableFromOpenAndFromInPreparation()
    {
        var fromOpen = Order.OpenForTable(Table(), Guid.NewGuid());
        fromOpen.MarkServed(DateTime.UtcNow);
        Assert.Equal(OrderStatus.Served, fromOpen.Status);

        var fromPreparation = Order.OpenForTable(Table(), Guid.NewGuid());
        fromPreparation.MarkInPreparation();
        fromPreparation.MarkServed(DateTime.UtcNow);
        Assert.Equal(OrderStatus.Served, fromPreparation.Status);
    }

    [Fact]
    public void StatusTransitions_RefuseIllegalMoves()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 2.20m), 1);
        order.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        // Nothing follows Paid.
        Assert.Throws<DomainException>(order.MarkInPreparation);
        Assert.Throws<DomainException>(() => order.MarkServed(DateTime.UtcNow));
        Assert.Throws<DomainException>(order.Cancel);
    }

    [Fact]
    public void Cancel_IsAllowedBeforePayment()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.True(order.IsClosed);
    }
}
