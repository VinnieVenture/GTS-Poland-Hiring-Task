using EmployeeManagement.Api.Dtos;
using FluentValidation;

namespace EmployeeManagement.Api.Validators;

public class UpdateEmployeeRequestValidator : AbstractValidator<EmployeeRequestDto>
{
    public UpdateEmployeeRequestValidator(TimeProvider timeProvider)
    {
        Include(new EmployeeRequestValidator(timeProvider));

        RuleFor(x => x.Status)
            .NotNull().WithMessage("Status is required when updating an employee.");
    }
}