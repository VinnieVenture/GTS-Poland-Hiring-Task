using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmployeeManagement.Api.Json;

/// <summary>
/// Reads and writes <see cref="DateOnly"/> as yyyy-MM-dd.
/// Without it, a value such as "2222-22-11" is reported to the client as
/// "The JSON value could not be converted to System.Nullable`1[System.DateOnly]",
/// which leaks an internal type name and means nothing to an API consumer.
/// System.Text.Json reuses this converter for DateOnly? as well.
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";
    private const string Message = $"Expected a date as a string in the {Format} format.";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            // e.g. hireDate: 2024 or hireDate: true
            throw new JsonException(Message);
        }

        // Exact format only: DateOnly.Parse would accept culture-dependent input such as "01/03/2024".
        return DateOnly.TryParseExact(reader.GetString(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            // The invalid value is deliberately not echoed back into the error message.
            : throw new JsonException(Message);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
