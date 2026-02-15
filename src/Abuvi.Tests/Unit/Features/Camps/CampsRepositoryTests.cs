using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampsRepository
/// Following TDD: Tests written FIRST before implementation
/// </summary>
public class CampsRepositoryTests : IDisposable
{
    private readonly AbuviDbContext _context;
    private readonly ICampsRepository _repository;

    public CampsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"CampsRepositoryTest_{Guid.NewGuid()}")
            .Options;

        _context = new AbuviDbContext(options);
        _repository = new CampsRepository(_context);
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCamp()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Test Camp",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true
        };
        _context.Camps.Add(camp);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(camp.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(camp.Id);
        result.Name.Should().Be("Test Camp");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WithNoFilter_ReturnsAllCamps()
    {
        // Arrange
        var camps = new[]
        {
            new Camp { Id = Guid.NewGuid(), Name = "Camp 1", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, IsActive = true },
            new Camp { Id = Guid.NewGuid(), Name = "Camp 2", PricePerAdult = 200m, PricePerChild = 140m, PricePerBaby = 70m, IsActive = false }
        };
        _context.Camps.AddRange(camps);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(c => c.Name == "Camp 1");
        result.Should().Contain(c => c.Name == "Camp 2");
    }

    [Fact]
    public async Task GetAllAsync_WithActiveFilter_ReturnsOnlyActiveCamps()
    {
        // Arrange
        var camps = new[]
        {
            new Camp { Id = Guid.NewGuid(), Name = "Active Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, IsActive = true },
            new Camp { Id = Guid.NewGuid(), Name = "Inactive Camp", PricePerAdult = 200m, PricePerChild = 140m, PricePerBaby = 70m, IsActive = false }
        };
        _context.Camps.AddRange(camps);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(isActive: true);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(c => c.Name == "Active Camp");
        result.Should().NotContain(c => c.Name == "Inactive Camp");
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var camps = Enumerable.Range(1, 15).Select(i => new Camp
        {
            Id = Guid.NewGuid(),
            Name = $"Camp {i}",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        }).ToArray();
        _context.Camps.AddRange(camps);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(skip: 5, take: 5);

        // Assert
        result.Should().HaveCount(5);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidCamp_CreatesCamp()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "New Camp",
            Description = "A new camp location",
            Location = "Test Location",
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true
        };

        // Act
        var result = await _repository.CreateAsync(camp);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(camp.Id);
        result.Name.Should().Be("New Camp");

        var savedCamp = await _context.Camps.FindAsync(camp.Id);
        savedCamp.Should().NotBeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidCamp_UpdatesCamp()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true
        };
        _context.Camps.Add(camp);
        await _context.SaveChangesAsync();

        // Act
        camp.Name = "Updated Name";
        camp.PricePerAdult = 200.00m;
        var result = await _repository.UpdateAsync(camp);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.PricePerAdult.Should().Be(200.00m);

        var updatedCamp = await _context.Camps.FindAsync(camp.Id);
        updatedCamp!.Name.Should().Be("Updated Name");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesCamp()
    {
        // Arrange
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = "Camp to Delete",
            PricePerAdult = 180.00m,
            PricePerChild = 120.00m,
            PricePerBaby = 60.00m,
            IsActive = true
        };
        _context.Camps.Add(camp);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(camp.Id);

        // Assert
        result.Should().BeTrue();
        var deletedCamp = await _context.Camps.FindAsync(camp.Id);
        deletedCamp.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ReturnsFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
