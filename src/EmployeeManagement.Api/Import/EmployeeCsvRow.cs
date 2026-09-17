using CsvHelper.Configuration.Attributes;
using EmployeeManagement.Api.Dtos;

namespace EmployeeManagement.Api.Import;

// One CSV line as raw text. HireDate stays a string so an invalid date becomes a row error
// instead of a CsvHelper conversion exception. Unknown columns (e.g. CreatedAt) are ignored.
public sealed class EmployeeCsvRow
{
    public const string RequiredColumns = "Name, HireDate, Email, PhoneNo, Status";

    public string? Name { get; set; }
    public string? HireDate { get; set; }
    public string? Email { get; set; }
    public string? PhoneNo { get; set; }
    // Required in a file: an import carries existing data, so the status must be stated explicitly.
    public string? Status { get; set; }

    [Optional] public string? ProfilePicture { get; set; }
    [Optional] public string? Address { get; set; }
    [Optional] public string? State { get; set; }
    [Optional] public string? Country { get; set; }
    [Optional] public string? City { get; set; }
    [Optional] public string? Pincode { get; set; }

    public EmployeeRequestDto ToRequest(DateOnly? hireDate) => new()
    {
        Name = Name,
        HireDate = hireDate,
        Email = Email,
        PhoneNo = PhoneNo,
        ProfilePicture = ProfilePicture,
        // An empty cell stays empty and fails validation - a row is never silently activated.
        Status = Status,
        Address = Address,
        State = State,
        Country = Country,
        City = City,
        Pincode = Pincode
    };
}
