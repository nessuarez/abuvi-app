# Backend Implementation Plan: fix-seq-logging-visibility — Corregir visibilidad de eventos en Seq

## Overview

La aplicación solo muestra eventos de healthcheck en Seq durante el despliegue. La causa raíz es que `UseSerilogRequestLogging()` está posicionado después de middlewares que pueden cortocircuitar peticiones (CORS, HTTPS redirect, auth), y la configuración de niveles de Serilog está hardcodeada en C# ignorando `appsettings.json`. Este plan corrige el pipeline de middlewares, habilita configuración dinámica de niveles y reduce el ruido del healthcheck.

## Architecture Context

- **Feature slice**: Cross-cutting concern (middleware pipeline y configuración de logging)
- **Archivos a modificar**:
  - `src/Abuvi.API/Program.cs` — Pipeline de middlewares y configuración de Serilog
- **Archivos a crear**:
  - Ninguno (no se requiere `appsettings.Production.json` en el repo; la URL de Seq en despliegue se gestiona por variables de entorno)
- **Cross-cutting concerns**: Middleware pipeline order, logging configuration, Seq sink

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/fix-seq-logging-visibility-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/fix-seq-logging-visibility-backend`
  4. Verify branch creation: `git branch`

---

### Step 1: Move `UseSerilogRequestLogging()` to the beginning of the middleware pipeline

- **File**: `src/Abuvi.API/Program.cs` (lines 291-332)
- **Action**: Relocate `UseSerilogRequestLogging()` so it's the **first middleware** after development-only middlewares (Swagger). This ensures ALL HTTP requests are logged, including those rejected by CORS, HTTPS redirect, or auth.

- **Implementation Steps**:
  1. **Cut** the entire `UseSerilogRequestLogging(...)` block (lines 307-332) from its current position
  2. **Paste** it immediately after the Swagger conditional block (after line 296), BEFORE `GlobalExceptionMiddleware`
  3. **Add healthcheck filter** inside the options to suppress `/health` noise using `GetLevel`:

```csharp
// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serilog HTTP request logging — MUST be first to capture all requests
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    // Suppress healthcheck logs (demote to Verbose so they're below minimum level)
    options.GetLevel = (httpContext, elapsed, ex) =>
        httpContext.Request.Path.StartsWithSegments("/health")
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        if (!string.IsNullOrEmpty(httpContext.Request.Host.Value))
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        }
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        if (!string.IsNullOrEmpty(httpContext.Request.Headers.UserAgent.ToString()))
        {
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        }

        // Add user ID if authenticated
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var userId = httpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                diagnosticContext.Set("UserId", userId);
            }
        }
    };
});

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();
app.UseHttpsRedirection();

// Authentication and Authorization middleware (order matters!)
app.UseAuthentication();
app.UseAuthorization();
```

- **Implementation Notes**:
  - The `EnrichDiagnosticContext` callback runs when the response flows back up through the pipeline. Since `UseAuthentication()` is downstream, the `User.Identity` will already be populated when the enrichment evaluates. No change needed in the enrichment logic.
  - The `GetLevel` callback demotes `/health` requests to `Verbose` level. Since the minimum level is `Information`, these events are effectively discarded.

---

### Step 2: Replace hardcoded Serilog levels with `ReadFrom.Configuration()`

- **File**: `src/Abuvi.API/Program.cs` (lines 33-73)
- **Action**: Replace the hardcoded `.MinimumLevel.Information()` and `.MinimumLevel.Override(...)` calls with `.ReadFrom.Configuration(builder.Configuration)` so log levels can be adjusted via `appsettings.json` or environment variables without recompiling.

- **Implementation Steps**:
  1. **Remove** lines 34-36 (the three hardcoded minimum level lines):
     ```csharp
     // REMOVE these lines:
     .MinimumLevel.Information()
     .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
     .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
     ```
  2. **Add** `.ReadFrom.Configuration(builder.Configuration)` as the first chained call:
     ```csharp
     Log.Logger = new LoggerConfiguration()
         .ReadFrom.Configuration(builder.Configuration)
         .Enrich.FromLogContext()
         .Enrich.WithMachineName()
         // ... rest unchanged ...
     ```
  3. **Verify** `appsettings.json` already has the correct Serilog section (it does — lines 2-9)
  4. **Verify** `appsettings.Development.json` already has override levels for Debug (it does — lines 2-9)

- **Dependencies**: `Serilog.Settings.Configuration` — this package is already included transitively via `Serilog.AspNetCore` v9.0.0. No new NuGet install needed.

- **Result** — the final Serilog configuration block:

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)

    // Enrich with contextual information
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithClientIp()
    .Enrich.WithCorrelationId()

    // Write to Console for development
    .WriteTo.Console()

    // Write to PostgreSQL (async with batching for performance)
    .WriteTo.Async(a => a.PostgreSQL(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        tableName: "logs",
        needAutoCreateTable: true,
        columnOptions: new Dictionary<string, ColumnWriterBase>
        {
            { "message", new RenderedMessageColumnWriter(NpgsqlDbType.Text) },
            { "message_template", new MessageTemplateColumnWriter(NpgsqlDbType.Text) },
            { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
            { "timestamp", new TimestampColumnWriter(NpgsqlDbType.TimestampTz) },
            { "exception", new ExceptionColumnWriter(NpgsqlDbType.Text) },
            { "log_event", new LogEventSerializedColumnWriter(NpgsqlDbType.Jsonb) },
            { "properties", new PropertiesColumnWriter(NpgsqlDbType.Jsonb) },
            { "user_id", new SinglePropertyColumnWriter("UserId", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar) },
            { "client_ip", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar) },
            { "correlation_id", new SinglePropertyColumnWriter("CorrelationId", PropertyWriteMethod.ToString, NpgsqlDbType.Varchar) }
        },
        batchSizeLimit: 100,
        period: TimeSpan.FromSeconds(5)
    ))

    // Write to Seq for UI
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")

    .CreateLogger();
```

