using EmployeeManagement.Api.Data;
using EmployeeManagement.Api.Dtos;
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
