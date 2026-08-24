using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// One line on an order.
/// </summary>
public class OrderItem : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid OrderId { get; set; }

    public Guid MenuItemId { get; set; }

    public int Quantity { get; set; }

    /// <summary>
    /// The menu item's price when the line was created. Copied deliberately so that repricing the
    /// menu never rewrites the history of an existing order.
    /// </summary>
    public decimal UnitPrice { get; set; }

    public string? Notes { get; set; }

    public Order? Order { get; set; }

    public MenuItem? MenuItem { get; set; }

    public Money LineTotal => new Money(UnitPrice) * Quantity;
}