- **Implementation Notes**:
  - `ReadFrom.Configuration()` reads the `Serilog:MinimumLevel:Default` and `Serilog:MinimumLevel:Override` keys from `appsettings.json`
  - In deployment, levels can be overridden via environment variables: `Serilog__MinimumLevel__Default=Warning`
  - The enrichers and sinks are kept in code (not moved to config) because they have complex C# object configuration (PostgreSQL column writers)

---

### Step 3: Verify and clean up unused Serilog imports

- **File**: `src/Abuvi.API/Program.cs`
- **Action**: Verify that the `using Serilog.Events;` import is still needed (yes, it's used for `LogEventLevel.Verbose` and `LogEventLevel.Information` in the `GetLevel` callback). No cleanup needed.

---

### Step 4: Run tests and verify build

- **Action**: Build the project and run all existing tests to ensure no regressions
- **Implementation Steps**:
  1. `dotnet build src/Abuvi.API`
  2. `dotnet test` (run all tests)
  3. Verify no warnings related to logging configuration

---

### Step 5: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. **Review Changes**: Analyze the two code changes (middleware reorder + ReadFrom.Configuration)
  2. **Update `ai-specs/specs/backend-standards.mdc`**:
     - Update the logging/middleware section to document the correct middleware pipeline order
     - Note that Serilog levels are configured via `appsettings.json` (not hardcoded)
     - Document the healthcheck log suppression pattern
  3. **Verify Documentation**: Confirm changes are accurately reflected
  4. **Report Updates**: Document which files were updated

---

## Implementation Order

1. **Step 0**: Create feature branch `feature/fix-seq-logging-visibility-backend`
2. **Step 1**: Move `UseSerilogRequestLogging()` + add healthcheck filter
3. **Step 2**: Replace hardcoded levels with `ReadFrom.Configuration()`
4. **Step 3**: Verify imports (quick check)
5. **Step 4**: Build and run tests
6. **Step 5**: Update technical documentation

---

## Testing Checklist

- [ ] `dotnet build` passes without errors or warnings
- [ ] All existing unit tests pass (`dotnet test`)
- [ ] **Manual verification in development**:
  - Start the API locally
  - Navigate to several endpoints (e.g., `/api/camps`, `/api/auth/login`)
  - Verify logs appear in the console with the format: `HTTP GET /api/camps responded 200 in X ms`
  - Verify `/health` requests do NOT appear in console logs
- [ ] **Manual verification in Seq** (if local Seq is running):
  - Open Seq UI at `http://localhost:80` (or configured port)
  - Confirm API request logs appear with structured properties
  - Confirm healthcheck logs are absent

---

## Error Response Format

No changes to error response format. This ticket only affects logging infrastructure.

---

## Dependencies

- **No new NuGet packages required** — `Serilog.Settings.Configuration` is already included via `Serilog.AspNetCore` v9.0.0
- **No EF Core migrations** — no schema changes

---

## Notes

- **No breaking changes**: This is a purely internal infrastructure change. No API contracts, DTOs, or endpoints are modified.
- **Deployment consideration**: After deploying, verify in Seq that API request logs appear. If they still don't, check:
  1. `Seq:ServerUrl` environment variable is correctly set in the deployment environment
  2. The Seq server is reachable from the API container/server (network/firewall)
  3. There are no Seq UI filters active that hide events
- **Middleware order is critical**: `UseSerilogRequestLogging()` MUST remain as the first middleware (after Swagger) to capture all requests. Document this in code comments to prevent future regressions.
- **Environment variable override**: In deployment, log levels can be tuned without redeploying via: `Serilog__MinimumLevel__Default=Debug`

---

## Next Steps After Implementation

1. Deploy to staging/production
2. Verify in Seq that API request logs now appear when navigating the application
3. Confirm healthcheck noise is eliminated
4. If needed, fine-tune log levels via environment variables in the deployment config