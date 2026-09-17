using System.Globalization;
using EmployeeManagement.Api.Data;
using EmployeeManagement.Api.Dtos;
using EmployeeManagement.Api.ErrorHandling;
using EmployeeManagement.Api.Services;
using EmployeeManagement.Api.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddKeyedScoped<IValidator<EmployeeRequestDto>, EmployeeRequestValidator>(ValidatorKeys.Create);
builder.Services.AddKeyedScoped<IValidator<EmployeeRequestDto>, UpdateEmployeeRequestValidator>(ValidatorKeys.Update);
builder.Services.AddScoped<IEmployeeService, EmployeeService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.InvariantCulture; // to avoid localization issues in validation messages

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the auto-generated Program class visible to WebApplicationFactory in the test project.
public partial class Program;
