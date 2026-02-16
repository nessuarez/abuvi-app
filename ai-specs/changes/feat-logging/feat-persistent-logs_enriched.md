# Persistent Logging System - Enriched User Story

## Overview

Implement a comprehensive persistent logging system for the ABUVI application to enhance debugging capabilities, track user interactions, monitor system health, and maintain an audit trail for compliance. The system will store structured logs in PostgreSQL, provide centralized access for authorized personnel, and implement automatic retention policies.

## Problem Statement

Currently, the application lacks a persistent logging mechanism, making it difficult to:

- Investigate production issues after they occur
- Track user actions for audit trails and compliance (RGPD)
- Monitor application performance and error patterns over time
- Debug issues that only manifest in production environments

## Goals

1. Persist all application logs (errors, warnings, info, debug) to PostgreSQL database
2. Provide a searchable log viewer UI for Board/Admin users
3. Implement automatic log rotation and retention policies (90-day default)
4. Capture contextual information (user ID, IP address, request path, timestamp)
5. Ensure logs are structured and easily queryable
6. Maintain acceptable performance (logging should not block request processing)
7. Comply with RGPD requirements for sensitive data in logs

## Database Schema

### Log Entity

Add a new `Log` entity to the data model:

```csharp
// Data model structure
public class Log
{
    public Guid Id { get; set; }                    // Primary key
    public DateTime Timestamp { get; set; }          // When the log entry was created
    public string Level { get; set; }                // Log level: Debug, Info, Warning, Error, Critical
    public string Message { get; set; }              // Log message (max 4000 characters)
    public string? Source { get; set; }              // Source of the log (class/method name, max 500 chars)
    public string? Exception { get; set; }           // Exception details if present (max 8000 chars)
    public string? UserId { get; set; }              // User ID if authenticated (optional, FK -> User, max 100 chars)
    public string? IpAddress { get; set; }           // Client IP address (max 50 chars)
    public string? RequestPath { get; set; }         // HTTP request path (max 500 chars)
    public string? RequestMethod { get; set; }       // HTTP request method (GET, POST, etc., max 10 chars)
    public int? StatusCode { get; set; }             // HTTP response status code
    public long? DurationMs { get; set; }            // Request duration in milliseconds
    public string? AdditionalData { get; set; }      // JSON string for additional context (max 8000 chars)
}
```

**Validation Rules:**

- `Timestamp` is required, defaults to UTC now
- `Level` is required, must be one of: Debug, Info, Warning, Error, Critical
- `Message` is required, max 4000 characters
- `Source` is optional, max 500 characters
- `Exception` is optional, max 8000 characters (stack traces can be long)
- `UserId` is optional, references User.Id
- `IpAddress` is optional, max 50 characters (IPv6 support)
- `RequestPath` is optional, max 500 characters
- `RequestMethod` is optional, max 10 characters
- `StatusCode` is optional, nullable integer
- `DurationMs` is optional, nullable long
- `AdditionalData` is optional, max 8000 characters (JSON format)

**Indexes:**

- Clustered index on `Timestamp DESC` (primary query pattern is recent-first)
- Index on `Level` for filtering by severity
- Index on `UserId` for user-specific queries
- Index on `Source` for filtering by component
- Composite index on `(Timestamp, Level)` for common filter combinations

**Relationships:**

- One User can have many Logs (via `UserId`, optional)

## Backend Implementation

### File Structure

Following Vertical Slice Architecture, create the following files:

```
src/Abuvi.API/
├── Features/
│   └── Logs/
│       ├── LogsEndpoints.cs          # API endpoint definitions
│       ├── LogsModels.cs             # Request/Response DTOs and domain entity
│       ├── LogsService.cs            # Business logic
│       ├── LogsRepository.cs         # Data access interface and implementation
│       └── GetLogsValidator.cs       # FluentValidation for query parameters
├── Common/
│   └── Middleware/
│       └── RequestLoggingMiddleware.cs  # Automatic HTTP request/response logging
└── Data/
    └── Configurations/
        └── LogConfiguration.cs       # EF Core entity configuration
```

### 1. LogsModels.cs

```csharp
namespace Abuvi.API.Features.Logs;

// Domain entity
public class Log
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Exception { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public int? StatusCode { get; set; }
    public long? DurationMs { get; set; }
    public string? AdditionalData { get; set; }
}

// Log levels enum
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

// Response DTO
public record LogResponse(
    Guid Id,
    DateTime Timestamp,
    string Level,
    string Message,
    string? Source,
    string? Exception,
    string? UserId,
    string? IpAddress,
    string? RequestPath,
    string? RequestMethod,
    int? StatusCode,
    long? DurationMs,
    Dictionary<string, object>? AdditionalData
);

// Query request
public record GetLogsRequest(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? Level = null,
    string? Source = null,
    string? UserId = null,
    string? SearchText = null,
    int Page = 1,
    int PageSize = 50
);

// Create log request (for manual logging from frontend in rare cases)
public record CreateLogRequest(
    string Level,
    string Message,
    string? Source = null,
    Dictionary<string, object>? AdditionalData = null
);
```

