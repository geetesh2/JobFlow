using FluentValidation;
using JobFlow.Application.Queries.SearchJobs;

namespace JobFlow.Application.Validators;

public sealed class SearchJobsQueryValidator : AbstractValidator<SearchJobsQuery>
{
    public SearchJobsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.SortOrder)
            .Must(order => string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortOrder must be 'asc' or 'desc'.");
    }
}
