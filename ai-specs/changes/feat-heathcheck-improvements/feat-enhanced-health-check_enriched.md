# Enhanced Health Check — Enriched User Story

**Task ID**: `feat-enhanced-health-check`
**Date**: 2026-02-18
**Status**: Ready for implementation

---

## Problem Statement

The current health check endpoint is a trivial static response that provides no real signal about system health:

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
```

This always returns `200 OK` even when the database is down or external services are misconfigured. Monitoring tools, Kubernetes probes, and on-call engineers get a false positive.

---

## Solution: ASP.NET Core Built-in Health Checks

Use **`Microsoft.AspNetCore.Diagnostics.HealthChecks`** — the .NET standard approach. This is already included in `Microsoft.AspNetCore.App` (no extra package for the base infrastructure). It provides:

- A standard `IHealthCheck` interface for custom checks
- Built-in middleware that registers a `/health` endpoint
- Per-check statuses (`Healthy`, `Degraded`, `Unhealthy`)
- Per-check duration and description in the response
- HTTP 200 for Healthy/Degraded, HTTP 503 for Unhealthy
- Native Kubernetes liveness/readiness probe support

### Standard vs Custom Status Page

The standard JSON format from this middleware is the **industry standard** and is supported out-of-the-box by:

- Kubernetes liveness/readiness probes
- Azure Monitor / Application Insights
- AWS CloudWatch
- Grafana + Prometheus (via `prometheus-net.AspNetCore`)
- Any monitoring tool that understands HTTP health check endpoints

A dedicated status page UI (`AspNetCore.HealthChecks.UI`) exists but is **out of scope** for this ticket — the JSON endpoint is sufficient.

---

## Acceptance Criteria

1. `GET /health` returns a structured JSON response with the status of each dependency
2. The database connectivity check performs a real query against PostgreSQL
3. The Resend service check verifies the API key is configured (non-empty)
4. The overall HTTP status is `200` when all checks pass and `503` when any check fails
5. Each check entry includes: `status`, `description`, and `duration`
6. Unit tests cover: happy path, database failure, and Resend misconfiguration scenarios

---

## Endpoints

### `GET /health`

Full health check. Used by monitoring tools and manual inspection.

**Response — All healthy (HTTP 200):**

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0523416",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "PostgreSQL connection verified",
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
```

**Response — Database down (HTTP 503):**

```json
{
  "status": "Unhealthy",
  "totalDuration": "00:00:05.0001234",
  "entries": {
    "database": {
      "status": "Unhealthy",
      "description": "Cannot connect to PostgreSQL",
      "duration": "00:00:05.0001123",
      "data": {}
    },
    "resend": {
      "status": "Healthy",
      "description": "Resend API key is configured",
      "duration": "00:00:00.0000101",
      "data": {}
    }
  }
}
```

---

## Files to Create / Modify

### New files

```
src/Abuvi.API/Common/HealthChecks/
├── ResendHealthCheck.cs        # Custom IHealthCheck for Resend config validation
```

### Modified files

```
src/Abuvi.API/Program.cs        # Replace manual /health endpoint with middleware
```

### New NuGet package

```
AspNetCore.HealthChecks.NpgSql  # PostgreSQL connectivity check
```

Install via:

```bash
dotnet add src/Abuvi.API/Abuvi.API.csproj package AspNetCore.HealthChecks.NpgSql
```

---

## Implementation Details

### 1. `ResendHealthCheck.cs`

Location: `src/Abuvi.API/Common/HealthChecks/ResendHealthCheck.cs`

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

### 2. `Program.cs` changes

**Remove** the manual health check endpoint:

```csharp
// REMOVE THIS:
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");
```

**Add** health check registration in the services section:

```csharp
// After the existing service registrations:
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString,
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "postgresql"])
    .AddCheck<ResendHealthCheck>(
        name: "resend",
        failureStatus: HealthStatus.Degraded,  // Degraded, not Unhealthy — app still works without email
        tags: ["email"]);
```

**Add** health check middleware after `app.Build()`:

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

> **Note on `UIResponseWriter`**: This requires `AspNetCore.HealthChecks.UI.Client` for the rich JSON format shown above. Without it, the default response is minimal. The alternative is to write a custom `ResponseWriter` using `HealthReportEntry` — see below.

