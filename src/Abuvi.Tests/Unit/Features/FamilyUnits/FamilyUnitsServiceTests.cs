using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.FamilyUnits;

/// <summary>
/// Unit tests for FamilyUnitsService following TDD approach
/// These tests are written BEFORE the service implementation
/// </summary>
public class FamilyUnitsServiceTests
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<FamilyUnitsService> _logger;
    private readonly FamilyUnitsService _sut; // System Under Test

    public FamilyUnitsServiceTests()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _logger = Substitute.For<ILogger<FamilyUnitsService>>();
        _sut = new FamilyUnitsService(_repository, _encryptionService, _logger);

        // Setup encryption mock to return predictable values
        _encryptionService.Encrypt(Arg.Any<string>())
            .Returns(x => $"ENCRYPTED_{x[0]}");
        _encryptionService.Decrypt(Arg.Any<string>())
            .Returns(x => x.ToString()!.Replace("ENCRYPTED_", ""));
    }

    #region Family Unit CRUD Tests

    [Fact]
    public async Task CreateFamilyUnitAsync_WhenValidRequest_CreatesFamilyUnitAndRepresentativeMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = "Juan",
            LastName = "Garcia",
            Email = "juan@example.com",
            FamilyUnitId = null,
            PasswordHash = "hash",
            Role = UserRole.Member,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var request = new CreateFamilyUnitRequest("Garcia Family");

        _repository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _sut.CreateFamilyUnitAsync(userId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Garcia Family");
        result.RepresentativeUserId.Should().Be(userId);

        // Verify repository calls
        await _repository.Received(1).CreateFamilyUnitAsync(
            Arg.Is<FamilyUnit>(fu => fu.Name == "Garcia Family" && fu.RepresentativeUserId == userId),
            Arg.Any<CancellationToken>());

        // Verify representative member was created
        await _repository.Received(1).CreateFamilyMemberAsync(
            Arg.Is<FamilyMember>(fm =>
                fm.FirstName == "Juan" &&
                fm.LastName == "Garcia" &&
                fm.UserId == userId &&
                fm.Relationship == FamilyRelationship.Parent),
            Arg.Any<CancellationToken>());

        // Verify user's familyUnitId was updated
        await _repository.Received(1).UpdateUserFamilyUnitIdAsync(userId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFamilyUnitAsync_WhenUserAlreadyHasFamilyUnit_ThrowsBusinessRuleException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingFamilyUnitId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            FirstName = "Juan",
            LastName = "Garcia",
            Email = "juan@example.com",
            FamilyUnitId = existingFamilyUnitId, // User already has a family unit
            PasswordHash = "hash",
            Role = UserRole.Member,
            IsActive = true,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var request = new CreateFamilyUnitRequest("Garcia Family");

        _repository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = async () => await _sut.CreateFamilyUnitAsync(userId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("Ya tienes una unidad familiar");

        // Verify no family unit was created
        await _repository.DidNotReceive().CreateFamilyUnitAsync(Arg.Any<FamilyUnit>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFamilyUnitAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateFamilyUnitRequest("Garcia Family");

        _repository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.CreateFamilyUnitAsync(userId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"No se encontró Usuario con ID '{userId}'");
    }

    [Fact]
    public async Task GetFamilyUnitByIdAsync_WhenFamilyUnitExists_ReturnsFamilyUnit()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Garcia Family",
            RepresentativeUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.GetFamilyUnitByIdAsync(familyUnitId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(familyUnitId);
        result.Name.Should().Be("Garcia Family");
        result.RepresentativeUserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetFamilyUnitByIdAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.GetFamilyUnitByIdAsync(familyUnitId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"No se encontró Unidad Familiar con ID '{familyUnitId}'");
    }

    [Fact]
    public async Task GetCurrentUserFamilyUnitAsync_WhenFamilyUnitExists_ReturnsFamilyUnit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = Guid.NewGuid(),
            Name = "Garcia Family",
            RepresentativeUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyUnitByRepresentativeIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.GetCurrentUserFamilyUnitAsync(userId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepresentativeUserId.Should().Be(userId);
        result.Name.Should().Be("Garcia Family");
    }

    [Fact]
    public async Task GetCurrentUserFamilyUnitAsync_WhenNoFamilyUnit_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _repository.GetFamilyUnitByRepresentativeIdAsync(userId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.GetCurrentUserFamilyUnitAsync(userId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("No se encontró unidad familiar para el usuario actual");
    }

    [Fact]
    public async Task UpdateFamilyUnitAsync_WhenFamilyUnitExists_UpdatesAndReturnsUpdatedFamilyUnit()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Old Name",
            RepresentativeUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var request = new UpdateFamilyUnitRequest("New Name");

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.UpdateFamilyUnitAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Name");

        await _repository.Received(1).UpdateFamilyUnitAsync(
            Arg.Is<FamilyUnit>(fu => fu.Name == "New Name" && fu.Id == familyUnitId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFamilyUnitAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var request = new UpdateFamilyUnitRequest("New Name");

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.UpdateFamilyUnitAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"No se encontró Unidad Familiar con ID '{familyUnitId}'");
    }

    [Fact]
    public async Task DeleteFamilyUnitAsync_WhenFamilyUnitExists_DeletesFamilyUnitAndClearsUserFamilyUnitId()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var representativeUserId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Garcia Family",
            RepresentativeUserId = representativeUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        await _sut.DeleteFamilyUnitAsync(familyUnitId, CancellationToken.None);

        // Assert
        await _repository.Received(1).DeleteFamilyUnitAsync(familyUnitId, Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateUserFamilyUnitIdAsync(representativeUserId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFamilyUnitAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.DeleteFamilyUnitAsync(familyUnitId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"No se encontró Unidad Familiar con ID '{familyUnitId}'");
    }

    #endregion

    #region Authorization Helper Tests

    [Fact]
    public async Task IsRepresentativeAsync_WhenUserIsRepresentative_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Test Family",
            RepresentativeUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.IsRepresentativeAsync(familyUnitId, userId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsRepresentativeAsync_WhenUserIsNotRepresentative_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Test Family",
            RepresentativeUserId = otherUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.IsRepresentativeAsync(familyUnitId, userId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsRepresentativeAsync_WhenFamilyUnitNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await _sut.IsRepresentativeAsync(familyUnitId, userId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Family Member CRUD Tests

    [Fact]
    public async Task CreateFamilyMemberAsync_WhenValidRequest_CreatesFamilyMember()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Test Family",
            RepresentativeUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            "12345678A", "maria@example.com", "+34612345678",
            "Asthma", "Peanuts");

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.FirstName.Should().Be("Maria");
        result.LastName.Should().Be("Garcia");
        result.DocumentNumber.Should().Be("12345678A");
        result.HasMedicalNotes.Should().BeTrue();
        result.HasAllergies.Should().BeTrue();

        // Verify encryption was called
        _encryptionService.Received(1).Encrypt("Asthma");
        _encryptionService.Received(1).Encrypt("Peanuts");

        // Verify repository call
        await _repository.Received(1).CreateFamilyMemberAsync(
            Arg.Is<FamilyMember>(fm =>
                fm.FirstName == "Maria" &&
                fm.MedicalNotes == "ENCRYPTED_Asthma" &&
                fm.Allergies == "ENCRYPTED_Peanuts"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFamilyMemberAsync_WhenFamilyUnitNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateFamilyMemberAsync_WhenDocumentNumberProvided_ConvertsToUppercase()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = new FamilyUnit { Id = familyUnitId, Name = "Test", RepresentativeUserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            DocumentNumber: "abc123xyz"); // lowercase

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.DocumentNumber.Should().Be("ABC123XYZ"); // uppercase

        await _repository.Received(1).CreateFamilyMemberAsync(
            Arg.Is<FamilyMember>(fm => fm.DocumentNumber == "ABC123XYZ"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFamilyMemberAsync_WhenNoSensitiveData_DoesNotEncrypt()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var familyUnit = new FamilyUnit { Id = familyUnitId, Name = "Test", RepresentativeUserId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var request = new CreateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var result = await _sut.CreateFamilyMemberAsync(familyUnitId, request, CancellationToken.None);

        // Assert
        result.HasMedicalNotes.Should().BeFalse();
        result.HasAllergies.Should().BeFalse();

        // Verify encryption was NOT called
        _encryptionService.DidNotReceive().Encrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task GetFamilyMembersByFamilyUnitIdAsync_ReturnsList()
    {
        // Arrange
        var familyUnitId = Guid.NewGuid();
        var members = new List<FamilyMember>
        {
            new() {
                Id = Guid.NewGuid(),
                FamilyUnitId = familyUnitId,
                FirstName = "Juan",
                LastName = "Garcia",
                DateOfBirth = new DateOnly(1980, 1, 1),
                Relationship = FamilyRelationship.Parent,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new() {
                Id = Guid.NewGuid(),
                FamilyUnitId = familyUnitId,
                FirstName = "Maria",
                LastName = "Garcia",
                DateOfBirth = new DateOnly(2015, 6, 15),
                Relationship = FamilyRelationship.Child,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _repository.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(members);

        // Act
        var result = await _sut.GetFamilyMembersByFamilyUnitIdAsync(familyUnitId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.FirstName == "Juan");
        result.Should().Contain(m => m.FirstName == "Maria");
    }

    [Fact]
    public async Task GetFamilyMemberByIdAsync_WhenMemberExists_ReturnsMember()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var member = new FamilyMember
        {
            Id = memberId,
            FamilyUnitId = Guid.NewGuid(),
            FirstName = "Maria",
            LastName = "Garcia",
            DateOfBirth = new DateOnly(2015, 6, 15),
            Relationship = FamilyRelationship.Child,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);

        // Act
        var result = await _sut.GetFamilyMemberByIdAsync(memberId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(memberId);
        result.FirstName.Should().Be("Maria");
    }

    [Fact]
    public async Task GetFamilyMemberByIdAsync_WhenMemberNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var memberId = Guid.NewGuid();

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.GetFamilyMemberByIdAsync(memberId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateFamilyMemberAsync_WhenValidRequest_UpdatesAndEncryptsSensitiveData()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var member = new FamilyMember
        {
            Id = memberId,
            FamilyUnitId = Guid.NewGuid(),
            FirstName = "Maria",
            LastName = "Garcia",
            DateOfBirth = new DateOnly(2015, 6, 15),
            Relationship = FamilyRelationship.Child,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var request = new UpdateFamilyMemberRequest(
            "Maria", "Garcia Lopez", new DateOnly(2015, 6, 15), FamilyRelationship.Child,
            null, null, null, "New medical notes", "New allergies");

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);

        // Act
        var result = await _sut.UpdateFamilyMemberAsync(memberId, request, CancellationToken.None);

        // Assert
        result.LastName.Should().Be("Garcia Lopez");
        result.HasMedicalNotes.Should().BeTrue();
        result.HasAllergies.Should().BeTrue();

        // Verify encryption was called
        _encryptionService.Received(1).Encrypt("New medical notes");
        _encryptionService.Received(1).Encrypt("New allergies");

        await _repository.Received(1).UpdateFamilyMemberAsync(
            Arg.Is<FamilyMember>(fm => fm.LastName == "Garcia Lopez"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFamilyMemberAsync_WhenMemberNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest(
            "Maria", "Garcia", new DateOnly(2015, 6, 15), FamilyRelationship.Child);

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.UpdateFamilyMemberAsync(memberId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_WhenMemberExists_DeletesMember()
    {
        // Arrange
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var representativeUserId = Guid.NewGuid(); // Different from member's userId

        var member = new FamilyMember
        {
            Id = memberId,
            FamilyUnitId = familyUnitId,
            UserId = userId,
            FirstName = "Maria",
            LastName = "Garcia",
            DateOfBirth = new DateOnly(2015, 6, 15),
            Relationship = FamilyRelationship.Child,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Test Family",
            RepresentativeUserId = representativeUserId, // Different user
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        await _sut.DeleteFamilyMemberAsync(memberId, CancellationToken.None);

        // Assert
        await _repository.Received(1).DeleteFamilyMemberAsync(memberId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_WhenDeletingRepresentativeOwnRecord_ThrowsBusinessRuleException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var familyUnitId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var member = new FamilyMember
        {
            Id = memberId,
            FamilyUnitId = familyUnitId,
            UserId = userId, // Member is linked to user
            FirstName = "Juan",
            LastName = "Garcia",
            DateOfBirth = new DateOnly(1980, 1, 1),
            Relationship = FamilyRelationship.Parent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var familyUnit = new FamilyUnit
        {
            Id = familyUnitId,
            Name = "Test Family",
            RepresentativeUserId = userId, // Same user is representative
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .Returns(member);
        _repository.GetFamilyUnitByIdAsync(familyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var act = async () => await _sut.DeleteFamilyMemberAsync(memberId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("No puedes eliminar tu propio perfil mientras seas representante");

        // Verify no deletion occurred
        await _repository.DidNotReceive().DeleteFamilyMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFamilyMemberAsync_WhenMemberNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var memberId = Guid.NewGuid();

        _repository.GetFamilyMemberByIdAsync(memberId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = async () => await _sut.DeleteFamilyMemberAsync(memberId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion
}
