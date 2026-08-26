using Abuvi.Setup.Geocoding;
using FluentAssertions;

namespace Abuvi.Tests.Unit.Setup;

public class GeocodeVerifierTests
{
    private static GeocodeCandidate Candidate(
        string name = "Selva de Oza",
        string expectedProvince = "Huesca",
        string googleProvince = "Huesca",
        decimal lat = 42.7833m,
        decimal lng = -0.6833m,
        string[]? types = null,
        int matchingPredictions = 1,
        string topPredictionMainText = "Selva de Oza") =>
        new(name, expectedProvince, googleProvince, lat, lng,
            types ?? ["campground"], matchingPredictions, topPredictionMainText);

    // --- Contraste de provincia ---

    [Fact]
    public void Verify_WhenProvinceMatches_IsOk()
    {
        GeocodeVerifier.Verify(Candidate()).Status.Should().Be(GeocodeStatus.Ok);
    }

    [Fact]
    public void Verify_WhenProvinceDiffers_NeedsReview()
    {
        var r = GeocodeVerifier.Verify(Candidate(expectedProvince: "Huesca", googleProvince: "Teruel"));

        r.Status.Should().Be(GeocodeStatus.Review);
        r.Notes.Should().Contain("provincia");
    }

    [Fact]
    public void Verify_ProvinceComparisonIgnoresAccentsAndCase()
    {
        GeocodeVerifier.Verify(Candidate(expectedProvince: "León", googleProvince: "LEON"))
            .Status.Should().Be(GeocodeStatus.Ok);
    }

    [Fact]
    public void Verify_ProvinceComparisonHandlesGooglePrefix()
    {
        // Google devuelve a veces "Provincia de Huesca"
        GeocodeVerifier.Verify(Candidate(expectedProvince: "Huesca", googleProvince: "Provincia de Huesca"))
            .Status.Should().Be(GeocodeStatus.Ok);
    }

    [Fact]
    public void Verify_WhenGoogleProvinceUnknown_NeedsReview()
    {
        GeocodeVerifier.Verify(Candidate(googleProvince: ""))
            .Status.Should().Be(GeocodeStatus.Review);
    }

    // --- Caja peninsular ---

    [Theory]
    [InlineData(28.29, -16.62)]   // Tenerife
    [InlineData(39.57, 2.65)]     // Mallorca
    [InlineData(48.85, 2.35)]     // Paris
    public void Verify_WhenOutsideIberianBox_NeedsReview(double lat, double lng)
    {
        var r = GeocodeVerifier.Verify(Candidate(lat: (decimal)lat, lng: (decimal)lng));

        r.Status.Should().Be(GeocodeStatus.Review);
        r.Notes.Should().Contain("península");
    }

    [Fact]
    public void Verify_WhenInsideIberianBox_IsOk()
    {
        GeocodeVerifier.Verify(Candidate(lat: 40.4168m, lng: -3.7038m))
            .Status.Should().Be(GeocodeStatus.Ok);
    }

    // --- Tipo de lugar ---

    [Theory]
    [InlineData("campground")]
    [InlineData("natural_feature")]
    [InlineData("locality")]
    [InlineData("lodging")]
    public void Verify_WithPlausiblePlaceType_IsOk(string type)
    {
        GeocodeVerifier.Verify(Candidate(types: [type])).Status.Should().Be(GeocodeStatus.Ok);
    }

    [Theory]
    [InlineData("restaurant")]
    [InlineData("store")]
    [InlineData("bar")]
    public void Verify_WithImplausiblePlaceType_NeedsReview(string type)
    {
        var r = GeocodeVerifier.Verify(Candidate(types: [type]));

        r.Status.Should().Be(GeocodeStatus.Review);
        r.Notes.Should().Contain("tipo");
    }

    // --- Ambigüedad ---

    [Fact]
    public void Verify_WithSeveralMatchingPredictions_NeedsReview()
    {
        var r = GeocodeVerifier.Verify(Candidate(matchingPredictions: 3));

        r.Status.Should().Be(GeocodeStatus.Review);
        r.Notes.Should().Contain("ambig");
    }

    [Fact]
    public void Verify_WhenTopPredictionDoesNotResembleName_NeedsReview()
    {
        var r = GeocodeVerifier.Verify(
            Candidate(name: "Selva de Oza", topPredictionMainText: "Bar Pepe"));

        r.Status.Should().Be(GeocodeStatus.Review);
        r.Notes.Should().Contain("nombre");
    }

