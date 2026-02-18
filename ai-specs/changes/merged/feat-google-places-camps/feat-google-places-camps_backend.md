# Backend Implementation Plan: Google Places API Integration

**Feature:** Google Places Autocomplete for Camp Locations
**Spec:** [feat-google-places-autocomplete_enriched.md](feat-google-places-camps/feat-google-places-autocomplete_enriched.md)
**Estimated Effort:** 2-3 days
**Target Branch:** `feature/feat-google-places-camps-backend`

---

## Step 0: Create Feature Branch

```bash
git checkout -b feature/feat-google-places-camps-backend
```

---

## Step 1: Create Database Migration for GooglePlaceId

**Objective:** Add `GooglePlaceId` field to the `Camps` table.

**Tasks:**

1. Create migration using EF Core CLI:

   ```bash
   dotnet ef migrations add AddGooglePlaceIdToCamp --project src/Abuvi.API
   ```

2. Verify the generated migration file `src/Abuvi.API/Migrations/YYYYMMDDHHMMSS_AddGooglePlaceIdToCamp.cs` contains:
   - `AddColumn` for `GooglePlaceId` (varchar(255), nullable)
   - `CreateIndex` for `IX_Camps_GooglePlaceId`
   - Proper `Down` migration for rollback

3. Expected migration structure:

   ```csharp
   public partial class AddGooglePlaceIdToCamp : Migration
   {
       protected override void Up(MigrationBuilder migrationBuilder)
       {
           migrationBuilder.AddColumn<string>(
               name: "GooglePlaceId",
               table: "Camps",
               type: "character varying(255)",
               maxLength: 255,
               nullable: true);

           migrationBuilder.CreateIndex(
               name: "IX_Camps_GooglePlaceId",
               table: "Camps",
               column: "GooglePlaceId");
       }

       protected override void Down(MigrationBuilder migrationBuilder)
       {
           migrationBuilder.DropIndex(
               name: "IX_Camps_GooglePlaceId",
               table: "Camps");

           migrationBuilder.DropColumn(
               name: "GooglePlaceId",
               table: "Camps");
       }
   }
   ```

4. Apply migration to local database:

   ```bash
   dotnet ef database update --project src/Abuvi.API
   ```

**Files Modified:**

- `src/Abuvi.API/Migrations/YYYYMMDDHHMMSS_AddGooglePlaceIdToCamp.cs` (NEW)

**Verification:**

- Migration creates `GooglePlaceId` column successfully
- Index `IX_Camps_GooglePlaceId` is created
- Existing camp records remain unchanged (null GooglePlaceId)
- Rollback migration works correctly

---

## Step 2: Update Camp Entity and DTOs

**Objective:** Add `GooglePlaceId` property to the Camp entity and all related DTOs.

**Tasks:**

1. Update `Camp` entity in [src/Abuvi.API/Features/Camps/CampsModels.cs](../../src/Abuvi.API/Features/Camps/CampsModels.cs):

   ```csharp
   public class Camp
   {
       public Guid Id { get; set; }
       public string Name { get; set; } = string.Empty;
       public string? Description { get; set; }
       public string? Location { get; set; }
       public decimal? Latitude { get; set; }
       public decimal? Longitude { get; set; }

       // ADD THIS FIELD
       public string? GooglePlaceId { get; set; }

       public decimal PricePerAdult { get; set; }
       public decimal PricePerChild { get; set; }
       public decimal PricePerBaby { get; set; }
       public bool IsActive { get; set; } = true;
       public DateTime CreatedAt { get; set; }
       public DateTime UpdatedAt { get; set; }
       public ICollection<CampEdition> Editions { get; set; } = new List<CampEdition>();
   }
   ```

2. Update `CreateCampRequest` record:

   ```csharp
   public record CreateCampRequest(
       string Name,
       string? Description,
       string? Location,
       decimal? Latitude,
       decimal? Longitude,
       string? GooglePlaceId, // ADD THIS PARAMETER
       decimal PricePerAdult,
       decimal PricePerChild,
       decimal PricePerBaby
   );
   ```

3. Update `UpdateCampRequest` record:

   ```csharp
   public record UpdateCampRequest(
       string Name,
       string? Description,
       string? Location,
       decimal? Latitude,
       decimal? Longitude,
       string? GooglePlaceId, // ADD THIS PARAMETER
       decimal PricePerAdult,
       decimal PricePerChild,
       decimal PricePerBaby,
       bool IsActive
   );
   ```

4. Update `CampResponse` record:

   ```csharp
   public record CampResponse(
       Guid Id,
       string Name,
       string? Description,
       string? Location,
       decimal? Latitude,
       decimal? Longitude,
       string? GooglePlaceId, // ADD THIS PARAMETER
       decimal PricePerAdult,
       decimal PricePerChild,
       decimal PricePerBaby,
       bool IsActive,
       DateTime CreatedAt,
       DateTime UpdatedAt
   );
   ```

**Files Modified:**

- [src/Abuvi.API/Features/Camps/CampsModels.cs](../../src/Abuvi.API/Features/Camps/CampsModels.cs)

**Verification:**

- All DTOs compile without errors
- Mapping between DTOs and entity includes GooglePlaceId
- Nullable field allows existing camps to work without GooglePlaceId

---

## Step 3: Update Entity Configuration

**Objective:** Configure GooglePlaceId field constraints in EF Core.

**Tasks:**

