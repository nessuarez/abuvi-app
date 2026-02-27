using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.Setup.Importers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Setup.Importers;

public class CampEditionImporterTests : IDisposable
{
    private readonly AbuviDbContext _db;
    private readonly List<string> _tempFiles = [];
    private Guid _campId;

    public CampEditionImporterTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"CampEditionImporterTest_{Guid.NewGuid()}")
            .Options;
        _db = new AbuviDbContext(options);
        _db.Database.EnsureCreated();
        SeedCamp();
    }

    private void SeedCamp()
    {
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Camp Sierra",
            PricePerAdult = 150,
            PricePerChild = 100,
            PricePerBaby = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Camps.Add(camp);
        _db.SaveChanges();
        _campId = camp.Id;
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
    public async Task ImportAsync_WithValidCsv_CreatesEditions()
    {
        var path = CreateTempCsv(
            "campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes\n" +
            "Camp Sierra,2027,2027-07-01,2027-07-15,150.00,100.00,0.00,100,Draft,Test edition");

        var importer = new CampEditionImporter(_db);
        var result = await importer.ImportAsync(path);

        result.TotalRows.Should().Be(1);
        result.Imported.Should().Be(1);
        _db.CampEditions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ImportAsync_WhenCampNameNotFound_SkipsRowAndReports()
    {
        var path = CreateTempCsv(
            "campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes\n" +
            "Unknown Camp,2027,2027-07-01,2027-07-15,150.00,100.00,0.00,,Open,");

        var importer = new CampEditionImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("Unknown Camp");
    }

    [Fact]
    public async Task ImportAsync_SetsStatusDirectly_NoWorkflowValidation()
    {
        var path = CreateTempCsv(
            "campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes\n" +
            "Camp Sierra,2027,2027-07-01,2027-07-15,150.00,100.00,0.00,,Open,");

        var importer = new CampEditionImporter(_db);
        await importer.ImportAsync(path);

        var edition = await _db.CampEditions.FirstAsync();
        edition.Status.Should().Be(CampEditionStatus.Open);
    }

    [Fact]
    public async Task ImportAsync_WithDuplicateCampYear_SkipsRowAndReports()
    {
        _db.CampEditions.Add(new CampEdition
        {
            Id = Guid.NewGuid(),
            CampId = _campId,
            Year = 2027,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(14),
            PricePerAdult = 150,
            PricePerChild = 100,
            PricePerBaby = 0,
            Status = CampEditionStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var path = CreateTempCsv(
            "campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes\n" +
            "Camp Sierra,2027,2027-08-01,2027-08-15,160.00,110.00,0.00,,Open,");

        var importer = new CampEditionImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task ImportAsync_OptionalMaxCapacity_CanBeNull()
    {
        var path = CreateTempCsv(
            "campName,year,startDate,endDate,pricePerAdult,pricePerChild,pricePerBaby,maxCapacity,status,notes\n" +
            "Camp Sierra,2028,2028-07-01,2028-07-15,150.00,100.00,0.00,,Draft,No capacity limit");

        var importer = new CampEditionImporter(_db);
        await importer.ImportAsync(path);

        var edition = await _db.CampEditions.FirstAsync(e => e.Year == 2028);
        edition.MaxCapacity.Should().BeNull();
    }
}
