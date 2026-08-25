using Abuvi.API.Features.GooglePlaces;
using Serilog;

namespace Abuvi.Setup.Geocoding;

/// <summary>
/// Fills in coordinates for camp venues listed in a CSV, using Google Places,
/// and records enough context for a human to verify each one before import.
///
/// Idempotent: rows that already carry a latitude are skipped, so re-running
/// costs no API quota and never overwrites a manual correction.
/// </summary>
public class GeocodeRunner(IGooglePlacesService places)
{
    /// <summary>Columns this command adds to (or refreshes in) the CSV.</summary>
    public static readonly string[] GeocodeColumns =
    [
        "latitude", "longitude", "googlePlaceId", "formattedAddress",
        "googleProvince", "googleTypes", "geocodeStatus", "reviewNotes"
    ];

    /// <summary>Statuses that mean "a human already settled this row".</summary>
    private static readonly string[] SettledStatuses = ["ok", "ok_manual"];

    public async Task<GeocodeReport> RunAsync(string csvPath, CancellationToken ct = default)
    {
        var rows = CsvHelper.Parse(csvPath).Select(r =>
            new Dictionary<string, string>(r, StringComparer.OrdinalIgnoreCase)).ToList();

        if (rows.Count == 0)
        {
            Log.Warning("{Path}: no rows to geocode", csvPath);
            return new GeocodeReport(0, 0, 0, 0, 0, []);
        }

        var headers = BuildHeaders(csvPath);
        var results = new List<GeocodeRow>();
        int skipped = 0, ok = 0, review = 0, failed = 0;

        foreach (var row in rows)
        {
            var name = row.GetValueOrDefault("name", string.Empty);
            var province = row.GetValueOrDefault("location", string.Empty);

            if (IsAlreadySettled(row))
            {
                skipped++;
                results.Add(GeocodeRow.FromCsv(row));
                continue;
            }

            Log.Information("Geocoding {Name} ({Province})...", name, province);
            var candidate = await ResolveAsync(name, province, ct);
            var verdict = GeocodeVerifier.Verify(candidate);

            ApplyToRow(row, candidate, verdict);
            results.Add(GeocodeRow.FromCsv(row));

            switch (verdict.Status)
            {
                case GeocodeStatus.Ok:
                    ok++;
                    Log.Information("  OK  {Lat}, {Lng} — {Address}",
                        candidate!.Latitude, candidate.Longitude, candidate.FormattedAddress);
                    break;
                case GeocodeStatus.Review:
                    review++;
                    Log.Warning("  REVISAR  {Notes}", verdict.Notes);
                    break;
                default:
                    failed++;
                    Log.Error("  FALLO  {Notes}", verdict.Notes);
                    break;
            }
        }

        CsvHelper.Write(csvPath, headers, rows);
        Log.Information("CSV actualizado: {Path}", csvPath);

        return new GeocodeReport(rows.Count, ok, review, failed, skipped, results);
    }

    /// <summary>Original columns first, then any geocoding column not already present.</summary>
    private static List<string> BuildHeaders(string csvPath)
    {
        var firstLine = File.ReadLines(csvPath).FirstOrDefault() ?? string.Empty;
        var existing = ParseHeaderLine(firstLine);

        var headers = new List<string>(existing);
        foreach (var col in GeocodeColumns)
            if (!headers.Contains(col, StringComparer.OrdinalIgnoreCase))
                headers.Add(col);

        return headers;
    }

    private static List<string> ParseHeaderLine(string line) =>
        [.. line.Split(',').Select(h => h.Trim().Trim('"').Trim())];