1. Update or create `CampConfiguration` in [src/Abuvi.API/Data/Configurations/CampConfiguration.cs](../../src/Abuvi.API/Data/Configurations/CampConfiguration.cs):

   ```csharp
   public class CampConfiguration : IEntityTypeConfiguration<Camp>
   {
       public void Configure(EntityTypeBuilder<Camp> builder)
       {
           builder.ToTable("Camps");

           builder.HasKey(c => c.Id);

           builder.Property(c => c.Name)
               .HasMaxLength(200)
               .IsRequired();

           builder.Property(c => c.Description)
               .HasMaxLength(2000);

           builder.Property(c => c.Location)
               .HasMaxLength(500);

           // ADD THIS CONFIGURATION
           builder.Property(c => c.GooglePlaceId)
               .HasMaxLength(255)
               .IsRequired(false);

           builder.HasIndex(c => c.GooglePlaceId)
               .HasDatabaseName("IX_Camps_GooglePlaceId");

           builder.Property(c => c.PricePerAdult)
               .HasColumnType("decimal(10,2)");

           builder.Property(c => c.PricePerChild)
               .HasColumnType("decimal(10,2)");

           builder.Property(c => c.PricePerBaby)
               .HasColumnType("decimal(10,2)");

           builder.HasMany(c => c.Editions)
               .WithOne(e => e.Camp)
               .HasForeignKey(e => e.CampId)
               .OnDelete(DeleteBehavior.Cascade);
       }
   }
   ```

2. Ensure configuration is registered in `DbContext` (usually in `OnModelCreating`):

   ```csharp
   modelBuilder.ApplyConfiguration(new CampConfiguration());
   ```

**Files Modified:**

- [src/Abuvi.API/Data/Configurations/CampConfiguration.cs](../../src/Abuvi.API/Data/Configurations/CampConfiguration.cs) (NEW or UPDATE)
- [src/Abuvi.API/Data/AbuviDbContext.cs](../../src/Abuvi.API/Data/AbuviDbContext.cs) (if configuration registration needed)

**Verification:**

- GooglePlaceId field properly configured with max length
- Index exists on GooglePlaceId
- Field is nullable

---

## Step 4: Update FluentValidation Validators

**Objective:** Add validation rules for GooglePlaceId in Camp request validators.

**Tasks:**

1. Update `CreateCampValidator` in [src/Abuvi.API/Features/Camps/CreateCampValidator.cs](../../src/Abuvi.API/Features/Camps/CreateCampValidator.cs):

   ```csharp
   public class CreateCampValidator : AbstractValidator<CreateCampRequest>
   {
       public CreateCampValidator()
       {
           RuleFor(x => x.Name)
               .NotEmpty().WithMessage("El nombre es obligatorio")
               .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres");

           RuleFor(x => x.Description)
               .MaximumLength(2000).WithMessage("La descripción no puede exceder 2000 caracteres");

           RuleFor(x => x.Location)
               .MaximumLength(500).WithMessage("La ubicación no puede exceder 500 caracteres");

           // ADD THIS RULE
           RuleFor(x => x.GooglePlaceId)
               .MaximumLength(255).WithMessage("El ID de lugar de Google no puede exceder 255 caracteres");

           RuleFor(x => x.Latitude)
               .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue)
               .WithMessage("La latitud debe estar entre -90 y 90");

           RuleFor(x => x.Longitude)
               .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue)
               .WithMessage("La longitud debe estar entre -180 y 180");

           RuleFor(x => x.PricePerAdult)
               .GreaterThanOrEqualTo(0).WithMessage("El precio por adulto debe ser mayor o igual a 0");

           RuleFor(x => x.PricePerChild)
               .GreaterThanOrEqualTo(0).WithMessage("El precio por niño debe ser mayor o igual a 0");

           RuleFor(x => x.PricePerBaby)
               .GreaterThanOrEqualTo(0).WithMessage("El precio por bebé debe ser mayor o igual a 0");
       }
   }
   ```

2. Update `UpdateCampValidator` similarly (if separate validator exists):

   ```csharp
   public class UpdateCampValidator : AbstractValidator<UpdateCampRequest>
   {
       public UpdateCampValidator()
       {
           // ... existing rules ...

           // ADD THIS RULE
           RuleFor(x => x.GooglePlaceId)
               .MaximumLength(255).WithMessage("El ID de lugar de Google no puede exceder 255 caracteres");
       }
   }
   ```

**Files Modified:**

- [src/Abuvi.API/Features/Camps/CreateCampValidator.cs](../../src/Abuvi.API/Features/Camps/CreateCampValidator.cs)
- [src/Abuvi.API/Features/Camps/UpdateCampValidator.cs](../../src/Abuvi.API/Features/Camps/UpdateCampValidator.cs) (if exists)

**Verification:**

- Validator accepts null GooglePlaceId
- Validator rejects GooglePlaceId exceeding 255 characters
- Validation messages are in Spanish with proper gender agreement

---

## Step 5: Create Google Places Service

**Objective:** Implement service to communicate with Google Places API.

**Tasks:**

1. Create new feature folder:

   ```bash
   mkdir -p src/Abuvi.API/Features/GooglePlaces
   ```

