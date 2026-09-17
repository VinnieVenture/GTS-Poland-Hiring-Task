using System.Text;
using EmployeeManagement.Api.Data;
using EmployeeManagement.Api.Models;
using EmployeeManagement.Api.Services;
using EmployeeManagement.Api.Validators;
using EmployeeManagement.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Tests.Services;

// Uses the EF Core InMemory provider: fast and isolated, but it does not enforce the unique
// index on Email, so duplicates are covered here only by the service's own check.
public sealed class EmployeeServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly EmployeeService _service;

    public EmployeeServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"employees-{Guid.NewGuid()}")
            .Options;
        _db = new AppDbContext(options);

        var timeProvider = new FixedTimeProvider(TestData.Now);
        _service = new EmployeeService(
            _db,
            new EmployeeRequestValidator(timeProvider),
            new UpdateEmployeeRequestValidator(timeProvider),
            timeProvider);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_WithoutStatus_DefaultsToActiveAndNormalizesEmail()
    {
        var request = TestData.ValidRequest("  Anna.K@Example.com ") with { Status = null };

        var result = await _service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value!.Status);
        Assert.Equal("anna.k@example.com", result.Value.Email);
        Assert.Equal(TestData.Now.UtcDateTime, result.Value.CreatedAt);
    }

    [Fact]
    public async Task Create_WithEmailDifferingOnlyByCase_ReturnsConflict()
    {
        await _service.CreateAsync(TestData.ValidRequest("anna@example.com"), CancellationToken.None);

        var result = await _service.CreateAsync(TestData.ValidRequest("ANNA@example.com"), CancellationToken.None);

        Assert.Equal(ServiceError.Conflict, result.Error);
        Assert.Equal(1, await _db.Employees.CountAsync());
    }

    [Fact]
    public async Task Update_ExistingEmployee_ChangesFieldsButKeepsIdAndCreatedAt()
    {
        var existing = await SeedEmployeeAsync("anna@example.com");
        var request = TestData.ValidRequest("anna.nowak@example.com") with
        {
            Name = "Anna Nowak",
            Status = "Inactive",
            City = null
        };

        var result = await _service.UpdateAsync(existing.Id, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = await _db.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(existing.Id, saved.Id);
        Assert.Equal(existing.CreatedAt, saved.CreatedAt);
        Assert.Equal("Anna Nowak", saved.Name);
        Assert.Equal("anna.nowak@example.com", saved.Email);
        Assert.Equal(EmployeeStatus.Inactive, saved.Status);
        Assert.Null(saved.City);
    }

    [Fact]
    public async Task Update_KeepingOwnEmail_IsNotAConflict()
    {
        var existing = await SeedEmployeeAsync("anna@example.com");

        var result = await _service.UpdateAsync(existing.Id, TestData.ValidRequest("anna@example.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_WithEmailOfAnotherEmployee_ReturnsConflict()
    {
        await SeedEmployeeAsync("taken@example.com");
        var existing = await SeedEmployeeAsync("anna@example.com");

        var result = await _service.UpdateAsync(existing.Id, TestData.ValidRequest("taken@example.com"), CancellationToken.None);

        Assert.Equal(ServiceError.Conflict, result.Error);
    }

    [Fact]
    public async Task Update_NonExistingEmployee_ReturnsNotFound()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), TestData.ValidRequest(), CancellationToken.None);

        Assert.Equal(ServiceError.NotFound, result.Error);
    }

    [Fact]
    public async Task Update_WithInvalidData_ReturnsValidationErrorsAndSavesNothing()
    {
        var existing = await SeedEmployeeAsync("anna@example.com");
        var request = TestData.ValidRequest("anna@example.com") with { Status = "Retired", Name = "Changed" };

        var result = await _service.UpdateAsync(existing.Id, request, CancellationToken.None);

        Assert.Equal(ServiceError.Validation, result.Error);
        Assert.Contains("Status", result.ValidationErrors!.Keys);
        var saved = await _db.Employees.AsNoTracking().SingleAsync();
        Assert.Equal(existing.Name, saved.Name);
    }

    [Fact]
    public async Task Update_WithoutStatus_IsRejectedInsteadOfReactivating()
    {
        var existing = await SeedEmployeeAsync("anna@example.com", EmployeeStatus.Inactive);

        var result = await _service.UpdateAsync(existing.Id, TestData.ValidRequest("anna@example.com") with { Status = null }, CancellationToken.None);

        Assert.Equal(ServiceError.Validation, result.Error);
        Assert.Equal(EmployeeStatus.Inactive, (await _db.Employees.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Import_SampleCsv_SavesValidRowsAndReportsInvalidOnes()
    {
        await using var file = File.OpenRead(TestData.SampleCsvPath);

        var result = await _service.ImportAsync(file, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var report = result.Value!;
        Assert.Equal(10, report.TotalRows);
        Assert.Equal(6, report.ImportedCount);
        // Line numbers as in the file (header = 1): two future dates, month 13, year 1899.
        Assert.Equal(new[] { 3, 8, 9, 11 }, report.Failed.Select(f => f.Row));
        Assert.All(report.Failed, f => Assert.Contains("HireDate", f.Errors.Keys));
        Assert.Equal(6, await _db.Employees.CountAsync());
    }

    [Fact]
    public async Task Import_DuplicateEmailInFileAndInDatabase_RejectsOnlyThoseRows()
    {
        await SeedEmployeeAsync("existing@example.com");
        var csv = """
            Name,HireDate,Email,PhoneNo,Status
            First,2024-01-01,dup@example.com,+48111111111,Inactive
            Second,2024-01-01,DUP@example.com,+48222222222,Active
            Third,2024-01-01,existing@example.com,+48333333333,Active
            """;

        var result = await _service.ImportAsync(ToStream(csv), CancellationToken.None);

        var report = result.Value!;
        Assert.Equal(1, report.ImportedCount);
        Assert.Equal(new[] { 3, 4 }, report.Failed.Select(f => f.Row));
        var imported = await _db.Employees.SingleAsync(e => e.Email == "dup@example.com");
        Assert.Equal(EmployeeStatus.Inactive, imported.Status);
    }

    [Fact]
    public async Task Import_EmptyStatusCell_IsRejectedNotDefaultedToActive()
    {
        var csv = """
            Name,HireDate,Email,PhoneNo,Status
            Anna,2024-01-01,anna@example.com,+48111111111,
            """;

        var result = await _service.ImportAsync(ToStream(csv), CancellationToken.None);

        var failedRow = Assert.Single(result.Value!.Failed);
        Assert.Equal(2, failedRow.Row);
        Assert.Contains("Status", failedRow.Errors.Keys);
        Assert.Equal(0, await _db.Employees.CountAsync());
    }

    [Fact]
    public async Task Import_WithoutStatusColumn_RejectsTheWholeFile()
    {
        var csv = """
            Name,HireDate,Email,PhoneNo
            Anna,2024-01-01,anna@example.com,+48111111111
            """;

        var result = await _service.ImportAsync(ToStream(csv), CancellationToken.None);

        Assert.Equal(ServiceError.Validation, result.Error);
        Assert.Contains("file", result.ValidationErrors!.Keys);
        Assert.Equal(0, await _db.Employees.CountAsync());
    }

    [Fact]
    public async Task Import_MissingRequiredColumns_RejectsTheWholeFile()
    {
        var result = await _service.ImportAsync(ToStream("Name,Email\nAnna,anna@example.com"), CancellationToken.None);

        Assert.Equal(ServiceError.Validation, result.Error);
        Assert.Contains("file", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Import_HeaderOnly_RejectsTheWholeFile()
    {
        var result = await _service.ImportAsync(ToStream("Name,HireDate,Email,PhoneNo,Status"), CancellationToken.None);

        Assert.Equal(ServiceError.Validation, result.Error);
    }

    private async Task<Employee> SeedEmployeeAsync(string email, EmployeeStatus status = EmployeeStatus.Active)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Name = "Seeded Employee",
            Email = email,
            PhoneNo = "+48100200300",
            HireDate = new DateOnly(2020, 1, 1),
            Status = status,
            City = "Lublin",
            CreatedAt = new DateTime(2020, 1, 1, 8, 0, 0, DateTimeKind.Utc)
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return employee;
    }

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
