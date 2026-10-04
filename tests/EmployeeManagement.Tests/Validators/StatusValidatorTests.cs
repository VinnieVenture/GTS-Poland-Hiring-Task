using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Validators;
using EmployeeManagement.Tests.TestHelpers;
using FluentValidation.TestHelper;

namespace EmployeeManagement.Tests.Validators;

public class StatusValidatorTests
{
    private readonly StatusValidator _validator = new();

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(TestData.ValidEmployeeStatusRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("Active")]
    [InlineData("Inactive")]
    [InlineData("INACTIVE")]
    public void Status_Valid_IsAccepted(string status)
    {
        var result = _validator.TestValidate(new EmployeeStatusRequestDto
        {
            Status = status
        });

        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData(null)]
    public void Status_Invalid_IsRejected(string? status)
    {
        var result = _validator.TestValidate(new EmployeeStatusRequestDto
        {
            Status = status
        });

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}