### 2. LogsRepository.cs

```csharp
namespace Abuvi.API.Features.Logs;

public interface ILogsRepository
{
    Task<PagedResult<Log>> GetPagedAsync(GetLogsRequest request, CancellationToken ct);
    Task AddAsync(Log log, CancellationToken ct);
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken ct);
    Task<Dictionary<string, int>> GetLogCountsByLevelAsync(DateTime since, CancellationToken ct);
}

public class LogsRepository(AbuviDbContext db) : ILogsRepository
{
    public async Task<PagedResult<Log>> GetPagedAsync(GetLogsRequest request, CancellationToken ct)
    {
        var query = db.Logs.AsNoTracking();

        // Apply filters
        if (request.StartDate.HasValue)
            query = query.Where(l => l.Timestamp >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(l => l.Timestamp <= request.EndDate.Value);

        if (!string.IsNullOrWhiteSpace(request.Level))
            query = query.Where(l => l.Level == request.Level);

        if (!string.IsNullOrWhiteSpace(request.Source))
            query = query.Where(l => l.Source != null && l.Source.Contains(request.Source));

        if (!string.IsNullOrWhiteSpace(request.UserId))
            query = query.Where(l => l.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
            query = query.Where(l => l.Message.Contains(request.SearchText) ||
                                   (l.Exception != null && l.Exception.Contains(request.SearchText)));

        var totalCount = await query.CountAsync(ct);

        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Log>(logs, totalCount, request.Page, request.PageSize);
    }

    public async Task AddAsync(Log log, CancellationToken ct)
    {
        db.Logs.Add(log);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken ct)
    {
        return await db.Logs
            .Where(l => l.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<Dictionary<string, int>> GetLogCountsByLevelAsync(DateTime since, CancellationToken ct)
    {
        return await db.Logs
            .Where(l => l.Timestamp >= since)
            .GroupBy(l => l.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Level, x => x.Count, ct);
    }
}
```

### 3. LogsService.cs

```csharp
namespace Abuvi.API.Features.Logs;

public class LogsService(ILogsRepository repository, ILogger<LogsService> logger)
{
    public async Task<PagedResult<LogResponse>> GetLogsAsync(GetLogsRequest request, CancellationToken ct)
    {
        var result = await repository.GetPagedAsync(request, ct);

        var responses = result.Items.Select(log => new LogResponse(
            log.Id,
            log.Timestamp,
            log.Level,
            log.Message,
            log.Source,
            log.Exception,
            log.UserId,
            log.IpAddress,
            log.RequestPath,
            log.RequestMethod,
            log.StatusCode,
            log.DurationMs,
            ParseAdditionalData(log.AdditionalData)
        )).ToList();

        return new PagedResult<LogResponse>(
            responses,
            result.TotalCount,
            result.Page,
            result.PageSize
        );
    }

    public async Task<LogResponse> CreateLogAsync(CreateLogRequest request, string? userId, string? ipAddress, CancellationToken ct)
    {
        var log = new Log
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Level = request.Level,
            Message = request.Message,
            Source = request.Source,
            UserId = userId,
            IpAddress = ipAddress,
            AdditionalData = request.AdditionalData != null
                ? JsonSerializer.Serialize(request.AdditionalData)
                : null
        };

        await repository.AddAsync(log, ct);

        return new LogResponse(
            log.Id,
            log.Timestamp,
            log.Level,
            log.Message,
            log.Source,
            log.Exception,
            log.UserId,
            log.IpAddress,
            log.RequestPath,
            log.RequestMethod,
            log.StatusCode,
            log.DurationMs,
            request.AdditionalData
        );
    }

    public async Task<int> CleanupOldLogsAsync(int retentionDays, CancellationToken ct)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var deletedCount = await repository.DeleteOlderThanAsync(cutoffDate, ct);

        if (deletedCount > 0)
        {
            logger.LogInformation("Deleted {Count} log entries older than {CutoffDate}",
                deletedCount, cutoffDate);
        }

        return deletedCount;
    }

    public async Task<Dictionary<string, int>> GetLogStatisticsAsync(int daysBack, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-daysBack);
        return await repository.GetLogCountsByLevelAsync(since, ct);
    }

    private Dictionary<string, object>? ParseAdditionalData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }
}
```

### 4. LogsEndpoints.cs

