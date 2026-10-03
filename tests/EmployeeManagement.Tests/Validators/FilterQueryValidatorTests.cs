using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Validators;
using EmployeeManagement.Tests.TestHelpers;
using FluentValidation.TestHelper;

namespace EmployeeManagement.Tests.Validators;

public class FilterQueryValidatorTests
{
    private readonly FilterQueryValidator _validator = new();

    [Fact]
    public void ValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(TestData.ValidFilterQuery());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Page_Invalid_IsRejected(int page)
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = 1,
            Page = page
        });

        result.ShouldHaveValidationErrorFor(x => x.Page);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_Invalid_IsRejected(int pageSize)
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = pageSize,
            Page = 1
        });

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void PageSize_Valid_IsAccepted(int pageSize)
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = pageSize,
            Page = 1
        });

        result.ShouldNotHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Status_Invalid_IsRejected()
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = 1,
            Page = 1,
            Status = "InvalidStatus"
        });

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
    
    [Fact]
    public void Status_Null_IsAccepted()
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = 1,
            Page = 1,
            Status = null
        });

        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }    
    
    [Fact]
    public void Status_Empty_IsRejected()
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = 1,
            Page = 1,
            Status = ""
        });

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("Inactive")]
    public void Status_IsCorrect_IsAccepted(string status)
    {
        var result = _validator.TestValidate(new FilterQuery
        {
            PageSize = 1,
            Page = 1,
            Status = status
        });

        result.ShouldNotHaveAnyValidationErrors();
    }
}