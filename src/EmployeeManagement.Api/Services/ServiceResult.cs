namespace EmployeeManagement.Api.Services;

public sealed record ServiceResult<T>
{
    public T? Value { get; private init; }
    public ServiceError? Error { get; private init; }
    public IDictionary<string, string[]>? ValidationErrors { get; private init; }
    public bool IsSuccess => Error is null;

    public static ServiceResult<T> Success(T value) => new() { Value = value };
    public static ServiceResult<T> NotFound() => new() { Error = ServiceError.NotFound };
    public static ServiceResult<T> Conflict() => new() { Error = ServiceError.Conflict };
    public static ServiceResult<T> Invalid(IDictionary<string, string[]> errors) =>
        new() { Error = ServiceError.Validation, ValidationErrors = errors };

    public static ServiceResult<T> Failure(ServiceError error)
    {
        if (error == ServiceError.Validation)
            throw new ArgumentException("Use Invalid() for validation errors so the error details are included.", nameof(error));

        return new() { Error = error };
    }
}
