using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.Tests.Endpoints;

// End-to-end through routing, model binding, validation, the service and error handling.
public class EmployeesApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateThenGet_ReturnsCreatedEmployee()
    {
        var request = TestData.ValidRequest() with { Status = null };

        var createResponse = await _client.PostAsJsonAsync("/employee", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var created = await createResponse.Content.ReadFromJsonAsync<EmployeeResponseDto>();
        Assert.Equal("Active", created!.Status);

        var getResponse = await _client.GetAsync(createResponse.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<EmployeeResponseDto>();
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Create_WithInvalidData_Returns400WithAllFieldErrors()
    {
        var request = TestData.ValidRequest() with
        {
            HireDate = TestData.Today.AddDays(1),
            Status = "Pending",
            PhoneNo = "123"
        };

        var response = await _client.PostAsJsonAsync("/employee", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        var fields = problem!.Errors.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("HireDate", fields);
        Assert.Contains("Status", fields);
        Assert.Contains("PhoneNo", fields);
    }

    [Fact]
    public async Task Create_WithUnparsableDate_Returns400WithoutLeakingTypeNames()
    {
        // Model binding fails before the validators run, so the message comes from DateOnlyJsonConverter.
        const string json = """
            {"name":"A","hireDate":"2222-22-11","email":"a@example.com","phoneNo":"+48123456789"}
            """;
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/employee", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        var messages = string.Join(" ", problem!.Errors.SelectMany(error => error.Value));
        Assert.Contains("yyyy-MM-dd", messages);
        Assert.DoesNotContain("System.", messages);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_Returns409()
    {
        var email = $"dup.{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/employee", TestData.ValidRequest(email));

        var response = await _client.PostAsJsonAsync("/employee", TestData.ValidRequest(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/employee/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGet_Returns404()
    {
        var createResponse = await _client.PostAsJsonAsync("/employee", TestData.ValidRequest());
        var location = createResponse.Headers.Location;

        var deleteResponse = await _client.DeleteAsync(location);
        var getResponse = await _client.GetAsync(location);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task BulkImport_WithoutFile_Returns400()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("ignored"), "notAFile");

        var response = await _client.PostAsync("/employees/bulk", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkImport_SampleCsv_ReturnsReport()
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(await File.ReadAllBytesAsync(TestData.SampleCsvPath));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "employees_sample.csv");

        var response = await _client.PostAsync("/employees/bulk", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<BulkImportResultDto>();
        Assert.Equal(6, report!.ImportedCount);
        Assert.Equal(4, report.FailedCount);
    }
}
