using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Models;
using FluentValidation;

namespace EmployeeManagement.Api.Validators;

public class StatusValidator : AbstractValidator<EmployeeStatusRequestDto>
{
    public StatusValidator()
    {
        RuleFor(x => x.Status)
            .Must(EmployeeRequestValidator.IsValidStatus)
            .WithMessage("Status must be one of: Active, Inactive.");
    }
}