using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Tests.TestHelpers;

namespace EmployeeManagement.Tests.Validators;

public class EmployeeRequestNormalizerTests
{
    [Fact]
    public void Normalize_CleansInputBeforeValidation()
    {
        var request = TestData.ValidRequest() with
        {
            Name = "  Anna Kowalska ",
            Email = "  Anna.Kowalska@Example.COM ",
            PhoneNo = "+1 (555) 010-1.0",
            City = "   "
        };

        var normalized = request.Normalize();

        Assert.Equal("Anna Kowalska", normalized.Name);
        Assert.Equal("anna.kowalska@example.com", normalized.Email);
        Assert.Equal("+155501010", normalized.PhoneNo);
        Assert.Null(normalized.City);
    }

    [Fact]
    public void Normalize_KeepsBlankStatusInsteadOfTurningItIntoNull()
    {
        // Optional text fields become null when blank, but Status must not: null means
        // "not provided" and defaults to Active on create, so a blank value has to stay
        // blank and be rejected by the validator instead of silently activating someone.
        var request = TestData.ValidRequest() with { Status = "   ", City = "   " };

        var normalized = request.Normalize();

        Assert.Equal(string.Empty, normalized.Status);
        Assert.Null(normalized.City);
    }
}
