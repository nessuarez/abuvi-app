using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;
using Abuvi.API.Common.Middleware;
using Abuvi.API.Common.HealthChecks;
using Abuvi.API.Features.Users;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.GooglePlaces;
using Abuvi.API.Features.FamilyUnits;
using Abuvi.API.Features.Guests;
using Abuvi.API.Features.Memberships;
using Abuvi.API.Features.Registrations;
using Abuvi.API.Features.Payments;
using Abuvi.API.Features.Memories;
using Abuvi.API.Features.MediaItems;
using Abuvi.API.Features.BlobStorage;
using Abuvi.API.Common.Services;
using FluentValidation;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using NpgsqlTypes;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// Serilog Configuration
// ========================================
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

// Use Serilog for all logging
builder.Host.UseSerilog();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required");

builder.Services.AddDbContext<AbuviDbContext>(options =>
    options.UseNpgsql(connectionString));

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ========================================
// Authentication & Authorization
// ========================================
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT secret not configured. Use: dotnet user-secrets set \"Jwt:Secret\" \"your-secret-key\"");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero // No tolerance for token expiry
        };
    });

builder.Services.AddAuthorization();

// Register IHttpContextAccessor for Serilog enrichers
builder.Services.AddHttpContextAccessor();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Feature Services
// Users
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<IUserRoleChangeLogsRepository, UserRoleChangeLogsRepository>();
builder.Services.AddScoped<UsersService>();

// Auth
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Camps
builder.Services.AddScoped<ICampsRepository, CampsRepository>();
builder.Services.AddScoped<CampsService>();
builder.Services.AddScoped<ICampObservationsRepository, CampObservationsRepository>();
builder.Services.AddScoped<ICampObservationsService, CampObservationsService>();
builder.Services.AddScoped<IAssociationSettingsRepository, AssociationSettingsRepository>();
builder.Services.AddScoped<AssociationSettingsService>();
builder.Services.AddScoped<ICampEditionsRepository, CampEditionsRepository>();
builder.Services.AddScoped<CampEditionsService>();
builder.Services.AddScoped<CampPhotosService>();
builder.Services.AddScoped<ICampEditionExtrasRepository, CampEditionExtrasRepository>();
builder.Services.AddScoped<CampEditionExtrasService>();
builder.Services.AddScoped<ICampEditionAccommodationsRepository, CampEditionAccommodationsRepository>();
builder.Services.AddScoped<CampEditionAccommodationsService>();

// Google Places API integration
builder.Services.AddHttpClient<IGooglePlacesService, GooglePlacesService>();
builder.Services.AddScoped<IGooglePlacesService, GooglePlacesService>();
builder.Services.AddScoped<IGooglePlacesMapperService, GooglePlacesMapperService>();

// Family Units
builder.Services.AddScoped<IFamilyUnitsRepository, FamilyUnitsRepository>();
builder.Services.AddScoped<FamilyUnitsService>();

// Guests
builder.Services.AddScoped<IGuestsRepository, GuestsRepository>();
builder.Services.AddScoped<GuestsService>();

// Memberships
builder.Services.AddScoped<IMembershipsRepository, MembershipsRepository>();
builder.Services.AddScoped<MembershipsService>();

// Registrations feature
builder.Services.AddScoped<IRegistrationsRepository, RegistrationsRepository>();
builder.Services.AddScoped<IRegistrationExtrasRepository, RegistrationExtrasRepository>();
builder.Services.AddScoped<IRegistrationAccommodationPreferencesRepository, RegistrationAccommodationPreferencesRepository>();
builder.Services.AddScoped<RegistrationPricingService>();
builder.Services.AddScoped<RegistrationsService>();

// Payments feature
builder.Services.AddScoped<Abuvi.API.Features.Payments.IPaymentsRepository, Abuvi.API.Features.Payments.PaymentsRepository>();
builder.Services.AddScoped<IPaymentsService, PaymentsService>();

// Blob Storage
builder.Services.AddBlobStorage(builder.Configuration);

// Memories
builder.Services.AddMemories();

// Media Items
builder.Services.AddMediaItems();

// Encryption Service
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// ========================================
// Email Service Configuration
// ========================================
var resendApiKey = builder.Configuration["Resend:ApiKey"];
if (string.IsNullOrEmpty(resendApiKey))
{
    var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
    logger.LogWarning(
        "Resend API key not configured. Email sending will fail. " +
        "Use: dotnet user-secrets set \"Resend:ApiKey\" \"your-key\"");
}

// Register Resend wrapper
builder.Services.AddScoped<Abuvi.API.Common.Services.IResendClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["Resend:ApiKey"] ?? string.Empty;
    return new Abuvi.API.Common.Services.ResendClientWrapper(apiKey);
});

// Register email service
builder.Services.AddScoped<Abuvi.API.Common.Services.IEmailService, Abuvi.API.Common.Services.ResendEmailService>();

// Background services
builder.Services.AddHostedService<Abuvi.API.Common.BackgroundServices.LogCleanupService>();
builder.Services.AddHostedService<Abuvi.API.Common.BackgroundServices.AnnualFeeGenerationService>();

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
        tags: ["email"])
    .AddCheck<GooglePlacesHealthCheck>(
        name: "google-places",
        failureStatus: HealthStatus.Degraded,
        tags: ["external"])
    .AddCheck<SeqHealthCheck>(
        name: "seq",
        failureStatus: HealthStatus.Degraded,
        tags: ["logging", "external"])
    .AddCheck<BlobStorageHealthCheck>(
        name: "blob-storage",
        failureStatus: HealthStatus.Degraded,
        tags: ["storage", "external"]);

// Increase Kestrel limit for file uploads (default is 30 MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 55 * 1024 * 1024; // 55 MB (50 MB file + headers)
});

// ========================================
// GlitchTip Error Tracking (Sentry-compatible)
// ========================================
var sentryDsn = builder.Configuration["Sentry:Dsn"];
if (!string.IsNullOrEmpty(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.TracesSampleRate = 0; // Disabled to conserve GlitchTip free tier quota (1K events/mo)
        o.Environment = builder.Environment.EnvironmentName;
    });
}

var app = builder.Build();

// ========================================
// Auto-apply Pending Migrations
// ========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AbuviDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

    if (pendingMigrations.Count == 0)
    {
        startupLogger.LogInformation("Database schema is up to date. No pending migrations.");
    }
    else
    {
        startupLogger.LogInformation(
            "Applying {PendingMigrationCount} pending database migration(s): {PendingMigrations}",
            pendingMigrations.Count,
            pendingMigrations);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await dbContext.Database.MigrateAsync();
        sw.Stop();

        startupLogger.LogInformation(
            "Successfully applied {AppliedMigrationCount} database migration(s) in {ElapsedMs}ms: {AppliedMigrations}",
            pendingMigrations.Count,
            sw.ElapsedMilliseconds,
            pendingMigrations);
    }
}

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
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

// Health check endpoint (anonymous access — no auth required for monitoring tools)
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

// API endpoints
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapCampsEndpoints();
app.MapGooglePlacesEndpoints();
app.MapFamilyUnitsEndpoints();
app.MapGuestsEndpoints();
app.MapMembershipsEndpoints();
app.MapMembershipFeeEndpoints();
app.MapRegistrationsEndpoints();
app.MapPaymentsEndpoints();
app.MapBlobStorageEndpoints();
app.MapMemoriesEndpoints();
app.MapMediaItemsEndpoints();

app.Run();

// Make Program accessible for testing
public partial class Program { }
