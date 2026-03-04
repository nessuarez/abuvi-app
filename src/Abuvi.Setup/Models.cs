namespace Abuvi.Setup;

public record SeedRowResult(int Row, bool Success, string? Error);

public record SeedResult(
    string Entity,
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<SeedRowResult> Rows)
{
    public void Print()
    {
        var color = Skipped > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.ForegroundColor = color;
        Console.WriteLine($"  {Entity}: {Imported}/{TotalRows} imported, {Skipped} skipped");
        Console.ResetColor();
        foreach (var row in Rows.Where(r => !r.Success))
            Console.WriteLine($"    Row {row.Row}: {row.Error}");
    }
}
