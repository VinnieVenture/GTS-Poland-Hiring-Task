using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Validators;
using EmployeeManagement.Tests.TestHelpers;
using FluentValidation.TestHelper;

namespace EmployeeManagement.Tests.Validators;

public class EmployeeRequestValidatorTests
{
    private readonly EmployeeRequestValidator _validator = new(new FixedTimeProvider(TestData.Now));

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(TestData.ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_MissingOrBlank_IsRejected(string? name)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Name = name });

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void HireDate_Today_IsAccepted()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { HireDate = TestData.Today });

        result.ShouldNotHaveValidationErrorFor(x => x.HireDate);
    }

    [Fact]
    public void HireDate_InFuture_IsRejected()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { HireDate = TestData.Today.AddDays(1) });

        result.ShouldHaveValidationErrorFor(x => x.HireDate)
            .WithErrorMessage("HireDate cannot be in the future.");
    }

    [Fact]
    public void HireDate_ExactlySeventyYearsAgo_IsAccepted()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { HireDate = TestData.Today.AddYears(-70) });

        result.ShouldNotHaveValidationErrorFor(x => x.HireDate);
    }

    [Fact]
    public void HireDate_MoreThanSeventyYearsAgo_IsRejected()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { HireDate = new DateOnly(1899, 2, 28) });

        result.ShouldHaveValidationErrorFor(x => x.HireDate);
    }

    [Fact]
    public void HireDate_Missing_ReportsOnlyTheMissingValue()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { HireDate = null });

        // CascadeMode.Stop: no misleading "in the future" error for a null date.
        Assert.Single(result.Errors, e => e.PropertyName == nameof(EmployeeRequestDto.HireDate));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("inactive")]
    [InlineData(null)]
    public void Status_AllowedOrMissing_IsAccepted(string? status)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Status = status });

        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("Active,Inactive")]
    public void Status_NotAllowed_IsRejectedNotCorrected(string status)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Status = status });

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Jan <jan@example.com>")]
    [InlineData("")]
    public void Email_Invalid_IsRejected(string email)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Email = email });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("+1-555-0101")]
    [InlineData("48123456789")]
    [InlineData("+0123456")]
    public void PhoneNo_NotE164_IsRejected(string phoneNo)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { PhoneNo = phoneNo });

        result.ShouldHaveValidationErrorFor(x => x.PhoneNo);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/a.jpg")]
    [InlineData("photos/anna.jpg")]
    public void ProfilePicture_NotAbsoluteHttpUrl_IsRejected(string url)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { ProfilePicture = url });

        result.ShouldHaveValidationErrorFor(x => x.ProfilePicture);
    }

    [Theory]
    [InlineData("+12")]                 // the shortest number the pattern allows
    [InlineData("+123456789012345")]    // 15 digits - the E.164 maximum
    public void PhoneNo_WithinE164Length_IsAccepted(string phoneNo)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { PhoneNo = phoneNo });

        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNo);
    }

    [Fact]
    public void PhoneNo_SixteenDigits_IsRejected()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { PhoneNo = "+1234567890123456" });

        result.ShouldHaveValidationErrorFor(x => x.PhoneNo);
    }

    [Theory]
    [InlineData("20-001")]      // Poland
    [InlineData("560001")]      // India
    [InlineData("SW1A 1AA")]    // United Kingdom
    [InlineData("02101")]       // a leading zero survives because the value is text, not a number
    public void Pincode_KnownCountryFormats_AreAccepted(string pincode)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Pincode = pincode });

        result.ShouldNotHaveValidationErrorFor(x => x.Pincode);
    }

    [Theory]
    [InlineData("20")]              // shorter than three characters
    [InlineData("12345678901")]     // longer than ten
    [InlineData("-20001")]          // must not start with a separator
    [InlineData("20_001")]          // underscore is not an allowed separator
    public void Pincode_OutsideTheAllowedShape_IsRejected(string pincode)
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Pincode = pincode });

        result.ShouldHaveValidationErrorFor(x => x.Pincode);
    }

    [Fact]
    public void Pincode_Missing_IsAccepted()
    {
        // Pincode is optional: the rule only runs when a value is present.
        var result = _validator.TestValidate(TestData.ValidRequest() with { Pincode = null });

        result.ShouldNotHaveValidationErrorFor(x => x.Pincode);
    }

    [Fact]
    public void Name_TooLong_IsRejectedBeforeReachingTheDatabase()
    {
        var result = _validator.TestValidate(TestData.ValidRequest() with { Name = new string('a', 201) });

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateValidator_RequiresStatus()
    {
        var updateValidator = new UpdateEmployeeRequestValidator(new FixedTimeProvider(TestData.Now));

        var result = updateValidator.TestValidate(TestData.ValidRequest() with { Status = null });

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}
