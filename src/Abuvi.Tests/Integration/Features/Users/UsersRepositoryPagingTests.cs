using Abuvi.API.Data;
using Abuvi.API.Features.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Abuvi.Tests.Integration.Features.Users;

/// <summary>
/// Repository tests for UsersRepository.GetPagedAsync on the EF InMemory provider.
/// Seeds 150 users so searches must reach beyond the legacy 100-user limit.
/// </summary>
public class UsersRepositoryPagingTests : IAsyncLifetime
{
    private const int SeedCount = 150;
    private readonly AbuviDbContext _dbContext;
    private readonly UsersRepository _repository;

    public UsersRepositoryPagingTests()
    {
        var options = new DbContextOptionsBuilder<AbuviDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AbuviDbContext(options);
        _repository = new UsersRepository(_dbContext);
    }

    public async Task InitializeAsync()
    {
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var users = new List<User>();

        for (var i = 0; i < SeedCount; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"member{i:000}@example.com",
                FirstName = $"First{i:000}",
                LastName = $"Last{i:000}",
                Role = i % 10 == 0 ? UserRole.Board : UserRole.Member,
                IsActive = i % 3 != 0,
                CreatedAt = baseTime.AddMinutes(i),
                UpdatedAt = baseTime.AddMinutes(i)
            });
        }

        // Special users: the newest ones (index 149 and 148 are replaced) and wildcard look-alikes
        users[149].Email = "ana.garcia@example.com";
        users[149].FirstName = "Ana";
        users[149].LastName = "García";
        users[148].Email = "ANA@upper.com";
        users[147].Email = "user_1@example.com";
        users[146].Email = "userX1@example.com";
        users[145].Email = "user%x@example.com";

        _dbContext.Users.AddRange(users);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _dbContext.DisposeAsync();

    private Task<(List<User> Items, int TotalCount)> Query(
        int page = 1, int pageSize = 20, string? search = null, UserRole? role = null,
        bool? isActive = null, string? sortBy = null, string? sortOrder = null)
        => _repository.GetPagedAsync(page, pageSize, search, role, isActive, sortBy, sortOrder);

    [Fact]
    public async Task Search_ByEmailFragment_FindsUserBeyondFirst100()
    {
        var (items, total) = await Query(search: "ana.garcia");

        total.Should().Be(1);
        items.Should().ContainSingle().Which.FirstName.Should().Be("Ana");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var (items, total) = await Query(search: "ana@UPPER");

        total.Should().Be(1);
        items.Single().Email.Should().Be("ANA@upper.com");
    }

    [Fact]
    public async Task Search_ByFirstName()
    {
        var (items, _) = await Query(search: "ana");

        items.Should().Contain(u => u.Email == "ana.garcia@example.com");
    }

    [Fact]
    public async Task Search_ByLastName()
    {
        var (items, total) = await Query(search: "garcía");

        total.Should().Be(1);
        items.Single().Email.Should().Be("ana.garcia@example.com");
    }

    [Fact]
    public async Task Search_ByFullName()
    {
        var (items, total) = await Query(search: "ana garcía");

        total.Should().Be(1);
        items.Single().Email.Should().Be("ana.garcia@example.com");
    }

    [Fact]
    public async Task Search_TrimsWhitespace()
    {
        var (items, total) = await Query(search: "  ana.garcia  ");

        total.Should().Be(1);
        items.Single().Email.Should().Be("ana.garcia@example.com");
    }

    [Fact]
    public async Task Search_WithLikeWildcards_TreatedLiterally()
    {
        var (underscore, underscoreTotal) = await Query(search: "user_1");
        var (percent, percentTotal) = await Query(search: "user%x");

        underscoreTotal.Should().Be(1);
        underscore.Single().Email.Should().Be("user_1@example.com");
        percentTotal.Should().Be(1);
        percent.Single().Email.Should().Be("user%x@example.com");
    }

    [Fact]
    public async Task Filter_ByRole_ReturnsOnlyThatRole()
    {
        var (items, total) = await Query(pageSize: 100, role: UserRole.Board);

        total.Should().BeGreaterThan(0);
        items.Should().OnlyContain(u => u.Role == UserRole.Board);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Filter_ByIsActive_ReturnsOnlyMatching(bool isActive)
    {
        var (items, total) = await Query(pageSize: 100, isActive: isActive);

        total.Should().BeGreaterThan(0);
        items.Should().OnlyContain(u => u.IsActive == isActive);
    }

    [Fact]
    public async Task Paging_ReturnsRequestedSliceAndTotal()
    {
        var (page1, _) = await Query(page: 1);
        var (page2, total) = await Query(page: 2);

        page2.Should().HaveCount(20);
        total.Should().Be(SeedCount);
        page2.Select(u => u.Id).Should().NotIntersectWith(page1.Select(u => u.Id));
    }

    [Fact]
    public async Task Paging_LastPagePartial()
    {
        var (items, total) = await Query(page: 8);

        items.Should().HaveCount(10);
        total.Should().Be(SeedCount);
    }

    [Fact]
    public async Task Sort_DefaultIsCreatedAtDesc()
    {
        var (items, _) = await Query();

        items.First().Email.Should().Be("ana.garcia@example.com");
        items.Select(u => u.CreatedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Sort_ByEmailAsc_OrdersByEmail()
    {
        var (items, _) = await Query(search: "member", sortBy: "email", sortOrder: "asc");

        items.Select(u => u.Email).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Sort_ByEmailDesc_IsCaseInsensitiveOnParameters()
    {
        var (items, _) = await Query(search: "member", sortBy: "EMAIL", sortOrder: "DESC");

        items.Select(u => u.Email).Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task Sort_UnknownField_FallsBackToDefault()
    {
        var (defaultItems, _) = await Query();
        var (unknownItems, _) = await Query(sortBy: "passwordHash", sortOrder: "sideways");

        unknownItems.Select(u => u.Id).Should().Equal(defaultItems.Select(u => u.Id));
    }
}
