using FluentValidation;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetWaiterPerformance;

public class GetWaiterPerformanceQueryValidator : AbstractValidator<GetWaiterPerformanceQuery>
{
    public GetWaiterPerformanceQueryValidator()
    {
        RuleFor(query => query.ToDate)
            .GreaterThanOrEqualTo(query => query.FromDate)
            .WithMessage("The period cannot end before it starts.");

        RuleFor(query => query)
            .Must(query => query.ToDate.DayNumber - query.FromDate.DayNumber <= 366)
            .WithMessage("Report over a year or less at a time.");
    }
}
