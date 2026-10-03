namespace EmployeeManagement.Api.Dtos;

public record EmployeeQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public record FilterQuery : EmployeeQuery
{
    public string? Status { get; init; }
    public string? City { get; init; }
    public string? Search { get; init; }
}