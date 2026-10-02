using EmployeeManagement.Api.Models;

namespace EmployeeManagement.Api.Services;

public record EmployeeQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 5;

}
public record FilterQuery : EmployeeQuery
{
    public string? Status { get; init; }
    public string? City { get; init; }
    public string? Search { get; init; }
}