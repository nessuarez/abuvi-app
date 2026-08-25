using System.Globalization;
using System.Text;

namespace Abuvi.Setup.Geocoding;

public enum GeocodeStatus
{
    /// <summary>Every automatic check passed.</summary>
    Ok,

    /// <summary>Resolved, but at least one check needs a human to look at it.</summary>
    Review,

    /// <summary>Google returned nothing usable.</summary>
    Failed
}

/// <summary>
/// What Google returned for one camp, alongside what we expected.
/// </summary>
/// <param name="Name">Camp name as written in the CSV.</param>
/// <param name="ExpectedProvince">Province from the CSV's <c>location</c> column.</param>
/// <param name="GoogleProvince">administrative_area_level_2 returned by Google.</param>
/// <param name="MatchingPredictions">
/// How many autocomplete predictions plausibly matched the name. More than one means ambiguity.
/// </param>
public record GeocodeCandidate(
    string Name,
    string ExpectedProvince,
    string GoogleProvince,
    decimal Latitude,
    decimal Longitude,
    string[] Types,
    int MatchingPredictions,
    string TopPredictionMainText,
    string PlaceId = "",
    string FormattedAddress = "");

public record GeocodeVerdict(GeocodeStatus Status, string Notes);

/// <summary>
/// Automatic checks run over a geocoding result before it is allowed into the database.
/// Pure logic on purpose: every rule here is unit-tested without touching Google.
/// </summary>
public static class GeocodeVerifier
{
    // Peninsular Spain. Google already restricts to country:es, so this exists to
    // catch the Canary and Balearic islands, not foreign results.
    private const decimal MinLatitude = 35.9m;
    private const decimal MaxLatitude = 43.9m;
    private const decimal MinLongitude = -9.4m;
    private const decimal MaxLongitude = 3.4m;

    // The Balearics overlap the peninsular box in longitude (Ibiza sits west of
    // Cap de Creus), so a single box cannot separate them. Carve them out explicitly.
    // Nothing peninsular falls inside: the closest point, the Ebro delta, is north
    // of this latitude range and west of its longitude range.
    private const decimal BalearicMinLatitude = 38.6m;
    private const decimal BalearicMaxLatitude = 40.1m;
    private const decimal BalearicMinLongitude = 1.15m;
    private const decimal BalearicMaxLongitude = 4.35m;

    private static readonly string[] PlausibleTypes =
    [
        "campground", "park", "natural_feature", "locality", "sublocality",
        "tourist_attraction", "lodging", "rv_park", "point_of_interest",
        "premise", "route", "political"
    ];

    public static GeocodeVerdict Verify(GeocodeCandidate? candidate)
    {
        if (candidate is null)
            return new(GeocodeStatus.Failed, "Google no devolvió ningún resultado");

        var notes = new List<string>();

        CheckProvince(candidate, notes);
        CheckIberianBox(candidate, notes);
        CheckPlaceType(candidate, notes);
        CheckAmbiguity(candidate, notes);

        return notes.Count == 0
            ? new(GeocodeStatus.Ok, string.Empty)
            : new(GeocodeStatus.Review, string.Join("; ", notes));
    }

    /// <summary>
    /// How many autocomplete predictions name the very same place we asked for.
    ///
    /// Google's autocomplete always returns about five suggestions, but most are
    /// streets, bars or campsites that merely contain the name ("Bar Boñar",
    /// "Camping Selva de Oza"). Only an exact name match represents a rival place,
    /// so counting raw predictions would flag every single row as ambiguous.
    /// </summary>
    public static int CountExactNameMatches(string name, IEnumerable<string> predictionMainTexts)
    {
        var target = Normalize(name);
        if (target.Length == 0)
            return 0;

        return predictionMainTexts.Count(t => Normalize(t) == target);
    }

