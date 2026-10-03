using EmployeeManagement.Api.Dtos;
using FluentValidation;

namespace EmployeeManagement.Api.Validators;

public class FilterQueryValidator : AbstractValidator<FilterQuery>
{
    public FilterQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be greater or equal to 1");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100");
        StatusRule();
    }

    private void StatusRule()
    {
        RuleFor(x => x.Status)
                    .Must(EmployeeRequestValidator.IsValidStatus)
                    .When(x => x.Status is not null)
                    .WithMessage("Status must be one of: Active, Inactive.");
    }
}