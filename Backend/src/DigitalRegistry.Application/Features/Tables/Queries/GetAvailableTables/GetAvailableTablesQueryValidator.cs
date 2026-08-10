using FluentValidation;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetAvailableTables;

public class GetAvailableTablesQueryValidator : AbstractValidator<GetAvailableTablesQuery>
{
    public GetAvailableTablesQueryValidator()
    {
        RuleFor(query => query.PartySize)
            .GreaterThan(0).WithMessage("Party size must be at least one guest.");

        RuleFor(query => query.To)
            .GreaterThan(query => query.From)
            .WithMessage("The end of the period must be after its start.");

        RuleFor(query => query)
            .Must(query => (query.To - query.From) <= TimeSpan.FromHours(12))
            .WithMessage("The period cannot be longer than 12 hours.")
            .WithName(nameof(GetAvailableTablesQuery.To));
    }
}
