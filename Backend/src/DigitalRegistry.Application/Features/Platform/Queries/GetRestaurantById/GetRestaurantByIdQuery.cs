using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetRestaurantById;

public record GetRestaurantByIdQuery(Guid Id) : IRequest<Result<RestaurantSummaryDto>>;
