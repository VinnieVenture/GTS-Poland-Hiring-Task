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
}
