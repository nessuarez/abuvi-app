using System.Data.Common;
using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.MediaItems;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Camps;

/// <summary>
/// Guards the history feed against N+1: the number of SQL round trips must not grow
/// with the number of editions. Runs against the real provider, because the whole
/// point is what EF actually sends to PostgreSQL.
/// </summary>
public class CampHistoryQueryCountTests : IAsyncLifetime
{
    private const int SmallHistoryFirstYear = 1801;
    private const int SmallHistoryCount = 3;
    private const int LargeHistoryFirstYear = 1811;
    private const int LargeHistoryCount = 12;

    private readonly CommandCountingInterceptor _interceptor = new();
    private AbuviDbContext _db = null!;
    private CampHistoryService _sut = null!;
    private readonly List<Guid> _seededCampIds = [];

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
            .AddInterceptors(_interceptor)
            .Options;

        _db = new AbuviDbContext(options);
        _sut = new CampHistoryService(
            new CampEditionsRepository(_db),
            new MediaItemsRepository(_db));

        await SeedHistoryAsync(SmallHistoryFirstYear, SmallHistoryCount);
    }

    public async Task DisposeAsync()
    {
        await _db.CampEditions.Where(e => _seededCampIds.Contains(e.CampId)).ExecuteDeleteAsync();
        await _db.Camps.Where(c => _seededCampIds.Contains(c.Id)).ExecuteDeleteAsync();
        await _db.DisposeAsync();
    }

    private async Task SeedHistoryAsync(int firstYear, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var camp = new Camp
            {
                Id = Guid.NewGuid(),
                Name = $"Query Count Venue {Guid.NewGuid():N}",
                Location = "Burgos",
                Latitude = 43m,
                Longitude = -3m,
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _seededCampIds.Add(camp.Id);

            var year = firstYear + i;
            _db.Camps.Add(camp);
            _db.CampEditions.Add(new CampEdition
            {
                Id = Guid.NewGuid(),
                CampId = camp.Id,
                Year = year,
                StartDate = new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(year, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                PricePerAdult = 180m,
                PricePerChild = 120m,
                PricePerBaby = 60m,
                Status = CampEditionStatus.Completed,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetHistoryAsync_IssuesAFixedNumberOfQueries()
    {
        _interceptor.Reset();

        await _sut.GetHistoryAsync();

        // One read of the editions, one photo count rollup, one preview rollup.
        _interceptor.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetHistoryAsync_QueryCountDoesNotGrowWithTheNumberOfEditions()
    {
        _interceptor.Reset();
        await _sut.GetHistoryAsync();
        var withSmallHistory = _interceptor.Count;

        await SeedHistoryAsync(LargeHistoryFirstYear, LargeHistoryCount);

        _interceptor.Reset();
        var history = await _sut.GetHistoryAsync();
        var withLargeHistory = _interceptor.Count;

        history.Should().HaveCountGreaterThanOrEqualTo(SmallHistoryCount + LargeHistoryCount);
        withLargeHistory.Should().Be(withSmallHistory);
    }

    /// <summary>Counts the commands EF sends, so a hidden per-row query cannot slip in.</summary>
    private sealed class CommandCountingInterceptor : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
