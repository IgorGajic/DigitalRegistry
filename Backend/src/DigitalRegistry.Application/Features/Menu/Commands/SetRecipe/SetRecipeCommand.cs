using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Menu.Commands.SetRecipe;

/// <summary>
/// Replaces a menu item's recipe — what one serving consumes.
/// </summary>
/// <remarks>
/// The whole recipe is sent, not individual changes: an ingredient dropped from the list is dropped
/// from the recipe. That is what lets the editor express a removal without a second endpoint, and it
/// makes the stored recipe exactly what the manager saw when they saved.
/// <para>
/// A bottled drink sold as it comes is a recipe of one line consuming one unit. The same mechanism
/// therefore covers the bar and the kitchen, and nothing needs a special case for "not really a
/// recipe".
/// </para>
/// </remarks>
public record SetRecipeCommand(
    Guid MenuItemId,
    IReadOnlyList<RecipeLineRequest> Lines) : IRequest<Result<MenuItemDetailDto>>;