2. Create [src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs](../../src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs):

   ```csharp
   namespace Abuvi.API.Features.GooglePlaces;

   public interface IGooglePlacesService
   {
       Task<IReadOnlyList<PlaceAutocomplete>> SearchPlacesAsync(string input, CancellationToken ct);
       Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId, CancellationToken ct);
   }

   public class GooglePlacesService(HttpClient httpClient, IConfiguration configuration, ILogger<GooglePlacesService> logger) : IGooglePlacesService
   {
       private readonly string _apiKey = configuration["GooglePlaces:ApiKey"]
           ?? throw new InvalidOperationException("GooglePlaces:ApiKey is required");
       private readonly string _autocompleteUrl = configuration["GooglePlaces:AutocompleteUrl"]
           ?? "https://maps.googleapis.com/maps/api/place/autocomplete/json";
       private readonly string _detailsUrl = configuration["GooglePlaces:DetailsUrl"]
           ?? "https://maps.googleapis.com/maps/api/place/details/json";

       public async Task<IReadOnlyList<PlaceAutocomplete>> SearchPlacesAsync(string input, CancellationToken ct)
       {
           if (string.IsNullOrWhiteSpace(input))
               return Array.Empty<PlaceAutocomplete>();

           var url = $"{_autocompleteUrl}?input={Uri.EscapeDataString(input)}&key={_apiKey}&language=es&components=country:es";

           try
           {
               var response = await httpClient.GetAsync(url, ct);
               response.EnsureSuccessStatusCode();

               var result = await response.Content.ReadFromJsonAsync<GoogleAutocompleteResponse>(ct);
               if (result?.Predictions == null)
                   return Array.Empty<PlaceAutocomplete>();

               return result.Predictions.Select(p => new PlaceAutocomplete(
                   p.PlaceId,
                   p.Description,
                   p.StructuredFormatting.MainText,
                   p.StructuredFormatting.SecondaryText
               )).ToList();
           }
           catch (HttpRequestException ex)
           {
               logger.LogError(ex, "Failed to call Google Places Autocomplete API");
               throw new ExternalServiceException("Google Places Autocomplete API is unavailable");
           }
       }

       public async Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId, CancellationToken ct)
       {
           var fields = "place_id,name,formatted_address,geometry,types";
           var url = $"{_detailsUrl}?place_id={Uri.EscapeDataString(placeId)}&key={_apiKey}&language=es&fields={fields}";

           try
           {
               var response = await httpClient.GetAsync(url, ct);
               response.EnsureSuccessStatusCode();

               var result = await response.Content.ReadFromJsonAsync<GooglePlaceDetailsResponse>(ct);
               if (result?.Result == null)
                   return null;

               var place = result.Result;
               return new PlaceDetails(
                   place.PlaceId,
                   place.Name,
                   place.FormattedAddress,
                   (decimal)place.Geometry.Location.Lat,
                   (decimal)place.Geometry.Location.Lng,
                   place.Types
               );
           }
           catch (HttpRequestException ex)
           {
               logger.LogError(ex, "Failed to call Google Places Details API for placeId {PlaceId}", placeId);
               throw new ExternalServiceException("Google Places Details API is unavailable");
           }
       }
   }

   // DTOs
   public record PlaceAutocomplete(
       string PlaceId,
       string Description,
       string MainText,
       string SecondaryText
   );

   public record PlaceDetails(
       string PlaceId,
       string Name,
       string FormattedAddress,
       decimal Latitude,
       decimal Longitude,
       string[] Types
   );

   // Custom exception
   public class ExternalServiceException(string message) : Exception(message);

   // Google API response models
   internal record GoogleAutocompleteResponse(List<Prediction> Predictions);
   internal record Prediction(
       string PlaceId,
       string Description,
       StructuredFormatting StructuredFormatting
   );
   internal record StructuredFormatting(string MainText, string SecondaryText);

   internal record GooglePlaceDetailsResponse(PlaceResult Result);
   internal record PlaceResult(
       string PlaceId,
       string Name,
       string FormattedAddress,
       Geometry Geometry,
       string[] Types
   );
   internal record Geometry(Location Location);
   internal record Location(double Lat, double Lng);
   ```

**Files Created:**

- [src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs](../../src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs) (NEW)

**Verification:**

- Service compiles without errors
- Empty input returns empty list
- HttpClient is properly injected
- Configuration values are read correctly
- ExternalServiceException is thrown on HTTP failures

---

## Step 6: Create Google Places Endpoints

**Objective:** Create minimal API endpoints that proxy Google Places API calls.

**Tasks:**

1. Create [src/Abuvi.API/Features/GooglePlaces/GooglePlacesEndpoints.cs](../../src/Abuvi.API/Features/GooglePlaces/GooglePlacesEndpoints.cs):

   ```csharp
   namespace Abuvi.API.Features.GooglePlaces;

   public static class GooglePlacesEndpoints
   {
       public static void MapGooglePlacesEndpoints(this IEndpointRouteBuilder app)
       {
           var group = app.MapGroup("/api/places")
               .WithTags("Google Places")
               .RequireAuthorization(); // Only authenticated users

           group.MapPost("/autocomplete", SearchPlaces)
               .WithName("SearchPlaces")
               .Produces<ApiResponse<IReadOnlyList<PlaceAutocomplete>>>();

           group.MapPost("/details", GetPlaceDetails)
               .WithName("GetPlaceDetails")
               .Produces<ApiResponse<PlaceDetails>>();
       }

       private static async Task<IResult> SearchPlaces(
           AutocompleteRequest request,
           IGooglePlacesService service,
           CancellationToken ct)
       {
           try
           {
               var results = await service.SearchPlacesAsync(request.Input, ct);
               return Results.Ok(ApiResponse<IReadOnlyList<PlaceAutocomplete>>.Ok(results));
           }
           catch (ExternalServiceException ex)
           {
               return Results.StatusCode(503, ApiResponse<IReadOnlyList<PlaceAutocomplete>>.Fail(
                   "El servicio de ubicaciones no está disponible. Por favor intenta más tarde.",
                   "PLACES_SERVICE_UNAVAILABLE"
               ));
           }
       }

       private static async Task<IResult> GetPlaceDetails(
           PlaceDetailsRequest request,
           IGooglePlacesService service,
           CancellationToken ct)
       {
           try
           {
               var details = await service.GetPlaceDetailsAsync(request.PlaceId, ct);
               if (details == null)
               {
                   return Results.NotFound(ApiResponse<PlaceDetails>.NotFound(
                       "No se encontró información para este lugar"
                   ));
               }

               return Results.Ok(ApiResponse<PlaceDetails>.Ok(details));
           }
           catch (ExternalServiceException ex)
           {
               return Results.StatusCode(503, ApiResponse<PlaceDetails>.Fail(
                   "El servicio de ubicaciones no está disponible. Por favor intenta más tarde.",
                   "PLACES_SERVICE_UNAVAILABLE"
               ));
           }
       }
   }

   public record AutocompleteRequest(string Input);
   public record PlaceDetailsRequest(string PlaceId);
   ```

**Files Created:**

- [src/Abuvi.API/Features/GooglePlaces/GooglePlacesEndpoints.cs](../../src/Abuvi.API/Features/GooglePlaces/GooglePlacesEndpoints.cs) (NEW)

