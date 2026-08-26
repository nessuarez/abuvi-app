using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.Setup.Importers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Setup.Importers;

public class CampImporterTests : IDisposable
{
    private readonly AbuviDbContext _db;
    private readonly List<string> _tempFiles = [];

    public CampImporterTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"CampImporterTest_{Guid.NewGuid()}")
            .Options;
        _db = new AbuviDbContext(options);
        _db.Database.EnsureCreated();
    }

    private string CreateTempCsv(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        _db.Dispose();
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public async Task ImportAsync_WithValidCsv_CreatesCamps()
    {
        var path = CreateTempCsv(
            "name,description,location,pricePerAdult,pricePerChild,pricePerBaby\n" +
            "Camp Sierra,Mountain camp,Sierra de Guadarrama,150.00,100.00,0.00\n" +
            "Camp Costa,Coastal camp,Costa Brava,180.00,120.00,0.00");

        var importer = new CampImporter(_db);
        var result = await importer.ImportAsync(path);

        result.TotalRows.Should().Be(2);
        result.Imported.Should().Be(2);
        _db.Camps.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportAsync_WithDuplicateName_SkipsRowAndReports()
    {
        _db.Camps.Add(new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Camp Sierra",
            PricePerAdult = 150,
            PricePerChild = 100,
            PricePerBaby = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var path = CreateTempCsv(
            "name,description,location,pricePerAdult,pricePerChild,pricePerBaby\n" +
            "Camp Sierra,Duplicate,Location,150.00,100.00,0.00");

        var importer = new CampImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task ImportAsync_SetsIsActiveTrue()
    {
        var path = CreateTempCsv(
            "name,description,location,pricePerAdult,pricePerChild,pricePerBaby\n" +
            "Active Camp,,Location,100.00,50.00,0.00");

        var importer = new CampImporter(_db);
        await importer.ImportAsync(path);

        var camp = await _db.Camps.FirstAsync();
        camp.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_ParsesDecimalPrices()
    {
        var path = CreateTempCsv(
            "name,description,location,pricePerAdult,pricePerChild,pricePerBaby\n" +
            "Price Camp,,Location,199.99,89.50,0.00");

        var importer = new CampImporter(_db);
        await importer.ImportAsync(path);

        var camp = await _db.Camps.FirstAsync();
        camp.PricePerAdult.Should().Be(199.99m);
        camp.PricePerChild.Should().Be(89.50m);
        camp.PricePerBaby.Should().Be(0.00m);
    }

    // --- Geolocalizacion (columnas opcionales) ---

    [Fact]
    public async Task ImportAsync_WithLatitudeAndLongitude_PersistsCoordinates()
    {
        var path = CreateTempCsv(
            "name,location,pricePerAdult,pricePerChild,pricePerBaby,latitude,longitude\n" +
            "\"Selva de Oza\",\"Huesca\",0,0,0,42.7833,-0.6833");

        var result = await new CampImporter(_db).ImportAsync(path);

        result.Imported.Should().Be(1);
        var camp = await _db.Camps.SingleAsync();
        camp.Latitude.Should().Be(42.7833m);
        camp.Longitude.Should().Be(-0.6833m);
    }

    [Fact]
    public async Task ImportAsync_WithoutCoordinateColumns_LeavesThemNull()
    {
        var path = CreateTempCsv(
            "name,location,pricePerAdult,pricePerChild,pricePerBaby\n" +
            "Oto,Huesca,0,0,0");

        var result = await new CampImporter(_db).ImportAsync(path);

        result.Imported.Should().Be(1);
        var camp = await _db.Camps.SingleAsync();
        camp.Latitude.Should().BeNull();
        camp.Longitude.Should().BeNull();
    }

    [Fact]
    public async Task ImportAsync_WithGooglePlaceMetadata_PersistsIt()
    {
        var path = CreateTempCsv(
            "name,location,pricePerAdult,pricePerChild,pricePerBaby,latitude,longitude,googlePlaceId,formattedAddress\n" +
            "\"Covaleda\",\"Soria\",0,0,0,41.9167,-2.9167,\"ChIJabc123\",\"Covaleda, Soria, Espana\"");

        await new CampImporter(_db).ImportAsync(path);

        var camp = await _db.Camps.SingleAsync();
        camp.GooglePlaceId.Should().Be("ChIJabc123");
        camp.FormattedAddress.Should().Be("Covaleda, Soria, Espana");
    }

    [Fact]
    public async Task ImportAsync_WithCommaInsideQuotedName_KeepsNameIntact()
    {
        // Caso real de docs/CAMPAMENTOS_HISTORICOS.csv
        var path = CreateTempCsv(
            "\"name\",\"location\",\"pricePerAdult\",\"pricePerChild\",\"pricePerBaby\"\n" +
            "\"Palacio de las Teyerias, Mestas de Con\",\"Asturias\",\"0\",\"0\",\"0\"");

        var result = await new CampImporter(_db).ImportAsync(path);

        result.Imported.Should().Be(1);
        var camp = await _db.Camps.SingleAsync();
        camp.Name.Should().Be("Palacio de las Teyerias, Mestas de Con");
        camp.Location.Should().Be("Asturias");
    }
}
