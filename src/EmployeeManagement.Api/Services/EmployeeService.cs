using EmployeeManagement.Api.Data;
using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Import;
using EmployeeManagement.Api.Models;
using EmployeeManagement.Api.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmployeeManagement.Api.Services;

public class EmployeeService(
    AppDbContext db,
    [FromKeyedServices(ValidatorKeys.Create)] IValidator<EmployeeRequestDto> createValidator,
    [FromKeyedServices(ValidatorKeys.Update)] IValidator<EmployeeRequestDto> updateValidator,
    TimeProvider timeProvider) : IEmployeeService
{
    public async Task<ServiceResult<EmployeeResponseDto>> CreateAsync(EmployeeRequestDto request, CancellationToken cancelToken)
    {
        var dto = request.Normalize();

        var validation = await createValidator.ValidateAsync(dto, cancelToken);
        if (!validation.IsValid)
            return ServiceResult<EmployeeResponseDto>.Invalid(validation.ToDictionary());

        if (await db.Employees.AnyAsync(e => e.Email == dto.Email, cancelToken))
            return ServiceResult<EmployeeResponseDto>.Conflict();

        var employee = CreateEmployeeFrom(dto);
        db.Employees.Add(employee);

        var saveError = await TrySaveAsync(cancelToken);
        return saveError is null
            ? ServiceResult<EmployeeResponseDto>.Success(employee.ToResponse())
            : ServiceResult<EmployeeResponseDto>.Failure(saveError.Value);
    }

    public async Task<ServiceResult<EmployeeResponseDto>> GetByIdAsync(Guid id, CancellationToken cancelToken)
    {
        var employee = await db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancelToken);

        return employee is null
            ? ServiceResult<EmployeeResponseDto>.NotFound()
            : ServiceResult<EmployeeResponseDto>.Success(employee.ToResponse());
    }

    public async Task<IReadOnlyList<EmployeeResponseDto>> GetAllAsync(CancellationToken cancelToken)
    {
        var employees = await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancelToken);

        return employees.Select(e => e.ToResponse()).ToList();
    }

    public async Task<ServiceResult<EmployeeResponseDto>> UpdateAsync(Guid id, EmployeeRequestDto request, CancellationToken cancelToken)
    {
        var dto = request.Normalize();

        var validation = await updateValidator.ValidateAsync(dto, cancelToken);
        if (!validation.IsValid)
            return ServiceResult<EmployeeResponseDto>.Invalid(validation.ToDictionary());

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancelToken);
        if (employee is null)
            return ServiceResult<EmployeeResponseDto>.NotFound();

        if (await db.Employees.AnyAsync(e => e.Email == dto.Email && e.Id != id, cancelToken))
            return ServiceResult<EmployeeResponseDto>.Conflict();

        UpdateEmployeeFrom(employee, dto);

        var saveError = await TrySaveAsync(cancelToken);
        return saveError is null
            ? ServiceResult<EmployeeResponseDto>.Success(employee.ToResponse())
            : ServiceResult<EmployeeResponseDto>.Failure(saveError.Value);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken cancelToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancelToken);
        if (employee is null)
            return ServiceResult<bool>.NotFound();

        db.Employees.Remove(employee);

        var saveError = await TrySaveAsync(cancelToken);
        return saveError is null
            ? ServiceResult<bool>.Success(true)
            : ServiceResult<bool>.Failure(saveError.Value);
    }

    // Three stages: validate every row, drop emails that already exist, save the rest.
    public async Task<ServiceResult<BulkImportResultDto>> ImportAsync(Stream csvStream, CancellationToken cancelToken)
    {
        var readResult = await EmployeeCsvReader.ReadAsync(csvStream, cancelToken);
        if (readResult.Error is not null)
            return ServiceResult<BulkImportResultDto>.Invalid(ErrorFor("file", readResult.Error));

        var failed = new List<BulkImportRowErrorDto>();
        var validRowsByEmail = await ValidateRowsAsync(readResult.Rows, failed, cancelToken);
        var toSave = await ExcludeExistingEmailsAsync(validRowsByEmail, failed, cancelToken);
        var imported = await SaveImportedAsync(toSave, failed, cancelToken);

        return ServiceResult<BulkImportResultDto>.Success(new BulkImportResultDto
        {
            TotalRows = readResult.Rows.Count,
            ImportedCount = imported.Count,
            FailedCount = failed.Count,
            Imported = imported,
            Failed = failed.OrderBy(f => f.Row).ToList()
        });
    }

    // Non-null values are guaranteed by the validator, which always runs first.
    private Employee CreateEmployeeFrom(EmployeeRequestDto dto)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Name = dto.Name!,
            Email = dto.Email!,
            PhoneNo = dto.PhoneNo!,
            HireDate = dto.HireDate!.Value,
            Status = dto.Status is null ? EmployeeStatus.Active : ParseStatus(dto.Status),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        ApplyOptionalFields(employee, dto);
        return employee;
    }

    // Id and CreatedAt are intentionally never updated.
    private static void UpdateEmployeeFrom(Employee employee, EmployeeRequestDto dto)
    {
        employee.Name = dto.Name!;
        employee.Email = dto.Email!;
        employee.PhoneNo = dto.PhoneNo!;
        employee.HireDate = dto.HireDate!.Value;
        employee.Status = ParseStatus(dto.Status!);
        ApplyOptionalFields(employee, dto);
    }

    private static void ApplyOptionalFields(Employee employee, EmployeeRequestDto dto)
    {
        employee.ProfilePicture = dto.ProfilePicture;
        employee.Address = dto.Address;
        employee.State = dto.State;
        employee.Country = dto.Country;
        employee.City = dto.City;
        employee.Pincode = dto.Pincode;
    }

    private const string EmailExistsMessage = "An employee with this email already exists.";

    private sealed record ValidRow(int Row, EmployeeRequestDto Dto);

    // Keyed by email: TryAdd both collects valid rows and detects duplicates within the file.
    private async Task<Dictionary<string, ValidRow>> ValidateRowsAsync(
        IReadOnlyList<ParsedCsvRow> rows,
        List<BulkImportRowErrorDto> failed,
        CancellationToken cancelToken)
    {
        var validRowsByEmail = new Dictionary<string, ValidRow>();
        foreach (var row in rows)
        {
            var (dto, errors) = await ValidateRowAsync(row, cancelToken);

            if (dto is null)
                failed.Add(new BulkImportRowErrorDto(row.RowNumber, errors));
            else if (!validRowsByEmail.TryAdd(dto.Email!, new ValidRow(row.RowNumber, dto)))
                failed.Add(new BulkImportRowErrorDto(row.RowNumber, ErrorFor("Email", "Email is duplicated within the file.")));
        }

        return validRowsByEmail;
    }

    // The same normalization and validator as POST /employee. Dto is null when the row is invalid.
    private async Task<(EmployeeRequestDto? Dto, IDictionary<string, string[]> Errors)> ValidateRowAsync(
        ParsedCsvRow row,
        CancellationToken cancelToken)
    {
        if (row.Request is null)
            return (null, new Dictionary<string, string[]>(row.ParseErrors));

        var dto = row.Request.Normalize();
        var errors = (await createValidator.ValidateAsync(dto, cancelToken)).ToDictionary();

        // A parse error is more precise than the validator's "HireDate must not be empty".
        foreach (var (field, messages) in row.ParseErrors)
            errors[field] = messages;

        if (errors.Count > 0)
            return (null, errors);

        return (dto, errors);
    }

    // One query for all emails from the file instead of one query per row.
    private async Task<List<(int Row, Employee Employee)>> ExcludeExistingEmailsAsync(
        Dictionary<string, ValidRow> validRowsByEmail,
        List<BulkImportRowErrorDto> failed,
        CancellationToken cancelToken)
    {
        var emails = validRowsByEmail.Keys.ToList();
        var existingEmails = await db.Employees
            .Where(e => emails.Contains(e.Email))
            .Select(e => e.Email)
            .ToListAsync(cancelToken);

        foreach (var email in existingEmails)
        {
            failed.Add(new BulkImportRowErrorDto(validRowsByEmail[email].Row, ErrorFor("Email", EmailExistsMessage)));
            validRowsByEmail.Remove(email);
        }

        return validRowsByEmail.Values
            .OrderBy(r => r.Row)
            .Select(r => (r.Row, Employee: CreateEmployeeFrom(r.Dto)))
            .ToList();
    }

    // Partial success: valid rows are saved even when other rows fail.
    // One SaveChanges for the whole batch; if a concurrent request inserted one of the emails
    // in the meantime, fall back to row-by-row saving so a single conflict does not reject the file.
    private async Task<List<BulkImportedRowDto>> SaveImportedAsync(
        List<(int Row, Employee Employee)> toSave,
        List<BulkImportRowErrorDto> failed,
        CancellationToken cancelToken)
    {
        var imported = new List<BulkImportedRowDto>();
        if (toSave.Count == 0)
            return imported;

        db.Employees.AddRange(toSave.Select(x => x.Employee));
        if (await TrySaveAsync(cancelToken) is null)
        {
            imported.AddRange(toSave.Select(x => new BulkImportedRowDto(x.Row, x.Employee.Id, x.Employee.Email)));
            return imported;
        }

        // A failed SaveChanges leaves the entities tracked - clear them before retrying.
        db.ChangeTracker.Clear();
        foreach (var (rowNumber, employee) in toSave)
        {
            db.Employees.Add(employee);
            if (await TrySaveAsync(cancelToken) is null)
            {
                imported.Add(new BulkImportedRowDto(rowNumber, employee.Id, employee.Email));
            }
            else
            {
                db.ChangeTracker.Clear();
                failed.Add(new BulkImportRowErrorDto(rowNumber, ErrorFor("Email", EmailExistsMessage)));
            }
        }

        return imported;
    }

    private static Dictionary<string, string[]> ErrorFor(string field, string message) =>
        new() { [field] = [message] };

    private static EmployeeStatus ParseStatus(string status) =>
        Enum.Parse<EmployeeStatus>(status, ignoreCase: true);

    // Returns null on success. Expected persistence failures become service errors;
    // anything else propagates to the global exception handler.
    private async Task<ServiceError?> TrySaveAsync(CancellationToken cancelToken)
    {
        try
        {
            await db.SaveChangesAsync(cancelToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The record was deleted by another request between our read and this save.
            return ServiceError.NotFound;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The unique index on Email is the last line of defence against two concurrent
            // requests with the same email - both can pass the AnyAsync check.
            return ServiceError.Conflict;
        }
    }
}
