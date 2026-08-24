using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders;

/// <summary>
/// Opens a tab and puts the first lines on it, deducting stock as it goes.
/// </summary>
/// <remarks>
/// Shared by the staff and guest-QR paths, which differ only in where the table comes from and
/// whether a waiter is attached. Keeping it here means the ordering rules — table in service, items
/// on the menu, stock available, prices captured — are written once.
/// </remarks>
internal sealed class OrderOpener(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator)
{
    public async Task<Result<OrderDto>> OpenAsync(
        Guid tableId,
        Guid? waiterId,
        IReadOnlyList<OrderLineRequest> requestedLines,
        CancellationToken cancellationToken)
    {
        var table = await context.Tables
            .FirstOrDefaultAsync(candidate => candidate.Id == tableId, cancellationToken);

        if (table is null)
        {
            return Result<OrderDto>.NotFound($"Table {tableId} was not found.");
        }

        if (!table.IsActive)
        {
            return Result<OrderDto>.Conflict($"Table {table.TableNumber} is not in service.");
        }

        var menuItemIds = requestedLines.Select(line => line.MenuItemId).Distinct().ToArray();

        var menuItems = await context.MenuItems
            .Where(menuItem => menuItemIds.Contains(menuItem.Id))
            .ToDictionaryAsync(menuItem => menuItem.Id, cancellationToken);

        var missingIds = menuItemIds.Where(id => !menuItems.ContainsKey(id)).ToArray();

        if (missingIds.Length > 0)
        {
            return Result<OrderDto>.NotFound(
                $"These menu items do not exist: {string.Join(", ", missingIds)}.");
        }

        var unavailable = menuItems.Values
            .Where(menuItem => !menuItem.IsAvailable)
            .Select(menuItem => menuItem.Name)
            .ToArray();

        if (unavailable.Length > 0)
        {
            return Result<OrderDto>.Conflict(
                $"These items are currently unavailable: {string.Join(", ", unavailable)}.");
        }

        // Summed per menu item, because the same item can appear on several lines — for instance two
        // coffees with different notes — and stock is drawn against the total.
        var servingsByMenuItemId = requestedLines
            .GroupBy(line => line.MenuItemId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        // Built before the stock moves, so the ledger movements can name the order that caused them.
        // Nothing is written yet: the order is only handed to the context once the deduction succeeds,
        // so a shortage still leaves no trace.
        var order = Order.OpenForTable(table, waiterId);

        var deduction = await inventoryAllocator.DeductAsync(
            servingsByMenuItemId,
            order.Id,
            cancellationToken);

        if (!deduction.Succeeded)
        {
            return Result<OrderDto>.Failure(deduction.ErrorType, deduction.Errors.ToArray());
        }

        context.Orders.Add(order);

        foreach (var line in requestedLines)
        {
            order.AddItem(menuItems[line.MenuItemId], line.Quantity, line.Notes);
        }

        // Same transaction as the stock movement, so the menu can never advertise something the
        // kitchen has just run out of.
        await inventoryAllocator.RefreshMenuAvailabilityAsync(deduction.Value, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(order.ToDto());
    }
}
