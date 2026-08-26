namespace Abuvi.Setup;

public static class CsvHelper
{
    /// <summary>
    /// Reads a CSV file and returns rows as dictionaries (header -> value).
    /// Comma-separated, UTF-8, first row is header.
    /// Supports RFC 4180 quoting: quoted fields may contain commas, newlines and
    /// escaped double quotes (""). Unquoted fields are trimmed; quoted fields are
    /// preserved verbatim. Quotes are also stripped from header names.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, string>> Parse(string filePath)
    {
        var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var records = ParseRecords(content);

        if (records.Count < 2)
            return [];

        var headers = records[0].Select(h => h.Trim()).ToArray();
        var rows = new List<Dictionary<string, string>>();

        for (var i = 1; i < records.Count; i++)
        {
            var values = records[i];
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length && j < values.Count; j++)
                dict[headers[j]] = values[j];
            rows.Add(dict);
        }

        return rows;
    }

    /// <summary>
    /// Splits raw CSV content into records of fields, honouring quoted sections.
    /// Blank records are skipped, matching the previous line-based behaviour.
    /// </summary>
    private static List<List<string>> ParseRecords(string content)
    {
        var records = new List<List<string>>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        var fieldWasQuoted = false;

        void EndField()
        {
            fields.Add(fieldWasQuoted ? field.ToString() : field.ToString().Trim());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRecord()
        {
            EndField();
            // Skip blank lines (a single empty, unquoted field)
            if (fields.Count > 1 || !string.IsNullOrWhiteSpace(fields[0]))
                records.Add([.. fields]);
            fields.Clear();
        }

        bool FieldIsBlankSoFar()
        {
            for (var k = 0; k < field.Length; k++)
                if (!char.IsWhiteSpace(field[k])) return false;
            return true;
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // "" inside a quoted field is a literal quote
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when FieldIsBlankSoFar():
                    inQuotes = true;
                    fieldWasQuoted = true;
                    field.Clear();
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRecord();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
            EndRecord();

        return records;
    }

    /// <summary>
    /// Writes rows back to a CSV file, quoting every field so that commas,
    /// quotes and newlines survive a Parse round-trip. Columns are written in
    /// the order given; a key missing from a row becomes an empty field.
    /// </summary>
    public static void Write(
        string filePath,
        IReadOnlyList<string> headers,
        IEnumerable<Dictionary<string, string>> rows)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(string.Join(',', headers.Select(Quote)));

        foreach (var row in rows)
            sb.AppendLine(string.Join(',', headers.Select(h =>
                Quote(row.TryGetValue(h, out var v) ? v : string.Empty))));

        File.WriteAllText(filePath, sb.ToString(), new System.Text.UTF8Encoding(false));
    }

    private static string Quote(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    public static string Require(Dictionary<string, string> row, string key)
    {
        if (row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val;
        throw new InvalidOperationException($"Missing required field: {key}");
    }

    public static string? Optional(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val)
            ? val : null;
    }

    public static decimal RequireDecimal(Dictionary<string, string> row, string key)
    {
        var val = Require(row, key);
        return decimal.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static decimal? OptionalDecimal(Dictionary<string, string> row, string key)
    {
        var val = Optional(row, key);
        return val is not null
            ? decimal.Parse(val, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    public static int? OptionalInt(Dictionary<string, string> row, string key)
    {
        var val = Optional(row, key);
        return val is not null ? int.Parse(val) : null;
    }
}
