using Abuvi.API.Common.Exceptions;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.GooglePlaces;
using Abuvi.API.Features.Users;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Abuvi.Tests.Unit.Features.Camps;

/// <summary>
/// Unit tests for CampsService
/// </summary>
public class CampsServiceTests
{
    private readonly ICampsRepository _repository;
    private readonly IGooglePlacesService _googlePlacesService;
    private readonly IGooglePlacesMapperService _mapper;
    private readonly IUsersRepository _usersRepository;
    private readonly CampsService _sut;

    public CampsServiceTests()
    {
        _repository = Substitute.For<ICampsRepository>();
        _googlePlacesService = Substitute.For<IGooglePlacesService>();
        _mapper = Substitute.For<IGooglePlacesMapperService>();
        _usersRepository = Substitute.For<IUsersRepository>();
        _sut = new CampsService(_repository, _googlePlacesService, _mapper, _usersRepository);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidPricingTemplate_CreatesCamp()
    {
        // Arrange
        var request = new CreateCampRequest(
            Name: "Test Camp",
            Description: "A test camp",
            Location: "Test Location",
            Latitude: 40.7128m,
            Longitude: -74.0060m,
            GooglePlaceId: null,
            PricePerAdult: 180.00m,
            PricePerChild: 120.00m,
            PricePerBaby: 60.00m
        );

        var createdCamp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PricePerAdult = request.PricePerAdult,
            PricePerChild = request.PricePerChild,
            PricePerBaby = request.PricePerBaby,
            IsActive = true
        };

        _repository.CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(createdCamp);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Camp");
        result.PricePerAdult.Should().Be(180.00m);
        result.PricePerChild.Should().Be(120.00m);
        result.PricePerBaby.Should().Be(60.00m);
        result.IsActive.Should().BeTrue();

        await _repository.Received(1).CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithGooglePlaceId_CallsEnrichment()
    {
        // Arrange
        var campId = Guid.NewGuid();
        var placeId = "ChIJN1t_tDeuEmsRUsoyG83frY4";
        var request = new CreateCampRequest(
            Name: "Test Camp",
            Description: null,
            Location: null,
            Latitude: null,
            Longitude: null,
            GooglePlaceId: placeId,
            PricePerAdult: 100m,
            PricePerChild: 60m,
            PricePerBaby: 30m
        );

        var createdCamp = new Camp
        {
            Id = campId,
            Name = request.Name,
            GooglePlaceId = placeId,
            PricePerAdult = 100m,
            PricePerChild = 60m,
            PricePerBaby = 30m,
            IsActive = true
        };

        var placeDetails = new PlaceDetails(
            PlaceId: placeId,
            Name: "Test Camp",
            FormattedAddress: "123 Test St, City, Spain",
            Latitude: 40.0m,
            Longitude: -3.0m,
            Types: ["campground"],
            PhoneNumber: "+34 912 345 678",
            NationalPhoneNumber: "912 345 678",
            Website: "https://test.com",
            GoogleMapsUrl: "https://maps.google.com/?q=test",
            Rating: 4.5m,
            RatingCount: 100,
            BusinessStatus: "OPERATIONAL",
            AddressComponents: [],
            Photos: []
        );

        var googleData = new CampGoogleData(
            FormattedAddress: "123 Test St, City, Spain",
            StreetAddress: null,
            Locality: "City",
            AdministrativeArea: null,
            PostalCode: null,
            Country: "Spain",
            PhoneNumber: "+34 912 345 678",
            NationalPhoneNumber: "912 345 678",
            WebsiteUrl: "https://test.com",
            GoogleMapsUrl: "https://maps.google.com/?q=test",
            GoogleRating: 4.5m,
            GoogleRatingCount: 100,
            BusinessStatus: "OPERATIONAL",
            PlaceTypes: "[\"campground\"]"
        );

        _repository.CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(createdCamp);
        _googlePlacesService.GetPlaceDetailsAsync(placeId, Arg.Any<CancellationToken>())
            .Returns(placeDetails);
        _mapper.MapToCampData(placeDetails).Returns(googleData);
        _mapper.MapToPhotos(placeDetails, campId).Returns([]);
        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Camp>());

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        await _googlePlacesService.Received(1).GetPlaceDetailsAsync(placeId, Arg.Any<CancellationToken>());
        _mapper.Received(1).MapToCampData(placeDetails);
        await _repository.Received(1).UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithNegativePrices_ThrowsArgumentException()
    {
        var request = new CreateCampRequest(
            Name: "Test Camp", Description: null, Location: null,
            Latitude: null, Longitude: null, GooglePlaceId: null,
            PricePerAdult: -10.00m, PricePerChild: 120.00m, PricePerBaby: 60.00m
        );

        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*price*cannot be negative*");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidLatitude_ThrowsArgumentException()
    {
        var request = new CreateCampRequest(
            Name: "Test Camp", Description: null, Location: null,
            Latitude: 95.0m, Longitude: -74.0060m, GooglePlaceId: null,
            PricePerAdult: 180.00m, PricePerChild: 120.00m, PricePerBaby: 60.00m
        );

        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Latitude*must be between -90 and 90*");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidLongitude_ThrowsArgumentException()
    {
        var request = new CreateCampRequest(
            Name: "Test Camp", Description: null, Location: null,
            Latitude: 40.7128m, Longitude: 185.0m, GooglePlaceId: null,
            PricePerAdult: 180.00m, PricePerChild: 120.00m, PricePerBaby: 60.00m
        );

        var act = async () => await _sut.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Longitude*must be between -180 and 180*");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCampDetailResponse()
    {
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId, Name = "Test Camp",
            PricePerAdult = 180.00m, PricePerChild = 120.00m, PricePerBaby = 60.00m,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>()).Returns(camp);

        var result = await _sut.GetByIdAsync(campId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(campId);
        result.Name.Should().Be("Test Camp");
        result.Photos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        _repository.GetByIdWithPhotosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithPhotos_ReturnsPhotosInResponse()
    {
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId, Name = "Test Camp",
            PricePerAdult = 100m, PricePerChild = 60m, PricePerBaby = 30m,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Photos = [new CampPhoto { Id = Guid.NewGuid(), CampId = campId, PhotoReference = "ref1", Width = 800, Height = 600, AttributionName = "Author", IsPrimary = true, DisplayOrder = 1 }]
        };

        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>()).Returns(camp);

        var result = await _sut.GetByIdAsync(campId);

        result.Should().NotBeNull();
        result!.Photos.Should().HaveCount(1);
        result.Photos[0].PhotoReference.Should().Be("ref1");
        result.Photos[0].IsPrimary.Should().BeTrue();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllCamps()
    {
        var camps = new List<Camp>
        {
            new() { Id = Guid.NewGuid(), Name = "Camp 1", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Camp 2", PricePerAdult = 200m, PricePerChild = 140m, PricePerBaby = 70m, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _repository.GetAllAsync(null, 0, 100, Arg.Any<CancellationToken>()).Returns(camps);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WithActiveFilter_ReturnsOnlyActiveCamps()
    {
        var camps = new List<Camp>
        {
            new() { Id = Guid.NewGuid(), Name = "Active Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _repository.GetAllAsync(true, 0, 100, Arg.Any<CancellationToken>()).Returns(camps);

        var result = await _sut.GetAllAsync(isActive: true);
        result.Should().HaveCount(1);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesCamp()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingCamp = CreateTestCamp(campId);

        var updateRequest = new UpdateCampRequest(
            Name: "Updated Name", Description: "Updated description",
            Location: null, Latitude: null, Longitude: null, GooglePlaceId: null,
            PricePerAdult: 200.00m, PricePerChild: 140.00m, PricePerBaby: 70.00m,
            IsActive: true
        );

        SetupUpdateMocks(campId, existingCamp);

        var result = await _sut.UpdateAsync(campId, updateRequest, userId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.PricePerAdult.Should().Be(200.00m);
        await _repository.Received(1).UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentId_ReturnsNull()
    {
        _repository.GetByIdWithPhotosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        var request = CreateTestUpdateRequest();
        var result = await _sut.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid());
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesCamp()
    {
        var campId = Guid.NewGuid();
        var camp = new Camp { Id = campId, Name = "Camp to Delete", PricePerAdult = 180.00m, PricePerChild = 120.00m, PricePerBaby = 60.00m, IsActive = true };

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>()).Returns(camp);
        _repository.DeleteAsync(campId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.DeleteAsync(campId);

        result.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(campId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsFalse()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Camp?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithActiveEditions_ThrowsInvalidOperationException()
    {
        var campId = Guid.NewGuid();
        var camp = new Camp
        {
            Id = campId, Name = "Camp with Editions",
            PricePerAdult = 180.00m, PricePerChild = 120.00m, PricePerBaby = 60.00m, IsActive = true,
            Editions = new List<CampEdition>
            {
                new() { Id = Guid.NewGuid(), CampId = campId, Year = 2026, Status = CampEditionStatus.Open, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10), PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m }
            }
        };

        _repository.GetByIdAsync(campId, Arg.Any<CancellationToken>()).Returns(camp);

        var act = async () => await _sut.DeleteAsync(campId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot delete camp*editions*");
    }

    #endregion

    #region AccommodationCapacity Tests

    [Fact]
    public async Task CreateAsync_WithAccommodationCapacity_SavesCapacityInJson()
    {
        var accommodation = new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 10,
            SharedRooms = new List<SharedRoomInfo> { new() { Quantity = 5, BedsPerRoom = 4, HasBathroom = true } }
        };

        var request = new CreateCampRequest(
            Name: "Camp with Capacity", Description: null, Location: null,
            Latitude: null, Longitude: null, GooglePlaceId: null,
            PricePerAdult: 180m, PricePerChild: 120m, PricePerBaby: 60m,
            AccommodationCapacity: accommodation
        );

        var createdCamp = new Camp { Id = Guid.NewGuid(), Name = request.Name, PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m };
        createdCamp.SetAccommodationCapacity(accommodation);

        _repository.CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>()).Returns(createdCamp);

        var result = await _sut.CreateAsync(request);

        result.AccommodationCapacity.Should().NotBeNull();
        result.AccommodationCapacity!.PrivateRoomsWithBathroom.Should().Be(10);
        result.CalculatedTotalBedCapacity.Should().Be(40);
    }

    [Fact]
    public async Task CreateAsync_WithNullAccommodation_SavesNullJson()
    {
        var request = new CreateCampRequest(
            Name: "Camp", Description: null, Location: null,
            Latitude: null, Longitude: null, GooglePlaceId: null,
            PricePerAdult: 180m, PricePerChild: 120m, PricePerBaby: 60m
        );

        var createdCamp = new Camp { Id = Guid.NewGuid(), Name = "Camp", PricePerAdult = 180m, PricePerChild = 120m, PricePerBaby = 60m };
        _repository.CreateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>()).Returns(createdCamp);

        var result = await _sut.CreateAsync(request);

        result.AccommodationCapacity.Should().BeNull();
        result.CalculatedTotalBedCapacity.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithAccommodationCapacity_UpdatesCapacity()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingCamp = CreateTestCamp(campId);

        var newAccommodation = new AccommodationCapacity { PrivateRoomsWithBathroom = 8 };
        var request = new UpdateCampRequest(
            Name: "Updated", Description: null, Location: null,
            Latitude: null, Longitude: null, GooglePlaceId: null,
            PricePerAdult: 180m, PricePerChild: 120m, PricePerBaby: 60m,
            IsActive: true, AccommodationCapacity: newAccommodation
        );

        SetupUpdateMocks(campId, existingCamp);

        var result = await _sut.UpdateAsync(campId, request, userId);

        result.Should().NotBeNull();
        result!.AccommodationCapacity.Should().NotBeNull();
        result.AccommodationCapacity!.PrivateRoomsWithBathroom.Should().Be(8);
        result.CalculatedTotalBedCapacity.Should().Be(16);
    }

    #endregion

    #region Audit Log Tests

    [Fact]
    public async Task UpdateAsync_WhenBasePriceChanges_CreatesAuditLogEntry()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var camp = CreateTestCamp(campId, basePrice: 100m);
        var request = CreateTestUpdateRequest(basePrice: 200m);

        SetupUpdateMocks(campId, camp);

        await _sut.UpdateAsync(campId, request, userId);

        await _repository.Received(1).AddAuditLogsAsync(
            Arg.Is<List<CampAuditLog>>(logs =>
                logs.Any(l => l.FieldName == "BasePrice" && l.OldValue == "100" && l.NewValue == "200")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenIsActiveChanges_CreatesAuditLogEntry()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var camp = CreateTestCamp(campId, isActive: true);
        var request = CreateTestUpdateRequest(isActive: false);

        SetupUpdateMocks(campId, camp);

        await _sut.UpdateAsync(campId, request, userId);

        await _repository.Received(1).AddAuditLogsAsync(
            Arg.Is<List<CampAuditLog>>(logs =>
                logs.Any(l => l.FieldName == "IsActive" && l.OldValue == "True" && l.NewValue == "False")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenNoTrackedFieldChanges_DoesNotCreateAuditLog()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var camp = CreateTestCamp(campId);
        var request = CreateTestUpdateRequest();

        SetupUpdateMocks(campId, camp);

        await _sut.UpdateAsync(campId, request, userId);

        await _repository.DidNotReceive().AddAuditLogsAsync(
            Arg.Any<IEnumerable<CampAuditLog>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenMultipleTrackedFieldsChange_CreatesOneEntryPerField()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var camp = CreateTestCamp(campId, basePrice: 100m, contactPerson: "Old Person");
        var request = CreateTestUpdateRequest(basePrice: 200m, contactPerson: "New Person");

        SetupUpdateMocks(campId, camp);

        await _sut.UpdateAsync(campId, request, userId);

        await _repository.Received(1).AddAuditLogsAsync(
            Arg.Is<List<CampAuditLog>>(logs => logs.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_AlwaysSetsLastModifiedByUserId()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var camp = CreateTestCamp(campId);
        var request = CreateTestUpdateRequest();

        SetupUpdateMocks(campId, camp);

        await _sut.UpdateAsync(campId, request, userId);

        await _repository.Received(1).UpdateAsync(
            Arg.Is<Camp>(c => c.LastModifiedByUserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenCampNotFound_ReturnsNull_AuditTest()
    {
        _repository.GetByIdWithPhotosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Camp?)null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), CreateTestUpdateRequest(), Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenAbuviManagedByUserIdChanges_CreatesAuditLogEntry()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldManagedBy = Guid.NewGuid();
        var newManagedBy = Guid.NewGuid();

        var camp = CreateTestCamp(campId, abuviManagedByUserId: oldManagedBy);
        var request = CreateTestUpdateRequest(abuviManagedByUserId: newManagedBy);

        SetupUpdateMocks(campId, camp);
        _usersRepository.GetByIdAsync(newManagedBy, Arg.Any<CancellationToken>())
            .Returns(new User { Id = newManagedBy, Role = UserRole.Board, FirstName = "Test", LastName = "User", Email = "t@t.com", PasswordHash = "h" });

        await _sut.UpdateAsync(campId, request, userId);

        await _repository.Received(1).AddAuditLogsAsync(
            Arg.Is<List<CampAuditLog>>(logs =>
                logs.Any(l => l.FieldName == "AbuviManagedByUserId")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenAbuviManagedByUserIdIsValidBoardUser_Succeeds()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var boardUserId = Guid.NewGuid();

        var camp = CreateTestCamp(campId);
        var request = CreateTestUpdateRequest(abuviManagedByUserId: boardUserId);

        SetupUpdateMocks(campId, camp);
        _usersRepository.GetByIdAsync(boardUserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = boardUserId, Role = UserRole.Board, FirstName = "B", LastName = "U", Email = "b@t.com", PasswordHash = "h" });

        var result = await _sut.UpdateAsync(campId, request, userId);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenAbuviManagedByUserIdIsNotBoardUser_ThrowsBusinessRuleException()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();

        var camp = CreateTestCamp(campId);
        var request = CreateTestUpdateRequest(abuviManagedByUserId: memberUserId);

        SetupUpdateMocks(campId, camp);
        _usersRepository.GetByIdAsync(memberUserId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = memberUserId, Role = UserRole.Member, FirstName = "M", LastName = "U", Email = "m@t.com", PasswordHash = "h" });

        var act = async () => await _sut.UpdateAsync(campId, request, userId);
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*Board or Admin role*");
    }

    [Fact]
    public async Task UpdateAsync_WhenAbuviManagedByUserIdDoesNotExist_ThrowsBusinessRuleException()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var nonExistentUserId = Guid.NewGuid();

        var camp = CreateTestCamp(campId);
        var request = CreateTestUpdateRequest(abuviManagedByUserId: nonExistentUserId);

        SetupUpdateMocks(campId, camp);
        _usersRepository.GetByIdAsync(nonExistentUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var act = async () => await _sut.UpdateAsync(campId, request, userId);
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*non-existent user*");
    }

    [Fact]
    public async Task UpdateAsync_WhenAbuviManagedByUserIdIsNull_ClearsAssignment()
    {
        var campId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldManagedBy = Guid.NewGuid();

        var camp = CreateTestCamp(campId, abuviManagedByUserId: oldManagedBy);
        var request = CreateTestUpdateRequest(abuviManagedByUserId: null);

        SetupUpdateMocks(campId, camp);

        var result = await _sut.UpdateAsync(campId, request, userId);

        result.Should().NotBeNull();
        result!.AbuviManagedByUserId.Should().BeNull();
        await _repository.Received(1).UpdateAsync(
            Arg.Is<Camp>(c => c.AbuviManagedByUserId == null),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Test Helpers

    private static Camp CreateTestCamp(
        Guid campId,
        bool isActive = true,
        decimal? basePrice = null,
        string? contactPerson = null,
        string? contactEmail = null,
        Guid? abuviManagedByUserId = null)
    {
        return new Camp
        {
            Id = campId,
            Name = "Test Camp",
            PricePerAdult = 100m,
            PricePerChild = 60m,
            PricePerBaby = 30m,
            IsActive = isActive,
            BasePrice = basePrice,
            ContactPerson = contactPerson,
            ContactEmail = contactEmail,
            AbuviManagedByUserId = abuviManagedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static UpdateCampRequest CreateTestUpdateRequest(
        bool isActive = true,
        decimal? basePrice = null,
        string? contactPerson = null,
        string? contactEmail = null,
        Guid? abuviManagedByUserId = null)
    {
        return new UpdateCampRequest(
            Name: "Test Camp",
            Description: null,
            Location: null,
            Latitude: null,
            Longitude: null,
            GooglePlaceId: null,
            PricePerAdult: 100m,
            PricePerChild: 60m,
            PricePerBaby: 30m,
            IsActive: isActive,
            BasePrice: basePrice,
            ContactPerson: contactPerson,
            ContactEmail: contactEmail,
            AbuviManagedByUserId: abuviManagedByUserId
        );
    }

    private void SetupUpdateMocks(Guid campId, Camp camp)
    {
        _repository.GetByIdWithPhotosAsync(campId, Arg.Any<CancellationToken>()).Returns(camp);
        _repository.UpdateAsync(Arg.Any<Camp>(), Arg.Any<CancellationToken>())
            .Returns(args => args.Arg<Camp>());
    }

    #endregion
}