**Verification:**

- Endpoints use ApiResponse<T> envelope pattern
- Error responses include Spanish messages
- Authentication is required for both endpoints
- Proper HTTP status codes: 200 (OK), 404 (Not Found), 503 (Service Unavailable)

---

## Step 7: Register Services and Endpoints

**Objective:** Wire up Google Places services and endpoints in Program.cs.

**Tasks:**

1. Update [src/Abuvi.API/Program.cs](../../src/Abuvi.API/Program.cs):

   **Add service registration (after existing service registrations):**

   ```csharp
   // Google Places API integration
   builder.Services.AddHttpClient<IGooglePlacesService, GooglePlacesService>();
   builder.Services.AddScoped<IGooglePlacesService, GooglePlacesService>();
   ```

   **Add endpoint mapping (after existing endpoint mappings):**

   ```csharp
   // Map Google Places endpoints
   app.MapGooglePlacesEndpoints();
   ```

2. Add configuration section to [src/Abuvi.API/appsettings.json](../../src/Abuvi.API/appsettings.json):

   ```json
   {
     "GooglePlaces": {
       "ApiKey": "",
       "AutocompleteUrl": "https://maps.googleapis.com/maps/api/place/autocomplete/json",
       "DetailsUrl": "https://maps.googleapis.com/maps/api/place/details/json"
     }
   }
   ```

3. Configure API key in User Secrets for development:

   ```bash
   dotnet user-secrets set "GooglePlaces:ApiKey" "YOUR_DEV_API_KEY_HERE" --project src/Abuvi.API
   ```

**Files Modified:**

- [src/Abuvi.API/Program.cs](../../src/Abuvi.API/Program.cs)
- [src/Abuvi.API/appsettings.json](../../src/Abuvi.API/appsettings.json)

**Verification:**

- Application starts without errors
- Services are registered in DI container
- Endpoints appear in Swagger/OpenAPI documentation
- Configuration is loaded correctly
- Missing API key throws clear exception on startup or first use

---

## Step 8: Update Camps Endpoints to Handle GooglePlaceId

**Objective:** Ensure existing Camp CRUD endpoints properly handle the new GooglePlaceId field.

**Tasks:**

1. Locate existing Camps endpoints (likely in [src/Abuvi.API/Features/Camps/CampsEndpoints.cs](../../src/Abuvi.API/Features/Camps/CampsEndpoints.cs) or similar).

2. Verify CREATE endpoint maps GooglePlaceId from request to entity:

   ```csharp
   // Example - adapt to your existing code structure
   var camp = new Camp
   {
       Id = Guid.NewGuid(),
       Name = request.Name,
       Description = request.Description,
       Location = request.Location,
       Latitude = request.Latitude,
       Longitude = request.Longitude,
       GooglePlaceId = request.GooglePlaceId, // ADD THIS LINE
       PricePerAdult = request.PricePerAdult,
       PricePerChild = request.PricePerChild,
       PricePerBaby = request.PricePerBaby,
       CreatedAt = DateTime.UtcNow,
       UpdatedAt = DateTime.UtcNow
   };
   ```

3. Verify UPDATE endpoint maps GooglePlaceId:

   ```csharp
   // Example - adapt to your existing code structure
   camp.Name = request.Name;
   camp.Description = request.Description;
   camp.Location = request.Location;
   camp.Latitude = request.Latitude;
   camp.Longitude = request.Longitude;
   camp.GooglePlaceId = request.GooglePlaceId; // ADD THIS LINE
   camp.PricePerAdult = request.PricePerAdult;
   camp.PricePerChild = request.PricePerChild;
   camp.PricePerBaby = request.PricePerBaby;
   camp.IsActive = request.IsActive;
   camp.UpdatedAt = DateTime.UtcNow;
   ```

4. Verify response mapping includes GooglePlaceId:

   ```csharp
   // Example - adapt to your existing code structure
   return new CampResponse(
       camp.Id,
       camp.Name,
       camp.Description,
       camp.Location,
       camp.Latitude,
       camp.Longitude,
       camp.GooglePlaceId, // ADD THIS LINE
       camp.PricePerAdult,
       camp.PricePerChild,
       camp.PricePerBaby,
       camp.IsActive,
       camp.CreatedAt,
       camp.UpdatedAt
   );
   ```

**Files Modified:**

- [src/Abuvi.API/Features/Camps/CampsEndpoints.cs](../../src/Abuvi.API/Features/Camps/CampsEndpoints.cs) (or wherever Camp CRUD endpoints are defined)

**Verification:**

- Creating a camp with GooglePlaceId saves it to database
- Updating a camp can modify GooglePlaceId
- Reading a camp returns GooglePlaceId in response
- Existing camps without GooglePlaceId return null (not break)

---

## Step 9: Write Unit Tests for GooglePlacesService

**Objective:** Achieve 90% code coverage for GooglePlacesService with comprehensive unit tests.

**Tasks:**

