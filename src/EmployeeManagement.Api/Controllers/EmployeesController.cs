using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[ApiController]
public class EmployeesController(IEmployeeService service) : ControllerBase
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    [HttpPost("employee")]
    public async Task<IActionResult> Create(EmployeeRequestDto request, CancellationToken cancelToken)
    {
        var result = await service.CreateAsync(request, cancelToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : ToErrorResponse(result);
    }

    [HttpGet("employee/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancelToken)
    {
        var result = await service.GetByIdAsync(id, cancelToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result);
    }

    [HttpGet("employees")]
    public async Task<IActionResult> GetAll(CancellationToken cancelToken) =>
        Ok(await service.GetAllAsync(cancelToken));

    [HttpPut("employee/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, EmployeeRequestDto request, CancellationToken cancelToken)
    {
        var result = await service.UpdateAsync(id, request, cancelToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result);
    }

    [HttpDelete("employee/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancelToken)
    {
        var result = await service.DeleteAsync(id, cancelToken);
        return result.IsSuccess ? NoContent() : ToErrorResponse(result);
    }

    // Returns 200 with a per-row report even when some rows fail (partial success).
    // 400 only when the file itself is unusable (missing, wrong type, bad header, no rows).
    [HttpPost("employees/bulk")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> BulkImport(IFormFile? file, CancellationToken cancelToken)
    {
        if (file is null || file.Length == 0)
            return FileError("A non-empty CSV file is required in the form field 'file'.");

        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return FileError("Only .csv files are supported.");

        await using var stream = file.OpenReadStream();
        var result = await service.ImportAsync(stream, cancelToken);
        return result.IsSuccess ? Ok(result.Value) : ToErrorResponse(result);
    }

    private IActionResult FileError(string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = [message] }));

    private IActionResult ToErrorResponse<T>(ServiceResult<T> result) => result.Error switch
    {
        ServiceError.Validation => ValidationProblem(new ValidationProblemDetails(result.ValidationErrors!)),
        ServiceError.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Employee not found."),
        ServiceError.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "An employee with this email already exists."),
        _ => throw new InvalidOperationException($"Unhandled service error: {result.Error}")
    };
}
