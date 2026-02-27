namespace Abuvi.Setup;

public static class CsvHelper
{
    /// <summary>
    /// Reads a CSV file and returns rows as dictionaries (header -> value).
    /// Comma-separated, UTF-8, first row is header.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, string>> Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count < 2)
            return [];

        var headers = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var rows = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Count; i++)
        {
            var values = lines[i].Split(',').Select(v => v.Trim()).ToArray();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length && j < values.Length; j++)
                dict[headers[j]] = values[j];
            rows.Add(dict);
        }

        return rows;
    }

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

    public static int? OptionalInt(Dictionary<string, string> row, string key)
    {
        var val = Optional(row, key);
        return val is not null ? int.Parse(val) : null;
    }
}
