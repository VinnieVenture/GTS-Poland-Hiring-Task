namespace EmployeeManagement.Api.Dtos;

public record BulkImportResultDto
{
    public required int TotalRows { get; init; }
    public required int ImportedCount { get; init; }
    public required int FailedCount { get; init; }
    public required IReadOnlyList<BulkImportedRowDto> Imported { get; init; }
    public required IReadOnlyList<BulkImportRowErrorDto> Failed { get; init; }
}

public record BulkImportedRowDto(int Row, Guid Id, string Email);

public record BulkImportRowErrorDto(int Row, IDictionary<string, string[]> Errors);