1. Create test file [src/Abuvi.Tests/Unit/Features/GooglePlaces/GooglePlacesServiceTests.cs](../../src/Abuvi.Tests/Unit/Features/GooglePlaces/GooglePlacesServiceTests.cs):

   ```csharp
   using Abuvi.API.Features.GooglePlaces;
   using FluentAssertions;
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.Logging;
   using Moq;
   using Moq.Protected;
   using System.Net;
   using Xunit;

   namespace Abuvi.Tests.Unit.Features.GooglePlaces;

   public class GooglePlacesServiceTests
   {
       [Fact]
       public async Task SearchPlacesAsync_WithValidInput_ReturnsPlaces()
       {
           // Arrange
           var mockHttp = new Mock<HttpMessageHandler>();
           mockHttp.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(@"{
                       ""predictions"": [{
                           ""place_id"": ""ChIJN1t_tDeuEmsRUsoyG83frY4"",
                           ""description"": ""Camping El Pinar, Madrid"",
                           ""structured_formatting"": {
                               ""main_text"": ""Camping El Pinar"",
                               ""secondary_text"": ""Madrid, España""
                           }
                       }]
                   }")
               });

           var httpClient = new HttpClient(mockHttp.Object);
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>
               {
                   { "GooglePlaces:ApiKey", "test_key" }
               })
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();
           var service = new GooglePlacesService(httpClient, config, logger);

           // Act
           var result = await service.SearchPlacesAsync("Camping", CancellationToken.None);

           // Assert
           result.Should().HaveCount(1);
           result[0].PlaceId.Should().Be("ChIJN1t_tDeuEmsRUsoyG83frY4");
           result[0].MainText.Should().Be("Camping El Pinar");
           result[0].SecondaryText.Should().Be("Madrid, España");
           result[0].Description.Should().Be("Camping El Pinar, Madrid");
       }

       [Fact]
       public async Task SearchPlacesAsync_WithEmptyInput_ReturnsEmpty()
       {
           // Arrange
           var httpClient = new HttpClient();
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>
               {
                   { "GooglePlaces:ApiKey", "test_key" }
               })
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();
           var service = new GooglePlacesService(httpClient, config, logger);

           // Act
           var result = await service.SearchPlacesAsync("", CancellationToken.None);

           // Assert
           result.Should().BeEmpty();
       }

       [Fact]
       public async Task SearchPlacesAsync_WithWhitespaceInput_ReturnsEmpty()
       {
           // Arrange
           var httpClient = new HttpClient();
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>
               {
                   { "GooglePlaces:ApiKey", "test_key" }
               })
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();
           var service = new GooglePlacesService(httpClient, config, logger);

           // Act
           var result = await service.SearchPlacesAsync("   ", CancellationToken.None);

           // Assert
           result.Should().BeEmpty();
       }

       [Fact]
       public async Task GetPlaceDetailsAsync_WithValidPlaceId_ReturnsDetails()
       {
           // Arrange
           var mockHttp = new Mock<HttpMessageHandler>();
           mockHttp.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(@"{
                       ""result"": {
                           ""place_id"": ""ChIJN1t_tDeuEmsRUsoyG83frY4"",
                           ""name"": ""Camping El Pinar"",
                           ""formatted_address"": ""Calle Example, 123, Madrid, España"",
                           ""geometry"": {
                               ""location"": {
                                   ""lat"": 40.416775,
                                   ""lng"": -3.703790
                               }
                           },
                           ""types"": [""campground"", ""lodging""]
                       }
                   }")
               });

           var httpClient = new HttpClient(mockHttp.Object);
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>
               {
                   { "GooglePlaces:ApiKey", "test_key" }
               })
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();
           var service = new GooglePlacesService(httpClient, config, logger);

           // Act
           var result = await service.GetPlaceDetailsAsync("ChIJN1t_tDeuEmsRUsoyG83frY4", CancellationToken.None);

           // Assert
           result.Should().NotBeNull();
           result!.PlaceId.Should().Be("ChIJN1t_tDeuEmsRUsoyG83frY4");
           result.Name.Should().Be("Camping El Pinar");
           result.FormattedAddress.Should().Be("Calle Example, 123, Madrid, España");
           result.Latitude.Should().Be(40.416775m);
           result.Longitude.Should().Be(-3.703790m);
           result.Types.Should().Contain("campground");
           result.Types.Should().Contain("lodging");
       }

       [Fact]
       public async Task SearchPlacesAsync_WhenHttpRequestFails_ThrowsExternalServiceException()
       {
           // Arrange
           var mockHttp = new Mock<HttpMessageHandler>();
           mockHttp.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ThrowsAsync(new HttpRequestException("Network error"));

           var httpClient = new HttpClient(mockHttp.Object);
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>
               {
                   { "GooglePlaces:ApiKey", "test_key" }
               })
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();
           var service = new GooglePlacesService(httpClient, config, logger);

           // Act & Assert
           await service.Invoking(s => s.SearchPlacesAsync("test", CancellationToken.None))
               .Should().ThrowAsync<ExternalServiceException>()
               .WithMessage("Google Places Autocomplete API is unavailable");
       }

       [Fact]
       public async Task GetPlaceDetailsAsync_WhenHttpRequestFails_ThrowsExternalServiceException()
       {
           // Arrange
           var mockHttp = new Mock<HttpMessageHandler>();
           mockHttp.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ThrowsAsync(new HttpRequestException("Network error"));

           var httpClient = new HttpClient(mockHttp.Object);
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>
               {
                   { "GooglePlaces:ApiKey", "test_key" }
               })
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();
           var service = new GooglePlacesService(httpClient, config, logger);

           // Act & Assert
           await service.Invoking(s => s.GetPlaceDetailsAsync("ChIJ123", CancellationToken.None))
               .Should().ThrowAsync<ExternalServiceException>()
               .WithMessage("Google Places Details API is unavailable");
       }

       [Fact]
       public void GooglePlacesService_WithoutApiKey_ThrowsInvalidOperationException()
       {
           // Arrange
           var httpClient = new HttpClient();
           var config = new ConfigurationBuilder()
               .AddInMemoryCollection(new Dictionary<string, string>())
               .Build();
           var logger = Mock.Of<ILogger<GooglePlacesService>>();

           // Act & Assert
           var act = () => new GooglePlacesService(httpClient, config, logger);
           act.Should().Throw<InvalidOperationException>()
               .WithMessage("GooglePlaces:ApiKey is required");
       }
   }
   ```

**Files Created:**

- [src/Abuvi.Tests/Unit/Features/GooglePlaces/GooglePlacesServiceTests.cs](../../src/Abuvi.Tests/Unit/Features/GooglePlaces/GooglePlacesServiceTests.cs) (NEW)

**Verification:**

- All 7 tests pass
- Tests use AAA (Arrange-Act-Assert) pattern
- Mock HTTP responses for isolated testing
- Test positive cases, edge cases, and error scenarios
- Coverage >= 90% for GooglePlacesService