    /// <summary>
    /// How many genuinely different places share the name we asked for.
    ///
    /// Two exact name matches are only rivals when they sit in different provinces.
    /// Google routinely returns the same town twice at different granularity
    /// ("Cervera de Pisuerga | Palencia" and "Cervera de Pisuerga | Pl. Modesto
    /// Lafuente, Cervera de Pisuerga, Palencia"), which is not ambiguity at all.
    /// </summary>
    public static int CountRivalPlaces(
        string name, IEnumerable<(string MainText, string SecondaryText)> predictions)
    {
        var target = Normalize(name);
        if (target.Length == 0)
            return 0;

        return predictions
            .Where(p => Normalize(p.MainText) == target)
            .Select(p => ProvinceOf(p.SecondaryText))
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    /// <summary>
    /// Last meaningful component of a Google secondary text, which in Spain is the
    /// province: "Pl. Mayor, Cervera de Pisuerga, Palencia, España" -> "palencia".
    /// </summary>
    private static string ProvinceOf(string secondaryText)
    {
        var parts = secondaryText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(p => p.Length > 0 && p != "espana")
            .ToList();

        return parts.Count > 0 ? parts[^1] : string.Empty;
    }

    private static void CheckProvince(GeocodeCandidate c, List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(c.GoogleProvince))
        {
            notes.Add($"provincia no devuelta por Google (se esperaba «{c.ExpectedProvince}»)");
            return;
        }

        var expected = Normalize(c.ExpectedProvince);
        var actual = StripProvincePrefix(Normalize(c.GoogleProvince));

        if (expected != actual)
            notes.Add($"provincia distinta: se esperaba «{c.ExpectedProvince}», Google dice «{c.GoogleProvince}»");
    }

    private static void CheckIberianBox(GeocodeCandidate c, List<string> notes)
    {
        var inBox = c.Latitude >= MinLatitude && c.Latitude <= MaxLatitude
                 && c.Longitude >= MinLongitude && c.Longitude <= MaxLongitude;

        var inBalearics = c.Latitude >= BalearicMinLatitude && c.Latitude <= BalearicMaxLatitude
                       && c.Longitude >= BalearicMinLongitude && c.Longitude <= BalearicMaxLongitude;

        if (!inBox || inBalearics)
            notes.Add($"coordenadas fuera de la península ({c.Latitude}, {c.Longitude})");
    }

    private static void CheckPlaceType(GeocodeCandidate c, List<string> notes)
    {
        if (c.Types.Length == 0)
            return;

        if (!c.Types.Any(t => PlausibleTypes.Contains(t, StringComparer.OrdinalIgnoreCase)))
            notes.Add($"tipo de lugar poco plausible para un campamento: {string.Join(", ", c.Types)}");
    }

    private static void CheckAmbiguity(GeocodeCandidate c, List<string> notes)
    {
        if (c.MatchingPredictions > 1)
            notes.Add($"ambiguo: {c.MatchingPredictions} candidatos parecidos en Google");

        if (!ResemblesName(c.Name, c.TopPredictionMainText))
            notes.Add($"el nombre devuelto («{c.TopPredictionMainText}») no se parece al buscado");
    }

    /// <summary>
    /// Loose containment either way, so "Covaleda" matches "Covaleda, Soria"
    /// but not "Bar Pepe".
    /// </summary>
    private static bool ResemblesName(string name, string prediction)
    {
        var a = Normalize(name);
        var b = Normalize(prediction);

        if (a.Length == 0 || b.Length == 0)
            return false;

        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
            return true;

        // Fall back to significant word overlap: names like
        // "Palacio de las Teyerias, Mestas de Con" rarely match end to end.
        var wordsA = SignificantWords(a);
        var wordsB = SignificantWords(b);

        return wordsA.Count > 0 && wordsA.Overlaps(wordsB);
    }

    private static HashSet<string> SignificantWords(string normalized) =>
        normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToHashSet(StringComparer.Ordinal);

    private static string StripProvincePrefix(string normalized)
    {
        foreach (var prefix in (string[])["provincia de ", "province of "])
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
                return normalized[prefix.Length..];

        return normalized;
    }

    /// <summary>Lowercase, accent-free, punctuation-free, single-spaced.</summary>
    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
