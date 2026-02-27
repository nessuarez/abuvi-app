using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using Abuvi.Setup.Importers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Setup.Importers;

public class FamilyUnitImporterTests : IDisposable
{
    private readonly AbuviDbContext _db;
    private readonly List<string> _tempFiles = [];

    public FamilyUnitImporterTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"FamilyUnitImporterTest_{Guid.NewGuid()}")
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

    private User SeedUser(string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    public void Dispose()
    {
        _db.Dispose();
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    [Fact]
    public async Task ImportAsync_WithValidCsv_CreatesFamilyUnits()
    {
        SeedUser("member1@test.com");
        SeedUser("member2@test.com");

        var path = CreateTempCsv(
            "name,representativeEmail\n" +
            "Garcia Family,member1@test.com\n" +
            "Lopez Family,member2@test.com");

        var importer = new FamilyUnitImporter(_db);
        var result = await importer.ImportAsync(path);

        result.TotalRows.Should().Be(2);
        result.Imported.Should().Be(2);
        _db.FamilyUnits.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportAsync_WhenRepresentativeEmailNotFound_SkipsRowAndReports()
    {
        var path = CreateTempCsv(
            "name,representativeEmail\n" +
            "Unknown Family,nobody@test.com");

        var importer = new FamilyUnitImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("nobody@test.com");
    }

    [Fact]
    public async Task ImportAsync_LinksUserFamilyUnitIdBack()
    {
        var user = SeedUser("linked@test.com");

        var path = CreateTempCsv(
            "name,representativeEmail\n" +
            "Linked Family,linked@test.com");

        var importer = new FamilyUnitImporter(_db);
        await importer.ImportAsync(path);

        var updatedUser = await _db.Users.FirstAsync(u => u.Email == "linked@test.com");
        updatedUser.FamilyUnitId.Should().NotBeNull();

        var unit = await _db.FamilyUnits.FirstAsync();
        updatedUser.FamilyUnitId.Should().Be(unit.Id);
    }

    [Fact]
    public async Task ImportAsync_WithDuplicateName_SkipsRowAndReports()
    {
        var user = SeedUser("dup@test.com");
        _db.FamilyUnits.Add(new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Existing Family",
            RepresentativeUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var user2 = SeedUser("dup2@test.com");
        var path = CreateTempCsv(
            "name,representativeEmail\n" +
            "Existing Family,dup2@test.com");

        var importer = new FamilyUnitImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("Duplicate");
    }
}