---

## Step 10: Write Integration Tests for GooglePlaces Endpoints

**Objective:** Test Google Places endpoints with authentication and proper HTTP responses.

**Tasks:**

1. Create test file [src/Abuvi.Tests/Integration/Features/GooglePlaces/GooglePlacesEndpointsTests.cs](../../src/Abuvi.Tests/Integration/Features/GooglePlaces/GooglePlacesEndpointsTests.cs):

   ```csharp
   using Abuvi.API.Features.GooglePlaces;
   using FluentAssertions;
   using Microsoft.AspNetCore.Mvc.Testing;
   using System.Net;
   using System.Net.Http.Headers;
   using System.Net.Http.Json;
   using Xunit;

   namespace Abuvi.Tests.Integration.Features.GooglePlaces;

   public class GooglePlacesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly HttpClient _client;
       private readonly WebApplicationFactory<Program> _factory;

       public GooglePlacesEndpointsTests(WebApplicationFactory<Program> factory)
       {
           _factory = factory;
           _client = factory.CreateClient();

           // Add auth token (adjust based on your auth implementation)
           var token = TestHelper.GetValidToken();
           _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
       }

       [Fact]
       public async Task SearchPlaces_WithValidInput_Returns200()
       {
           // Arrange
           var request = new { Input = "Camping Madrid" };

           // Act
           var response = await _client.PostAsJsonAsync("/api/places/autocomplete", request);

           // Assert
           response.StatusCode.Should().Be(HttpStatusCode.OK);
           var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaceAutocomplete>>>();
           body.Should().NotBeNull();
           body!.Success.Should().BeTrue();
           body.Data.Should().NotBeNull();
       }

       [Fact]
       public async Task SearchPlaces_WithoutAuth_Returns401()
       {
           // Arrange
           var client = _factory.CreateClient();
           var request = new { Input = "Camping" };

           // Act
           var response = await client.PostAsJsonAsync("/api/places/autocomplete", request);

           // Assert
           response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
       }

       [Fact]
       public async Task SearchPlaces_WithEmptyInput_Returns200WithEmptyList()
       {
           // Arrange
           var request = new { Input = "" };

           // Act
           var response = await _client.PostAsJsonAsync("/api/places/autocomplete", request);

           // Assert
           response.StatusCode.Should().Be(HttpStatusCode.OK);
           var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlaceAutocomplete>>>();
           body.Should().NotBeNull();
           body!.Success.Should().BeTrue();
           body.Data.Should().BeEmpty();
       }

       [Fact]
       public async Task GetPlaceDetails_WithValidPlaceId_Returns200()
       {
           // Arrange
           var request = new { PlaceId = "ChIJN1t_tDeuEmsRUsoyG83frY4" };

           // Act
           var response = await _client.PostAsJsonAsync("/api/places/details", request);

           // Assert
           response.StatusCode.Should().Be(HttpStatusCode.OK);
           var body = await response.Content.ReadFromJsonAsync<ApiResponse<PlaceDetails>>();
           body.Should().NotBeNull();
           body!.Success.Should().BeTrue();
           body.Data.Should().NotBeNull();
       }

       [Fact]
       public async Task GetPlaceDetails_WithoutAuth_Returns401()
       {
           // Arrange
           var client = _factory.CreateClient();
           var request = new { PlaceId = "ChIJ123" };

           // Act
           var response = await client.PostAsJsonAsync("/api/places/details", request);

           // Assert
           response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
       }
   }
   ```

2. Create test helper if needed (adjust to your existing test infrastructure):

   ```csharp
   // src/Abuvi.Tests/Helpers/TestHelper.cs
   public static class TestHelper
   {
       public static string GetValidToken()
       {
           // Return a valid JWT token for testing
           // Implementation depends on your auth setup
       }
   }
   ```

**Files Created:**

- [src/Abuvi.Tests/Integration/Features/GooglePlaces/GooglePlacesEndpointsTests.cs](../../src/Abuvi.Tests/Integration/Features/GooglePlaces/GooglePlacesEndpointsTests.cs) (NEW)
- [src/Abuvi.Tests/Helpers/TestHelper.cs](../../src/Abuvi.Tests/Helpers/TestHelper.cs) (NEW or UPDATE)

**Verification:**

- All 5 integration tests pass
- Tests verify authentication requirements
- Tests verify proper HTTP status codes
- Tests use real API calls to Google (or mocked with test server configuration)

---

## Step 11: Manual Testing and Verification

**Objective:** Manually verify all endpoints work correctly.

**Tasks:**

1. Run the application:

   ```bash
   dotnet run --project src/Abuvi.API
   ```

2. Open Swagger UI: `https://localhost:5001/swagger`

3. **Test Authentication:**
   - Verify `/api/places/autocomplete` requires authentication
   - Verify `/api/places/details` requires authentication
   - Test with invalid/missing token → Should return 401

4. **Test Autocomplete Endpoint:**

   ```bash
   POST /api/places/autocomplete
   Authorization: Bearer {your_token}
   Content-Type: application/json

   {
     "input": "Camping Madrid"
   }
   ```

   **Expected Response (200 OK):**

   ```json
   {
     "success": true,
     "data": [
       {
         "placeId": "ChIJ...",
         "description": "Camping El Pinar, Madrid",
         "mainText": "Camping El Pinar",
         "secondaryText": "Madrid, España"
       }
     ],
     "error": null
   }
   ```

5. **Test Place Details Endpoint:**

   ```bash
   POST /api/places/details
   Authorization: Bearer {your_token}
   Content-Type: application/json

   {
     "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4"
   }
   ```

   **Expected Response (200 OK):**

   ```json
   {
     "success": true,
     "data": {
       "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
       "name": "Camping El Pinar",
       "formattedAddress": "Calle Example, 123, Madrid, España",
       "latitude": 40.416775,
       "longitude": -3.703790,
       "types": ["campground", "lodging"]
     },
     "error": null
   }
   ```