    [Fact]
    public void Verify_WhenTopPredictionResemblesNameLoosely_IsOk()
    {
        GeocodeVerifier.Verify(
            Candidate(name: "Covaleda", topPredictionMainText: "Covaleda, Soria"))
            .Status.Should().Be(GeocodeStatus.Ok);
    }

    // --- Acumulación ---

    [Fact]
    public void Verify_AccumulatesEveryFailureInNotes()
    {
        var r = GeocodeVerifier.Verify(Candidate(
            expectedProvince: "Huesca",
            googleProvince: "Teruel",
            types: ["restaurant"],
            matchingPredictions: 4));

        r.Status.Should().Be(GeocodeStatus.Review);
        r.Notes.Should().Contain("provincia").And.Contain("tipo").And.Contain("ambig");
    }

    [Fact]
    public void Verify_WhenNotResolved_IsFailed()
    {
        GeocodeVerifier.Verify(null).Status.Should().Be(GeocodeStatus.Failed);
    }

    // --- Conteo de coincidencias exactas (base de la deteccion de ambiguedad) ---

    [Fact]
    public void CountExactNameMatches_IgnoresStreetsAndBusinessesContainingTheName()
    {
        // Predicciones reales de Google para "Bonar, Leon, Espana"
        string[] mains =
        [
            "Boñar", "Bar Boñar", "Carretera Boñar-Paseo", "Calle Boñar", "Casa rural Boñar"
        ];

        GeocodeVerifier.CountExactNameMatches("Boñar", mains).Should().Be(1);
    }

    [Fact]
    public void CountExactNameMatches_IgnoresCampingsAndParkings()
    {
        // Predicciones reales para "Selva de Oza, Huesca, Espana"
        string[] mains =
        [
            "Selva de Oza", "Parque de Tirolinas Bosque de Oza",
            "Camping Selva de Oza", "Parking Selva de Oza"
        ];

        GeocodeVerifier.CountExactNameMatches("Selva de Oza", mains).Should().Be(1);
    }

    [Fact]
    public void CountExactNameMatches_CountsGenuineHomonyms()
    {
        // Dos poblaciones distintas con el mismo nombre
        string[] mains = ["El Bosque", "El Bosque", "Calle el Bosque"];

        GeocodeVerifier.CountExactNameMatches("El Bosque", mains).Should().Be(2);
    }

    [Fact]
    public void CountExactNameMatches_IgnoresAccentsAndCase()
    {
        GeocodeVerifier.CountExactNameMatches("Villamanin", ["Villamanín", "VILLAMANIN"])
            .Should().Be(2);
    }

    [Fact]
    public void CountExactNameMatches_WithNoMatch_ReturnsZero()
    {
        GeocodeVerifier.CountExactNameMatches("Selva de Oza", ["Bar Pepe", "Calle Mayor"])
            .Should().Be(0);
    }

    // --- Lugares rivales (ambiguedad real) ---

    [Fact]
    public void CountRivalPlaces_SameTownAtDifferentGranularity_IsNotAmbiguous()
    {
        // Caso real: Google devuelve el municipio y un punto dentro de el.
        (string, string)[] preds =
        [
            ("Cervera de Pisuerga", "Palencia, España"),
            ("Cervera de Pisuerga", "Pl. Modesto Lafuente, Cervera de Pisuerga, Palencia, España"),
            ("Notaría Cervera de Pisuerga", "Avenida de Aguilar, Cervera de Pisuerga, Palencia, España")
        ];

        GeocodeVerifier.CountRivalPlaces("Cervera de Pisuerga", preds).Should().Be(1);
    }

    [Fact]
    public void CountRivalPlaces_SameNameInDifferentProvinces_IsAmbiguous()
    {
        (string, string)[] preds =
        [
            ("El Bosque", "Cádiz, España"),
            ("El Bosque", "Madrid, España")
        ];

        GeocodeVerifier.CountRivalPlaces("El Bosque", preds).Should().Be(2);
    }

    [Fact]
    public void CountRivalPlaces_IgnoresStreetsAndBusinesses()
    {
        (string, string)[] preds =
        [
            ("Boñar", "León, España"),
            ("Bar Boñar", "Avenida de Mariano Andrés, León, España"),
            ("Calle Boñar", "Villarente, León, España")
        ];

        GeocodeVerifier.CountRivalPlaces("Boñar", preds).Should().Be(1);
    }

    [Fact]
    public void CountRivalPlaces_WithNoExactMatch_ReturnsZero()
    {
        (string, string)[] preds = [("Bar Pepe", "Madrid, España")];

        GeocodeVerifier.CountRivalPlaces("Selva de Oza", preds).Should().Be(0);
    }
}
