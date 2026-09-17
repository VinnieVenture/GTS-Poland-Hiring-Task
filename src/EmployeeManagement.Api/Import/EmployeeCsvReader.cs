using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using EmployeeManagement.Api.Dtos;

namespace EmployeeManagement.Api.Import;

// Request is null when the row could not be parsed at all; ParseErrors then explains why.
public sealed record ParsedCsvRow(int RowNumber, EmployeeRequestDto? Request, IReadOnlyDictionary<string, string[]> ParseErrors);

public sealed record CsvReadResult(IReadOnlyList<ParsedCsvRow> Rows, string? Error)
{
    public static CsvReadResult Failed(string error) => new([], error);
}

public static class EmployeeCsvReader
{
    public const int MaxRows = 10_000;
    private const string DateFormat = "yyyy-MM-dd";

    public static async Task<CsvReadResult> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var badDataFound = false;
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // Header matching ignores case and surrounding spaces ("Hiredate" == "HireDate").
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = _ => badDataFound = true
        };

        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync() || !csv.ReadHeader())
            return CsvReadResult.Failed("The file is empty.");

        try
        {
            csv.ValidateHeader<EmployeeCsvRow>();
        }
        catch (HeaderValidationException)
        {
            return CsvReadResult.Failed($"The header must contain the columns: {EmployeeCsvRow.RequiredColumns}.");
        }

        var rows = new List<ParsedCsvRow>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rows.Count == MaxRows)
                return CsvReadResult.Failed($"The file contains more than {MaxRows} data rows.");

            // Row 1 is the header, so the first data row is 2 - the same number a spreadsheet shows.
            var rowNumber = csv.Parser.Row;

            if (badDataFound)
            {
                badDataFound = false;
                rows.Add(new ParsedCsvRow(rowNumber, null, Error("Row", "The row is not valid CSV (check quotes).")));
                continue;
            }

            var record = csv.GetRecord<EmployeeCsvRow>();
            if (record is null)
            {
                rows.Add(new ParsedCsvRow(rowNumber, null, Error("Row", "The row could not be read.")));
                continue;
            }

            var parseErrors = new Dictionary<string, string[]>();
            DateOnly? hireDate = null;
            if (!string.IsNullOrWhiteSpace(record.HireDate))
            {
                if (DateOnly.TryParseExact(record.HireDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    hireDate = parsed;
                else
                    parseErrors["HireDate"] = [$"HireDate '{record.HireDate}' is not a valid date in format {DateFormat}."];
            }

            rows.Add(new ParsedCsvRow(rowNumber, record.ToRequest(hireDate), parseErrors));
        }

        return rows.Count == 0
            ? CsvReadResult.Failed("The file contains no data rows.")
            : new CsvReadResult(rows, null);
    }

    private static Dictionary<string, string[]> Error(string field, string message) =>
        new() { [field] = [message] };
}
