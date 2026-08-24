using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Orders.Commands.CreateGuestQrOrder;
using DigitalRegistry.Application.Features.Orders.Commands.CreateOrder;
using DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;
using DigitalRegistry.Application.Features.Orders.Commands.UpdateOrderItem;
using DigitalRegistry.Application.Features.Orders.Commands.VoidOpenOrder;
using DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;
using DigitalRegistry.Application.Features.Orders.Commands.VoidPaidOrder;
using DigitalRegistry.Application.Features.Orders.Queries.GetOrderById;
using DigitalRegistry.Application.Features.Orders.Queries.GetReceipt;
using DigitalRegistry.Application.Features.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// Tabs: opening them as staff or by QR scan, changing their lines, and taking payment.
/// </summary>
public class OrdersController : ApiControllerBase
{
    /// <summary>Fetches one tab with its lines and total.</summary>
    /// <response code="200">The order.</response>
    /// <response code="404">No order with that id is visible to the caller.</response>
    [HttpGet("{id:guid}", Name = "GetOrderById")]
    [Authorize(Policy = AuthorizationPolicies.ViewMenu)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetOrderByIdQuery(id), cancellationToken));

    /// <summary>Opens a tab against a table as staff.</summary>
    /// <response code="201">The tab was opened and stock deducted.</response>
    /// <response code="404">The table or a menu item does not exist.</response>
    /// <response code="409">The table is out of service, or an item is unavailable or out of stock.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.OpenStaffOrder)]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return ToCreatedResult(
            result,
            nameof(GetById),
            new { id = result.Succeeded ? result.Value.Id : Guid.Empty });
    }

    /// <summary>
    /// Places an order for the table the caller's QR session was opened at.
    /// </summary>
    /// <remarks>The table is taken from the session token, so no table id is accepted here.</remarks>
    /// <response code="201">The order was placed and the floor alerted.</response>
    /// <response code="403">The caller has no table session.</response>
    /// <response code="409">An item is unavailable or out of stock.</response>
    [HttpPost("qr")]
    [Authorize(Policy = AuthorizationPolicies.PlaceGuestQrOrder)]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CreateFromQrSession(
        [FromBody] CreateGuestQrOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return ToCreatedResult(
            result,
            nameof(GetById),
            new { id = result.Succeeded ? result.Value.Id : Guid.Empty });
    }

    /// <summary>Adds, changes or removes a line on an open tab.</summary>
    /// <response code="200">The updated order.</response>
    /// <response code="404">The order, line or menu item does not exist.</response>
    /// <response code="409">The order is closed, or an item is unavailable or out of stock.</response>
    [HttpPatch("{id:guid}/items")]
    [Authorize(Policy = AuthorizationPolicies.ModifyOrder)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateItems(
        Guid id,
        [FromBody] UpdateOrderItemCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.OrderId)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The order id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Totals the tab, records the payment and closes the order.</summary>
    /// <response code="200">The recorded transaction.</response>
    /// <response code="404">No order with that id.</response>
    /// <response code="409">The order is empty, already closed, or already paid.</response>
    [HttpPost("{id:guid}/payment")]
    [Authorize(Policy = AuthorizationPolicies.ProcessPayment)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransactionDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ProcessPayment(
        Guid id,
        [FromBody] ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.OrderId)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The order id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Everything needed to print the bill.</summary>
    /// <remarks>
    /// A simulation, not a fiscal receipt: no tax authority has seen it and no fiscal device produced
    /// it. A reversed bill is marked as such on the copy, so it cannot be passed off as a valid one.
    /// </remarks>
    /// <response code="200">The bill.</response>
    /// <response code="404">No order with that id.</response>
    [HttpGet("{id:guid}/receipt")]
    [Authorize(Policy = AuthorizationPolicies.ProcessPayment)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReceiptDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetReceipt(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetReceiptQuery(id), cancellationToken));

    /// <summary>Cancels part or all of a line on a running tab and returns what it consumed to stock.</summary>
    /// <remarks>
    /// The only way to take something off a tab. A reason is required, and every use is recorded
    /// against the member of staff who performed it for the owner's void report.
    /// </remarks>
    /// <response code="200">What the cancellation took off the bill.</response>
    /// <response code="404">No such order, or the line is not on it.</response>
    /// <response code="409">The order is closed; a settled bill is reversed instead.</response>
    [HttpPost("{id:guid}/items/{itemId:guid}/void")]
    [Authorize(Policy = AuthorizationPolicies.VoidOrder)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VoidResultDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> VoidItem(
        Guid id,
        Guid itemId,
        [FromBody] VoidRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new VoidOrderItemCommand(id, itemId, request.Reason, request.Quantity),
            cancellationToken));

    /// <summary>Cancels an unpaid tab in full, returns its stock and frees the table.</summary>
    /// <response code="409">The bill has been settled; reverse it instead.</response>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = AuthorizationPolicies.VoidOrder)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VoidResultDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> VoidOpen(
        Guid id,
        [FromBody] VoidRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new VoidOpenOrderCommand(id, request.Reason), cancellationToken));

    /// <summary>Reverses a settled bill, writing a counter-transaction and returning its stock.</summary>
    /// <remarks>
    /// Manager or owner only. This is the one void a waiter cannot perform, because it takes money
    /// back out of the day's takings.
    /// </remarks>
    /// <response code="409">The order was never paid, or has already been reversed.</response>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = AuthorizationPolicies.ApproveVoid)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VoidResultDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Reverse(
        Guid id,
        [FromBody] VoidRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new VoidPaidOrderCommand(id, request.Reason), cancellationToken));
}

/// <summary>
/// The justification a void requires, and optionally how much of a line to cancel.
/// </summary>
/// <param name="Reason">Why the cancellation is being made. Recorded against the member of staff.</param>
/// <param name="Quantity">
/// Servings to cancel. Omit to cancel the whole line; ignored for whole-order voids.
/// </param>
public record VoidRequest(string Reason, int? Quantity = null);
