namespace Abuvi.Setup;

using Serilog;

public record SeedRowResult(int Row, bool Success, string? Error);

public record SeedResult(
    string Entity,
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<SeedRowResult> Rows)
{
    public void Print(bool dryRun = false)
    {
        var verb = dryRun ? "would import" : "imported";
        var skipVerb = dryRun ? "would skip" : "skipped";

        if (Skipped > 0)
            Log.Warning("{Entity}: {Imported}/{Total} {Verb}, {Skipped} {SkipVerb}",
                Entity, Imported, TotalRows, verb, Skipped, skipVerb);
        else
            Log.Information("{Entity}: {Imported}/{Total} {Verb}, {Skipped} {SkipVerb}",
                Entity, Imported, TotalRows, verb, Skipped, skipVerb);

        foreach (var row in Rows.Where(r => !r.Success))
            Log.Warning("  Row {Row}: {Error}", row.Row, row.Error);
    }
}
