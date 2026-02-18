# Backend Implementation Plan: feat-enhanced-health-check Enhanced Health Check

## Overview

Replace the trivial static `/health` endpoint with ASP.NET Core's built-in Health Checks middleware. This provides real dependency verification (PostgreSQL connectivity, Resend API key configuration) with a structured JSON response compatible with Kubernetes probes and monitoring tools.

This feature lives in `Common/` since it is a cross-cutting infrastructure concern, not a domain feature slice. No entity, service layer, or repository is required — only a custom `IHealthCheck` implementation and `Program.cs` registration changes.

---

## Architecture Context

### Feature slice involved
This is **not** a domain feature. Changes go in:
- `src/Abuvi.API/Common/HealthChecks/` — new folder (cross-cutting infrastructure)
- `src/Abuvi.API/Program.cs` — service registration and middleware

### Files to create
```
src/Abuvi.API/Common/HealthChecks/ResendHealthCheck.cs
```

### Files to modify
```
src/Abuvi.API/Program.cs
src/Abuvi.API/Abuvi.API.csproj           (add NuGet package)
src/Abuvi.Tests/Integration/HealthCheckTests.cs  (update existing test to new format)
```

### New test files to create
```
src/Abuvi.Tests/Unit/Common/HealthChecks/ResendHealthCheckTests.cs
```

### Cross-cutting concerns
- No middleware changes
- No authentication: `/health` must be publicly accessible (anonymous)
- No new EF Core migrations (no schema changes)

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch name**: `feature/feat-enhanced-health-check-backend`
- **Implementation Steps**:
  1. Ensure you are on `main`: `git checkout main`
  2. Pull latest: `git pull origin main`
  3. Create branch: `git checkout -b feature/feat-enhanced-health-check-backend`
  4. Verify: `git branch`

---

### Step 1: Install NuGet Package

- **File**: `src/Abuvi.API/Abuvi.API.csproj`
- **Action**: Add the community `AspNetCore.HealthChecks.NpgSql` package for PostgreSQL connectivity check
- **Command**:
  ```bash
  dotnet add src/Abuvi.API/Abuvi.API.csproj package AspNetCore.HealthChecks.NpgSql
  ```
- **Result in .csproj**:
  ```xml
  <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
  ```
- **Notes**:
  - This package (by Xabaril) is the standard community extension for Npgsql health checks
  - It internally executes a `SELECT 1` query against the provided connection string
  - It depends on `Npgsql`, which is already a transitive dependency via `Npgsql.EntityFrameworkCore.PostgreSQL`
  - No additional `using` directive needed in `Program.cs` — the `AddNpgSql()` extension method is auto-discovered via the package's namespace

---

### Step 2: Write Unit Tests FIRST (TDD — RED phase)

- **File**: `src/Abuvi.Tests/Unit/Common/HealthChecks/ResendHealthCheckTests.cs`
- **Action**: Create test file with all scenarios **before** implementing `ResendHealthCheck`
- **Namespace**: `Abuvi.Tests.Unit.Common.HealthChecks`
- **Required usings**:
  ```csharp
  using Abuvi.API.Common.HealthChecks;
  using FluentAssertions;
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Diagnostics.HealthChecks;
  using Xunit;
  ```
- **Implementation**:

```csharp
namespace Abuvi.Tests.Unit.Common.HealthChecks;

using Abuvi.API.Common.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

public class ResendHealthCheckTests
{
    private static HealthCheckContext CreateContext(ResendHealthCheck check)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "resend", check, HealthStatus.Degraded, null)
        };

    [Fact]
    public async Task CheckHealthAsync_WhenApiKeyIsConfigured_ReturnsHealthy()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Resend:ApiKey", "re_abc123")])
            .Build();
        var sut = new ResendHealthCheck(config);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Resend API key is configured");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckHealthAsync_WhenApiKeyIsMissingOrEmpty_ReturnsUnhealthy(string? apiKey)
    {
        // Arrange
        var inMemory = apiKey is null
            ? Array.Empty<KeyValuePair<string, string?>>()
            : [new KeyValuePair<string, string?>("Resend:ApiKey", apiKey)];

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();
        var sut = new ResendHealthCheck(config);

        // Act
        var result = await sut.CheckHealthAsync(CreateContext(sut));

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Resend API key is not configured");
    }
}
```

- **TDD notes**:
  - Run `dotnet test` at this point — tests will **fail to compile** (ResendHealthCheck does not exist yet). That is expected (RED).
  - Do NOT implement `ResendHealthCheck` until tests compile and fail at runtime.

---

### Step 3: Implement `ResendHealthCheck` (TDD — GREEN phase)

- **File**: `src/Abuvi.API/Common/HealthChecks/ResendHealthCheck.cs`
- **Action**: Create the custom health check for Resend API key validation
- **Required usings**:
  ```csharp
  using Microsoft.Extensions.Configuration;
  using Microsoft.Extensions.Diagnostics.HealthChecks;
  ```
