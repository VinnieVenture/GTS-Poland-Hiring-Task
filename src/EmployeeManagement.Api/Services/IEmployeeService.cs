using EmployeeManagement.Api.Dtos;

namespace EmployeeManagement.Api.Services;

public interface IEmployeeService
{
    Task<ServiceResult<EmployeeResponseDto>> CreateAsync(EmployeeRequestDto request, CancellationToken cancelToken);
    Task<ServiceResult<EmployeeResponseDto>> GetByIdAsync(Guid id, CancellationToken cancelToken);
    Task<IReadOnlyList<EmployeeResponseDto>> GetAllAsync(CancellationToken cancelToken);
    Task<ServiceResult<EmployeeResponseDto>> UpdateAsync(Guid id, EmployeeRequestDto request, CancellationToken cancelToken);
    Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken cancelToken);
}