```csharp
namespace Abuvi.API.Features.Logs;

public static class LogsEndpoints
{
    public static void MapLogsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs")
            .WithTags("Logs")
            .RequireAuthorization(); // All endpoints require authentication

        // GET /api/logs - Query logs (Board/Admin only)
        group.MapGet("/", GetLogs)
            .WithName("GetLogs")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .Produces<ApiResponse<PagedResult<LogResponse>>>()
            .WithOpenApi(operation => new(operation)
            {
                Summary = "Get paginated logs with filters",
                Description = "Retrieve application logs with optional filtering by date range, level, source, user, and search text. Board and Admin only."
            });

        // POST /api/logs - Create manual log entry (authenticated users)
        group.MapPost("/", CreateLog)
            .WithName("CreateLog")
            .Produces<ApiResponse<LogResponse>>(StatusCodes.Status201Created)
            .WithOpenApi(operation => new(operation)
            {
                Summary = "Create a manual log entry",
                Description = "Create a log entry from the frontend (rare use case for client-side critical errors)"
            });

        // GET /api/logs/statistics - Get log statistics (Board/Admin only)
        group.MapGet("/statistics", GetStatistics)
            .WithName("GetLogStatistics")
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
            .Produces<ApiResponse<Dictionary<string, int>>>()
            .WithOpenApi(operation => new(operation)
            {
                Summary = "Get log statistics",
                Description = "Get count of logs by level for the last N days"
            });
    }

    private static async Task<IResult> GetLogs(
        [AsParameters] GetLogsRequest request,
        LogsService service,
        CancellationToken ct)
    {
        var result = await service.GetLogsAsync(request, ct);
        return Results.Ok(ApiResponse<PagedResult<LogResponse>>.Ok(result));
    }

    private static async Task<IResult> CreateLog(
        CreateLogRequest request,
        LogsService service,
        HttpContext context,
        CancellationToken ct)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        var log = await service.CreateLogAsync(request, userId, ipAddress, ct);
        return Results.Created($"/api/logs/{log.Id}", ApiResponse<LogResponse>.Ok(log));
    }

    private static async Task<IResult> GetStatistics(
        [FromQuery] int daysBack,
        LogsService service,
        CancellationToken ct)
    {
        var stats = await service.GetLogStatisticsAsync(daysBack, ct);
        return Results.Ok(ApiResponse<Dictionary<string, int>>.Ok(stats));
    }
}
```

### 5. GetLogsValidator.cs

```csharp
namespace Abuvi.API.Features.Logs;

public class GetLogsValidator : AbstractValidator<GetLogsRequest>
{
    public GetLogsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100");

        RuleFor(x => x.Level)
            .Must(level => string.IsNullOrEmpty(level) ||
                           Enum.TryParse<LogLevel>(level, true, out _))
            .WithMessage("Level must be one of: Debug, Info, Warning, Error, Critical");

        RuleFor(x => x.StartDate)
            .LessThanOrEqualTo(x => x.EndDate ?? DateTime.MaxValue)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue)
            .WithMessage("Start date must be before or equal to end date");
    }
}
```

### 6. LogConfiguration.cs (EF Core)

```csharp
namespace Abuvi.API.Data.Configurations;

public class LogConfiguration : IEntityTypeConfiguration<Log>
{
    public void Configure(EntityTypeBuilder<Log> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.Timestamp)
            .IsRequired();

        builder.Property(l => l.Level)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(l => l.Message)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(l => l.Source)
            .HasMaxLength(500);

        builder.Property(l => l.Exception)
            .HasMaxLength(8000);

        builder.Property(l => l.UserId)
            .HasMaxLength(100);

        builder.Property(l => l.IpAddress)
            .HasMaxLength(50);

        builder.Property(l => l.RequestPath)
            .HasMaxLength(500);

        builder.Property(l => l.RequestMethod)
            .HasMaxLength(10);

        builder.Property(l => l.AdditionalData)
            .HasMaxLength(8000);

        // Indexes for performance
        builder.HasIndex(l => l.Timestamp)
            .IsDescending();

        builder.HasIndex(l => l.Level);

        builder.HasIndex(l => l.UserId);

        builder.HasIndex(l => l.Source);

        builder.HasIndex(l => new { l.Timestamp, l.Level });
    }
}
```

### 7. RequestLoggingMiddleware.cs

```csharp
namespace Abuvi.API.Common.Middleware;

public class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly HashSet<string> ExcludedPaths = new()
    {
        "/health",
        "/swagger",
        "/api/logs" // Don't log the logs endpoint to avoid recursion
    };

    public async Task InvokeAsync(HttpContext context, AbuviDbContext db)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip logging for excluded paths
        if (ExcludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Log the exception and re-throw
            await LogRequestAsync(context, db, startTime, stopwatch.ElapsedMilliseconds, ex);
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Log successful requests (or those that threw but we caught them)
            if (context.Response.StatusCode >= 400)
            {
                await LogRequestAsync(context, db, startTime, stopwatch.ElapsedMilliseconds, null);
            }
        }
    }

    private async Task LogRequestAsync(
        HttpContext context,
        AbuviDbContext db,
        DateTime timestamp,
        long durationMs,
        Exception? exception)
    {
        try
        {
            var userId = context.User.FindFirst("sub")?.Value;
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var statusCode = context.Response.StatusCode;

            var level = statusCode switch
            {
                >= 500 => "Error",
                >= 400 => "Warning",
                _ => "Info"
            };

            var message = exception != null
                ? $"Request failed: {context.Request.Method} {context.Request.Path}"
                : $"Request completed: {context.Request.Method} {context.Request.Path} - {statusCode}";

            var log = new Log
            {
                Id = Guid.NewGuid(),
                Timestamp = timestamp,
                Level = level,
                Message = message,
                Source = "RequestLoggingMiddleware",
                Exception = exception?.ToString(),
                UserId = userId,
                IpAddress = ipAddress,
                RequestPath = context.Request.Path,
                RequestMethod = context.Request.Method,
                StatusCode = statusCode,
                DurationMs = durationMs
            };

            db.Logs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Don't throw from logging - just log to console/file
            logger.LogError(ex, "Failed to write request log to database");
        }
    }
}
```

