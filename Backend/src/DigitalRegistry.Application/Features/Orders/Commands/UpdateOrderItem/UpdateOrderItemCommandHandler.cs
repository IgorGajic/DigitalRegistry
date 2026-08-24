using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.UpdateOrderItem;

internal sealed class UpdateOrderItemCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator)
    : IRequestHandler<UpdateOrderItemCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        UpdateOrderItemCommand request,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .Include(candidate => candidate.Table)
            .Include(candidate => candidate.OrderItems)
            .ThenInclude(item => item.MenuItem)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result<OrderDto>.NotFound($"Order {request.OrderId} was not found.");
        }

        if (!order.IsEditable)
        {
            return Result<OrderDto>.Conflict(
                $"This order is {order.Status} and its lines can no longer be changed.");
        }

        var outcome = request.Change switch
        {
            OrderItemChange.Add => await AddLineAsync(order, request, cancellationToken),
            OrderItemChange.IncreaseQuantity => await IncreaseQuantityAsync(order, request, cancellationToken),
            OrderItemChange.ChangeNotes => ChangeNotes(order, request),
            _ => Result.Invalid("Unknown change type.")
        };

        if (!outcome.Succeeded)
        {
            return Result<OrderDto>.Failure(outcome.ErrorType, outcome.Errors.ToArray());
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(order.ToDto());
    }

    private async Task<Result> AddLineAsync(
        Order order,
        UpdateOrderItemCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await context.MenuItems
            .FirstOrDefaultAsync(candidate => candidate.Id == request.MenuItemId!.Value, cancellationToken);

        if (menuItem is null)
        {
            return Result.NotFound($"Menu item {request.MenuItemId} was not found.");
        }

        if (!menuItem.IsAvailable)
        {
            return Result.Conflict($"'{menuItem.Name}' is currently unavailable.");
        }

        var quantity = request.Quantity!.Value;

        var deduction = await inventoryAllocator.DeductAsync(
            new Dictionary<Guid, int> { [menuItem.Id] = quantity },
            order.Id,
            cancellationToken);

        if (!deduction.Succeeded)
        {
            return Result.Failure(deduction.ErrorType, deduction.Errors.ToArray());
        }

        var line = order.AddItem(menuItem, quantity, request.Notes);

        // Handing the line to the set explicitly, not only to the order's collection. BaseEntity
        // gives every entity its key up front, so a child discovered on an already-tracked parent
        // looks to EF Core like a row that exists: it issues an UPDATE that matches nothing instead
        // of an INSERT. The mirror image of removing a line, which also has to reach the set.
        context.OrderItems.Add(line);

        await inventoryAllocator.RefreshMenuAvailabilityAsync(deduction.Value, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> IncreaseQuantityAsync(
        Order order,
        UpdateOrderItemCommand request,
        CancellationToken cancellationToken)
    {
        if (FindLine(order, request.OrderItemId!.Value) is not { } line)
        {
            return Result.NotFound($"Line {request.OrderItemId} is not on this order.");
        }

        var newQuantity = request.Quantity!.Value;

        if (newQuantity == line.Quantity)
        {
            return Result.Success();
        }

        if (newQuantity < line.Quantity)
        {
            return Result.Invalid(
                "This endpoint only increases a line. Cancel servings through a void, which records "
                + "who did it and why.");
        }

        // Only the increase is deducted; what the line already consumed stays consumed.
        var deduction = await inventoryAllocator.DeductAsync(
            new Dictionary<Guid, int> { [line.MenuItemId] = newQuantity - line.Quantity },
            order.Id,
            cancellationToken);

        if (!deduction.Succeeded)
        {
            return Result.Failure(deduction.ErrorType, deduction.Errors.ToArray());
        }

        order.IncreaseItemQuantity(line, newQuantity);
        await inventoryAllocator.RefreshMenuAvailabilityAsync(deduction.Value, cancellationToken);

        return Result.Success();
    }

    private static Result ChangeNotes(Order order, UpdateOrderItemCommand request)
    {
        if (FindLine(order, request.OrderItemId!.Value) is not { } line)
        {
            return Result.NotFound($"Line {request.OrderItemId} is not on this order.");
        }

        // Notes consume no stock, so nothing to move here.
        order.ChangeItemNotes(line, request.Notes);

        return Result.Success();
    }

    private static OrderItem? FindLine(Order order, Guid orderItemId) =>
        order.OrderItems.FirstOrDefault(item => item.Id == orderItemId);
}
