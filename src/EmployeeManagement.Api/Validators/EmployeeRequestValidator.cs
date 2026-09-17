using System.Net.Mail;
using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Models;
using FluentValidation;

namespace EmployeeManagement.Api.Validators;

public class EmployeeRequestValidator : AbstractValidator<EmployeeRequestDto>
{
    private const int MaxEmploymentYears = 70;
    private readonly TimeProvider _timeProvider;

    public EmployeeRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .Must(IsValidEmail).WithMessage("Email has an invalid format.");

        RuleFor(x => x.PhoneNo)
            .NotEmpty()
            .Matches(@"^\+[1-9]\d{1,14}$")
            .WithMessage("PhoneNo must be in E.164 format, e.g. +48123456789.");

        RuleFor(x => x.HireDate)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(date => date <= Today())
                .WithMessage("HireDate cannot be in the future.")
            .Must(date => date >= Today().AddYears(-MaxEmploymentYears))
                .WithMessage($"HireDate cannot be more than {MaxEmploymentYears} years in the past.");

        RuleFor(x => x.Status)
            .Must(IsValidStatus)
            .When(x => x.Status is not null)
            .WithMessage("Status must be one of: Active, Inactive.");

        RuleFor(x => x.ProfilePicture)
            .MaximumLength(2048)
            .Must(IsHttpUrl).WithMessage("ProfilePicture must be an absolute http or https URL.")
            .When(x => x.ProfilePicture is not null);

        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(100);

        RuleFor(x => x.Pincode)
            .Matches(@"^[A-Za-z0-9][A-Za-z0-9 \-]{1,8}[A-Za-z0-9]$")
            .When(x => x.Pincode is not null)
            .WithMessage("Pincode must be 3-10 characters: letters, digits, spaces or hyphens.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private static bool IsValidEmail(string? email) =>
        MailAddress.TryCreate(email, out var address) && address.Address == email;

    private static bool IsValidStatus(string? status) =>
        Enum.GetNames<EmployeeStatus>()
            .Any(name => string.Equals(name, status, StringComparison.OrdinalIgnoreCase));

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}