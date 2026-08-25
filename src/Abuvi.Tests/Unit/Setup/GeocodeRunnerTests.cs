using Abuvi.API.Features.GooglePlaces;
using Abuvi.Setup;
using Abuvi.Setup.Geocoding;
using FluentAssertions;
using NSubstitute;

namespace Abuvi.Tests.Unit.Setup;

public class GeocodeRunnerTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private readonly IGooglePlacesService _places = Substitute.For<IGooglePlacesService>();

    private string CreateTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"geocode_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            if (File.Exists(f)) File.Delete(f);
            var map = Path.Combine(Path.GetDirectoryName(f)!,
                Path.GetFileNameWithoutExtension(f) + "-geocode-review.html");
            if (File.Exists(map)) File.Delete(map);
        }
    }

    private void StubGoogle(
        string placeId = "place-1",
        string mainText = "Selva de Oza",
        decimal lat = 42.7833m,
        decimal lng = -0.6833m,
        string province = "Huesca",
        string[]? types = null,
        int predictionCount = 1)
    {
        var predictions = Enumerable.Range(0, predictionCount)
            .Select(i => new PlaceAutocomplete($"{placeId}-{i}", mainText, mainText, province))
            .ToList();

        _places.SearchPlacesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(predictions);

        StubDetails(lat, lng, province, types, placeId, mainText);
    }

    private void StubDetails(
        decimal lat = 42.7833m,
        decimal lng = -0.6833m,
        string province = "Huesca",
        string[]? types = null,
        string placeId = "place-1",
        string mainText = "Selva de Oza")
    {
        _places.GetPlaceDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PlaceDetails(
                PlaceId: placeId,
                Name: mainText,
                FormattedAddress: $"{mainText}, {province}, España",
                Latitude: lat,
                Longitude: lng,
                Types: types ?? ["campground"],
                PhoneNumber: null, NationalPhoneNumber: null, Website: null,
                GoogleMapsUrl: null, Rating: null, RatingCount: null, BusinessStatus: null,
                AddressComponents:
                [
                    new GoogleAddressComponent(province, province, ["administrative_area_level_2"])
                ],
                Photos: []));
    }

    private const string OneRowCsv =
        "\"id\",\"name\",\"location\",\"pricePerAdult\",\"pricePerChild\",\"pricePerBaby\"\n" +
        "\"abc\",\"Selva de Oza\",\"Huesca\",\"0\",\"0\",\"0\"";

    [Fact]
    public async Task RunAsync_WritesCoordinatesBackToCsv()
    {
        StubGoogle();
        var path = CreateTempCsv(OneRowCsv);

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Ok.Should().Be(1);
        report.ReadyToImport.Should().BeTrue();

        var row = CsvHelper.Parse(path)[0];
        row["latitude"].Should().Be("42.7833");
        row["longitude"].Should().Be("-0.6833");
        row["geocodeStatus"].Should().Be("ok");
        row["googleProvince"].Should().Be("Huesca");
    }

    [Fact]
    public async Task RunAsync_PreservesOriginalColumns()
    {
        StubGoogle();
        var path = CreateTempCsv(OneRowCsv);

        await new GeocodeRunner(_places).RunAsync(path);

        var row = CsvHelper.Parse(path)[0];
        row["id"].Should().Be("abc");
        row["name"].Should().Be("Selva de Oza");
        row["pricePerAdult"].Should().Be("0");
    }

    [Fact]
    public async Task RunAsync_FlagsProvinceMismatchForReview()
    {
        StubGoogle(province: "Teruel");
        var path = CreateTempCsv(OneRowCsv);

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Review.Should().Be(1);
        report.ReadyToImport.Should().BeFalse();
        CsvHelper.Parse(path)[0]["reviewNotes"].Should().Contain("provincia");
    }

    [Fact]
    public async Task RunAsync_SamePlaceListedTwice_IsNotFlaggedAsAmbiguous()
    {
        // Google devuelve el mismo pueblo dos veces, con distinta granularidad
        _places.SearchPlacesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([
                new PlaceAutocomplete("p1", "Selva de Oza", "Selva de Oza", "Huesca, España"),
                new PlaceAutocomplete("p2", "Selva de Oza", "Selva de Oza", "Hecho, Huesca, España")
            ]);
        StubDetails();
        var path = CreateTempCsv(OneRowCsv);

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Ok.Should().Be(1);
        report.Review.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_SameNameInDifferentProvinces_IsFlaggedAsAmbiguous()
    {
        _places.SearchPlacesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([
                new PlaceAutocomplete("p1", "Selva de Oza", "Selva de Oza", "Huesca, España"),
                new PlaceAutocomplete("p2", "Selva de Oza", "Selva de Oza", "Teruel, España")
            ]);
        StubDetails();
        var path = CreateTempCsv(OneRowCsv);

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Review.Should().Be(1);
        CsvHelper.Parse(path)[0]["reviewNotes"].Should().Contain("ambig");
    }

    [Fact]
    public async Task RunAsync_WhenFullNameFindsNothing_RetriesWithTrailingLocality()
    {
        // "Palacio de la Teyeria, Mestas de Con" no devuelve nada; "Mestas de Con" si
        _places.SearchPlacesAsync(
            Arg.Is<string>(q => q.StartsWith("Palacio")), Arg.Any<CancellationToken>())
            .Returns([]);
        _places.SearchPlacesAsync(
            Arg.Is<string>(q => q.StartsWith("Mestas de Con")), Arg.Any<CancellationToken>())
            .Returns([new PlaceAutocomplete("p1", "Mestas de Con", "Mestas de Con", "Asturias, España")]);
        StubDetails(lat: 43.31m, lng: -5.09m, province: "Asturias");

        var path = CreateTempCsv(
            "\"name\",\"location\"\n" +
            "\"Palacio de la Teyeria, Mestas de Con\",\"Asturias\"");

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Failed.Should().Be(0);
        CsvHelper.Parse(path)[0]["latitude"].Should().Be("43.31");
    }

    [Fact]
    public async Task RunAsync_WhenGoogleReturnsNothing_MarksFailed()
    {
        _places.SearchPlacesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var path = CreateTempCsv(OneRowCsv);

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Failed.Should().Be(1);
        report.ReadyToImport.Should().BeFalse();
        CsvHelper.Parse(path)[0]["geocodeStatus"].Should().Be("failed");
    }

    [Fact]
    public async Task RunAsync_IsIdempotent_SkipsRowsAlreadySettled()
    {
        StubGoogle();
        var path = CreateTempCsv(
            "\"name\",\"location\",\"latitude\",\"longitude\",\"geocodeStatus\"\n" +
            "\"Selva de Oza\",\"Huesca\",\"1.5\",\"2.5\",\"ok_manual\"");

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Skipped.Should().Be(1);
        await _places.DidNotReceive().SearchPlacesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        // La correccion manual sobrevive intacta
        var row = CsvHelper.Parse(path)[0];
        row["latitude"].Should().Be("1.5");
        row["geocodeStatus"].Should().Be("ok_manual");
    }

    [Fact]
    public async Task RunAsync_ReGeocodesRowsPreviouslyFlaggedForReview()
    {
        StubGoogle();
        var path = CreateTempCsv(
            "\"name\",\"location\",\"latitude\",\"longitude\",\"geocodeStatus\"\n" +
            "\"Selva de Oza\",\"Huesca\",\"1.5\",\"2.5\",\"review\"");

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Skipped.Should().Be(0);
        CsvHelper.Parse(path)[0]["latitude"].Should().Be("42.7833");
    }

    [Fact]
    public async Task RunAsync_HandlesNameContainingComma()
    {
        StubGoogle(mainText: "Palacio de las Teyerias", province: "Asturias",
                   lat: 43.31m, lng: -5.09m);
        var path = CreateTempCsv(
            "\"name\",\"location\"\n" +
            "\"Palacio de las Teyerias, Mestas de Con\",\"Asturias\"");

        var report = await new GeocodeRunner(_places).RunAsync(path);

        report.Total.Should().Be(1);
        CsvHelper.Parse(path)[0]["name"].Should().Be("Palacio de las Teyerias, Mestas de Con");
    }

    [Fact]
    public void ReviewMapWriter_ProducesSelfContainedHtmlWithEveryRow()
    {
        var report = new GeocodeReport(2, 1, 1, 0, 0,
        [
            new("Selva de Oza", "Huesca", "Huesca", "Selva de Oza, Huesca",
                "campground", "ok", "", 42.7833, -0.6833),
            new("El Bosque", "Cádiz", "Sevilla", "El Bosque, Sevilla",
                "locality", "review", "provincia distinta", 37.1, -5.5)
        ]);
        var path = Path.Combine(Path.GetTempPath(), $"map_{Guid.NewGuid():N}.html");
        _tempFiles.Add(path);

        ReviewMapWriter.Write(path, report);

        var html = File.ReadAllText(path);
        html.Should().Contain("Selva de Oza").And.Contain("El Bosque");
        html.Should().Contain("provincia distinta");
        html.Should().Contain("42.7833");
        html.Should().NotContain("__DATA__");
    }
}
