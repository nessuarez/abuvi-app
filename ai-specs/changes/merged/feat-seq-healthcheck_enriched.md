# User Story: Add Seq Health Check

**ID**: feat-seq-healthcheck
**Branch**: `feature/feat-seq-healthcheck-backend`

---

## Summary

Add a Seq health check to the `/health` endpoint that verifies whether the Seq log server is actually **reachable over the network**, not just configured. This allows monitoring tools and operators to detect when the logging infrastructure is unavailable.

---

## Context & Motivation

The `/health` endpoint currently checks:
- `database` — PostgreSQL connectivity (NpgSql)
- `resend` — Resend API key presence
- `google-places` — Google Places API key presence

The application already sends structured logs to a Seq server (`Seq:ServerUrl` in `appsettings.json`, default: `http://localhost:5341`). If Seq is down, logs are silently lost with no observable signal. This health check makes that failure visible.

---

## Existing Patterns to Follow

- **Health check class**: `src/Abuvi.API/Common/HealthChecks/` (e.g., `ResendHealthCheck.cs`, `GooglePlacesHealthCheck.cs`)
- **Registration**: `Program.cs` lines 217–231, inside `builder.Services.AddHealthChecks()`
- **Unit tests**: `src/Abuvi.Tests/Unit/Common/HealthChecks/` (follow `ResendHealthCheckTests.cs` pattern)
- **Integration test**: `src/Abuvi.Tests/Integration/HealthCheckTests.cs`

---

## Functional Requirements

### Health Check Behaviour

| Scenario | Result |
|---|---|
| `Seq:ServerUrl` is missing or empty | `Degraded` — "Seq server URL is not configured" |
| HTTP GET to `{Seq:ServerUrl}/api` returns any 2xx | `Healthy` — "Seq is reachable" |
| HTTP GET times out (> 5 s) or throws network error | `Degraded` — "Seq is unreachable: {reason}" |
| HTTP GET returns non-2xx status | `Degraded` — "Seq returned unexpected status {statusCode}" |

**Why `Degraded` and not `Unhealthy`?** Seq is a logging sink — the application continues to function without it. `Degraded` correctly signals an issue without triggering hard-failure alerts.

**Reachability endpoint**: `GET {Seq:ServerUrl}/api` — this is Seq's public REST API root, which returns server metadata with HTTP 200 when the server is running. It requires no authentication.

### Health Check Registration

- **Name**: `"seq"`
- **FailureStatus**: `HealthStatus.Degraded`
- **Tags**: `["logging", "external"]`
- **Timeout**: `TimeSpan.FromSeconds(5)` (consistent with the `database` check)

---

## Technical Specification

### 1. Add NuGet Package — `IHttpClientFactory` (already available, no new package needed)

The standard `Microsoft.Extensions.Http` package is transitively included. No new NuGet dependency is required.

> **Alternative considered**: `AspNetCore.HealthChecks.Seq` community package. Rejected because the custom check is trivial, follows the existing project pattern, and avoids adding a dependency for ~10 lines of code.

### 2. New File: `SeqHealthCheck.cs`

**Path**: `src/Abuvi.API/Common/HealthChecks/SeqHealthCheck.cs`

```csharp
namespace Abuvi.API.Common.HealthChecks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class SeqHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var serverUrl = configuration["Seq:ServerUrl"];

        if (string.IsNullOrWhiteSpace(serverUrl))
            return HealthCheckResult.Degraded("Seq server URL is not configured");

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync($"{serverUrl.TrimEnd('/')}/api", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Seq is reachable")
                : HealthCheckResult.Degraded($"Seq returned unexpected status {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Seq is unreachable: {ex.Message}");
        }
    }
}
```

### 3. Register in `Program.cs`

**File**: `src/Abuvi.API/Program.cs` (Health Checks section, lines ~217–231)

Add after `.AddCheck<GooglePlacesHealthCheck>(...)`:

```csharp
.AddCheck<SeqHealthCheck>(
    name: "seq",
    failureStatus: HealthStatus.Degraded,
    tags: ["logging", "external"])
```

Also register `IHttpClientFactory` if not already present (check if `builder.Services.AddHttpClient()` exists anywhere in Program.cs):

```csharp
builder.Services.AddHttpClient(); // Only if not already registered
```

### 4. Unit Tests

**Path**: `src/Abuvi.Tests/Unit/Common/HealthChecks/SeqHealthCheckTests.cs`

Test cases (TDD — write these first):

1. `CheckHealthAsync_WhenServerUrlIsNotConfigured_ReturnsDegraded`
2. `CheckHealthAsync_WhenServerUrlIsEmpty_ReturnsDegraded`
3. `CheckHealthAsync_WhenServerUrlIsWhitespace_ReturnsDegraded`
4. `CheckHealthAsync_WhenSeqResponds200_ReturnsHealthy`
5. `CheckHealthAsync_WhenSeqRespondsNon2xx_ReturnsDegraded`
6. `CheckHealthAsync_WhenSeqThrowsHttpRequestException_ReturnsDegraded`
7. `CheckHealthAsync_WhenSeqThrowsTaskCanceledException_ReturnsDegraded`

Use `NSubstitute` to mock `IHttpClientFactory` + `HttpMessageHandler`.

### 5. Update Integration Test

**File**: `src/Abuvi.Tests/Integration/HealthCheckTests.cs`

Update `Health_Endpoint_Returns_StructuredJsonResponse` to assert that `entries` also contains `"seq"`:

```csharp
entries.TryGetProperty("seq", out _).Should().BeTrue("entries must include 'seq' check");
```

---

## Files to Modify / Create

| Action | File |
|---|---|
| **Create** | `src/Abuvi.API/Common/HealthChecks/SeqHealthCheck.cs` |
| **Modify** | `src/Abuvi.API/Program.cs` — register `SeqHealthCheck` in health checks chain |
| **Create** | `src/Abuvi.Tests/Unit/Common/HealthChecks/SeqHealthCheckTests.cs` |
| **Modify** | `src/Abuvi.Tests/Integration/HealthCheckTests.cs` — assert `"seq"` entry exists |

---

## Non-Functional Requirements

- **No breaking changes**: Existing `/health` response shape is unchanged; a new `"seq"` key is added to `entries`.
- **No secrets**: The Seq server URL is not sensitive (it's a monitoring endpoint URL, not a credential).
- **Timeout**: 5 seconds maximum for the Seq reachability check (consistent with database check).
- **Test coverage**: All 7 test cases must pass. No integration test requires a live Seq instance.

---

## Definition of Done

- [ ] `SeqHealthCheck` created at `Common/HealthChecks/SeqHealthCheck.cs`
- [ ] `SeqHealthCheck` registered in `Program.cs` health checks chain with name `"seq"`, `Degraded` failure status, tags `["logging", "external"]`
- [ ] All 7 unit tests written (TDD: red → green) and passing
- [ ] Integration test updated to assert `"seq"` entry in response
- [ ] `dotnet test` passes with no new failures
- [ ] `/health` JSON response includes `"seq"` entry when app is running
