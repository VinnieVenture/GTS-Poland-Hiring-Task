using EmployeeManagement.Api.Dtos;

namespace EmployeeManagement.Tests.TestHelpers;

internal static class TestData
{
    // All date rules are evaluated against this fixed "today".
    public static readonly DateTimeOffset Now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    public static EmployeeRequestDto ValidRequest(string? email = null) => new()
    {
        Name = "Anna Kowalska",
        HireDate = new DateOnly(2024, 3, 1),
        Email = email ?? $"anna.{Guid.NewGuid():N}@example.com",
        PhoneNo = "+48123456789",
        ProfilePicture = "https://example.com/photos/anna.jpg",
        Status = "Active",
        Address = "ul. Lipowa 1",
        State = "Lubelskie",
        Country = "Poland",
        City = "Lublin",
        Pincode = "20-001"
    };

    public static string SampleCsvPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EmployeeManagement.slnx")))
                directory = directory.Parent;

            return directory is null
                ? throw new InvalidOperationException("Repository root (EmployeeManagement.slnx) not found.")
                : Path.Combine(directory.FullName, "samples", "employees_sample.csv");
        }
    }
}
