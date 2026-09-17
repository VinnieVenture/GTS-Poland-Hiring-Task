using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Models;

namespace EmployeeManagement.Api.Services;

public static class EmployeeMappings
{
    public static EmployeeResponseDto ToResponse(this Employee e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        HireDate = e.HireDate,
        Email = e.Email,
        PhoneNo = e.PhoneNo,
        ProfilePicture = e.ProfilePicture,
        Status = e.Status.ToString(),
        Address = e.Address,
        State = e.State,
        Country = e.Country,
        City = e.City,
        Pincode = e.Pincode,
        CreatedAt = e.CreatedAt
    };
}