### 8. Program.cs Modifications

```csharp
// Register services
builder.Services.AddScoped<ILogsRepository, LogsRepository>();
builder.Services.AddScoped<LogsService>();

// Register middleware (add after UseRouting, before MapEndpoints)
app.UseMiddleware<RequestLoggingMiddleware>();

// Register endpoints
app.MapLogsEndpoints();
```

### 9. Background Job for Log Cleanup (Optional but Recommended)

```csharp
// Common/BackgroundServices/LogCleanupService.cs
namespace Abuvi.API.Common.BackgroundServices;

public class LogCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<LogCleanupService> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly int _retentionDays = configuration.GetValue<int>("Logging:RetentionDays", 90);
    private readonly TimeSpan _interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Log cleanup service started. Retention: {Days} days, Interval: {Interval}",
            _retentionDays, _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var logsService = scope.ServiceProvider.GetRequiredService<LogsService>();

                var deletedCount = await logsService.CleanupOldLogsAsync(_retentionDays, stoppingToken);

                if (deletedCount > 0)
                {
                    logger.LogInformation("Log cleanup completed. Deleted {Count} old log entries", deletedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during log cleanup");
            }
        }
    }
}

// Register in Program.cs
builder.Services.AddHostedService<LogCleanupService>();
```

### 10. appsettings.json Configuration

```json
{
  "Logging": {
    "RetentionDays": 90,
    "EnableRequestLogging": true,
    "MinimumLevel": "Information"
  }
}
```

## Frontend Implementation

### File Structure

```
frontend/src/
├── views/
│   └── LogsPage.vue                  # Main logs viewer page
├── components/
│   └── admin/
│       ├── LogViewer.vue             # Log viewer component
│       ├── LogFilters.vue            # Filter controls
│       └── LogTable.vue              # Table displaying logs
├── composables/
│   └── useLogs.ts                    # API composable for logs
└── types/
    └── log.ts                        # TypeScript type definitions
```

### 1. types/log.ts

```typescript
export interface Log {
  id: string
  timestamp: string
  level: LogLevel
  message: string
  source: string | null
  exception: string | null
  userId: string | null
  ipAddress: string | null
  requestPath: string | null
  requestMethod: string | null
  statusCode: number | null
  durationMs: number | null
  additionalData: Record<string, any> | null
}

export type LogLevel = 'Debug' | 'Info' | 'Warning' | 'Error' | 'Critical'

export interface LogFilters {
  startDate: string | null
  endDate: string | null
  level: LogLevel | null
  source: string | null
  userId: string | null
  searchText: string | null
  page: number
  pageSize: number
}

export interface LogStatistics {
  [level: string]: number
}

export interface CreateLogRequest {
  level: LogLevel
  message: string
  source?: string
  additionalData?: Record<string, any>
}
```

### 2. composables/useLogs.ts

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse, PagedResult } from '@/types/api'
import type { Log, LogFilters, LogStatistics } from '@/types/log'

export function useLogs() {
  const logs = ref<Log[]>([])
  const totalCount = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchLogs = async (filters: LogFilters) => {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams()
      if (filters.startDate) params.append('startDate', filters.startDate)
      if (filters.endDate) params.append('endDate', filters.endDate)
      if (filters.level) params.append('level', filters.level)
      if (filters.source) params.append('source', filters.source)
      if (filters.userId) params.append('userId', filters.userId)
      if (filters.searchText) params.append('searchText', filters.searchText)
      params.append('page', filters.page.toString())
      params.append('pageSize', filters.pageSize.toString())

      const response = await api.get<ApiResponse<PagedResult<Log>>>(
        `/logs?${params.toString()}`
      )

      if (response.data.success && response.data.data) {
        logs.value = response.data.data.items
        totalCount.value = response.data.data.totalCount
      }
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al cargar los logs'
    } finally {
      loading.value = false
    }
  }

  const fetchStatistics = async (daysBack: number): Promise<LogStatistics | null> => {
    loading.value = true
    error.value = null
    try {
      const response = await api.get<ApiResponse<LogStatistics>>(
        `/logs/statistics?daysBack=${daysBack}`
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }
      return null
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al cargar estadísticas'
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    logs,
    totalCount,
    loading,
    error,
    fetchLogs,
    fetchStatistics
  }
}
```

### 3. components/admin/LogViewer.vue

```vue
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useLogs } from '@/composables/useLogs'
import type { LogLevel, LogFilters } from '@/types/log'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import Calendar from 'primevue/calendar'
import Button from 'primevue/button'
import Chip from 'primevue/chip'
import ProgressSpinner from 'primevue/progressspinner'
import Message from 'primevue/message'
import Dialog from 'primevue/dialog'

