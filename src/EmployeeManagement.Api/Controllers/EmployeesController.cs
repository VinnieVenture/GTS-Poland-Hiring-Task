using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Api.Controllers;

[ApiController]
public class EmployeesController : ControllerBase
{
    [HttpPost("employee")]
    public IActionResult Create()
    {
        return Ok("TODO: create");
    }

    [HttpGet("employee/{id}")]
    public IActionResult GetById(Guid id)
    {
        return Ok("TODO: get by id");
    }

    [HttpGet("employees")]
    public IActionResult GetAll()
    {
        return Ok("TODO: get all");
    }

    [HttpPut("employee/{id}")]
    public IActionResult Update(Guid id)
    {
        return Ok("TODO: update");
    }

    [HttpDelete("employee/{id}")]
    public IActionResult Delete(Guid id)
    {
        return Ok("TODO: delete");
    }

    [HttpPost("employees/bulk")]
    public IActionResult BulkImport()
    {
        return Ok("TODO: bulk import");
    }
}