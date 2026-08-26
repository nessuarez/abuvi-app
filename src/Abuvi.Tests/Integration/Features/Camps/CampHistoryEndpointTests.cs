using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abuvi.API.Common.Models;
using Abuvi.API.Data;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Camps;

/// <summary>
/// Integration tests for GET /api/camps/history — the 50th anniversary history map feed.
/// Seeds its own venues in a year range no real edition uses (1901-1904) and removes
/// them afterwards, so assertions stay deterministic on a shared database.
/// </summary>
public class CampHistoryEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Years far outside the real archive (1976-2025), so nothing else can collide.
    private const int RepeatVenueFirstYear = 1901;
    private const int RepeatVenueSecondYear = 1903;
    private const int SingleVenueYear = 1902;
    private const int UpcomingYear = 1904;

    private Guid _repeatVenueId;
    private Guid _singleVenueId;
    private Guid _uploaderId;
    private readonly List<Guid> _seededMediaIds = [];

    public CampHistoryEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Seeding

    public async Task InitializeAsync()
    {
        _uploaderId = await RegisterMemberAndGetIdAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        var repeatVenue = BuildCamp("History Repeat Venue", 43.077348m, -3.552172m);
        var singleVenue = BuildCamp("History Single Venue", 40.001m, -2.001m);
        _repeatVenueId = repeatVenue.Id;
        _singleVenueId = singleVenue.Id;
        db.Camps.AddRange(repeatVenue, singleVenue);

        db.CampEditions.AddRange(
            BuildEdition(repeatVenue.Id, RepeatVenueFirstYear, CampEditionStatus.Completed),
            BuildEdition(repeatVenue.Id, RepeatVenueSecondYear, CampEditionStatus.Completed),
            BuildEdition(singleVenue.Id, SingleVenueYear, CampEditionStatus.Completed),
            // Not completed: must never reach the history feed.
            BuildEdition(singleVenue.Id, UpcomingYear, CampEditionStatus.Open));

        // Five publishable photos for the first year, plus three that must not count.
        for (var i = 0; i < 5; i++)
            db.MediaItems.Add(TrackMedia(BuildPhoto(RepeatVenueFirstYear, $"Photo {i}", displayOrder: i)));

        db.MediaItems.Add(TrackMedia(BuildPhoto(
            RepeatVenueFirstYear, "Awaiting approval", isApproved: false)));
        db.MediaItems.Add(TrackMedia(BuildPhoto(
            RepeatVenueFirstYear, "Approved but unpublished", isPublished: false)));
        db.MediaItems.Add(TrackMedia(BuildPhoto(
            RepeatVenueFirstYear, "Another section", context: "camp-2026")));

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();

        await db.MediaItems.Where(m => _seededMediaIds.Contains(m.Id)).ExecuteDeleteAsync();
        await db.CampEditions
            .Where(e => e.CampId == _repeatVenueId || e.CampId == _singleVenueId)
            .ExecuteDeleteAsync();
        await db.Camps
            .Where(c => c.Id == _repeatVenueId || c.Id == _singleVenueId)
            .ExecuteDeleteAsync();
    }

    private static Camp BuildCamp(string name, decimal latitude, decimal longitude) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{name} {Guid.NewGuid():N}",
        Location = "Burgos",
        Latitude = latitude,
        Longitude = longitude,
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static CampEdition BuildEdition(Guid campId, int year, CampEditionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        CampId = campId,
        Year = year,
        StartDate = new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(year, 7, 15, 0, 0, 0, DateTimeKind.Utc),
        PricePerAdult = 180m,
        PricePerChild = 120m,
        PricePerBaby = 60m,
        Status = status,
        IsArchived = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private MediaItem BuildPhoto(
        int year,
        string title,
        bool isApproved = true,
        bool isPublished = true,
        string context = CampHistoryService.AnniversaryContext,
        int displayOrder = 0) => new()
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = _uploaderId,
            FileUrl = $"https://blob.test/{Guid.NewGuid():N}.jpg",
            ThumbnailUrl = $"https://blob.test/{Guid.NewGuid():N}-thumb.jpg",
            Type = MediaItemType.Photo,
            Title = title,
            Year = year,
            Decade = MediaItemMappingExtensions.DeriveDecade(year),
            Context = context,
            IsApproved = isApproved,
            IsPublished = isPublished,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private MediaItem TrackMedia(MediaItem item)
    {
        _seededMediaIds.Add(item.Id);
        return item;
    }

    #endregion

    #region Auth helpers

    private async Task<Guid> RegisterMemberAndGetIdAsync()
    {
        var email = $"history-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "MemberPass123!", "History", "Tester", null));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        return user.Id;
    }

    private async Task<HttpClient> CreateMemberClientAsync()
    {
        var email = $"member-{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "MemberPass123!", "Member", "User", null));

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "MemberPass123!"));
        var login = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Data!.Token);
        return client;
    }

    private static async Task<List<CampHistoryResponse>> ReadHistoryAsync(HttpResponseMessage response)
    {
        var payload = await response.Content
            .ReadFromJsonAsync<ApiResponse<List<CampHistoryResponse>>>(JsonOptions);
        return payload!.Data!;
    }

    #endregion

    #region Authorization

    [Fact]
    public async Task GetHistory_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/camps/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_WithMemberToken_ReturnsOk()
    {
        var client = await CreateMemberClientAsync();

        var response = await client.GetAsync("/api/camps/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Editions

    [Fact]
    public async Task GetHistory_ReturnsEditionsOrderedByYear()
    {
        var client = await CreateMemberClientAsync();

        var history = await ReadHistoryAsync(await client.GetAsync("/api/camps/history"));

        history.Select(h => h.Year).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetHistory_IncludesOnlyCompletedEditions()
    {
        var client = await CreateMemberClientAsync();

        var history = await ReadHistoryAsync(await client.GetAsync("/api/camps/history"));

        history.Should().NotContain(h => h.Year == UpcomingYear);
        history.Should().Contain(h => h.Year == SingleVenueYear);
    }

    [Fact]
    public async Task GetHistory_CountsRepeatedVisitsToTheSameVenue()
    {
        var client = await CreateMemberClientAsync();

        var history = await ReadHistoryAsync(await client.GetAsync("/api/camps/history"));

        var first = history.Single(h => h.Year == RepeatVenueFirstYear);
        var second = history.Single(h => h.Year == RepeatVenueSecondYear);

        first.EditionNumber.Should().Be(1);
        second.EditionNumber.Should().Be(2);
        first.TotalEditionsAtVenue.Should().Be(2);
        second.TotalEditionsAtVenue.Should().Be(2);
    }

    [Fact]
    public async Task GetHistory_ResolvesVenueNameAndCoordinates()
    {
        var client = await CreateMemberClientAsync();

        var row = (await ReadHistoryAsync(await client.GetAsync("/api/camps/history")))
            .Single(h => h.Year == RepeatVenueFirstYear);

        row.CampId.Should().Be(_repeatVenueId);
        row.CampName.Should().StartWith("History Repeat Venue");
        row.Location.Should().Be("Burgos");
        row.Latitude.Should().Be(43.077348m);
        row.Longitude.Should().Be(-3.552172m);
    }

    #endregion

    #region Photos

    [Fact]
    public async Task GetHistory_CountsOnlyApprovedPublishedAnniversaryPhotos()
    {
        var client = await CreateMemberClientAsync();

        var row = (await ReadHistoryAsync(await client.GetAsync("/api/camps/history")))
            .Single(h => h.Year == RepeatVenueFirstYear);

        // Five publishable photos; the pending, unpublished and other-context ones do not count.
        row.PhotoCount.Should().Be(5);
    }

    [Fact]
    public async Task GetHistory_ReturnsAtMostThreePreviewPhotos()
    {
        var client = await CreateMemberClientAsync();

        var row = (await ReadHistoryAsync(await client.GetAsync("/api/camps/history")))
            .Single(h => h.Year == RepeatVenueFirstYear);

        row.PreviewPhotos.Should().HaveCount(3);
        row.PreviewPhotos.Should().OnlyContain(p =>
            p.ThumbnailUrl != null && p.ThumbnailUrl != string.Empty);
        row.PreviewPhotos.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetHistory_YearWithoutPhotos_ReturnsZeroAndEmptyListNeverNull()
    {
        var client = await CreateMemberClientAsync();

        var row = (await ReadHistoryAsync(await client.GetAsync("/api/camps/history")))
            .Single(h => h.Year == SingleVenueYear);

        row.PhotoCount.Should().Be(0);
        row.PreviewPhotos.Should().NotBeNull().And.BeEmpty();
    }

    #endregion
}
