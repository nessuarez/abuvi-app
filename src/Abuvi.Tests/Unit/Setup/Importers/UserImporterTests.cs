using Abuvi.API.Data;
using Abuvi.API.Features.Users;
using Abuvi.Setup.Importers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Setup.Importers;

public class UserImporterTests : IDisposable
{
    private readonly AbuviDbContext _db;
    private readonly List<string> _tempFiles = [];

    public UserImporterTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"UserImporterTest_{Guid.NewGuid()}")
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
    public async Task ImportAsync_WithValidCsv_CreatesAllUsers()
    {
        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            "board@test.com,Pass@123,Ana,Lopez,+34612345678,Board,12345678A\n" +
            "member@test.com,Pass@123,Carlos,Garcia,,Member,\n" +
            "admin2@test.com,Pass@123,Laura,Martin,+34698765432,Admin,87654321B");

        var importer = new UserImporter(_db);
        var result = await importer.ImportAsync(path);

        result.TotalRows.Should().Be(3);
        result.Imported.Should().Be(3);
        result.Skipped.Should().Be(0);
        _db.Users.Should().HaveCount(3);
    }

    [Fact]
    public async Task ImportAsync_WithDuplicateEmail_SkipsRowAndReports()
    {
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            PasswordHash = "hashed",
            FirstName = "Existing",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            "existing@test.com,Pass@123,Duplicate,User,,Member,");

        var importer = new UserImporter(_db);
        var result = await importer.ImportAsync(path);

        result.TotalRows.Should().Be(1);
        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
        result.Rows[0].Error.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task ImportAsync_WithMissingRequiredField_SkipsRowAndReports()
    {
        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            ",Pass@123,NoEmail,User,,Member,");

        var importer = new UserImporter(_db);
        var result = await importer.ImportAsync(path);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_HashesPasswordWithBCrypt()
    {
        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            "hash@test.com,PlainText123,Hash,Test,,Member,");

        var importer = new UserImporter(_db);
        await importer.ImportAsync(path);

        var user = await _db.Users.FirstAsync(u => u.Email == "hash@test.com");
        user.PasswordHash.Should().NotBe("PlainText123");
        BCrypt.Net.BCrypt.Verify("PlainText123", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_SetsEmailVerifiedTrue()
    {
        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            "verified@test.com,Pass@123,Test,User,,Member,");

        var importer = new UserImporter(_db);
        await importer.ImportAsync(path);

        var user = await _db.Users.FirstAsync(u => u.Email == "verified@test.com");
        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_SetsIsActiveTrue()
    {
        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            "active@test.com,Pass@123,Test,User,,Member,");

        var importer = new UserImporter(_db);
        await importer.ImportAsync(path);

        var user = await _db.Users.FirstAsync(u => u.Email == "active@test.com");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_ParsesRoleEnum()
    {
        var path = CreateTempCsv(
            "email,password,firstName,lastName,phone,role,documentNumber\n" +
            "board@test.com,Pass@123,Board,User,,Board,\n" +
            "member@test.com,Pass@123,Member,User,,member,");

        var importer = new UserImporter(_db);
        await importer.ImportAsync(path);

        var board = await _db.Users.FirstAsync(u => u.Email == "board@test.com");
        board.Role.Should().Be(UserRole.Board);

        var member = await _db.Users.FirstAsync(u => u.Email == "member@test.com");
        member.Role.Should().Be(UserRole.Member);
    }
}