#### Option A: Minimal dependency (custom response writer, no extra package)

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
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

#### Option B: Use `AspNetCore.HealthChecks.UI.Client` (standard rich format)

Install:

```bash
dotnet add src/Abuvi.API/Abuvi.API.csproj package AspNetCore.HealthChecks.UI.Client
```

Then:

```csharp
using HealthChecks.UI.Client;

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

**Recommended**: Option A (no extra package) since a custom writer is simple and avoids an unnecessary dependency.

---

## Failure Status Strategy

| Check     | Failure Status | Rationale |
|-----------|---------------|-----------|
| Database  | `Unhealthy`   | App cannot function without the DB |
| Resend    | `Degraded`    | App works but email sending will fail; ops should be alerted but it's not critical |

- `Healthy` → HTTP 200
- `Degraded` → HTTP 200 (by default, can be overridden to 200 or a custom code)
- `Unhealthy` → HTTP 503

> To make `Degraded` also return `503`, configure `ResultStatusCodes` in `HealthCheckOptions`:
>
> ```csharp
> ResultStatusCodes = {
>     [HealthStatus.Healthy] = StatusCodes.Status200OK,
>     [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
>     [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
> }
> ```

---

## TDD Steps

### Test file location

`tests/Abuvi.Tests/Unit/Common/HealthChecks/ResendHealthCheckTests.cs`

### Test scenarios (write these FIRST, in order)

1. **RED** — `CheckHealthAsync_WhenApiKeyIsConfigured_ReturnsHealthy`
2. **GREEN** — Implement `ResendHealthCheck` minimally to pass
3. **RED** — `CheckHealthAsync_WhenApiKeyIsEmpty_ReturnsUnhealthy`
4. **GREEN** — Handle empty string case
5. **RED** — `CheckHealthAsync_WhenApiKeyIsWhitespace_ReturnsUnhealthy`
6. **GREEN** — Handle whitespace case (use `IsNullOrWhiteSpace`)
7. **RED** — `CheckHealthAsync_WhenApiKeyIsNull_ReturnsUnhealthy`
8. **GREEN** — Handle null case (already covered by `IsNullOrWhiteSpace`)

### Test structure (AAA)

```csharp
public class ResendHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenApiKeyIsConfigured_ReturnsHealthy()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Resend:ApiKey", "re_abc123")])
            .Build();
        var sut = new ResendHealthCheck(config);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("resend", sut, HealthStatus.Degraded, null)
        };

        // Act
        var result = await sut.CheckHealthAsync(context);

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
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("resend", sut, HealthStatus.Degraded, null)
        };

        // Act
        var result = await sut.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("Resend API key is not configured");
    }
}
```

> **Note**: The PostgreSQL check (`AddNpgSql`) comes from the `AspNetCore.HealthChecks.NpgSql` library and does not need unit tests — it's a well-tested community package.

---

## Security Considerations

- The `/health` endpoint **must not** be authenticated — monitoring tools need anonymous access
- The endpoint **must not** expose sensitive data: do not log or return the actual API key value, connection string credentials, etc.
- Consider restricting `/health` to internal network / VPN in production via network policies, not at the application level

---

## Non-Functional Requirements

- Each health check should have a **timeout** to prevent a slow DB from blocking the endpoint indefinitely

  ```csharp
  .AddNpgSql(connectionString, timeout: TimeSpan.FromSeconds(5), ...)
  ```

- The database check uses `SELECT 1` internally (from the `AspNetCore.HealthChecks.NpgSql` package) — this is extremely lightweight
- The Resend check is synchronous and configuration-only — no network call, so no timeout needed

---

## Future Extensibility (Out of Scope for This Ticket)

When new external services are added to the project, register a new `IHealthCheck` in `Common/HealthChecks/` and add it to `AddHealthChecks()` in `Program.cs`. No other changes needed.

Examples for future services:

- **Redsys** (payment gateway): Check that secret key and merchant code are configured
- **Google Places**: Check that API key is configured
- **Seq**: Optional degraded check if the log server is unreachable