6. **Test Invalid Place ID:**

   ```bash
   POST /api/places/details
   {
     "placeId": "INVALID_ID"
   }
   ```

   **Expected Response (404 Not Found):**

   ```json
   {
     "success": false,
     "data": null,
     "error": {
       "message": "No se encontró información para este lugar",
       "code": "NOT_FOUND"
     }
   }
   ```

7. **Test Service Unavailable (disable API key temporarily):**
   - Remove API key from configuration
   - Make request
   - **Expected Response (503 Service Unavailable):**

     ```json
     {
       "success": false,
       "data": null,
       "error": {
         "message": "El servicio de ubicaciones no está disponible. Por favor intenta más tarde.",
         "code": "PLACES_SERVICE_UNAVAILABLE"
       }
     }
     ```

8. **Test Camp CRUD with GooglePlaceId:**
   - Create a camp with GooglePlaceId → Should save successfully
   - Read the camp → Should return GooglePlaceId
   - Update the camp's GooglePlaceId → Should update successfully
   - Create a camp without GooglePlaceId → Should work (null)

**Verification Checklist:**

- [ ] Autocomplete endpoint returns suggestions
- [ ] Place details endpoint returns coordinates
- [ ] Authentication is required for both endpoints
- [ ] Error responses use Spanish messages
- [ ] HTTP status codes are correct (200, 401, 404, 503)
- [ ] Camp CRUD operations handle GooglePlaceId correctly
- [ ] Existing camps without GooglePlaceId still work

---

## Step 12: Update API Documentation

**Objective:** Document the new Google Places endpoints in the project's API documentation.

**Tasks:**

1. Update [ai-specs/specs/api-endpoints.md](../../ai-specs/specs/api-endpoints.md):

   Add new section for Google Places endpoints:

   ```markdown
   ## Google Places API

   ### POST /api/places/autocomplete

   Search for places using Google Places Autocomplete API (backend proxy).

   **Authentication:** Required

   **Request Body:**
   ```json
   {
     "input": "string (min 3 characters recommended)"
   }
   ```

   **Response (200 OK):**

   ```json
   {
     "success": true,
     "data": [
       {
         "placeId": "string",
         "description": "string",
         "mainText": "string",
         "secondaryText": "string"
       }
     ],
     "error": null
   }
   ```

   **Error Responses:**
   - `401 Unauthorized` - Missing or invalid authentication token
   - `503 Service Unavailable` - Google Places API unavailable

   ---

   ### POST /api/places/details

   Get detailed information for a specific place by Place ID.

   **Authentication:** Required

   **Request Body:**

   ```json
   {
     "placeId": "string (Google Place ID)"
   }
   ```

   **Response (200 OK):**

   ```json
   {
     "success": true,
     "data": {
       "placeId": "string",
       "name": "string",
       "formattedAddress": "string",
       "latitude": "decimal",
       "longitude": "decimal",
       "types": ["string"]
     },
     "error": null
   }
   ```

   **Error Responses:**
   - `401 Unauthorized` - Missing or invalid authentication token
   - `404 Not Found` - Place ID not found
   - `503 Service Unavailable` - Google Places API unavailable

   ```

2. Update Camp endpoints documentation to reflect GooglePlaceId field:

   ```markdown
   ### POST /api/camps

   **Request Body:**
   ```json
   {
     "name": "string (required, max 200)",
     "description": "string (optional, max 2000)",
     "location": "string (optional, max 500)",
     "latitude": "decimal (optional, -90 to 90)",
     "longitude": "decimal (optional, -180 to 180)",
     "googlePlaceId": "string (optional, max 255)",  // NEW FIELD
     "pricePerAdult": "decimal (required, >= 0)",
     "pricePerChild": "decimal (required, >= 0)",
     "pricePerBaby": "decimal (required, >= 0)"
   }
   ```

   **Response:**

   ```json
   {
     "success": true,
     "data": {
       "id": "uuid",
       "name": "string",
       "description": "string",
       "location": "string",
       "latitude": "decimal",
       "longitude": "decimal",
       "googlePlaceId": "string",  // NEW FIELD
       "pricePerAdult": "decimal",
       "pricePerChild": "decimal",
       "pricePerBaby": "decimal",
       "isActive": "boolean",
       "createdAt": "datetime",
       "updatedAt": "datetime"
     }
   }
   ```

   ```

3. Add configuration documentation to project README or deployment guide:

   ```markdown
   ### Google Places API Configuration

   **Development:**
   ```bash
   dotnet user-secrets set "GooglePlaces:ApiKey" "YOUR_API_KEY" --project src/Abuvi.API
   ```

   **Production (Environment Variables):**

   ```bash
   export GOOGLEPLACES__APIKEY="your_production_key"
   ```

   **Production (Azure App Service):**

   ```bash
   az webapp config appsettings set \
     --resource-group abuvi-rg \
     --name abuvi-api \
     --settings GooglePlaces__ApiKey="YOUR_PROD_KEY"
   ```

   **Obtaining API Key:**
   1. Go to [Google Cloud Console](https://console.cloud.google.com/)
   2. Create or select a project
   3. Enable "Places API"
   4. Create credentials (API Key)
   5. Restrict API key to Places API only
   6. Configure HTTP referrer restrictions for production domain

   ```

**Files Modified:**

- [ai-specs/specs/api-endpoints.md](../../ai-specs/specs/api-endpoints.md)
- [README.md](../../README.md) or deployment documentation

**Verification:**

- Documentation is clear and complete
- Example requests/responses are accurate
- Configuration instructions are tested and working
- Error codes match implementation

---

## Testing Checklist

Before marking this feature as complete, ensure all tests pass:

### Unit Tests

