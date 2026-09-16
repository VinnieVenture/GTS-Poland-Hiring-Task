namespace EmployeeManagement.Api.Models{
    public class Employee
    {
        public Guid Id { get; set; } // Guid used intentionally to avoid the security risks of predictable integer ID generation. Read more: README.md
        public required string Name { get; set; }
        public DateOnly HireDate { get; set; }
        public required string Email { get; set; }
        public required string PhoneNo { get; set; }
        public string? ProfilePicture { get; set; }
        public EmployeeStatus Status { get; set; }
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Pincode { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}