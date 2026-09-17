namespace EmployeeManagement.Api.Dtos;

public static class EmployeeRequestNormalizer
{
    public static EmployeeRequestDto Normalize(this EmployeeRequestDto dto) => dto with
    {
        Name = dto.Name?.Trim(),
        Email = dto.Email?.Trim().ToLowerInvariant(),
        PhoneNo = NormalizePhone(dto.PhoneNo),
        Status = dto.Status?.Trim().ToLowerInvariant(),
        ProfilePicture = BlankToNull(dto.ProfilePicture),
        Address = BlankToNull(dto.Address),
        State = BlankToNull(dto.State),
        Country = BlankToNull(dto.Country),
        City = BlankToNull(dto.City),
        Pincode = BlankToNull(dto.Pincode)
    };

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizePhone(string? phone) =>
        phone is null
            ? null
            : new string(phone.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')' or '.')).ToArray());
}