    private static bool IsAlreadySettled(Dictionary<string, string> row)
    {
        var status = row.GetValueOrDefault("geocodeStatus", string.Empty).Trim();
        var hasCoords = !string.IsNullOrWhiteSpace(row.GetValueOrDefault("latitude"))
                     && !string.IsNullOrWhiteSpace(row.GetValueOrDefault("longitude"));

        return hasCoords && SettledStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<GeocodeCandidate?> ResolveAsync(string name, string province, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var query = string.IsNullOrWhiteSpace(province)
            ? $"{name}, España"
            : $"{name}, {province}, España";

        var predictions = await places.SearchPlacesAsync(query, ct);

        // Compound names ("Palacio de la Teyeria, Mestas de Con") sometimes return
        // nothing. Retry with the trailing segment, which is usually the locality:
        // a pin on the right village beats no pin at all, and it lands as `review`.
        if (predictions.Count == 0 && name.Contains(','))
        {
            var locality = name.Split(',').Last().Trim();
            if (locality.Length > 0)
            {
                Log.Information("  sin resultados; reintentando con «{Locality}»", locality);
                predictions = await places.SearchPlacesAsync(
                    string.IsNullOrWhiteSpace(province)
                        ? $"{locality}, España"
                        : $"{locality}, {province}, España", ct);
            }
        }

        if (predictions.Count == 0)
            return null;

        var top = predictions[0];
        var details = await places.GetPlaceDetailsAsync(top.PlaceId, ct);
        if (details is null)
            return null;

        return new GeocodeCandidate(
            Name: name,
            ExpectedProvince: province,
            GoogleProvince: ExtractProvince(details),
            Latitude: details.Latitude,
            Longitude: details.Longitude,
            Types: details.Types ?? [],
            MatchingPredictions: GeocodeVerifier.CountRivalPlaces(
                name, predictions.Select(p => (p.MainText, p.SecondaryText ?? string.Empty))),
            TopPredictionMainText: top.MainText,
            PlaceId: details.PlaceId,
            FormattedAddress: details.FormattedAddress);
    }

    /// <summary>In Spain the province is administrative_area_level_2.</summary>
    private static string ExtractProvince(PlaceDetails details) =>
        details.AddressComponents?
            .FirstOrDefault(c => c.Types.Contains("administrative_area_level_2"))?
            .LongName ?? string.Empty;

    private static void ApplyToRow(
        Dictionary<string, string> row, GeocodeCandidate? candidate, GeocodeVerdict verdict)
    {
        row["geocodeStatus"] = verdict.Status switch
        {
            GeocodeStatus.Ok => "ok",
            GeocodeStatus.Review => "review",
            _ => "failed"
        };
        row["reviewNotes"] = verdict.Notes;

        if (candidate is null)
        {
            foreach (var col in (string[])["latitude", "longitude", "googleProvince",
                                           "googleTypes", "googlePlaceId", "formattedAddress"])
                row[col] = string.Empty;
            return;
        }

        row["latitude"] = candidate.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        row["longitude"] = candidate.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
        row["googleProvince"] = candidate.GoogleProvince;
        row["googleTypes"] = string.Join(" ", candidate.Types);
        row["googlePlaceId"] = candidate.PlaceId;
        row["formattedAddress"] = candidate.FormattedAddress;
    }
}

/// <summary>One row as it stands after geocoding, for the review map.</summary>
public record GeocodeRow(
    string Name,
    string ExpectedProvince,
    string GoogleProvince,
    string FormattedAddress,
    string Types,
    string Status,
    string Notes,
    double? Latitude,
    double? Longitude)
{
    public static GeocodeRow FromCsv(Dictionary<string, string> row) => new(
        row.GetValueOrDefault("name", string.Empty),
        row.GetValueOrDefault("location", string.Empty),
        row.GetValueOrDefault("googleProvince", string.Empty),
        row.GetValueOrDefault("formattedAddress", string.Empty),
        row.GetValueOrDefault("googleTypes", string.Empty),
        row.GetValueOrDefault("geocodeStatus", string.Empty),
        row.GetValueOrDefault("reviewNotes", string.Empty),
        ParseCoord(row.GetValueOrDefault("latitude")),
        ParseCoord(row.GetValueOrDefault("longitude")));

    private static double? ParseCoord(string? value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d : null;
}

public record GeocodeReport(
    int Total, int Ok, int Review, int Failed, int Skipped, IReadOnlyList<GeocodeRow> Rows)
{
    /// <summary>True when every row is resolved and nothing awaits a human.</summary>
    public bool ReadyToImport => Review == 0 && Failed == 0
        && Rows.All(r => r.Latitude is not null && r.Longitude is not null);
}
