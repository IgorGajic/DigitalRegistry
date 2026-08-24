using FluentValidation;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetVoidReport;

public class GetVoidReportQueryValidator : AbstractValidator<GetVoidReportQuery>
{
    public GetVoidReportQueryValidator()
    {
        RuleFor(query => query.FromUtc)
            .LessThan(query => query.ToUtc)
            .WithMessage("The period must start before it ends.");

        RuleFor(query => query.ToUtc)
            .Must((query, to) => (to - query.FromUtc).TotalDays <= 366)
            .WithMessage("Report over a year or less at a time.");
    }
}
