namespace EmployeeManagement.Api.Dtos;

public record EmployeeRequestDto
{
    public string? Name { get; init; }
    public DateOnly? HireDate { get; init; }
    public string? Email { get; init; }
    public string? PhoneNo { get; init; }
    public string? ProfilePicture { get; init; }
    public string? Status { get; init; }
    public string? Address { get; init; }
    public string? State { get; init; }
    public string? Country { get; init; }
    public string? City { get; init; }
    public string? Pincode { get; init; }
}