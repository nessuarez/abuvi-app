using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;
using Abuvi.API.Common.Middleware;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

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

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");

// API endpoints will be mapped here
// Example: app.MapCampsEndpoints();

app.Run();

// Make Program accessible for testing
public partial class Program { }
