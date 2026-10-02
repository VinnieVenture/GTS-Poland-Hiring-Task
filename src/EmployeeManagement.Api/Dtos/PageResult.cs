using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Models;

namespace EmployeeManagement.Api.Services;

public record PageResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; } = 0;
    public int TotalPages { get; init; }
    required public IEnumerable<T> Items { get; init; }
}