const { logs, totalCount, loading, error, fetchLogs } = useLogs()

const filters = reactive<LogFilters>({
  startDate: null,
  endDate: null,
  level: null,
  source: null,
  userId: null,
  searchText: null,
  page: 1,
  pageSize: 50
})

const logLevels = [
  { label: 'Todos', value: null },
  { label: 'Debug', value: 'Debug' },
  { label: 'Info', value: 'Info' },
  { label: 'Warning', value: 'Warning' },
  { label: 'Error', value: 'Error' },
  { label: 'Critical', value: 'Critical' }
]

const selectedLog = ref<Log | null>(null)
const showDetailsDialog = ref(false)

const applyFilters = () => {
  filters.page = 1
  fetchLogs(filters)
}

const resetFilters = () => {
  filters.startDate = null
  filters.endDate = null
  filters.level = null
  filters.source = null
  filters.userId = null
  filters.searchText = null
  filters.page = 1
  fetchLogs(filters)
}

const onPageChange = (event: any) => {
  filters.page = event.page + 1
  filters.pageSize = event.rows
  fetchLogs(filters)
}

const openDetails = (log: Log) => {
  selectedLog.value = log
  showDetailsDialog.value = true
}

const getLevelSeverity = (level: LogLevel) => {
  const severityMap = {
    Debug: 'secondary',
    Info: 'info',
    Warning: 'warn',
    Error: 'error',
    Critical: 'danger'
  }
  return severityMap[level] || 'secondary'
}

const formatDate = (timestamp: string) => {
  return new Intl.DateTimeFormat('es-ES', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  }).format(new Date(timestamp))
}

onMounted(() => {
  fetchLogs(filters)
})
</script>

<template>
  <div class="space-y-4">
    <!-- Filters -->
    <div class="rounded-lg border border-gray-200 bg-white p-4">
      <h3 class="mb-4 text-lg font-semibold">Filtros</h3>
      <div class="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
        <div>
          <label class="mb-1 block text-sm font-medium">Fecha Inicio</label>
          <Calendar
            v-model="filters.startDate"
            date-format="dd/mm/yy"
            show-time
            class="w-full"
          />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium">Fecha Fin</label>
          <Calendar
            v-model="filters.endDate"
            date-format="dd/mm/yy"
            show-time
            class="w-full"
          />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium">Nivel</label>
          <Select
            v-model="filters.level"
            :options="logLevels"
            option-label="label"
            option-value="value"
            placeholder="Todos"
            class="w-full"
          />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium">Búsqueda</label>
          <InputText
            v-model="filters.searchText"
            placeholder="Buscar en mensajes..."
            class="w-full"
          />
        </div>
      </div>
      <div class="mt-4 flex gap-2">
        <Button label="Aplicar Filtros" icon="pi pi-filter" @click="applyFilters" />
        <Button
          label="Limpiar"
          icon="pi pi-filter-slash"
          severity="secondary"
          outlined
          @click="resetFilters"
        />
      </div>
    </div>

    <!-- Error Message -->
    <Message v-if="error" severity="error" :closable="false">
      {{ error }}
    </Message>

    <!-- Loading Spinner -->
    <div v-if="loading" class="flex justify-center p-8">
      <ProgressSpinner />
    </div>

    <!-- Logs Table -->
    <DataTable
      v-else
      :value="logs"
      :total-records="totalCount"
      :rows="filters.pageSize"
      :lazy="true"
      paginator
      @page="onPageChange"
      striped-rows
      class="rounded-lg border border-gray-200"
    >
      <Column field="timestamp" header="Fecha/Hora" style="min-width: 180px">
        <template #body="{ data }">
          <span class="text-sm">{{ formatDate(data.timestamp) }}</span>
        </template>
      </Column>

      <Column field="level" header="Nivel" style="min-width: 100px">
        <template #body="{ data }">
          <Chip :label="data.level" :severity="getLevelSeverity(data.level)" />
        </template>
      </Column>

      <Column field="message" header="Mensaje" style="min-width: 300px">
        <template #body="{ data }">
          <span class="line-clamp-2 text-sm">{{ data.message }}</span>
        </template>
      </Column>

      <Column field="source" header="Origen" style="min-width: 150px">
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ data.source || '-' }}</span>
        </template>
      </Column>

      <Column field="requestPath" header="Ruta" style="min-width: 150px">
        <template #body="{ data }">
          <span class="text-sm text-gray-600">{{ data.requestPath || '-' }}</span>
        </template>
      </Column>

      <Column header="Acciones" style="min-width: 100px">
        <template #body="{ data }">
          <Button
            icon="pi pi-eye"
            text
            rounded
            severity="secondary"
            @click="openDetails(data)"
          />
        </template>
      </Column>
    </DataTable>

    <!-- Log Details Dialog -->
    <Dialog
      v-model:visible="showDetailsDialog"
      header="Detalles del Log"
      modal
      class="w-full max-w-3xl"
    >
      <div v-if="selectedLog" class="space-y-3">
        <div>
          <strong class="text-sm text-gray-600">Fecha/Hora:</strong>
          <p class="mt-1">{{ formatDate(selectedLog.timestamp) }}</p>
        </div>
        <div>
          <strong class="text-sm text-gray-600">Nivel:</strong>
          <p class="mt-1">
            <Chip :label="selectedLog.level" :severity="getLevelSeverity(selectedLog.level)" />
          </p>
        </div>
        <div>
          <strong class="text-sm text-gray-600">Mensaje:</strong>
          <p class="mt-1">{{ selectedLog.message }}</p>
        </div>
        <div v-if="selectedLog.source">
          <strong class="text-sm text-gray-600">Origen:</strong>
          <p class="mt-1">{{ selectedLog.source }}</p>
        </div>
        <div v-if="selectedLog.exception">
          <strong class="text-sm text-gray-600">Excepción:</strong>
          <pre class="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs">{{
            selectedLog.exception
          }}</pre>
        </div>
        <div v-if="selectedLog.requestPath">
          <strong class="text-sm text-gray-600">Ruta de Solicitud:</strong>
          <p class="mt-1">{{ selectedLog.requestMethod }} {{ selectedLog.requestPath }}</p>
        </div>
        <div v-if="selectedLog.statusCode">
          <strong class="text-sm text-gray-600">Código de Estado:</strong>
          <p class="mt-1">{{ selectedLog.statusCode }}</p>
        </div>
        <div v-if="selectedLog.durationMs">
          <strong class="text-sm text-gray-600">Duración:</strong>
          <p class="mt-1">{{ selectedLog.durationMs }}ms</p>
        </div>
        <div v-if="selectedLog.userId">
          <strong class="text-sm text-gray-600">Usuario:</strong>
          <p class="mt-1">{{ selectedLog.userId }}</p>
        </div>
        <div v-if="selectedLog.ipAddress">
          <strong class="text-sm text-gray-600">Dirección IP:</strong>
          <p class="mt-1">{{ selectedLog.ipAddress }}</p>
        </div>
        <div v-if="selectedLog.additionalData">
          <strong class="text-sm text-gray-600">Datos Adicionales:</strong>
          <pre class="mt-1 max-h-64 overflow-auto rounded bg-gray-50 p-2 text-xs">{{
            JSON.stringify(selectedLog.additionalData, null, 2)
          }}</pre>
        </div>
      </div>
    </Dialog>
  </div>
