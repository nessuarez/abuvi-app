using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;
using Abuvi.API.Common.Middleware;
using Abuvi.API.Features.Users;
using Abuvi.API.Features.Auth;
using Abuvi.API.Features.Camps;
using FluentValidation;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IAssociationSettingsRepository, AssociationSettingsRepository>();
builder.Services.AddScoped<AssociationSettingsService>();
builder.Services.AddScoped<ICampEditionsRepository, CampEditionsRepository>();
builder.Services.AddScoped<CampEditionsService>();

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

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();
app.UseHttpsRedirection();

// Authentication and Authorization middleware (order matters!)
app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");

// API endpoints
app.MapAuthEndpoints();
app.MapUsersEndpoints();
app.MapCampsEndpoints();

app.Run();

// Make Program accessible for testing
public partial class Program { }
