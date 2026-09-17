using EmployeeManagement.Api.Dtos;

namespace EmployeeManagement.Api.Services;

public interface IEmployeeService
{
    Task<ServiceResult<EmployeeResponseDto>> CreateAsync(EmployeeRequestDto request, CancellationToken ct);
    Task<ServiceResult<EmployeeResponseDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EmployeeResponseDto>> GetAllAsync(CancellationToken ct);
    Task<ServiceResult<EmployeeResponseDto>> UpdateAsync(Guid id, EmployeeRequestDto request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken ct);
}