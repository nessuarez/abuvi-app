# Google Places API - Auto-completado de Datos de Campamento (ENRICHED)

## Executive Summary

Integrate Google Places API to auto-complete camp location data (name, coordinates, address) when creating or editing camps, reducing manual data entry errors and improving UX.

**Target Entities:** `Camp` (camp location template)
**Primary Use Case:** Creating/editing camp locations with autocomplete suggestions
**Key Dependencies:** Google Places API, Backend proxy endpoints, PrimeVue AutoComplete component

---

## Table of Contents

1. [Objective](#objective)
2. [Context](#context)
3. [Data Model Changes](#data-model-changes)
4. [Backend Implementation](#backend-implementation)
5. [Frontend Implementation](#frontend-implementation)
6. [Testing Specifications](#testing-specifications)
7. [Security & Performance](#security--performance)
8. [Deployment & Migration](#deployment--migration)
9. [Acceptance Criteria](#acceptance-criteria)

---

## Objective

Facilitate the creation and editing of camp locations by integrating Google Places API to auto-complete location data (name, coordinates, address) from user input, reducing manual data entry and coordinate lookup errors.

---

## Context

### Current Behavior

When creating or editing a camp location (the `Camp` entity, which serves as a template for yearly editions):

**Manual Data Entry:**

- User must manually enter:
  - `Name` - Camp name
  - `Description` - Optional description
  - `Location` - Optional address/location text
  - `Latitude` - Geographic latitude (optional)
  - `Longitude` - Geographic longitude (optional)
  - Pricing fields (PricePerAdult, PricePerChild, PricePerBaby)

**Problems:**

1. **Tedious:** Users must find coordinates manually (e.g., using Google Maps, copying lat/lng)
2. **Error-prone:** Coordinates can be mistyped or swapped (lat/lng confusion)
3. **Inconsistent:** Location text may not match actual geographic coordinates
4. **Time-consuming:** Extra steps slow down camp location creation

### Proposed Solution

**Google Places Autocomplete Integration:**

1. **Search-as-you-type:** As user types camp name, show Google Places suggestions
2. **Select from suggestions:** User picks the correct location from dropdown
3. **Auto-populate fields:** On selection, automatically fill:
   - `Name` - Place name from Google
   - `Location` - Formatted address from Google
   - `Latitude` - Geographic latitude
   - `Longitude` - Geographic longitude
   - `GooglePlaceId` (NEW) - Google Place ID for reference
4. **Manual override:** User can still edit any auto-filled field
5. **Optional usage:** User can skip autocomplete and enter data manually

**Benefits:**

- ✅ Faster camp creation (seconds vs minutes)
- ✅ Accurate coordinates from trusted source
- ✅ Consistent address formatting
- ✅ Reduced data entry errors
- ✅ Better UX for non-technical users

---

## Data Model Changes

### Database Schema Changes

#### 1. Add GooglePlaceId to Camp Table

**Migration:** `AddGooglePlaceIdToCamp`

```sql
ALTER TABLE "Camps"
ADD COLUMN "GooglePlaceId" VARCHAR(255) NULL;

CREATE INDEX "IX_Camps_GooglePlaceId" ON "Camps" ("GooglePlaceId");

COMMENT ON COLUMN "Camps"."GooglePlaceId" IS 'Google Place ID for reference (e.g., ChIJN1t_tDeuEmsRUsoyG83frY4). Used to re-fetch updated data from Google Places API.';
```

**Field Specification:**

- **Name:** `GooglePlaceId`
- **Type:** `string?` (nullable)
- **Max Length:** 255 characters
- **Purpose:** Store Google Place ID for future reference (e.g., updating place data, photos)
- **Index:** Yes (for lookups)
- **Nullable:** Yes (existing camps won't have it, manual entry allowed)

### Updated Camp Entity

```csharp
// src/Abuvi.API/Features/Camps/CampsModels.cs

public class Camp
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // NEW FIELD
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

### Updated DTOs

```csharp
// src/Abuvi.API/Features/Camps/CampsModels.cs

public record CreateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId, // NEW
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby
);

public record UpdateCampRequest(
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId, // NEW
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive
);

public record CampResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    decimal? Latitude,
    decimal? Longitude,
    string? GooglePlaceId, // NEW
    decimal PricePerAdult,
    decimal PricePerChild,
    decimal PricePerBaby,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

### Entity Configuration Update

```csharp
// src/Abuvi.API/Data/Configurations/CampConfiguration.cs

public class CampConfiguration : IEntityTypeConfiguration<Camp>
{
    public void Configure(EntityTypeBuilder<Camp> builder)
    {
        // ... existing configuration ...

        builder.Property(c => c.GooglePlaceId)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.HasIndex(c => c.GooglePlaceId)
            .HasDatabaseName("IX_Camps_GooglePlaceId");
    }
}
```

---

## Backend Implementation

### 1. Google Places Service

**Purpose:** Encapsulates all Google Places API communication

**File:** `src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs`

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

### 2. Google Places Endpoints

**Purpose:** Backend proxy for Google Places API calls

**File:** `src/Abuvi.API/Features/GooglePlaces/GooglePlacesEndpoints.cs`

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

### 3. Service Registration

**File:** `src/Abuvi.API/Program.cs`

```csharp
// Add Google Places service
builder.Services.AddHttpClient<IGooglePlacesService, GooglePlacesService>();
builder.Services.AddScoped<IGooglePlacesService, GooglePlacesService>();

// ... later in the file ...

// Map Google Places endpoints
app.MapGooglePlacesEndpoints();
```

### 4. Configuration

**File:** `src/Abuvi.API/appsettings.json`

```json
{
  "GooglePlaces": {
    "ApiKey": "",
    "AutocompleteUrl": "https://maps.googleapis.com/maps/api/place/autocomplete/json",
    "DetailsUrl": "https://maps.googleapis.com/maps/api/place/details/json"
  }
}
```

**User Secrets (Development):**

```bash
dotnet user-secrets set "GooglePlaces:ApiKey" "YOUR_API_KEY_HERE" --project src/Abuvi.API
```

**Environment Variables (Production):**

```bash
export GOOGLEPLACES__APIKEY="your_production_key"
```

### 5. Validation

**File:** `src/Abuvi.API/Features/Camps/CreateCampValidator.cs`

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

---

## Frontend Implementation

### 1. Google Places Composable

**Purpose:** Encapsulate Google Places API calls from frontend

**File:** `frontend/src/composables/useGooglePlaces.ts`

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { ApiResponse } from '@/types/api'

export interface PlaceAutocomplete {
  placeId: string
  description: string
  mainText: string
  secondaryText: string
}

export interface PlaceDetails {
  placeId: string
  name: string
  formattedAddress: string
  latitude: number
  longitude: number
  types: string[]
}

export function useGooglePlaces() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  const searchPlaces = async (input: string): Promise<PlaceAutocomplete[]> => {
    if (!input || input.length < 3) {
      return []
    }

    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<PlaceAutocomplete[]>>(
        '/places/autocomplete',
        { input }
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      error.value = response.data.error?.message || 'Error al buscar lugares'
      return []
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al buscar lugares'
      return []
    } finally {
      loading.value = false
    }
  }

  const getPlaceDetails = async (placeId: string): Promise<PlaceDetails | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<PlaceDetails>>(
        '/places/details',
        { placeId }
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      error.value = response.data.error?.message || 'Error al obtener detalles del lugar'
      return null
    } catch (err: any) {
      error.value = err.response?.data?.error?.message || 'Error al obtener detalles del lugar'
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    searchPlaces,
    getPlaceDetails
  }
}
```

### 2. Updated Camp Types

**File:** `frontend/src/types/camp.ts`

```typescript
export interface Camp {
  id: string
  name: string
  description: string | null
  location: string | null
  latitude: number | null
  longitude: number | null
  googlePlaceId: string | null // NEW
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateCampRequest {
  name: string
  description: string | null
  location: string | null
  latitude: number | null
  longitude: number | null
  googlePlaceId: string | null // NEW
  pricePerAdult: number
  pricePerChild: number
  pricePerBaby: number
}

export interface UpdateCampRequest extends CreateCampRequest {
  isActive: boolean
}
```

### 3. Camp Location Form Component

**File:** `frontend/src/components/camps/CampLocationForm.vue`

**Changes:**

1. Replace plain `InputText` for camp name with `AutoComplete` from PrimeVue
2. Add debounced search handler (300ms delay)
3. On place selection, auto-populate name, location, latitude, longitude, googlePlaceId
4. Add visual indicator when fields are auto-filled
5. Add "Clear and enter manually" button
6. Show loading state during autocomplete search

```vue
<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { useDebounceFn } from '@vueuse/core'
import AutoComplete from 'primevue/autocomplete'
import InputText from 'primevue/inputtext'
import InputNumber from 'primevue/inputnumber'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useGooglePlaces, type PlaceAutocomplete } from '@/composables/useGooglePlaces'
import type { CreateCampRequest } from '@/types/camp'

interface Props {
  initialData?: Partial<CreateCampRequest>
}

const props = withDefaults(defineProps<Props>(), {
  initialData: () => ({})
})

const emit = defineEmits<{
  submit: [data: CreateCampRequest]
  cancel: []
}>()

const { loading: placesLoading, error: placesError, searchPlaces, getPlaceDetails } = useGooglePlaces()

const formData = reactive<CreateCampRequest>({
  name: props.initialData.name || '',
  description: props.initialData.description || null,
  location: props.initialData.location || null,
  latitude: props.initialData.latitude || null,
  longitude: props.initialData.longitude || null,
  googlePlaceId: props.initialData.googlePlaceId || null,
  pricePerAdult: props.initialData.pricePerAdult || 0,
  pricePerChild: props.initialData.pricePerChild || 0,
  pricePerBaby: props.initialData.pricePerBaby || 0
})

const placeSuggestions = ref<PlaceAutocomplete[]>([])
const selectedPlace = ref<PlaceAutocomplete | null>(null)
const autoFilledFromPlaces = ref(false)
const searchQuery = ref(formData.name)
const errors = ref<Record<string, string>>({})

// Debounced autocomplete search
const debouncedSearch = useDebounceFn(async (query: string) => {
  if (!query || query.length < 3) {
    placeSuggestions.value = []
    return
  }
  placeSuggestions.value = await searchPlaces(query)
}, 300)

// Watch search query for autocomplete
watch(searchQuery, (newQuery) => {
  debouncedSearch(newQuery)
})

// Handle place selection from autocomplete
const handlePlaceSelected = async (event: { value: PlaceAutocomplete }) => {
  const place = event.value
  if (!place) return

  selectedPlace.value = place
  const details = await getPlaceDetails(place.placeId)

  if (details) {
    formData.name = details.name
    formData.location = details.formattedAddress
    formData.latitude = details.latitude
    formData.longitude = details.longitude
    formData.googlePlaceId = details.placeId

    // Generate description if empty
    if (!formData.description) {
      formData.description = generateDescription(details)
    }

    autoFilledFromPlaces.value = true
  }
}

// Generate automatic description from place details
const generateDescription = (details: { name: string; formattedAddress: string; types: string[] }): string => {
  const typeDescriptions: Record<string, string> = {
    'campground': 'Zona de camping',
    'park': 'Parque natural',
    'lodging': 'Alojamiento',
    'establishment': 'Establecimiento'
  }

  const matchedType = details.types.find(t => typeDescriptions[t])
  const typeDesc = matchedType ? typeDescriptions[matchedType] : 'Ubicación'

  return `${typeDesc} ubicada en ${details.formattedAddress}`
}

// Clear autocomplete and allow manual entry
const clearAutocomplete = () => {
  selectedPlace.value = null
  autoFilledFromPlaces.value = false
  formData.googlePlaceId = null
  searchQuery.value = formData.name
  placeSuggestions.value = []
}

// Validation
const validate = (): boolean => {
  errors.value = {}

  if (!formData.name.trim()) {
    errors.value.name = 'El nombre es obligatorio'
  }

  if (formData.latitude !== null && (formData.latitude < -90 || formData.latitude > 90)) {
    errors.value.latitude = 'La latitud debe estar entre -90 y 90'
  }

  if (formData.longitude !== null && (formData.longitude < -180 || formData.longitude > 180)) {
    errors.value.longitude = 'La longitud debe estar entre -180 y 180'
  }

  if (formData.pricePerAdult < 0) {
    errors.value.pricePerAdult = 'El precio debe ser mayor o igual a 0'
  }

  if (formData.pricePerChild < 0) {
    errors.value.pricePerChild = 'El precio debe ser mayor o igual a 0'
  }

  if (formData.pricePerBaby < 0) {
    errors.value.pricePerBaby = 'El precio debe ser mayor o igual a 0'
  }

  return Object.keys(errors.value).length === 0
}

const handleSubmit = () => {
  if (!validate()) return
  emit('submit', formData)
}
</script>

<template>
  <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
    <!-- Name with Autocomplete -->
    <div>
      <label for="name" class="mb-1 block text-sm font-medium text-gray-700">
        Nombre del Campamento *
        <span class="text-xs text-gray-500">(Empieza a escribir para buscar)</span>
      </label>

      <AutoComplete
        id="name"
        v-model="searchQuery"
        :suggestions="placeSuggestions"
        option-label="description"
        placeholder="Buscar ubicación..."
        class="w-full"
        :loading="placesLoading"
        @complete="debouncedSearch(searchQuery)"
        @item-select="handlePlaceSelected"
      >
        <template #item="{ item }">
          <div class="flex flex-col">
            <span class="font-semibold">{{ item.mainText }}</span>
            <span class="text-sm text-gray-500">{{ item.secondaryText }}</span>
          </div>
        </template>
      </AutoComplete>

      <small v-if="errors.name" class="text-red-500">{{ errors.name }}</small>

      <!-- Button to clear and write manually -->
      <Button
        v-if="autoFilledFromPlaces"
        label="Escribir manualmente"
        icon="pi pi-pencil"
        text
        size="small"
        class="mt-1"
        @click="clearAutocomplete"
      />
    </div>

    <!-- Auto-filled indicator -->
    <Message
      v-if="autoFilledFromPlaces"
      severity="info"
      :closable="false"
      class="mt-2"
    >
      <i class="pi pi-check-circle mr-2"></i>
      Datos cargados desde Google Places. Puedes ajustarlos antes de guardar.
    </Message>

    <!-- Places API error -->
    <Message v-if="placesError" severity="error" :closable="true">
      {{ placesError }}
    </Message>

    <!-- Description -->
    <div>
      <label for="description" class="mb-1 block text-sm font-medium text-gray-700">
        Descripción
      </label>
      <textarea
        id="description"
        v-model="formData.description"
        rows="3"
        class="w-full rounded border border-gray-300 p-2"
        placeholder="Descripción del campamento..."
      />
    </div>

    <!-- Location -->
    <div>
      <label for="location" class="mb-1 block text-sm font-medium text-gray-700">
        Ubicación
        <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
      </label>
      <InputText
        id="location"
        v-model="formData.location"
        class="w-full"
        placeholder="Dirección del campamento..."
      />
    </div>

    <!-- Latitude & Longitude -->
    <div class="grid grid-cols-2 gap-4">
      <div>
        <label for="latitude" class="mb-1 block text-sm font-medium text-gray-700">
          Latitud
          <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
        </label>
        <InputNumber
          id="latitude"
          v-model="formData.latitude"
          mode="decimal"
          :min-fraction-digits="6"
          :max-fraction-digits="6"
          class="w-full"
          placeholder="40.416775"
        />
        <small v-if="errors.latitude" class="text-red-500">{{ errors.latitude }}</small>
      </div>

      <div>
        <label for="longitude" class="mb-1 block text-sm font-medium text-gray-700">
          Longitud
          <span v-if="autoFilledFromPlaces" class="text-xs text-blue-600">(Auto-completado)</span>
        </label>
        <InputNumber
          id="longitude"
          v-model="formData.longitude"
          mode="decimal"
          :min-fraction-digits="6"
          :max-fraction-digits="6"
          class="w-full"
          placeholder="-3.703790"
        />
        <small v-if="errors.longitude" class="text-red-500">{{ errors.longitude }}</small>
      </div>
    </div>

    <!-- Pricing -->
    <div class="grid grid-cols-3 gap-4">
      <div>
        <label for="priceAdult" class="mb-1 block text-sm font-medium text-gray-700">
          Precio Adulto *
        </label>
        <InputNumber
          id="priceAdult"
          v-model="formData.pricePerAdult"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          class="w-full"
        />
        <small v-if="errors.pricePerAdult" class="text-red-500">{{ errors.pricePerAdult }}</small>
      </div>

      <div>
        <label for="priceChild" class="mb-1 block text-sm font-medium text-gray-700">
          Precio Niño *
        </label>
        <InputNumber
          id="priceChild"
          v-model="formData.pricePerChild"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          class="w-full"
        />
        <small v-if="errors.pricePerChild" class="text-red-500">{{ errors.pricePerChild }}</small>
      </div>

      <div>
        <label for="priceBaby" class="mb-1 block text-sm font-medium text-gray-700">
          Precio Bebé *
        </label>
        <InputNumber
          id="priceBaby"
          v-model="formData.pricePerBaby"
          mode="currency"
          currency="EUR"
          locale="es-ES"
          class="w-full"
        />
        <small v-if="errors.pricePerBaby" class="text-red-500">{{ errors.pricePerBaby }}</small>
      </div>
    </div>

    <!-- Actions -->
    <div class="flex justify-end gap-2 mt-4">
      <Button label="Cancelar" severity="secondary" @click="emit('cancel')" />
      <Button label="Guardar" type="submit" :disabled="placesLoading" />
    </div>
  </form>
</template>
```

---

## Testing Specifications

### Backend Unit Tests

#### 1. GooglePlacesService Tests

**File:** `src/Abuvi.Tests/Unit/Features/GooglePlaces/GooglePlacesServiceTests.cs`

```csharp
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
        result!.Name.Should().Be("Camping El Pinar");
        result.Latitude.Should().Be(40.416775m);
        result.Longitude.Should().Be(-3.703790m);
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
}
```

#### 2. GooglePlacesEndpoints Tests

**File:** `src/Abuvi.Tests/Integration/Features/GooglePlaces/GooglePlacesEndpointsTests.cs`

```csharp
public class GooglePlacesEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GooglePlacesEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        // Add auth token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestHelper.GetValidToken());
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
    }

    [Fact]
    public async Task SearchPlaces_WithoutAuth_Returns401()
    {
        // Arrange
        var client = new HttpClient();
        var request = new { Input = "Camping" };

        // Act
        var response = await client.PostAsJsonAsync("/api/places/autocomplete", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    }
}
```

### Frontend Unit Tests

#### 1. useGooglePlaces Composable Tests

**File:** `frontend/src/composables/__tests__/useGooglePlaces.test.ts`

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useGooglePlaces } from '@/composables/useGooglePlaces'
import { api } from '@/utils/api'

vi.mock('@/utils/api')

describe('useGooglePlaces', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('should search places successfully', async () => {
    // Arrange
    const mockPlaces = [
      {
        placeId: 'ChIJ1',
        description: 'Camping El Pinar, Madrid',
        mainText: 'Camping El Pinar',
        secondaryText: 'Madrid, España'
      }
    ]
    vi.mocked(api.post).mockResolvedValue({
      data: { success: true, data: mockPlaces, error: null }
    })

    // Act
    const { loading, error, searchPlaces } = useGooglePlaces()
    const result = await searchPlaces('Camping')

    // Assert
    expect(result).toEqual(mockPlaces)
    expect(loading.value).toBe(false)
    expect(error.value).toBeNull()
    expect(api.post).toHaveBeenCalledWith('/places/autocomplete', { input: 'Camping' })
  })

  it('should return empty array for input less than 3 characters', async () => {
    // Act
    const { searchPlaces } = useGooglePlaces()
    const result = await searchPlaces('Ca')

    // Assert
    expect(result).toEqual([])
    expect(api.post).not.toHaveBeenCalled()
  })

  it('should get place details successfully', async () => {
    // Arrange
    const mockDetails = {
      placeId: 'ChIJ1',
      name: 'Camping El Pinar',
      formattedAddress: 'Calle Example, Madrid',
      latitude: 40.416775,
      longitude: -3.703790,
      types: ['campground']
    }
    vi.mocked(api.post).mockResolvedValue({
      data: { success: true, data: mockDetails, error: null }
    })

    // Act
    const { getPlaceDetails } = useGooglePlaces()
    const result = await getPlaceDetails('ChIJ1')

    // Assert
    expect(result).toEqual(mockDetails)
    expect(api.post).toHaveBeenCalledWith('/places/details', { placeId: 'ChIJ1' })
  })

  it('should set error when API call fails', async () => {
    // Arrange
    vi.mocked(api.post).mockRejectedValue({
      response: { data: { error: { message: 'Service unavailable' } } }
    })

    // Act
    const { error, searchPlaces } = useGooglePlaces()
    const result = await searchPlaces('Camping')

    // Assert
    expect(result).toEqual([])
    expect(error.value).toBe('Service unavailable')
  })
})
```

#### 2. CampLocationForm Component Tests

**File:** `frontend/src/components/camps/__tests__/CampLocationForm.test.ts`

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CampLocationForm from '@/components/camps/CampLocationForm.vue'
import { useGooglePlaces } from '@/composables/useGooglePlaces'

vi.mock('@/composables/useGooglePlaces')

describe('CampLocationForm', () => {
  it('should render all form fields', () => {
    // Arrange
    vi.mocked(useGooglePlaces).mockReturnValue({
      loading: { value: false },
      error: { value: null },
      searchPlaces: vi.fn(),
      getPlaceDetails: vi.fn()
    })

    // Act
    const wrapper = mount(CampLocationForm)

    // Assert
    expect(wrapper.find('#name').exists()).toBe(true)
    expect(wrapper.find('#description').exists()).toBe(true)
    expect(wrapper.find('#location').exists()).toBe(true)
    expect(wrapper.find('#latitude').exists()).toBe(true)
    expect(wrapper.find('#longitude').exists()).toBe(true)
  })

  it('should auto-fill fields when place is selected', async () => {
    // Arrange
    const mockDetails = {
      placeId: 'ChIJ1',
      name: 'Camping El Pinar',
      formattedAddress: 'Calle Example, Madrid',
      latitude: 40.416775,
      longitude: -3.703790,
      types: ['campground']
    }

    const getPlaceDetails = vi.fn().mockResolvedValue(mockDetails)
    vi.mocked(useGooglePlaces).mockReturnValue({
      loading: { value: false },
      error: { value: null },
      searchPlaces: vi.fn(),
      getPlaceDetails
    })

    const wrapper = mount(CampLocationForm)

    // Act
    await wrapper.vm.handlePlaceSelected({
      value: {
        placeId: 'ChIJ1',
        description: 'Camping El Pinar, Madrid',
        mainText: 'Camping El Pinar',
        secondaryText: 'Madrid'
      }
    })

    // Assert
    expect(wrapper.vm.formData.name).toBe('Camping El Pinar')
    expect(wrapper.vm.formData.location).toBe('Calle Example, Madrid')
    expect(wrapper.vm.formData.latitude).toBe(40.416775)
    expect(wrapper.vm.formData.longitude).toBe(-3.703790)
    expect(wrapper.vm.formData.googlePlaceId).toBe('ChIJ1')
    expect(wrapper.vm.autoFilledFromPlaces).toBe(true)
  })

  it('should validate required fields', async () => {
    // Arrange
    vi.mocked(useGooglePlaces).mockReturnValue({
      loading: { value: false },
      error: { value: null },
      searchPlaces: vi.fn(),
      getPlaceDetails: vi.fn()
    })

    const wrapper = mount(CampLocationForm)

    // Act
    await wrapper.find('form').trigger('submit')

    // Assert
    expect(wrapper.vm.errors.name).toBe('El nombre es obligatorio')
  })
})
```

### End-to-End Tests (Cypress)

**File:** `frontend/cypress/e2e/camps/google-places-autocomplete.cy.ts`

```typescript
describe('Google Places Autocomplete for Camps', () => {
  beforeEach(() => {
    cy.login('admin@abuvi.org', 'Admin123!@#')
    cy.visit('/camps/locations')
  })

  it('should autocomplete camp data when selecting from Google Places', () => {
    // Mock Google Places API responses
    cy.intercept('POST', '/api/places/autocomplete', {
      success: true,
      data: [
        {
          placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
          description: 'Camping El Pinar, Madrid',
          mainText: 'Camping El Pinar',
          secondaryText: 'Madrid, España'
        }
      ]
    }).as('autocomplete')

    cy.intercept('POST', '/api/places/details', {
      success: true,
      data: {
        placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
        name: 'Camping El Pinar',
        formattedAddress: 'Calle Example, 123, Madrid, España',
        latitude: 40.416775,
        longitude: -3.703790,
        types: ['campground']
      }
    }).as('details')

    // Click new camp button
    cy.get('[data-testid=new-camp-btn]').click()

    // Type in autocomplete field
    cy.get('#name').type('Camping El')

    // Wait for autocomplete results
    cy.wait('@autocomplete')

    // Select first suggestion
    cy.get('.p-autocomplete-item').first().click()

    // Wait for details
    cy.wait('@details')

    // Verify auto-filled fields
    cy.get('#name').should('have.value', 'Camping El Pinar')
    cy.get('#location').should('have.value', 'Calle Example, 123, Madrid, España')
    cy.get('#latitude').should('have.value', '40.416775')
    cy.get('#longitude').should('have.value', '-3.703790')

    // Verify auto-fill indicator
    cy.contains('Datos cargados desde Google Places').should('be.visible')
  })

  it('should allow manual entry after clearing autocomplete', () => {
    // Autocomplete a place first (reuse previous test setup)
    cy.get('[data-testid=new-camp-btn]').click()
    cy.get('#name').type('Camping{enter}')

    // Click "Write manually" button
    cy.contains('Escribir manualmente').click()

    // Verify fields are editable
    cy.get('#name').clear().type('My Custom Camp')
    cy.get('#location').clear().type('Custom Location')

    // Verify no autocomplete indicator
    cy.contains('Datos cargados desde Google Places').should('not.exist')
  })

  it('should handle Google Places API errors gracefully', () => {
    // Mock API error
    cy.intercept('POST', '/api/places/autocomplete', {
      statusCode: 503,
      body: {
        success: false,
        error: {
          message: 'El servicio de ubicaciones no está disponible',
          code: 'PLACES_SERVICE_UNAVAILABLE'
        }
      }
    }).as('autocompleteError')

    cy.get('[data-testid=new-camp-btn]').click()
    cy.get('#name').type('Camping')

    // Wait for error
    cy.wait('@autocompleteError')

    // Verify error message
    cy.contains('El servicio de ubicaciones no está disponible').should('be.visible')

    // Verify manual entry still works
    cy.get('#name').clear().type('My Camp')
    cy.get('#latitude').type('40.416775')
    cy.get('#longitude').type('-3.703790')
  })
})
```

### Test Coverage Requirements

- **Backend:** 90% coverage for GooglePlacesService and endpoints
- **Frontend:** 90% coverage for useGooglePlaces composable and CampLocationForm component
- **E2E:** Cover happy path, error scenarios, and manual override

---

## Security & Performance

### Security

#### 1. API Key Protection

- **Development:** Store in User Secrets (`dotnet user-secrets set`)
- **Production:** Store in environment variables, never commit to repo
- **Restriction:** Configure API key restrictions in Google Cloud Console:
  - **HTTP referrers:** Only allow your domain
  - **API restrictions:** Only allow Places API
  - **Quotas:** Set daily request limits

#### 2. Backend Proxy

**Why:**

- API key is never exposed to frontend
- Centralized rate limiting and logging
- Can add caching layer to reduce API costs
- Can add additional validation/filtering

#### 3. Authentication

- All Google Places endpoints require authentication (`RequireAuthorization()`)
- Only logged-in users can use autocomplete
- Admin/Board roles can create/edit camps

### Performance

#### 1. Debouncing

- Frontend debounces autocomplete requests (300ms delay)
- Prevents excessive API calls while user is typing
- Minimum 3 characters before search

#### 2. Caching (Optional - Future Enhancement)

```csharp
// Add distributed cache for autocomplete results
builder.Services.AddDistributedMemoryCache();

// In GooglePlacesService
public async Task<IReadOnlyList<PlaceAutocomplete>> SearchPlacesAsync(string input, CancellationToken ct)
{
    var cacheKey = $"places:autocomplete:{input.ToLower()}";
    var cached = await _cache.GetStringAsync(cacheKey, ct);

    if (cached != null)
    {
        return JsonSerializer.Deserialize<List<PlaceAutocomplete>>(cached) ?? Array.Empty<PlaceAutocomplete>();
    }

    var results = // ... call Google API ...

    await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(results),
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
        ct);

    return results;
}
```

#### 3. Cost Optimization

**Google Places API Pricing (2026):**

| API Call | Cost | Optimization Strategy |
|----------|------|----------------------|
| Autocomplete (per session) | First 100k/month FREE, then $2.83 per 1000 | Debounce (300ms), Min 3 chars, Cache results |
| Place Details | $0.017 per request (Basic Data) | Only call on selection, Request minimal fields (`fields` parameter) |

**Estimated Monthly Cost:**

- **Low usage** (100 camps/month): ~$2-5/month
- **Medium usage** (500 camps/month): ~$10-20/month
- **High usage** (2000 camps/month): ~$40-80/month

**Fields Optimization:**

Only request necessary fields in Place Details API:

```
fields=place_id,name,formatted_address,geometry
```

**Don't request:** photos, reviews, opening_hours (charged extra)

#### 4. Rate Limiting (Future Enhancement)

```csharp
// Add rate limiting for Places endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("PlacesApiLimiter", opt =>
    {
        opt.PermitLimit = 60; // 60 requests
        opt.Window = TimeSpan.FromMinutes(1); // per minute
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Apply to endpoints
group.MapPost("/autocomplete", SearchPlaces)
    .RequireRateLimiting("PlacesApiLimiter");
```

---

## Deployment & Migration

### Database Migration

**1. Create Migration:**

```bash
dotnet ef migrations add AddGooglePlaceIdToCamp --project src/Abuvi.API
```

**2. Review Generated Migration:**

```csharp
// Migrations/YYYYMMDDHHMMSS_AddGooglePlaceIdToCamp.cs

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

**3. Apply Migration:**

```bash
# Development
dotnet ef database update --project src/Abuvi.API

# Production (generate SQL script)
dotnet ef migrations script --idempotent --project src/Abuvi.API > migration.sql
```

### Existing Data Handling

**Existing camps will:**

- Have `GooglePlaceId = null`
- Continue to work normally
- Can be edited to add Google Place ID via autocomplete
- No data migration needed

**Optional:** Add background job to attempt matching existing camps to Google Places (low priority)

### Configuration Deployment

**Development:**

```bash
dotnet user-secrets set "GooglePlaces:ApiKey" "YOUR_DEV_KEY" --project src/Abuvi.API
```

**Production (Azure App Service example):**

```bash
az webapp config appsettings set \
  --resource-group abuvi-rg \
  --name abuvi-api \
  --settings GooglePlaces__ApiKey="YOUR_PROD_KEY"
```

**Production (Docker/Kubernetes):**

```yaml
env:
  - name: GooglePlaces__ApiKey
    valueFrom:
      secretKeyRef:
        name: google-api-secrets
        key: places-api-key
```

### Rollback Strategy

**If issues occur:**

1. **Disable autocomplete frontend feature:**
   - Add feature flag: `VITE_ENABLE_GOOGLE_PLACES=false`
   - Frontend falls back to manual entry only

2. **Rollback database migration:**

   ```bash
   dotnet ef database update PreviousMigration --project src/Abuvi.API
   ```

3. **Remove endpoints:**
   - Comment out `app.MapGooglePlacesEndpoints()` in `Program.cs`

### Feature Flag (Optional)

```typescript
// frontend/.env
VITE_ENABLE_GOOGLE_PLACES=true

// frontend/src/components/camps/CampLocationForm.vue
const enableGooglePlaces = import.meta.env.VITE_ENABLE_GOOGLE_PLACES === 'true'

// Conditionally render AutoComplete vs plain InputText
```

---

## Acceptance Criteria

### Must Have (Phase 1)

- [ ] **Backend Proxy Endpoints:**
  - [ ] `POST /api/places/autocomplete` returns suggestions for valid input
  - [ ] `POST /api/places/details` returns place details for valid Place ID
  - [ ] Both endpoints require authentication
  - [ ] Proper error handling for Google API failures (503 response)
  - [ ] API key stored securely (User Secrets in dev, env vars in prod)

- [ ] **Database Schema:**
  - [ ] `GooglePlaceId` field added to `Camps` table (nullable, varchar(255), indexed)
  - [ ] Migration applied successfully
  - [ ] Existing camps continue to work (null GooglePlaceId)

- [ ] **Frontend Autocomplete:**
  - [ ] Typing in camp name shows Google Places suggestions (debounced, min 3 chars)
  - [ ] Selecting a suggestion auto-fills: Name, Location, Latitude, Longitude, GooglePlaceId
  - [ ] Auto-generated description if description field is empty
  - [ ] Visual indicator shows when fields are auto-filled
  - [ ] "Write manually" button clears autocomplete and allows manual entry
  - [ ] All auto-filled fields remain editable

- [ ] **Error Handling:**
  - [ ] Google API unavailable → Show user-friendly error message in Spanish
  - [ ] Invalid Place ID → Show "Place not found" message
  - [ ] Network errors → Show retry option
  - [ ] Fallback to manual entry always available

- [ ] **Testing:**
  - [ ] Backend unit tests for GooglePlacesService (90% coverage)
  - [ ] Backend integration tests for endpoints
  - [ ] Frontend unit tests for useGooglePlaces composable (90% coverage)
  - [ ] Frontend component tests for CampLocationForm
  - [ ] E2E test for happy path (autocomplete → select → auto-fill → save)
  - [ ] E2E test for error scenario (API failure → manual entry)

- [ ] **Documentation:**
  - [ ] API endpoints documented in `ai-specs/specs/api-endpoints.md`
  - [ ] Configuration instructions in README
  - [ ] Migration instructions for deployment

### Nice to Have (Phase 2 - Future)

- [ ] Response caching to reduce API costs
- [ ] Rate limiting per user
- [ ] Support for updating existing camps with Google Place ID
- [ ] Background job to match existing camps to Google Places
- [ ] Photo import from Google Places
- [ ] Display Google Maps preview on camp detail page

---

## Implementation Phases

### Phase 1: Backend Foundation (2-3 days)

**Tasks:**

1. Create database migration for `GooglePlaceId` field
2. Update Camp entity, DTOs, and validation
3. Implement `GooglePlacesService` with HttpClient
4. Implement `GooglePlacesEndpoints` with authentication
5. Configure API key in User Secrets
6. Write backend unit tests (90% coverage target)
7. Write backend integration tests

**Files to Create/Modify:**

- `src/Abuvi.API/Features/GooglePlaces/GooglePlacesService.cs` (NEW)
- `src/Abuvi.API/Features/GooglePlaces/GooglePlacesEndpoints.cs` (NEW)
- `src/Abuvi.API/Features/Camps/CampsModels.cs` (UPDATE - add GooglePlaceId)
- `src/Abuvi.API/Features/Camps/CreateCampValidator.cs` (UPDATE)
- `src/Abuvi.API/Features/Camps/UpdateCampValidator.cs` (UPDATE)
- `src/Abuvi.API/Data/Configurations/CampConfiguration.cs` (UPDATE)
- `src/Abuvi.API/Program.cs` (UPDATE - register services and endpoints)
- `src/Abuvi.API/appsettings.json` (UPDATE - add GooglePlaces config)
- `src/Abuvi.Tests/Unit/Features/GooglePlaces/GooglePlacesServiceTests.cs` (NEW)
- `src/Abuvi.Tests/Integration/Features/GooglePlaces/GooglePlacesEndpointsTests.cs` (NEW)

**Acceptance Criteria:**

- All backend tests pass
- API endpoints respond correctly to authenticated requests
- Google API calls work with valid API key
- Error handling returns proper HTTP status codes

### Phase 2: Frontend Integration (2-3 days)

**Tasks:**

1. Create `useGooglePlaces` composable
2. Update camp types to include `googlePlaceId`
3. Modify `CampLocationForm` component with autocomplete
4. Add debouncing for search input
5. Implement auto-fill logic on place selection
6. Add manual override ("Write manually" button)
7. Write frontend unit tests (90% coverage target)
8. Write component tests

**Files to Create/Modify:**

- `frontend/src/composables/useGooglePlaces.ts` (NEW)
- `frontend/src/types/camp.ts` (UPDATE - add googlePlaceId)
- `frontend/src/components/camps/CampLocationForm.vue` (UPDATE - add autocomplete)
- `frontend/src/composables/__tests__/useGooglePlaces.test.ts` (NEW)
- `frontend/src/components/camps/__tests__/CampLocationForm.test.ts` (UPDATE)

**Acceptance Criteria:**

- All frontend tests pass
- Autocomplete appears after typing 3+ characters
- Selecting a place auto-fills all fields correctly
- Manual override works correctly

### Phase 3: Testing & Refinement (1-2 days)

**Tasks:**

1. Write E2E tests with Cypress
2. Manual testing on different scenarios
3. Error scenario testing (API failures, network issues)
4. Performance testing (debounce behavior, loading states)
5. UX refinement (loading indicators, error messages)
6. Documentation updates

**Files to Create/Modify:**

- `frontend/cypress/e2e/camps/google-places-autocomplete.cy.ts` (NEW)
- `ai-specs/specs/api-endpoints.md` (UPDATE - document new endpoints)
- `README.md` (UPDATE - configuration instructions)

**Acceptance Criteria:**

- All E2E tests pass
- Manual testing checklist completed
- Error scenarios handled gracefully
- Documentation complete

### Phase 4: Deployment (1 day)

**Tasks:**

1. Apply database migration to staging environment
2. Configure API key in production environment
3. Deploy backend changes
4. Deploy frontend changes
5. Smoke testing in production
6. Monitor logs for errors

**Acceptance Criteria:**

- Migration applied successfully
- No production errors
- Autocomplete works in production
- API costs within budget

---

## Error Codes

| Code | HTTP Status | Spanish Message | English Message |
|------|-------------|-----------------|-----------------|
| `PLACES_SERVICE_UNAVAILABLE` | 503 | El servicio de ubicaciones no está disponible. Por favor intenta más tarde. | Places service is unavailable. Please try again later. |
| `PLACE_NOT_FOUND` | 404 | No se encontró información para este lugar | Place details not found |
| `VALIDATION_ERROR` | 400 | Error de validación | Validation error |
| `UNAUTHORIZED` | 401 | No autorizado | Unauthorized |

---

## References

- [Google Places API - Autocomplete Documentation](https://developers.google.com/maps/documentation/places/web-service/autocomplete)
- [Google Places API - Place Details Documentation](https://developers.google.com/maps/documentation/places/web-service/details)
- [PrimeVue AutoComplete Component](https://primevue.org/autocomplete/)
- [VueUse - useDebounceFn](https://vueuse.org/shared/useDebounceFn/)
- [Backend Standards](../../specs/backend-standards.mdc)
- [Frontend Standards](../../specs/frontend-standards.mdc)
- [Data Model](../../specs/data-model.md)
- [API Endpoints](../../specs/api-endpoints.md)

---

## Notes

### Future Enhancements

1. **Photos Integration:** Import camp photos from Google Places Photos API
2. **Reverse Geocoding:** Allow user to select location on map, get address automatically
3. **Nearby Search:** Show nearby campgrounds based on selected location
4. **Place Reviews:** Display Google reviews for the camp location
5. **Opening Hours:** Import and display camp opening hours from Google

### Known Limitations

1. **Country Restriction:** Currently restricted to Spain (`components=country:es`). Can be made configurable.
2. **Language:** Results are in Spanish (`language=es`). Can be made configurable.
3. **Place Types:** Not all Google Places types are suitable for camps (e.g., restaurants). Could add type filtering.

### Cost Monitoring

**Set up monitoring for:**

- Daily API request count
- Monthly costs
- Quota approaching limits (80% threshold alert)

**Budget Alerts (Google Cloud Console):**

```
Budget: €50/month
Alert thresholds: 50%, 80%, 100%
```

---

## Summary

This enriched specification provides all necessary details for autonomous development:

✅ **Data Model:** Complete schema changes with migration scripts
✅ **Backend:** Full service implementation with endpoints, DTOs, validation, error handling
✅ **Frontend:** Complete composable and component implementation
✅ **Testing:** Comprehensive test cases for unit, integration, and E2E
✅ **Security:** API key protection, authentication, rate limiting strategies
✅ **Performance:** Debouncing, caching, cost optimization
✅ **Deployment:** Migration strategy, configuration, rollback plan
✅ **Documentation:** API endpoints, configuration, error codes

**Total Estimated Effort:** 6-10 development days

**Dependencies:**

- Google Places API key (obtain from Google Cloud Console)
- Backend deployed with migration applied
- Frontend environment variables configured

**Risks:**

- Google API quota exceeded → Implement caching and rate limiting
- Google API unavailable → Always allow manual entry fallback
- Incorrect place mapping → Allow manual override of all fields