- **Implementation**:

```csharp
namespace Abuvi.API.Common.HealthChecks;

public class ResendHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Resend:ApiKey"];

        return string.IsNullOrWhiteSpace(apiKey)
            ? Task.FromResult(HealthCheckResult.Unhealthy("Resend API key is not configured"))
            : Task.FromResult(HealthCheckResult.Healthy("Resend API key is configured"));
    }
}
```

- **Notes**:
  - This check is **synchronous** (configuration-only, no I/O). Wrapping in `Task.FromResult` is correct.
  - No network call is made — this only validates that the key is configured in `appsettings`.
  - Do NOT return the actual API key value in the description (security).
  - Run `dotnet test` — the two new unit tests should now pass (GREEN).

---

### Step 4: Register Health Checks in `Program.cs`

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Replace the static `/health` endpoint with the standard health check infrastructure

#### 4a. Add service registration (services section, after existing service registrations)

Place this block **before** `var app = builder.Build();`, after the email service registration block:

```csharp
// ========================================
// Health Checks
// ========================================
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString,
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "postgresql"],
        timeout: TimeSpan.FromSeconds(5))
    .AddCheck<ResendHealthCheck>(
        name: "resend",
        failureStatus: HealthStatus.Degraded,
        tags: ["email"]);
```

**Required using** — add at the top of `Program.cs`:
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
```

#### 4b. Remove the old static endpoint

**Delete** these lines (currently lines 236–238 in `Program.cs`):
```csharp
// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");
```

#### 4c. Add health check middleware (after `app.UseAuthorization()`, before `app.UseSerilogRequestLogging()`)

```csharp
// Health check endpoint (anonymous access, no auth required)
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            entries = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    status = kvp.Value.Status.ToString(),
                    description = kvp.Value.Description,
                    duration = kvp.Value.Duration.ToString(),
                    data = kvp.Value.Data
                })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});
```

- **Notes**:
  - `app.MapHealthChecks` must NOT be behind `.RequireAuthorization()` — health endpoints must be publicly accessible
  - The custom `ResponseWriter` replaces the minimal default output with the structured JSON format
  - `timeout: TimeSpan.FromSeconds(5)` on the NpgSql check prevents DB latency from hanging the endpoint
  - `failureStatus: HealthStatus.Degraded` on ResendHealthCheck means if the check **throws an exception** (not returns Unhealthy), it is treated as Degraded — appropriate since email is non-critical

---

### Step 5: Update Existing Integration Test

- **File**: `src/Abuvi.Tests/Integration/HealthCheckTests.cs`
- **Action**: Update the existing test to match the new response format
- **Why**: The old test checks `content.Should().Contain("healthy")` (lowercase). The new response returns `"status":"Healthy"` (PascalCase). The test will fail post-migration.

Replace the existing content with:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using Xunit;
using System.Text.Json;

namespace Abuvi.Tests.Integration;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Endpoint_Returns_200_WithStructuredResponse()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"status\"");
        content.Should().Contain("\"entries\"");
        content.Should().Contain("\"database\"");
        content.Should().Contain("\"resend\"");
    }

    [Fact]
    public async Task Health_Endpoint_Returns_ValidJson()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var act = () => JsonDocument.Parse(content);
        act.Should().NotThrow("response must be valid JSON");
    }
}
```

