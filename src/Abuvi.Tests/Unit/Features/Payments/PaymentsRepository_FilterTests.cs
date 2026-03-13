using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Registrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Abuvi.Tests.Unit.Features.Payments;

public class PaymentsRepository_FilterTests : IDisposable
{
    private readonly AbuviDbContext _context;
    private readonly PaymentsRepository _repository;

    // Shared seed IDs
    private static readonly Guid CampId = Guid.NewGuid();
    private static readonly Guid CampEditionId = Guid.NewGuid();
    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid RegistrationId = Guid.NewGuid();

    public PaymentsRepository_FilterTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase($"PaymentsFilterTest_{Guid.NewGuid()}")
            .Options;

        _context = new AbuviDbContext(options);
        _repository = new PaymentsRepository(_context);

        SeedBaseEntities();
    }

    private void SeedBaseEntities()
    {
        var camp = new Camp
        {
            Id = CampId,
            Name = "Test Camp",
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m,
            IsActive = true
        };

        var edition = new CampEdition
        {
            Id = CampEditionId,
            CampId = CampId,
            Year = 2026,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 15),
            PricePerAdult = 180m,
            PricePerChild = 120m,
            PricePerBaby = 60m
        };

        var familyUnit = new FamilyUnit
        {
            Id = FamilyUnitId,
            Name = "Test Family",
            RepresentativeUserId = Guid.NewGuid()
        };

        var registration = new Registration
        {
            Id = RegistrationId,
            FamilyUnitId = FamilyUnitId,
            CampEditionId = CampEditionId,
            RegisteredByUserId = Guid.NewGuid(),
            TotalAmount = 400m
        };

        _context.Camps.Add(camp);
        _context.CampEditions.Add(edition);
        _context.FamilyUnits.Add(familyUnit);
        _context.Registrations.Add(registration);
        _context.SaveChanges();
    }

    private Payment CreatePayment(int installmentNumber, PaymentStatus status = PaymentStatus.Pending)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            RegistrationId = RegistrationId,
            InstallmentNumber = installmentNumber,
            Amount = 100m,
            Status = status,
            Method = PaymentMethod.Transfer,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetFilteredAsync_WithInstallmentNumber1_ReturnsOnlyFirstInstallment()
    {
        // Arrange
        _context.Payments.AddRange(
            CreatePayment(1),
            CreatePayment(2),
            CreatePayment(3));
        await _context.SaveChangesAsync();

        var filter = new PaymentFilterRequest(InstallmentNumber: 1);

        // Act
        var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

        // Assert
        totalCount.Should().Be(1);
        items.Should().AllSatisfy(p => p.InstallmentNumber.Should().Be(1));
    }

    [Fact]
    public async Task GetFilteredAsync_WithInstallmentNumber2_ReturnsOnlySecondInstallment()
    {
        // Arrange
        _context.Payments.AddRange(
            CreatePayment(1),
            CreatePayment(2),
            CreatePayment(3));
        await _context.SaveChangesAsync();

        var filter = new PaymentFilterRequest(InstallmentNumber: 2);

        // Act
        var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

        // Assert
        totalCount.Should().Be(1);
        items.Should().AllSatisfy(p => p.InstallmentNumber.Should().Be(2));
    }

    [Fact]
    public async Task GetFilteredAsync_WithInstallmentNumber3_ReturnsThirdAndHigher()
    {
        // Arrange
        _context.Payments.AddRange(
            CreatePayment(1),
            CreatePayment(2),
            CreatePayment(3),
            CreatePayment(4));
        await _context.SaveChangesAsync();

        var filter = new PaymentFilterRequest(InstallmentNumber: 3);

        // Act
        var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

        // Assert
        totalCount.Should().Be(2);
        items.Should().AllSatisfy(p => p.InstallmentNumber.Should().BeGreaterThanOrEqualTo(3));
    }

    [Fact]
    public async Task GetFilteredAsync_WithoutInstallmentNumber_ReturnsAll()
    {
        // Arrange
        _context.Payments.AddRange(
            CreatePayment(1),
            CreatePayment(2),
            CreatePayment(3));
        await _context.SaveChangesAsync();

        var filter = new PaymentFilterRequest();

        // Act
        var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

        // Assert
        totalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetFilteredAsync_CombinedStatusAndInstallment_AppliesBothFilters()
    {
        // Arrange
        _context.Payments.AddRange(
            CreatePayment(1, PaymentStatus.Completed),
            CreatePayment(1, PaymentStatus.Pending),
            CreatePayment(2, PaymentStatus.Completed));
        await _context.SaveChangesAsync();

        var filter = new PaymentFilterRequest(
            Status: PaymentStatus.Completed,
            InstallmentNumber: 1);

        // Act
        var (items, totalCount) = await _repository.GetFilteredAsync(filter, CancellationToken.None);

        // Assert
        totalCount.Should().Be(1);
        items.Should().ContainSingle()
            .Which.Should().Match<Payment>(p =>
                p.InstallmentNumber == 1 && p.Status == PaymentStatus.Completed);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
