using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetOrders;

public class GetOrdersQueryValidator : AbstractValidator<GetOrdersQuery>
{
    public GetOrdersQueryValidator()
    {
        RuleFor(query => query.Take)
            .InclusiveBetween(1, 500)
            .WithMessage("Between 1 and 500 bills can be listed at a time.");

        RuleFor(query => query)
            .Must(query => query.From is not { } from || query.To is not { } to || from <= to)
            .WithMessage("The period must end after it starts.")
            .WithName(nameof(GetOrdersQuery.To));
    }
}
