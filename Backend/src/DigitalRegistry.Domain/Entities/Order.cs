using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A tab opened against a table, either by a waiter or by a guest scanning the table's QR code.
/// </summary>
public class Order : AggregateRoot
{
    public Guid TableId { get; set; }

    /// <summary>Null when the guest placed the order themselves via the table QR code.</summary>
    public Guid? WaiterId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Open;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Table? Table { get; set; }

    public ApplicationUser? Waiter { get; set; }

    public bool PlacedByGuest => WaiterId is null;

    /// <summary>Lines may only be changed while the order is still open or being prepared.</summary>
    public bool IsEditable => Status is OrderStatus.Open or OrderStatus.InPreparation;

    public bool IsClosed => Status is OrderStatus.Paid or OrderStatus.Cancelled;

    /// <summary>
    /// Sum of the lines, using each line's captured price. Requires <see cref="OrderItems"/> to be
    /// loaded.
    /// </summary>
    public Money Total => OrderItems
        .Aggregate(Money.Zero, (running, item) => running + item.LineTotal)
        .Round();

    /// <summary>
    /// Opens a new order against a table.
    /// </summary>
    /// <param name="table">The table being served; also supplies the number used in alerts.</param>
    /// <param name="waiterId">The serving waiter, or null for a guest QR self-order.</param>
    public static Order OpenForTable(Table table, Guid? waiterId)
    {
        if (!table.IsActive)
        {
            throw new DomainException($"Table {table.TableNumber} is not in service.");
        }

        var order = new Order
        {
            TableId = table.Id,
            Table = table,
            WaiterId = waiterId,
            Status = OrderStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        order.RaiseDomainEvent(new OrderCreatedDomainEvent(order.Id, table.Id, table.TableNumber, waiterId));

        if (order.PlacedByGuest)
        {
            order.RaiseDomainEvent(new GuestQrOrderPlacedDomainEvent(order.Id, table.Id, table.TableNumber));
        }

        return order;
    }

    /// <summary>
    /// Adds a line, capturing the menu item's price at the moment of ordering so later price
    /// changes never alter an existing tab.
    /// </summary>
    public OrderItem AddItem(MenuItem menuItem, int quantity, string? notes = null)
    {
        EnsureEditable();

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        if (!menuItem.IsAvailable)
        {
            throw new DomainException($"'{menuItem.Name}' is currently unavailable.");
        }

        var item = new OrderItem
        {
            OrderId = Id,
            MenuItemId = menuItem.Id,
            MenuItem = menuItem,
            Quantity = quantity,
            UnitPrice = menuItem.UnitPrice,
            Notes = notes
        };

        OrderItems.Add(item);
        RaiseDomainEvent(new OrderItemUpdatedDomainEvent(Id, item.Id, menuItem.Id, quantity, Removed: false));

        return item;
    }

    /// <summary>
    /// Changes a line's quantity.
    /// </summary>
    /// <returns>
    /// The change in quantity: positive when more was ordered, negative when reduced. The caller
    /// uses this to deduct or return exactly the difference in ingredient stock.
    /// </returns>
    public int ChangeItemQuantity(OrderItem item, int newQuantity)
    {
        EnsureEditable();
        EnsureOwnsItem(item);

        if (newQuantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero; remove the line instead.");
        }

        var delta = newQuantity - item.Quantity;
        item.Quantity = newQuantity;

        RaiseDomainEvent(new OrderItemUpdatedDomainEvent(Id, item.Id, item.MenuItemId, newQuantity, Removed: false));

        return delta;
    }

    /// <summary>Updates a line's kitchen notes.</summary>
    public void ChangeItemNotes(OrderItem item, string? notes)
    {
        EnsureEditable();
        EnsureOwnsItem(item);

        item.Notes = notes;
        RaiseDomainEvent(new OrderItemUpdatedDomainEvent(Id, item.Id, item.MenuItemId, item.Quantity, Removed: false));
    }

    /// <summary>
    /// Removes a line.
    /// </summary>
    /// <returns>The quantity removed, so the caller can return the corresponding stock.</returns>
    public int RemoveItem(OrderItem item)
    {
        EnsureEditable();
        EnsureOwnsItem(item);

        var removedQuantity = item.Quantity;
        OrderItems.Remove(item);

        RaiseDomainEvent(new OrderItemUpdatedDomainEvent(Id, item.Id, item.MenuItemId, removedQuantity, Removed: true));

        return removedQuantity;
    }

    public void MarkInPreparation() => TransitionTo(OrderStatus.InPreparation, OrderStatus.Open);

    public void MarkServed() => TransitionTo(OrderStatus.Served, OrderStatus.Open, OrderStatus.InPreparation);

    public void Cancel() => TransitionTo(
        OrderStatus.Cancelled,
        OrderStatus.Open,
        OrderStatus.InPreparation,
        OrderStatus.Served);

    /// <summary>
    /// Settles the tab: records a transaction for the current total and closes the order.
    /// </summary>
    /// <param name="processedByWaiterId">The waiter taking the payment.</param>
    /// <param name="paymentMethod">How the guest paid.</param>
    public Transaction Pay(Guid processedByWaiterId, PaymentMethod paymentMethod)
    {
        if (IsClosed)
        {
            throw new DomainException($"Order is already {Status} and cannot be paid.");
        }

        if (OrderItems.Count == 0)
        {
            throw new DomainException("An empty order cannot be paid.");
        }

        var total = Total;

        var transaction = new Transaction
        {
            OrderId = Id,
            Order = this,
            ProcessedByWaiterId = processedByWaiterId,
            Amount = total.Amount,
            PaymentMethod = paymentMethod,
            TransactionDate = DateTime.UtcNow
        };

        Status = OrderStatus.Paid;
        RaiseDomainEvent(new OrderPaidDomainEvent(Id, TableId, total.Amount));

        return transaction;
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new DomainException($"An order with status {Status} can no longer be modified.");
        }
    }

    private void EnsureOwnsItem(OrderItem item)
    {
        if (item.OrderId != Id)
        {
            throw new DomainException("The line does not belong to this order.");
        }
    }

    private void TransitionTo(OrderStatus target, params OrderStatus[] allowedFrom)
    {
        if (!allowedFrom.Contains(Status))
        {
            throw new DomainException($"Cannot move an order from {Status} to {target}.");
        }

        Status = target;
    }
}