</template>
```

### 4. views/LogsPage.vue

```vue
<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import { onMounted } from 'vue'
import LogViewer from '@/components/admin/LogViewer.vue'

const auth = useAuthStore()
const router = useRouter()

onMounted(() => {
  // Additional role check (route guard should already handle this)
  if (!auth.isAdmin && !auth.isBoard) {
    router.push('/home')
  }
})
</script>

<template>
  <div class="container mx-auto px-4 py-8">
    <h1 class="mb-6 text-3xl font-bold">Logs del Sistema</h1>
    <LogViewer />
  </div>
</template>
```

### 5. router/index.ts Modification

Add the logs route:

```typescript
{
  path: '/admin/logs',
  component: () => import('@/views/LogsPage.vue'),
  meta: {
    title: 'ABUVI | Logs del Sistema',
    requiresAuth: true,
    requiresAdmin: true // Or requiresBoard: true if Board can also access
  }
}
```

## API Endpoints

### 1. GET /api/logs

**Description**: Retrieve paginated logs with optional filters
**Authorization**: Board, Admin only
**Query Parameters**:

- `startDate` (optional): ISO 8601 datetime (e.g., `2026-01-01T00:00:00Z`)
- `endDate` (optional): ISO 8601 datetime
- `level` (optional): One of `Debug`, `Info`, `Warning`, `Error`, `Critical`
- `source` (optional): Filter by source component (partial match)
- `userId` (optional): Filter by user ID
- `searchText` (optional): Search in message and exception fields
- `page` (default: 1): Page number (min: 1)
- `pageSize` (default: 50): Results per page (min: 1, max: 100)

**Response**:

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "timestamp": "2026-02-16T10:30:45.123Z",
        "level": "Error",
        "message": "Request failed: GET /api/camps/123",
        "source": "RequestLoggingMiddleware",
        "exception": "System.InvalidOperationException: ...",
        "userId": "user-123",
        "ipAddress": "192.168.1.100",
        "requestPath": "/api/camps/123",
        "requestMethod": "GET",
        "statusCode": 500,
        "durationMs": 1234,
        "additionalData": null
      }
    ],
    "totalCount": 1523,
    "page": 1,
    "pageSize": 50,
    "totalPages": 31,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "error": null
}
```

### 2. POST /api/logs

**Description**: Create a manual log entry (rare use case for critical client-side errors)
**Authorization**: Authenticated users
**Request Body**:

```json
{
  "level": "Error",
  "message": "Critical client-side error occurred",
  "source": "FrontendApp",
  "additionalData": {
    "component": "CampRegistration",
    "stackTrace": "Error at line 42..."
  }
}
```

**Response**:

```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2026-02-16T10:30:45.123Z",
    "level": "Error",
    "message": "Critical client-side error occurred",
    "source": "FrontendApp",
    "exception": null,
    "userId": "user-123",
    "ipAddress": "192.168.1.100",
    "requestPath": null,
    "requestMethod": null,
    "statusCode": null,
    "durationMs": null,
    "additionalData": {
      "component": "CampRegistration",
      "stackTrace": "Error at line 42..."
    }
  },
  "error": null
}
```

### 3. GET /api/logs/statistics

**Description**: Get log count statistics by level for the last N days
**Authorization**: Board, Admin only
**Query Parameters**:

- `daysBack` (required): Number of days to look back (e.g., 7, 30, 90)

**Response**:

```json
{
  "success": true,
  "data": {
    "Debug": 1523,
    "Info": 4231,
    "Warning": 234,
    "Error": 45,
    "Critical": 3
  },
  "error": null
}
```

## Testing Requirements

### Backend Tests

#### Unit Tests (src/Abuvi.Tests/Unit/Features/Logs/)

**LogsServiceTests.cs:**

- ✅ GetLogsAsync_WithNoFilters_ReturnsAllLogs
- ✅ GetLogsAsync_WithLevelFilter_ReturnsFilteredLogs
- ✅ GetLogsAsync_WithDateRangeFilter_ReturnsLogsInRange
- ✅ GetLogsAsync_WithSearchText_ReturnsMatchingLogs
- ✅ GetLogsAsync_WithPagination_ReturnsCorrectPage
- ✅ CreateLogAsync_WithValidData_CreatesLog
- ✅ CleanupOldLogsAsync_DeletesLogsOlderThanRetentionPeriod
- ✅ GetLogStatisticsAsync_ReturnsCountsByLevel

**LogsRepositoryTests.cs:**

- ✅ GetPagedAsync_AppliesFiltersCorrectly
- ✅ GetPagedAsync_OrdersByTimestampDescending
- ✅ AddAsync_SavesLogToDatabase
- ✅ DeleteOlderThanAsync_DeletesOnlyOldLogs
- ✅ GetLogCountsByLevelAsync_GroupsCorrectly

**GetLogsValidatorTests.cs:**

- ✅ Validate_WithInvalidPage_ReturnsError
- ✅ Validate_WithInvalidPageSize_ReturnsError
- ✅ Validate_WithInvalidLevel_ReturnsError
- ✅ Validate_WithEndDateBeforeStartDate_ReturnsError

#### Integration Tests (src/Abuvi.Tests/Integration/Features/Logs/)

**LogsIntegrationTests.cs:**

- ✅ GetLogs_AsAdmin_Returns200
- ✅ GetLogs_AsMember_Returns403
- ✅ GetLogs_WithFilters_ReturnsFilteredResults
- ✅ CreateLog_AsAuthenticatedUser_Returns201
- ✅ GetStatistics_AsAdmin_Returns200
- ✅ RequestLoggingMiddleware_LogsFailedRequests
- ✅ RequestLoggingMiddleware_LogsWarningsFor4xxErrors

### Frontend Tests

#### Unit Tests (frontend/src/composables/**tests**/)

**useLogs.test.ts:**

- ✅ fetchLogs_WithNoFilters_ReturnsLogs
- ✅ fetchLogs_WithFilters_AppliesFiltersCorrectly
- ✅ fetchLogs_OnError_SetsErrorMessage
- ✅ fetchStatistics_ReturnsStatistics
- ✅ fetchStatistics_OnError_SetsErrorMessage

#### Component Tests (frontend/src/components/admin/**tests**/)

**LogViewer.test.ts:**

- ✅ Renders log table with data
- ✅ Applies filters when clicking "Apply Filters"
- ✅ Resets filters when clicking "Clear"
- ✅ Opens details dialog when clicking eye icon
- ✅ Paginates correctly

#### E2E Tests (frontend/cypress/e2e/)

**logs.cy.ts:**

- ✅ Admin can access logs page
- ✅ Member cannot access logs page (403)
- ✅ Logs table displays data correctly
- ✅ Filters work as expected
- ✅ Pagination works correctly
- ✅ Log details dialog shows all information

## Non-Functional Requirements

### Security

- **Access Control**: Logs are accessible only to users with Admin or Board roles
- **RGPD Compliance**: Logs may contain sensitive information (IP addresses, user IDs). Ensure retention policies comply with RGPD requirements.
- **No Sensitive Data**: Do not log passwords, tokens, or other authentication credentials
- **IP Address Handling**: IP addresses are collected for audit purposes, retained for 90 days by default

### Performance

