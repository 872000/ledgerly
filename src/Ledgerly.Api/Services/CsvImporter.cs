using System.Globalization;
using System.Text;

namespace Ledgerly.Api.Services;

public sealed record ImportRow(DateTime Date, string Description, decimal Amount, string Category);

public sealed class CsvFormatException(string message) : Exception(message);

/// <summary>
/// Parses bank-style CSV exports: date,description,amount,category.
/// Handles quoted fields containing commas and quotes. Amounts may carry
/// currency symbols; parentheses denote negatives, e.g. ($12.50).
/// </summary>
public sealed class CsvImporter
{
    public IReadOnlyList<ImportRow> Parse(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var rows = new List<ImportRow>();
        var lineNumber = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Skip a header row.
            if (lineNumber == 1 && LooksLikeHeader(line))
            {
                continue;
            }

            rows.Add(ParseLine(line, lineNumber));
        }

        return rows;
    }

    private static bool LooksLikeHeader(string line) =>
        line.TrimStart().StartsWith("date", StringComparison.OrdinalIgnoreCase);

    private static ImportRow ParseLine(string line, int lineNumber)
    {
        var fields = SplitFields(line);
        if (fields.Count < 4)
        {
            throw new CsvFormatException($"Line {lineNumber}: expected 4 columns (date,description,amount,category).");
        }

        if (!DateTime.TryParse(fields[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
        {
            throw new CsvFormatException($"Line {lineNumber}: cannot parse date '{fields[0]}'.");
        }

        var description = fields[1].Trim();
        if (description.Length == 0)
        {
            throw new CsvFormatException($"Line {lineNumber}: description is empty.");
        }

        var amount = ParseAmount(fields[2].Trim(), lineNumber);

        return new ImportRow(date.Date, description, amount, fields[3].Trim());
    }

    private static decimal ParseAmount(string raw, int lineNumber)
    {
        var negative = false;
        var text = raw;

        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            negative = true;
            text = text[1..^1];
        }

        var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            throw new CsvFormatException($"Line {lineNumber}: cannot parse amount '{raw}'.");
        }

        return negative ? -Math.Abs(amount) : amount;
    }

    /// <summary>Splits one CSV line respecting double-quoted fields.</summary>
    private static List<string> SplitFields(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
