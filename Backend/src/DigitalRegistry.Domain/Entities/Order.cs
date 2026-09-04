using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A tab opened against a table, either by a waiter or by a guest scanning the table's QR code.
/// </summary>
public class Order : AggregateRoot, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid TableId { get; set; }

    /// <summary>Null when the guest placed the order themselves via the table QR code.</summary>
    public Guid? WaiterId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Open;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the round was carried out to the table, or null while it is still waiting.
    /// </summary>
    /// <remarks>
    /// <see cref="OrderStatus.Served"/> says <em>that</em> it went out; this says when, which is what
    /// lets the floor screen offer the last few back for a press that was meant for another table.
    /// Cleared again by <see cref="ReopenForService"/>, so it always agrees with the status.
    /// </remarks>
    public DateTime? ServedAtUtc { get; set; }

    /// <summary>
    /// Who carried the round out, or null while it is still waiting.
    /// </summary>
    /// <remarks>
    /// Not the same person as <see cref="WaiterId"/>, and on a guest QR round there is no other
    /// person: nobody took that order, so without this the one measurable act of service in the
    /// whole system belongs to nobody. It is what lets the owner's report say how long the tables
    /// <em>this</em> waiter covered actually waited.
    /// <para>
    /// Cleared again by <see cref="ReopenForService"/>, alongside <see cref="ServedAtUtc"/>: a round
    /// put back on the queue was not carried out, so nobody carried it.
    /// </para>
    /// </remarks>
    public Guid? ServedByWaiterId { get; set; }

    public Table? Table { get; set; }

    public ApplicationUser? Waiter { get; set; }

    public ApplicationUser? ServedByWaiter { get; set; }

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
            // Taken from the table rather than from the ambient tenant so the aggregate is
            // self-consistent even before the DbContext sees it.
            RestaurantId = table.RestaurantId,
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
            RestaurantId = RestaurantId,
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
    /// Increases a line's quantity — a guest asking for another round of the same thing.
    /// </summary>
    /// <remarks>
    /// Only ever upwards. Reducing a line takes money off the bill and consumed stock back out of a
    /// guest's order, which is a void: it needs a reason and an audit record, so it goes through
    /// <see cref="VoidItem"/> instead. Allowing a quiet decrease here would be a way around the void
    /// report that nobody would ever see.
    /// </remarks>
    /// <returns>How many servings were added, so the caller deducts exactly the difference.</returns>
    public int IncreaseItemQuantity(OrderItem item, int newQuantity)
    {
        EnsureEditable();
        EnsureOwnsItem(item);

        if (newQuantity <= item.Quantity)
        {
            throw new DomainException(
                $"A line can only be increased here; cancel servings through a void instead.");
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
    /// Cancels part or all of a line.
    /// </summary>
    /// <remarks>
    /// The one way anything comes off a running tab. <see cref="ChangeItemQuantity"/> deliberately
    /// only increases and there is no plain removal, because a reduction that left no record would
    /// make the void report — the control this all exists for — worthless.
    /// </remarks>
    /// <param name="quantity">Servings to cancel. Cancelling them all removes the line.</param>
    /// <returns>What the cancellation takes off the bill, at the price the line captured.</returns>
    public Money VoidItem(OrderItem item, int quantity)
    {
        EnsureEditable();
        EnsureOwnsItem(item);

        if (quantity <= 0)
        {
            throw new DomainException("Cancel at least one serving.");
        }

        if (quantity > item.Quantity)
        {
            throw new DomainException(
                $"Only {item.Quantity} of this line remain; {quantity} cannot be cancelled.");
        }

        var amount = new Money(item.UnitPrice) * quantity;
        var removesLine = quantity == item.Quantity;

        if (removesLine)
        {
            OrderItems.Remove(item);
        }
        else
        {
            item.Quantity -= quantity;
        }

        RaiseDomainEvent(new OrderItemUpdatedDomainEvent(
            Id,
            item.Id,
            item.MenuItemId,
            removesLine ? quantity : item.Quantity,
            Removed: removesLine));

        return amount.Round();
    }

    /// <summary>
    /// Cancels a tab that was never paid, freeing the table.
    /// </summary>
    /// <returns>What the tab would have come to.</returns>
    public Money VoidOpen()
    {
        if (IsClosed || Status == OrderStatus.Voided)
        {
            throw new DomainException($"An order that is already {Status} cannot be cancelled.");
        }

        var total = Total;

        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderVoidedDomainEvent(Id, TableId, total.Amount, WasPaid: false));

        return total;
    }

    /// <summary>
    /// Reverses a settled bill.
    /// </summary>
    /// <remarks>
    /// Produces a counter-transaction carrying the negative of what was taken, rather than deleting or
    /// amending the payment. The original stays on file, summing the column still yields the true
    /// takings, and the reversal is visible to anyone reconciling the till.
    /// </remarks>
    /// <param name="original">The payment being backed out.</param>
    /// <param name="processedByUserId">The manager or owner authorising it.</param>
    public Transaction Reverse(Transaction original, Guid processedByUserId)
    {
        if (Status != OrderStatus.Paid)
        {
            throw new DomainException($"Only a paid order can be reversed; this one is {Status}.");
        }

        if (original.OrderId != Id)
        {
            throw new DomainException("That payment does not belong to this order.");
        }

        if (original.IsReversal)
        {
            throw new DomainException("A reversal cannot itself be reversed.");
        }

        var reversal = new Transaction
        {
            RestaurantId = RestaurantId,
            OrderId = Id,
            Order = this,
            ProcessedByWaiterId = processedByUserId,
            Amount = -original.Amount,
            PaymentMethod = original.PaymentMethod,
            TransactionDate = DateTime.UtcNow,
            ReversesTransactionId = original.Id,
            Reverses = original
        };

        Status = OrderStatus.Voided;
        RaiseDomainEvent(new OrderVoidedDomainEvent(Id, TableId, original.Amount, WasPaid: true));

        return reversal;
    }

    public void MarkInPreparation() => TransitionTo(OrderStatus.InPreparation, OrderStatus.Open);

    /// <summary>Records that the round went out, when, and who took it.</summary>
    /// <param name="servedAtUtc">The instant it reached the table.</param>
    /// <param name="servedByWaiterId">
    /// Whoever pressed the button, or null where the caller is not a member of staff.
    /// </param>
    public void MarkServed(DateTime servedAtUtc, Guid? servedByWaiterId = null)
    {
        TransitionTo(OrderStatus.Served, OrderStatus.Open, OrderStatus.InPreparation);
        ServedAtUtc = servedAtUtc;
        ServedByWaiterId = servedByWaiterId;
    }

    /// <summary>
    /// Puts a round back on the floor screen's queue after it was marked carried out.
    /// </summary>
    /// <remarks>
    /// For the press of a button that was meant for a different table. The queue is worked one-handed
    /// while carrying a tray, the cards sit one under another, and the wrong one is a realistic slip
    /// rather than a hypothetical one — so it has to be reversible.
    /// <para>
    /// Only from <see cref="OrderStatus.Served"/>, which means it can never resurrect something that
    /// has been paid, cancelled or voided. Nothing about the money moves either way: serving never
    /// touched it, so neither does taking it back.
    /// </para>
    /// </remarks>
    public void ReopenForService()
    {
        TransitionTo(OrderStatus.Open, OrderStatus.Served);
        ServedAtUtc = null;
        ServedByWaiterId = null;
    }

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
            RestaurantId = RestaurantId,
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