- **Async Logging**: Request logging should not block HTTP request processing
- **Database Indexes**: Ensure indexes on `Timestamp`, `Level`, `UserId`, `Source` for fast queries
- **Pagination**: Always use pagination when retrieving logs (max 100 per page)
- **Log Cleanup**: Background service runs daily to delete logs older than retention period

### Scalability

- **Log Volume**: Expect 10,000-50,000 log entries per day in production
- **Retention Period**: Default 90 days, configurable via `appsettings.json`
- **Partitioning**: For very high-volume scenarios, consider table partitioning by date range (future enhancement)

### Monitoring

- **Health Checks**: Monitor log database size and growth rate
- **Alerting**: Set up alerts for excessive error/critical log counts
- **Backup**: Include logs table in regular database backups

## Implementation Steps

### Phase 1: Database and Backend Foundation (1-2 days)

1. Create `Log` entity in `Features/Logs/LogsModels.cs`
2. Create `LogConfiguration.cs` for EF Core entity configuration
3. Generate and apply EF Core migration: `dotnet ef migrations add AddLogsTable`
4. Create `ILogsRepository` and `LogsRepository` with CRUD operations
5. Create `LogsService` with business logic
6. Write unit tests for repository and service

### Phase 2: API Endpoints and Middleware (1 day)

1. Create `LogsEndpoints.cs` with GET/POST endpoints
2. Create `GetLogsValidator.cs` for request validation
3. Register endpoints in `Program.cs`
4. Create `RequestLoggingMiddleware.cs` for automatic request logging
5. Register middleware in `Program.cs`
6. Write integration tests for endpoints and middleware

### Phase 3: Background Cleanup Service (0.5 days)

1. Create `LogCleanupService.cs` background service
2. Register service in `Program.cs`
3. Add configuration to `appsettings.json`
4. Test cleanup service with various retention periods

### Phase 4: Frontend Log Viewer (1-2 days)

1. Create TypeScript types in `types/log.ts`
2. Create `useLogs.ts` composable
3. Create `LogViewer.vue` component with filters and table
4. Create `LogsPage.vue` view
5. Add route to `router/index.ts`
6. Write component tests

### Phase 5: Testing and Documentation (1 day)

1. Write comprehensive unit tests (backend and frontend)
2. Write E2E tests for log viewer
3. Verify test coverage meets 90% threshold
4. Update API documentation
5. Add logging to existing error handlers where missing

## Files to be Created/Modified

### Backend Files to Create

- `src/Abuvi.API/Features/Logs/LogsEndpoints.cs`
- `src/Abuvi.API/Features/Logs/LogsModels.cs`
- `src/Abuvi.API/Features/Logs/LogsService.cs`
- `src/Abuvi.API/Features/Logs/LogsRepository.cs`
- `src/Abuvi.API/Features/Logs/GetLogsValidator.cs`
- `src/Abuvi.API/Data/Configurations/LogConfiguration.cs`
- `src/Abuvi.API/Common/Middleware/RequestLoggingMiddleware.cs`
- `src/Abuvi.API/Common/BackgroundServices/LogCleanupService.cs`
- `src/Abuvi.Tests/Unit/Features/Logs/LogsServiceTests.cs`
- `src/Abuvi.Tests/Unit/Features/Logs/LogsRepositoryTests.cs`
- `src/Abuvi.Tests/Unit/Features/Logs/GetLogsValidatorTests.cs`
- `src/Abuvi.Tests/Integration/Features/Logs/LogsIntegrationTests.cs`

### Backend Files to Modify

- `src/Abuvi.API/Program.cs` (register services, middleware, endpoints)
- `src/Abuvi.API/appsettings.json` (add Logging configuration section)

### Frontend Files to Create

- `frontend/src/views/LogsPage.vue`
- `frontend/src/components/admin/LogViewer.vue`
- `frontend/src/composables/useLogs.ts`
- `frontend/src/types/log.ts`
- `frontend/src/composables/__tests__/useLogs.test.ts`
- `frontend/src/components/admin/__tests__/LogViewer.test.ts`
- `frontend/cypress/e2e/logs.cy.ts`

### Frontend Files to Modify

- `frontend/src/router/index.ts` (add logs route)

## Success Criteria

1. ✅ Logs are persisted to PostgreSQL database
2. ✅ All HTTP requests with status >= 400 are automatically logged
3. ✅ Admin/Board users can view logs through a searchable UI
4. ✅ Logs can be filtered by date range, level, source, user, and search text
5. ✅ Log cleanup service runs daily and deletes logs older than retention period
6. ✅ All endpoints are protected with role-based authorization
7. ✅ Test coverage meets 90% threshold
8. ✅ No performance degradation (request logging does not block responses)
9. ✅ Logs comply with RGPD requirements (no sensitive data, retention policies)

## Future Enhancements (Out of Scope for Initial Implementation)

- Real-time log streaming with SignalR for live monitoring
- Log aggregation and visualization (charts, graphs)
- Export logs to CSV or JSON
- Integration with external log management systems (ELK, Datadog, Application Insights)
- Advanced search with regex support
- Log annotations and comments
- Alerting rules for critical errors
