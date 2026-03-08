using Abuvi.API.Common.Exceptions;
using Abuvi.API.Common.Services;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Features.FamilyUnits;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Abuvi.Tests.Unit.Features.FamilyUnits;

public class ProfilePhotoTests
{
    private readonly IFamilyUnitsRepository _repository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly FamilyUnitsService _sut;

    private static readonly Guid FamilyUnitId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public ProfilePhotoTests()
    {
        _repository = Substitute.For<IFamilyUnitsRepository>();
        _blobStorageService = Substitute.For<IBlobStorageService>();
        var encryptionService = Substitute.For<IEncryptionService>();
        var blobOptions = Options.Create(new BlobStorageOptions());
        var logger = Substitute.For<ILogger<FamilyUnitsService>>();
        _sut = new FamilyUnitsService(
            _repository, encryptionService, _blobStorageService, blobOptions, logger);
    }

    #region Upload FamilyMember Profile Photo

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_RepresentativeUploadsForOwnMember_SetsProfilePhotoUrl()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(FamilyUnitId);
        var file = CreateMockFile("photo.jpg", "image/jpeg");
        var uploadResult = new BlobUploadResult(
            "https://cdn.example.com/profile-photos/orig.jpg",
            "https://cdn.example.com/profile-photos/thumbs/thumb.webp",
            "photo.jpg", "image/jpeg", 1024);

