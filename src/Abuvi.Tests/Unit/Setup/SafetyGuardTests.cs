using Abuvi.API.Data;
using Abuvi.API.Features.Users;
using Abuvi.Setup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Setup;

public class SafetyGuardTests : IDisposable
{
    private static readonly Guid AdminId = new("00000000-0000-0000-0000-000000000001");
    private readonly AbuviDbContext _db;

    public SafetyGuardTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"SafetyGuardTest_{Guid.NewGuid()}")
            .Options;
        _db = new AbuviDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    // --- EnsureResetAllowed ---

    [Fact]
    public void EnsureResetAllowed_InDevMode_ReturnsTrue()
    {
        var config = new SetupConfig { Env = SetupEnv.Dev };
        var guard = new SafetyGuard(_db, config);

        var result = guard.EnsureResetAllowed();

        result.Should().BeTrue();
    }

    [Fact]
    public void EnsureResetAllowed_InProduction_WithoutConfirm_ReturnsFalse()
    {
        var config = new SetupConfig { Env = SetupEnv.Production, Confirm = false };
        var guard = new SafetyGuard(_db, config);

        var result = guard.EnsureResetAllowed();

        result.Should().BeFalse();
    }

    // --- EnsureImportAllowed ---

    [Fact]
    public async Task EnsureImportAllowed_InDevMode_AlwaysReturnsTrue()
    {
        var config = new SetupConfig { Env = SetupEnv.Dev };
        var guard = new SafetyGuard(_db, config);

        var result = await guard.EnsureImportAllowedAsync("users");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureImportAllowed_InProduction_EmptyTable_ReturnsTrue()
    {
        var config = new SetupConfig { Env = SetupEnv.Production };
        var guard = new SafetyGuard(_db, config);

        var result = await guard.EnsureImportAllowedAsync("camps");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureImportAllowed_InProduction_TableHasData_ReturnsFalse()
    {
        _db.Camps.Add(new Abuvi.API.Features.Camps.Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 100,
            PricePerChild = 50,
            PricePerBaby = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var config = new SetupConfig { Env = SetupEnv.Production };
        var guard = new SafetyGuard(_db, config);

        var result = await guard.EnsureImportAllowedAsync("camps");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureImportAllowed_InProduction_UsersWithOnlyAdmin_ReturnsTrue()
    {
        // Seed admin user only
        _db.Users.Add(new User
        {
            Id = AdminId,
            Email = "admin@abuvi.local",
            PasswordHash = "hashed",
            FirstName = "Admin",
            LastName = "User",
            Role = UserRole.Admin,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var config = new SetupConfig { Env = SetupEnv.Production };
        var guard = new SafetyGuard(_db, config);

        var result = await guard.EnsureImportAllowedAsync("users");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureImportAllowed_InProduction_UsersWithMoreThanAdmin_ReturnsFalse()
    {
        _db.Users.Add(new User
        {
            Id = AdminId,
            Email = "admin@abuvi.local",
            PasswordHash = "hashed",
            FirstName = "Admin",
            LastName = "User",
            Role = UserRole.Admin,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "member@abuvi.local",
            PasswordHash = "hashed",
            FirstName = "Member",
            LastName = "User",
            Role = UserRole.Member,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var config = new SetupConfig { Env = SetupEnv.Production };
        var guard = new SafetyGuard(_db, config);

        var result = await guard.EnsureImportAllowedAsync("users");

        result.Should().BeFalse();
    }
}
