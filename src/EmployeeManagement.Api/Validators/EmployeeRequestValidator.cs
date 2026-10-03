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

        NameRule();
        EmailRule();
        PhoneNumberRule();
        HireDateRule();
        StatusRule();
        ProfilePictureRule();
        AddressRule();
        StateRule();
        CountryRule();
        CityRule();
        PinCodeRule();
    }
    
    public static bool IsValidStatus(string? status) =>
        Enum.GetNames<EmployeeStatus>()
            .Any(name => string.Equals(name, status, StringComparison.OrdinalIgnoreCase));

    private void PinCodeRule()
    {
        RuleFor(x => x.Pincode)
            .Matches(@"^[A-Za-z0-9][A-Za-z0-9 \-]{1,8}[A-Za-z0-9]$")
            .When(x => x.Pincode is not null)
            .WithMessage("Pincode must be 3-10 characters: letters, digits, spaces or hyphens.");
    }
    
    private void CityRule()
    {
        RuleFor(x => x.City).MaximumLength(100);
    }

    private void CountryRule()
    {
        RuleFor(x => x.Country).MaximumLength(100);
    }
    
    private void StateRule()
    {
        RuleFor(x => x.State).MaximumLength(100);
    }

    private void AddressRule()
    {
        RuleFor(x => x.Address).MaximumLength(500);
    }

    private void ProfilePictureRule()
    {
        RuleFor(x => x.ProfilePicture)
            .MaximumLength(2048)
            .Must(IsHttpUrl).WithMessage("ProfilePicture must be an absolute http or https URL.")
            .When(x => x.ProfilePicture is not null);
    }

    private void StatusRule()
    {
        RuleFor(x => x.Status)
                    .Must(IsValidStatus)
                    .When(x => x.Status is not null)
                    .WithMessage("Status must be one of: Active, Inactive.");
    }

    private void HireDateRule()
    {
        RuleFor(x => x.HireDate)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(date => date <= Today())
                .WithMessage("HireDate cannot be in the future.")
            .Must(date => date >= Today().AddYears(-MaxEmploymentYears))
                .WithMessage($"HireDate cannot be more than {MaxEmploymentYears} years in the past.");
    }

    private void PhoneNumberRule()
    {
        RuleFor(x => x.PhoneNo)
            .NotEmpty()
            .Matches(@"^\+[1-9]\d{1,14}$")
            .WithMessage("PhoneNo must be in E.164 format, e.g. +48123456789.");
    }

    private void EmailRule()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .Must(IsValidEmail).WithMessage("Email has an invalid format.");
    }

    private void NameRule()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }

    private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private static bool IsValidEmail(string? email) =>
        MailAddress.TryCreate(email, out var address) && address.Address == email;

    private static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}