- **Notes**:
  - The integration test cannot fully verify database health (it uses `WebApplicationFactory` which doesn't connect to a real DB)
  - The test verifies the structural shape of the response: that it is JSON with `status`, `entries`, `database`, and `resend` keys
  - The HTTP status will be `503` if the database check fails (which it will in test environment without a real DB)
  - Consider marking this test with `[Trait("Category", "Integration")]` if you later add CI filtering
  - **Important**: If `WebApplicationFactory` uses `UseInMemoryDatabase` override, the `AddNpgSql` check will still try to connect to the real connection string. See the Notes section below for how to handle this.

---

### Step 6: Update Technical Documentation

- **File**: `ai-specs/specs/api-endpoints.md` — add entry for the new `/health` format
- **Action**: Review and update to document the new structured health check response
- **Implementation Steps**:
  1. Open `ai-specs/specs/api-endpoints.md`
  2. Find the existing `/health` endpoint documentation (if present)
  3. Update it to reflect the new request/response format, HTTP status codes (200 / 503), and the dependency entries (`database`, `resend`)
  4. All documentation must be in English

---

## Implementation Order

1. **Step 0** — Create feature branch `feature/feat-enhanced-health-check-backend`
2. **Step 1** — Install `AspNetCore.HealthChecks.NpgSql` NuGet package
3. **Step 2** — Write unit tests for `ResendHealthCheck` (RED — tests fail to compile)
4. **Step 3** — Implement `ResendHealthCheck.cs` (GREEN — tests pass)
5. **Step 4** — Register health checks and update `Program.cs` (replace static endpoint)
6. **Step 5** — Update existing integration test `HealthCheckTests.cs`
7. **Step 6** — Update `api-endpoints.md` documentation

---

## Testing Checklist

### Unit tests (Step 2–3)
- [x] `CheckHealthAsync_WhenApiKeyIsConfigured_ReturnsHealthy`
- [x] `CheckHealthAsync_WhenApiKeyIsMissingOrEmpty_ReturnsUnhealthy` — Theory with `[null, "", "   "]`

### Integration tests (Step 5)
- [ ] `Health_Endpoint_Returns_200_WithStructuredResponse`
- [ ] `Health_Endpoint_Returns_ValidJson`

### Manual verification (post-implementation)
- [ ] `GET /health` returns JSON with `status`, `entries`, `database`, `resend`
- [ ] When DB is available: HTTP 200, `status: "Healthy"`
- [ ] When DB is down: HTTP 503, `status: "Unhealthy"`
- [ ] When Resend key not set: HTTP 200, `status: "Degraded"` with `resend.status: "Unhealthy"`
- [ ] Swagger still works (UI not affected)
- [ ] No authentication required on `/health`

---

## Error Response Format

The health check endpoint uses its own structured format (not `ApiResponse<T>`):

```json
// HTTP 200 — All checks pass
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0523416",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "Host=localhost;Database=abuvi",
      "duration": "00:00:00.0412341",
      "data": {}
    },
    "resend": {
      "status": "Healthy",
      "description": "Resend API key is configured",
      "duration": "00:00:00.0000123",
      "data": {}
    }
  }
}

// HTTP 503 — Database down
{
  "status": "Unhealthy",
  "totalDuration": "00:00:05.0001234",
  "entries": {
    "database": {
      "status": "Unhealthy",
      "description": "Exception during check: ...",
      "duration": "00:00:05.0001123",
      "data": {}
    }
  }
}
```

HTTP status code mapping:
| Overall Status | HTTP Code |
|---------------|-----------|
| Healthy       | 200       |
| Degraded      | 200       |
| Unhealthy     | 503       |

---

## Dependencies

### New NuGet packages

| Package | Version | Reason |
|---------|---------|--------|
| `AspNetCore.HealthChecks.NpgSql` | `9.*` | PostgreSQL connectivity check via `SELECT 1` |

### No new packages in test project needed
- `Microsoft.Extensions.Configuration` is already available transitively
- `Microsoft.Extensions.Diagnostics.HealthChecks` is part of `Microsoft.AspNetCore.App`

### No EF Core migration
This feature adds no database schema changes.

---

## Notes

### Integration test caveat: DB health check in `WebApplicationFactory`
The `WebApplicationFactory<Program>` in tests will try to run the NpgSql health check against the real PostgreSQL connection string. In CI or test environments without PostgreSQL, the health endpoint will return `503 Unhealthy`.

**Options**:
1. **Accept `503`** in the integration test (just verify response is JSON, not HTTP status) — simplest approach
2. **Override `AddHealthChecks`** in `WithWebHostBuilder` to remove DB check in tests
3. **Use Testcontainers** — starts a real PostgreSQL instance (overkill for this feature)

**Recommended**: Option 1 — change the assertion from `response.Should().BeSuccessful()` to `response.Content.ReadAsStringAsync()` and verify JSON structure only, regardless of HTTP status.

### Security
- `/health` endpoint must NOT be placed behind `.RequireAuthorization()`
- Do NOT log or return actual connection string credentials or API key values in the response
- The `description` field from NpgSql may include partial connection info — verify in dev environment

### Failure status strategy
| Check    | `failureStatus` (exception) | Explicit return |
|----------|-----------------------------|-----------------|
| Database | `Unhealthy`                 | N/A (library handles it) |
| Resend   | `Degraded`                  | `Unhealthy` when key missing |

The `failureStatus` is only used when the health check **throws an exception** (unexpected). The explicit `HealthCheckResult.Unhealthy(...)` return is for the known "key not configured" case.

---

## Next Steps After Implementation

1. Verify all existing tests still pass: `dotnet test src/Abuvi.Tests`
2. Verify the build compiles cleanly: `dotnet build src/Abuvi.API`
3. Manually test the `/health` endpoint with the dev server running against real PostgreSQL

---

## Implementation Verification

- [ ] **Code Quality**: No `TreatWarningsAsErrors` violations; nullable reference types enabled and respected
- [ ] **Functionality**: `GET /health` returns JSON with `status`, `entries.database`, `entries.resend`; HTTP 503 when DB is unreachable
- [ ] **Testing**: Unit tests pass (`ResendHealthCheckTests` — 4 test cases via Theory); Integration test updated and passing
- [ ] **No Breaking Changes**: No migration required, no existing API contracts changed
- [ ] **Documentation**: `api-endpoints.md` updated with new `/health` response format
- [ ] **Security**: Endpoint is anonymous; no sensitive data in response descriptions
