using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// Cancelling: part of a line, a whole unpaid tab, and a settled bill.
/// </summary>
/// <remarks>
/// The rules here are what stop a till leaking. Money only ever comes off a bill through one of these,
/// and each leaves something behind that says so.
/// </remarks>
public class OrderVoidTests
{
    private static Table Table(bool isActive = true) => new()
    {
        RestaurantId = Guid.NewGuid(),
        TableNumber = 3,
        Capacity = 4,
        IsActive = isActive
    };

    private static MenuItem MenuItem(string name, decimal price) => new()
    {
        Name = name,
        Category = "Coffee",
        UnitPrice = price,
        IsAvailable = true
    };

    [Fact]
    public void VoidItem_CancellingEverythingRemovesTheLine()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 180m), 3);

        var amount = order.VoidItem(line, 3);

        Assert.Equal(540m, amount.Amount);
        Assert.Empty(order.OrderItems);
        Assert.Equal(0m, order.Total.Amount);
    }

    [Fact]
    public void VoidItem_CancellingPartLeavesTheRest()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 180m), 3);

        var amount = order.VoidItem(line, 1);

        Assert.Equal(180m, amount.Amount);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(360m, order.Total.Amount);
    }

    [Fact]
    public void VoidItem_PricesAtWhatTheLineCaptured()
    {
        var menuItem = MenuItem("Espresso", 180m);
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(menuItem, 2);

        // Repricing the menu after the order was taken must not change what the void is worth.
        menuItem.UnitPrice = 500m;

        Assert.Equal(360m, order.VoidItem(line, 2).Amount);
    }

    [Fact]
    public void VoidItem_RefusesMoreThanTheLineHolds()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 180m), 2);

        Assert.Throws<DomainException>(() => order.VoidItem(line, 3));
        Assert.Equal(2, line.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VoidItem_RefusesANonPositiveQuantity(int quantity)
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 180m), 2);

        Assert.Throws<DomainException>(() => order.VoidItem(line, quantity));
    }

    [Fact]
    public void VoidItem_RefusesOnAClosedOrder()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        var line = order.AddItem(MenuItem("Espresso", 180m), 1);
        order.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        Assert.Throws<DomainException>(() => order.VoidItem(line, 1));
    }

    [Fact]
    public void VoidOpen_CancelsTheTabAndReportsWhatItWouldHaveCome()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 180m), 2);
        order.AddItem(MenuItem("Cheeseburger", 890m), 1);
        order.ClearDomainEvents();

        var total = order.VoidOpen();

        Assert.Equal(1250m, total.Amount);
        Assert.Equal(OrderStatus.Cancelled, order.Status);

        var voided = Assert.Single(order.DomainEvents.OfType<OrderVoidedDomainEvent>());
        Assert.False(voided.WasPaid);
        Assert.Equal(1250m, voided.Amount);
    }

    [Fact]
    public void VoidOpen_RefusesASettledBill()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 180m), 1);
        order.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        Assert.Throws<DomainException>(() => order.VoidOpen());
    }

    [Fact]
    public void Reverse_WritesACounterTransactionRatherThanAmendingThePayment()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 180m), 2);
        var payment = order.Pay(Guid.NewGuid(), PaymentMethod.Card);
        order.ClearDomainEvents();

        var managerId = Guid.NewGuid();
        var reversal = order.Reverse(payment, managerId);

        // The original is untouched, so summing the column still gives the true takings and the fact
        // that a bill was reversed stays visible.
        Assert.Equal(360m, payment.Amount);
        Assert.Equal(-360m, reversal.Amount);
        Assert.Equal(payment.Id, reversal.ReversesTransactionId);
        Assert.True(reversal.IsReversal);
        Assert.False(payment.IsReversal);

        Assert.Equal(managerId, reversal.ProcessedByWaiterId);
        Assert.Equal(payment.PaymentMethod, reversal.PaymentMethod);
        Assert.Equal(order.RestaurantId, reversal.RestaurantId);
        Assert.Equal(OrderStatus.Voided, order.Status);

        var voided = Assert.Single(order.DomainEvents.OfType<OrderVoidedDomainEvent>());
        Assert.True(voided.WasPaid);
        Assert.Equal(360m, voided.Amount);
    }

    [Fact]
    public void Reverse_RefusesAnOrderThatWasNeverPaid()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 180m), 1);

        var stray = new Transaction { OrderId = order.Id, Amount = 180m };

        Assert.Throws<DomainException>(() => order.Reverse(stray, Guid.NewGuid()));
    }

    [Fact]
    public void Reverse_RefusesAPaymentFromAnotherOrder()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 180m), 1);
        order.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        var otherOrder = Order.OpenForTable(Table(), Guid.NewGuid());
        otherOrder.AddItem(MenuItem("Espresso", 180m), 1);
        var otherPayment = otherOrder.Pay(Guid.NewGuid(), PaymentMethod.Cash);

        Assert.Throws<DomainException>(() => order.Reverse(otherPayment, Guid.NewGuid()));
    }

    [Fact]
    public void Reverse_RefusesToReverseAReversal()
    {
        var order = Order.OpenForTable(Table(), Guid.NewGuid());
        order.AddItem(MenuItem("Espresso", 180m), 1);
        var payment = order.Pay(Guid.NewGuid(), PaymentMethod.Cash);
        var reversal = order.Reverse(payment, Guid.NewGuid());

        // Both because the order is no longer Paid and because a reversal is not a payment.
        Assert.Throws<DomainException>(() => order.Reverse(reversal, Guid.NewGuid()));
    }
}