        SetupRepositoryMocks(familyUnit, member);
        _blobStorageService.UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
            "profile-photos", MemberId, true, Arg.Any<CancellationToken>())
            .Returns(uploadResult);

        // Act
        var result = await _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        result.ProfilePhotoUrl.Should().Be("https://cdn.example.com/profile-photos/thumbs/thumb.webp");
        await _repository.Received(1).UpdateFamilyMemberAsync(
            Arg.Is<FamilyMember>(m => m.ProfilePhotoUrl == "https://cdn.example.com/profile-photos/thumbs/thumb.webp"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_AdminUploadsForAnyMember_SetsProfilePhotoUrl()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(OtherUserId);
        var member = CreateFamilyMember(FamilyUnitId);
        var file = CreateMockFile("photo.png", "image/png");
        var uploadResult = new BlobUploadResult(
            "https://cdn.example.com/orig.png",
            "https://cdn.example.com/thumbs/thumb.webp",
            "photo.png", "image/png", 2048);

        SetupRepositoryMocks(familyUnit, member);
        _blobStorageService.UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(uploadResult);

        // Act
        var result = await _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, true, file, CancellationToken.None);

        // Assert
        result.ProfilePhotoUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_NonRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(OtherUserId);
        var file = CreateMockFile("photo.jpg", "image/jpeg");

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var act = () => _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_MemberNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var file = CreateMockFile("photo.jpg", "image/jpeg");

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _repository.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = () => _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_FamilyUnitNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var file = CreateMockFile("photo.jpg", "image/jpeg");

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var act = () => _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_ExistingPhotoPresent_DeletesOldBlobFirst()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(FamilyUnitId);
        member.ProfilePhotoUrl = "https://cdn.example.com/profile-photos/old-thumb.webp";
        var file = CreateMockFile("new.jpg", "image/jpeg");
        var uploadResult = new BlobUploadResult(
            "https://cdn.example.com/new.jpg",
            "https://cdn.example.com/thumbs/new.webp",
            "new.jpg", "image/jpeg", 1024);

        SetupRepositoryMocks(familyUnit, member);
        _blobStorageService.UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(uploadResult);

        // Act
        await _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("profile-photos/old-thumb.webp")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_MemberNotInUnit_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(Guid.NewGuid()); // Different family unit
        var file = CreateMockFile("photo.jpg", "image/jpeg");

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _repository.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(member);

        // Act
        var act = () => _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_InvalidExtension_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(FamilyUnitId);
        var file = CreateMockFile("document.pdf", "application/pdf");

        SetupRepositoryMocks(familyUnit, member);

        // Act
        var act = () => _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UploadFamilyMemberProfilePhoto_FileTooLarge_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(FamilyUnitId);
        var file = CreateMockFile("photo.jpg", "image/jpeg", 100_000_000); // 100 MB > 50 MB limit

        SetupRepositoryMocks(familyUnit, member);

        // Act
        var act = () => _sut.UploadFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    #endregion

    #region Remove FamilyMember Profile Photo

    [Fact]
    public async Task RemoveFamilyMemberProfilePhoto_RepresentativeRemovesOwnMember_ClearsField()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(FamilyUnitId);
        member.ProfilePhotoUrl = "https://cdn.example.com/profile-photos/thumb.webp";

        SetupRepositoryMocks(familyUnit, member);

        // Act
        await _sut.RemoveFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateFamilyMemberAsync(
            Arg.Is<FamilyMember>(m => m.ProfilePhotoUrl == null),
            Arg.Any<CancellationToken>());
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFamilyMemberProfilePhoto_NoExistingPhoto_ReturnsWithoutError()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var member = CreateFamilyMember(FamilyUnitId);
        member.ProfilePhotoUrl = null;

        SetupRepositoryMocks(familyUnit, member);

        // Act
        await _sut.RemoveFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, CancellationToken.None);

        // Assert — no blob delete and no repository update
        await _blobStorageService.DidNotReceive().DeleteManyAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateFamilyMemberAsync(
            Arg.Any<FamilyMember>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFamilyMemberProfilePhoto_NonRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(OtherUserId);

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var act = () => _sut.RemoveFamilyMemberProfilePhotoAsync(
            FamilyUnitId, MemberId, UserId, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    #endregion

    #region Upload FamilyUnit Profile Photo

    [Fact]
    public async Task UploadFamilyUnitProfilePhoto_RepresentativeUploads_SetsProfilePhotoUrl()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        var file = CreateMockFile("unit-photo.jpg", "image/jpeg");
        var uploadResult = new BlobUploadResult(
            "https://cdn.example.com/orig.jpg",
            "https://cdn.example.com/thumbs/thumb.webp",
            "unit-photo.jpg", "image/jpeg", 1024);

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _blobStorageService.UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
            "profile-photos", FamilyUnitId, true, Arg.Any<CancellationToken>())
            .Returns(uploadResult);

        // Act
        var result = await _sut.UploadFamilyUnitProfilePhotoAsync(
            FamilyUnitId, UserId, false, file, CancellationToken.None);

        // Assert
        result.ProfilePhotoUrl.Should().Be("https://cdn.example.com/thumbs/thumb.webp");
    }

    [Fact]
    public async Task UploadFamilyUnitProfilePhoto_AdminUploads_SetsProfilePhotoUrl()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(OtherUserId);
        var file = CreateMockFile("photo.jpg", "image/jpeg");
        var uploadResult = new BlobUploadResult(
            "https://cdn.example.com/orig.jpg",
            "https://cdn.example.com/thumbs/thumb.webp",
            "photo.jpg", "image/jpeg", 1024);

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _blobStorageService.UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(uploadResult);

        // Act
        var result = await _sut.UploadFamilyUnitProfilePhotoAsync(
            FamilyUnitId, UserId, true, file, CancellationToken.None);

        // Assert
        result.ProfilePhotoUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task UploadFamilyUnitProfilePhoto_NonRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(OtherUserId);
        var file = CreateMockFile("photo.jpg", "image/jpeg");

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var act = () => _sut.UploadFamilyUnitProfilePhotoAsync(
            FamilyUnitId, UserId, false, file, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task UploadFamilyUnitProfilePhoto_ExistingPhotoPresent_DeletesOldBlobFirst()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        familyUnit.ProfilePhotoUrl = "https://cdn.example.com/profile-photos/old.webp";
        var file = CreateMockFile("new.jpg", "image/jpeg");
        var uploadResult = new BlobUploadResult(
            "https://cdn.example.com/new.jpg",
            "https://cdn.example.com/thumbs/new.webp",
            "new.jpg", "image/jpeg", 1024);

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _blobStorageService.UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(uploadResult);

        // Act
        await _sut.UploadFamilyUnitProfilePhotoAsync(
            FamilyUnitId, UserId, false, file, CancellationToken.None);

        // Assert
        await _blobStorageService.Received(1).DeleteManyAsync(
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("profile-photos/old.webp")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Remove FamilyUnit Profile Photo

    [Fact]
    public async Task RemoveFamilyUnitProfilePhoto_RepresentativeRemoves_ClearsField()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(UserId);
        familyUnit.ProfilePhotoUrl = "https://cdn.example.com/profile-photos/thumb.webp";

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        await _sut.RemoveFamilyUnitProfilePhotoAsync(
            FamilyUnitId, UserId, false, CancellationToken.None);

        // Assert
        await _repository.Received(1).UpdateFamilyUnitAsync(
            Arg.Is<FamilyUnit>(u => u.ProfilePhotoUrl == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveFamilyUnitProfilePhoto_NonRepresentative_ThrowsBusinessRuleException()
    {
        // Arrange
        var familyUnit = CreateFamilyUnit(OtherUserId);

        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);

        // Act
        var act = () => _sut.RemoveFamilyUnitProfilePhotoAsync(
            FamilyUnitId, UserId, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    #endregion

    #region Helpers

    private static FamilyUnit CreateFamilyUnit(Guid representativeUserId) => new()
    {
        Id = FamilyUnitId,
        Name = "Test Family",
        RepresentativeUserId = representativeUserId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static FamilyMember CreateFamilyMember(Guid familyUnitId) => new()
    {
        Id = MemberId,
        FamilyUnitId = familyUnitId,
        FirstName = "Test",
        LastName = "Member",
        DateOfBirth = new DateOnly(2000, 1, 1),
        Relationship = FamilyRelationship.Parent,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private void SetupRepositoryMocks(FamilyUnit familyUnit, FamilyMember member)
    {
        _repository.GetFamilyUnitByIdAsync(FamilyUnitId, Arg.Any<CancellationToken>())
            .Returns(familyUnit);
        _repository.GetFamilyMemberByIdAsync(MemberId, Arg.Any<CancellationToken>())
            .Returns(member);
    }

    private static IFormFile CreateMockFile(string fileName, string contentType, long length = 1024)
    {
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.ContentType.Returns(contentType);
        file.Length.Returns(length);
        file.OpenReadStream().Returns(new MemoryStream(new byte[length > 1024 ? 1024 : length]));
        return file;
    }

    #endregion
}
