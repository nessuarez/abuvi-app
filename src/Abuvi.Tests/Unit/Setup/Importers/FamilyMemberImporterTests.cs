using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using Abuvi.Setup.Importers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Setup.Importers;

public class FamilyMemberImporterTests : IDisposable
{
    private readonly AbuviDbContext _db;
    private readonly List<string> _tempFiles = [];
    private Guid _familyUnitId;

    public FamilyMemberImporterTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"FamilyMemberImporterTest_{Guid.NewGuid()}")
            .Options;
        _db = new AbuviDbContext(options);
        _db.Database.EnsureCreated();
        SeedFamilyUnit();
    }

    private void SeedFamilyUnit()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "rep@test.com",
            PasswordHash = "hashed",
            FirstName = "Rep",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);

        var unit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Garcia Family",
            RepresentativeUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.FamilyUnits.Add(unit);
        _db.SaveChanges();
        _familyUnitId = unit.Id;
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
    public async Task ImportAsync_WithValidCsv_CreatesMembers()
    {
        var path = CreateTempCsv(
            "familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone\n" +
            "Garcia Family,Carlos,Garcia,1982-03-15,Parent,87654321B,carlos@test.com,+34698765432\n" +
            "Garcia Family,Laura,Garcia,1985-07-22,Spouse,,,\n" +
            "Garcia Family,Pablo,Garcia,2010-11-05,Child,,,");

        var importer = new FamilyMemberImporter(_db);
        var result = await importer.ImportAsync(path);

        result.TotalRows.Should().Be(3);
        result.Imported.Should().Be(3);
        _db.FamilyMembers.Should().HaveCount(3);
    }

    [Fact]
    public async Task ImportAsync_WhenFamilyUnitNameNotFound_SkipsRowAndReports()
    {
        var path = CreateTempCsv(
            "familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone\n" +
            "Unknown Family,Test,Member,1990-01-01,Parent,,,");

        var importer = new FamilyMemberImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("Unknown Family");
    }

    [Fact]
    public async Task ImportAsync_ParsesDateOfBirthCorrectly()
    {
        var path = CreateTempCsv(
            "familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone\n" +
            "Garcia Family,Test,Member,1990-06-15,Parent,,,");

        var importer = new FamilyMemberImporter(_db);
        await importer.ImportAsync(path);

        var member = await _db.FamilyMembers.FirstAsync();
        member.DateOfBirth.Should().Be(new DateOnly(1990, 6, 15));
    }

    [Fact]
    public async Task ImportAsync_ParsesRelationshipEnum()
    {
        var path = CreateTempCsv(
            "familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone\n" +
            "Garcia Family,Test,Spouse,1988-01-01,Spouse,,,\n" +
            "Garcia Family,Test,Child,2015-01-01,child,,,");

        var importer = new FamilyMemberImporter(_db);
        await importer.ImportAsync(path);

        var members = await _db.FamilyMembers.ToListAsync();
        members.Should().Contain(m => m.Relationship == FamilyRelationship.Spouse);
        members.Should().Contain(m => m.Relationship == FamilyRelationship.Child);
    }

    [Fact]
    public async Task ImportAsync_OptionalFieldsCanBeEmpty()
    {
        var path = CreateTempCsv(
            "familyUnitName,firstName,lastName,dateOfBirth,relationship,documentNumber,email,phone\n" +
            "Garcia Family,Test,Optional,2010-01-01,Child,,,");

        var importer = new FamilyMemberImporter(_db);
        await importer.ImportAsync(path);

        var member = await _db.FamilyMembers.FirstAsync();
        member.DocumentNumber.Should().BeNull();
        member.Email.Should().BeNull();
        member.Phone.Should().BeNull();
    }
}