- [ ] `GooglePlacesServiceTests` - All 7 tests pass
  - [ ] SearchPlacesAsync with valid input returns places
  - [ ] SearchPlacesAsync with empty input returns empty list
  - [ ] SearchPlacesAsync with whitespace returns empty list
  - [ ] GetPlaceDetailsAsync with valid place ID returns details
  - [ ] SearchPlacesAsync with HTTP failure throws ExternalServiceException
  - [ ] GetPlaceDetailsAsync with HTTP failure throws ExternalServiceException
  - [ ] Constructor without API key throws InvalidOperationException

### Integration Tests

- [ ] `GooglePlacesEndpointsTests` - All 5 tests pass
  - [ ] SearchPlaces with valid input returns 200
  - [ ] SearchPlaces without auth returns 401
  - [ ] SearchPlaces with empty input returns 200 with empty list
  - [ ] GetPlaceDetails with valid place ID returns 200
  - [ ] GetPlaceDetails without auth returns 401

### Manual Testing

- [ ] Autocomplete endpoint returns Google Places suggestions
- [ ] Place details endpoint returns coordinates and address
- [ ] Authentication is enforced on both endpoints
- [ ] Error messages are in Spanish
- [ ] HTTP status codes are correct
- [ ] Camp CRUD operations handle GooglePlaceId field
- [ ] Existing camps without GooglePlaceId work correctly

### Code Coverage

- [ ] GooglePlacesService >= 90% coverage
- [ ] GooglePlacesEndpoints >= 85% coverage

---

## Error Response Format

All error responses follow the `ApiResponse<T>` envelope pattern:

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Spanish error message for users",
    "code": "ERROR_CODE"
  }
}
```

### Error Codes

| Code | HTTP Status | Spanish Message | Scenario |
|------|-------------|-----------------|----------|
| `PLACES_SERVICE_UNAVAILABLE` | 503 | El servicio de ubicaciones no está disponible. Por favor intenta más tarde. | Google API unavailable or network error |
| `NOT_FOUND` | 404 | No se encontró información para este lugar | Invalid Place ID |
| `VALIDATION_ERROR` | 400 | Error de validación | Invalid request data |
| `UNAUTHORIZED` | 401 | No autorizado | Missing or invalid authentication token |

---

## Dependencies

### External Services

- **Google Places API** - Requires API key configured in User Secrets (dev) or environment variables (prod)
- API key restrictions configured in Google Cloud Console

### NuGet Packages

No additional packages required. Uses existing:

- `Microsoft.AspNetCore.Http`
- `FluentValidation`
- `Microsoft.EntityFrameworkCore`

### Database

- PostgreSQL with EF Core migration applied

---

## Verification Steps

After completing all implementation steps:

1. **Build succeeds:**

   ```bash
   dotnet build
   ```

2. **All tests pass:**

   ```bash
   dotnet test
   ```

3. **Migration applied:**

   ```bash
   dotnet ef database update --project src/Abuvi.API
   ```

4. **Application runs:**

   ```bash
   dotnet run --project src/Abuvi.API
   ```

5. **Swagger shows new endpoints:**
   - Navigate to `https://localhost:5001/swagger`
   - Verify `/api/places/autocomplete` endpoint exists
   - Verify `/api/places/details` endpoint exists
   - Both require authentication

6. **Manual API calls work:**
   - Test autocomplete with valid input → Returns suggestions
   - Test place details with valid ID → Returns coordinates
   - Test without auth → Returns 401
   - Test with invalid place ID → Returns 404

7. **Camp operations work:**
   - Create camp with GooglePlaceId → Saves successfully
   - Update camp GooglePlaceId → Updates successfully
   - Read camp → Returns GooglePlaceId
   - Existing camps without GooglePlaceId → Still work

---

## Commit Strategy

Suggested commit breakdown for this feature:

1. **Database migration:**

   ```
   feat(camps): Add GooglePlaceId field to Camps table

   - Add migration for GooglePlaceId column (varchar 255, nullable)
   - Add index on GooglePlaceId
   - Update Camp entity and DTOs
   ```

2. **Google Places service:**

   ```
   feat(google-places): Implement GooglePlacesService for API integration

   - Create IGooglePlacesService interface
   - Implement autocomplete and place details methods
   - Add ExternalServiceException for error handling
   - Configure HttpClient and API key
   ```

3. **API endpoints:**

   ```
   feat(google-places): Add backend proxy endpoints for Google Places

   - POST /api/places/autocomplete
   - POST /api/places/details
   - Both require authentication
   - Return ApiResponse<T> envelope
   ```

4. **Validation and configuration:**

   ```
   feat(camps): Add validation for GooglePlaceId field

   - Update CreateCampValidator with GooglePlaceId max length rule
   - Update UpdateCampValidator with GooglePlaceId max length rule
   - Add EF Core configuration for GooglePlaceId field
   ```

5. **Tests:**

   ```
   test(google-places): Add comprehensive unit and integration tests

   - GooglePlacesServiceTests (7 unit tests)
   - GooglePlacesEndpointsTests (5 integration tests)
   - Achieve 90% code coverage
   ```

6. **Documentation:**

   ```
   docs(api): Document Google Places endpoints and configuration

   - Add Google Places endpoints to api-endpoints.md
   - Update Camp endpoint documentation with GooglePlaceId
   - Add configuration instructions for API key
   ```

---

## Next Steps

After completing this backend implementation:

1. **Frontend Integration:** Implement frontend autocomplete UI (separate ticket)
2. **Production Deployment:** Apply migration and configure API key in production
3. **Monitoring:** Set up cost monitoring for Google Places API usage
4. **Future Enhancements:**
   - Add response caching to reduce API costs
   - Implement rate limiting per user
   - Add photo import from Google Places

---

## Definition of Done

This backend ticket is complete when:

- [ ] All 12 implementation steps completed
- [ ] Database migration applied successfully
- [ ] All unit tests pass (90% coverage)
- [ ] All integration tests pass
- [ ] Manual testing checklist complete
- [ ] API documentation updated
- [ ] Code reviewed and approved
- [ ] Feature branch merged to main

---

**Estimated Time:** 2-3 days
**Priority:** High
**Complexity:** Medium
