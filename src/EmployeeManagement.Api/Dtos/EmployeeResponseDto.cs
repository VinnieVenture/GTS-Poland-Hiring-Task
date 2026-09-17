namespace EmployeeManagement.Api.Dtos;

public record EmployeeResponseDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required DateOnly HireDate { get; init; }
    public required string Email { get; init; }
    public required string PhoneNo { get; init; }
    public required string? ProfilePicture { get; init; }
    public required string Status { get; init; }
    public required string? Address { get; init; }
    public required string? State { get; init; }
    public required string? Country { get; init; }
    public required string? City { get; init; }
    public required string? Pincode { get; init; }
    public required DateTime CreatedAt { get; init; }
}