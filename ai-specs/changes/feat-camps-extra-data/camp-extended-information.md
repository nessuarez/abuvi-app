# Ampliación de Información de Campamentos - Datos Estáticos Adicionales

## Objetivo

Enriquecer el modelo Camp con información adicional estática proveniente de Google Places API u otras fuentes, incluyendo:
- Dirección postal completa
- Enlaces a Google Maps
- Datos de contacto (teléfono, web)
- Fotografías originales del lugar (Google Maps)
- Metadatos adicionales (rating, horarios, etc.)

## Contexto

Actualmente, el modelo `Camp` solo contiene información básica:
- Nombre
- Descripción
- Coordenadas (latitud/longitud)
- Precios base
- Estado

Sin embargo, Google Places API proporciona mucha más información útil que podemos almacenar y mostrar a los usuarios.

## Análisis de Datos Disponibles en Google Places

Basado en el ejemplo real de `ai-specs/changes/feat-google-places-camps/examples/google-places-response.json`:

### Datos Disponibles y Relevantes

| Campo Google Places | Utilidad | Prioridad |
|-------------------|----------|-----------|
| `formattedAddress` | Dirección legible completa | Alta |
| `addressComponents` | Componentes estructurados (calle, ciudad, CP) | Alta |
| `nationalPhoneNumber` / `internationalPhoneNumber` | Contacto telefónico | Alta |
| `websiteUri` | Sitio web oficial | Alta |
| `googleMapsUri` | Enlace directo a Google Maps | Alta |
| `photos[]` | Fotografías del lugar | Alta |
| `rating` | Valoración de usuarios | Media |
| `userRatingCount` | Número de valoraciones | Media |
| `reviews[]` | Reseñas de usuarios | Baja (info externa) |
| `regularOpeningHours` | Horarios de apertura | Media |
| `types[]` | Tipos de establecimiento | Baja |
| `plusCode` | Plus Code de ubicación | Baja |

## Nuevo Modelo de Datos

### Campos a Añadir al Modelo Camp

#### Grupo 1: Información de Contacto (Prioridad Alta)

```typescript
// Dirección Postal
formattedAddress?: string           // "Crta Pujarnol, km 5, 17834 Pujarnol, Girona, España"
streetAddress?: string              // "Crta Pujarnol, km 5"
locality?: string                   // "Pujarnol"
administrativeArea?: string         // "Girona"
postalCode?: string                 // "17834"
country?: string                    // "España"

// Contacto
phoneNumber?: string                // "+34 972 59 05 07"
nationalPhoneNumber?: string        // "972 59 05 07"
websiteUrl?: string                 // "http://www.albacolonies.com/"

// Enlaces
googleMapsUrl?: string              // URL directa a Google Maps
googlePlaceId?: string              // "ChIJ38SpLTDCuhIRgdtW_484UBk" (para futuras actualizaciones)
```

#### Grupo 2: Información Visual (Prioridad Alta)

```typescript
// Fotografías originales de Google Maps
photos?: CampPhoto[]

interface CampPhoto {
  id: string                        // ID interno
  photoReference: string            // Reference de Google Places
  photoUrl?: string                 // URL de la foto (si se almacena)
  width: number                     // Ancho en píxeles
  height: number                    // Alto en píxeles
  attributionName: string           // Autor de la foto
  attributionUrl?: string           // Enlace al perfil del autor
  isOriginal: boolean               // true = de Google Places, false = subida por nosotros
  isPrimary: boolean                // Foto principal para mostrar
  createdAt: string                 // Fecha de adición
}
```

#### Grupo 3: Metadatos Adicionales (Prioridad Media)

```typescript
// Valoraciones
googleRating?: number               // 3.7
googleRatingCount?: number          // 113
lastGoogleSyncAt?: string          // Última sincronización con Google Places

// Información adicional
businessStatus?: string             // "OPERATIONAL" | "CLOSED_TEMPORARILY" | "CLOSED_PERMANENTLY"
placeTypes?: string[]              // ["hostel", "summer_camp_organizer", "lodging"]
```

#### Grupo 4: Horarios (Prioridad Baja - Futuro)

```typescript
// Para futuras implementaciones
openingHours?: {
  monday?: { open: string; close: string }
  tuesday?: { open: string; close: string }
  // ... resto de días
  weekdayDescriptions?: string[]
}
```

## Especificación Técnica

### Backend Changes

#### 1. Actualización de Entidad Camp

**Archivo:** `backend/Models/Camp.cs`

**Nuevas propiedades:**

```csharp
public class Camp
{
    // ... propiedades existentes

    // Contact Information
    public string? FormattedAddress { get; set; }
    public string? StreetAddress { get; set; }
    public string? Locality { get; set; }
    public string? AdministrativeArea { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NationalPhoneNumber { get; set; }
    public string? WebsiteUrl { get; set; }

    // Google Maps Integration
    public string? GoogleMapsUrl { get; set; }
    public string? GooglePlaceId { get; set; }

    // Ratings & Reviews
    public decimal? GoogleRating { get; set; }
    public int? GoogleRatingCount { get; set; }
    public DateTime? LastGoogleSyncAt { get; set; }

    // Business Info
    public string? BusinessStatus { get; set; }
    public string? PlaceTypes { get; set; } // JSON array stored as string

    // Navigation Properties
    public ICollection<CampPhoto> Photos { get; set; } = new List<CampPhoto>();
}
```

#### 2. Nueva Entidad: CampPhoto

**Archivo:** `backend/Models/CampPhoto.cs`

```csharp
public class CampPhoto
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }

    // Google Places Integration
    public string? PhotoReference { get; set; }  // Google Places photo reference
    public string? PhotoUrl { get; set; }        // Stored URL or Google URL

    // Dimensions
    public int Width { get; set; }
    public int Height { get; set; }

    // Attribution
    public string AttributionName { get; set; } = string.Empty;
    public string? AttributionUrl { get; set; }

    // Metadata
    public bool IsOriginal { get; set; }         // true = Google Places, false = uploaded
    public bool IsPrimary { get; set; }          // Primary photo for display
    public int DisplayOrder { get; set; }        // Order in gallery

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}
```

#### 3. Migración de Base de Datos

**Nombre:** `YYYYMMDDHHMMSS_AddExtendedCampInformation.cs`

```csharp
public partial class AddExtendedCampInformation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add columns to Camps table
        migrationBuilder.AddColumn<string>(
            name: "FormattedAddress",
            table: "Camps",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StreetAddress",
            table: "Camps",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Locality",
            table: "Camps",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdministrativeArea",
            table: "Camps",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PostalCode",
            table: "Camps",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Country",
            table: "Camps",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PhoneNumber",
            table: "Camps",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NationalPhoneNumber",
            table: "Camps",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WebsiteUrl",
            table: "Camps",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GoogleMapsUrl",
            table: "Camps",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GooglePlaceId",
            table: "Camps",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "GoogleRating",
            table: "Camps",
            type: "decimal(3,2)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "GoogleRatingCount",
            table: "Camps",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastGoogleSyncAt",
            table: "Camps",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BusinessStatus",
            table: "Camps",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlaceTypes",
            table: "Camps",
            type: "nvarchar(max)",
            nullable: true);

        // Create CampPhotos table
        migrationBuilder.CreateTable(
            name: "CampPhotos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CampId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PhotoReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                PhotoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Width = table.Column<int>(type: "int", nullable: false),
                Height = table.Column<int>(type: "int", nullable: false),
                AttributionName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AttributionUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsOriginal = table.Column<bool>(type: "bit", nullable: false),
                IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                DisplayOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CampPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_CampPhotos_Camps_CampId",
                    column: x => x.CampId,
                    principalTable: "Camps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CampPhotos_CampId",
            table: "CampPhotos",
            column: "CampId");

        migrationBuilder.CreateIndex(
            name: "IX_Camps_GooglePlaceId",
            table: "Camps",
            column: "GooglePlaceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CampPhotos");

        migrationBuilder.DropIndex(
            name: "IX_Camps_GooglePlaceId",
            table: "Camps");

        // Drop columns from Camps table
        migrationBuilder.DropColumn(name: "FormattedAddress", table: "Camps");
        migrationBuilder.DropColumn(name: "StreetAddress", table: "Camps");
        // ... resto de columnas
    }
}
```

#### 4. Actualización de DTOs

**Archivo:** `backend/DTOs/CampDTOs.cs`

```csharp
public record CampPhotoDto(
    Guid Id,
    string? PhotoReference,
    string? PhotoUrl,
    int Width,
    int Height,
    string AttributionName,
    string? AttributionUrl,
    bool IsOriginal,
    bool IsPrimary,
    int DisplayOrder,
    DateTime CreatedAt
);

public record CampDetailDto(
    Guid Id,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    decimal BasePriceAdult,
    decimal BasePriceChild,
    decimal BasePriceBaby,
    CampStatus Status,

    // Extended Information
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    string? GooglePlaceId,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    DateTime? LastGoogleSyncAt,
    string? BusinessStatus,
    List<string>? PlaceTypes,

    // Photos
    List<CampPhotoDto> Photos,

    // Existing fields
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? EditionCount
);

public record CreateCampRequest(
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    decimal BasePriceAdult,
    decimal BasePriceChild,
    decimal BasePriceBaby,
    CampStatus Status,

    // Extended fields (optional)
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    string? GooglePlaceId,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    List<string>? PlaceTypes
);

public record UpdateCampRequest(
    Guid Id,
    string Name,
    string Description,
    double Latitude,
    double Longitude,
    decimal BasePriceAdult,
    decimal BasePriceChild,
    decimal BasePriceBaby,
    CampStatus Status,

    // Extended fields (optional)
    string? FormattedAddress,
    string? StreetAddress,
    string? Locality,
    string? AdministrativeArea,
    string? PostalCode,
    string? Country,
    string? PhoneNumber,
    string? NationalPhoneNumber,
    string? WebsiteUrl,
    string? GoogleMapsUrl,
    string? GooglePlaceId,
    decimal? GoogleRating,
    int? GoogleRatingCount,
    string? BusinessStatus,
    List<string>? PlaceTypes
);
```

#### 5. Servicio de Mapeo Google Places → Camp

**Archivo:** `backend/Services/GooglePlacesMapperService.cs`

```csharp
public interface IGooglePlacesMapperService
{
    CampExtendedInfo MapPlaceDetailsToCamp(PlaceDetailsResponse placeDetails);
    List<CampPhotoDto> MapPlacePhotos(PlaceDetailsResponse placeDetails);
}

public class GooglePlacesMapperService : IGooglePlacesMapperService
{
    public CampExtendedInfo MapPlaceDetailsToCamp(PlaceDetailsResponse details)
    {
        return new CampExtendedInfo
        {
            FormattedAddress = details.FormattedAddress,
            StreetAddress = ExtractStreetAddress(details.AddressComponents),
            Locality = ExtractComponent(details.AddressComponents, "locality"),
            AdministrativeArea = ExtractComponent(details.AddressComponents, "administrative_area_level_2"),
            PostalCode = ExtractComponent(details.AddressComponents, "postal_code"),
            Country = ExtractComponent(details.AddressComponents, "country"),
            PhoneNumber = details.InternationalPhoneNumber,
            NationalPhoneNumber = details.NationalPhoneNumber,
            WebsiteUrl = details.WebsiteUri,
            GoogleMapsUrl = details.GoogleMapsUri,
            GooglePlaceId = details.Id,
            GoogleRating = details.Rating,
            GoogleRatingCount = details.UserRatingCount,
            LastGoogleSyncAt = DateTime.UtcNow,
            BusinessStatus = details.BusinessStatus,
            PlaceTypes = details.Types
        };
    }

    public List<CampPhotoDto> MapPlacePhotos(PlaceDetailsResponse details)
    {
        return details.Photos?.Select((photo, index) => new CampPhotoDto(
            Id: Guid.NewGuid(),
            PhotoReference: photo.Name,
            PhotoUrl: null, // Se construirá con Google Places Photo API
            Width: photo.WidthPx,
            Height: photo.HeightPx,
            AttributionName: photo.AuthorAttributions?.FirstOrDefault()?.DisplayName ?? "Google",
            AttributionUrl: photo.AuthorAttributions?.FirstOrDefault()?.Uri,
            IsOriginal: true,
            IsPrimary: index == 0,
            DisplayOrder: index,
            CreatedAt: DateTime.UtcNow
        )).ToList() ?? new List<CampPhotoDto>();
    }

    private string? ExtractComponent(List<AddressComponent> components, string type)
    {
        return components
            ?.FirstOrDefault(c => c.Types?.Contains(type) == true)
            ?.LongText;
    }

    private string? ExtractStreetAddress(List<AddressComponent> components)
    {
        var route = ExtractComponent(components, "route");
        var streetNumber = ExtractComponent(components, "street_number");

        if (route != null && streetNumber != null)
            return $"{route}, {streetNumber}";

        return route ?? streetNumber;
    }
}
```

### Frontend Changes

#### 1. Actualización de Types

**Archivo:** `frontend/src/types/camp.ts`

```typescript
export interface CampPhoto {
  id: string
  photoReference?: string
  photoUrl?: string
  width: number
  height: number
  attributionName: string
  attributionUrl?: string
  isOriginal: boolean
  isPrimary: boolean
  displayOrder: number
  createdAt: string
}

export interface Camp {
  id: string
  name: string
  description: string
  latitude: number
  longitude: number
  basePriceAdult: number
  basePriceChild: number
  basePriceBaby: number
  status: CampStatus

  // Extended Information
  formattedAddress?: string
  streetAddress?: string
  locality?: string
  administrativeArea?: string
  postalCode?: string
  country?: string
  phoneNumber?: string
  nationalPhoneNumber?: string
  websiteUrl?: string
  googleMapsUrl?: string
  googlePlaceId?: string
  googleRating?: number
  googleRatingCount?: number
  lastGoogleSyncAt?: string
  businessStatus?: string
  placeTypes?: string[]

  // Photos
  photos?: CampPhoto[]

  createdAt: string
  updatedAt: string
  editionCount?: number
}
```

#### 2. Componente: CampContactInfo

**Archivo:** `frontend/src/components/camps/CampContactInfo.vue`

```vue
<script setup lang="ts">
import type { Camp } from '@/types/camp'

interface Props {
  camp: Camp
}

const props = defineProps<Props>()
</script>

<template>
  <div class="rounded-lg border border-gray-200 bg-white p-6">
    <h3 class="mb-4 text-lg font-semibold text-gray-900">Información de Contacto</h3>

    <div class="space-y-3">
      <!-- Dirección -->
      <div v-if="camp.formattedAddress" class="flex items-start gap-2">
        <i class="pi pi-map-marker mt-1 text-gray-500"></i>
        <div>
          <p class="text-sm font-medium text-gray-700">Dirección</p>
          <p class="text-sm text-gray-600">{{ camp.formattedAddress }}</p>
        </div>
      </div>

      <!-- Teléfono -->
      <div v-if="camp.phoneNumber" class="flex items-start gap-2">
        <i class="pi pi-phone mt-1 text-gray-500"></i>
        <div>
          <p class="text-sm font-medium text-gray-700">Teléfono</p>
          <a
            :href="`tel:${camp.phoneNumber}`"
            class="text-sm text-blue-600 hover:underline"
          >
            {{ camp.nationalPhoneNumber || camp.phoneNumber }}
          </a>
        </div>
      </div>

      <!-- Website -->
      <div v-if="camp.websiteUrl" class="flex items-start gap-2">
        <i class="pi pi-globe mt-1 text-gray-500"></i>
        <div>
          <p class="text-sm font-medium text-gray-700">Sitio Web</p>
          <a
            :href="camp.websiteUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="text-sm text-blue-600 hover:underline"
          >
            {{ camp.websiteUrl }}
            <i class="pi pi-external-link ml-1 text-xs"></i>
          </a>
        </div>
      </div>

      <!-- Google Maps -->
      <div v-if="camp.googleMapsUrl" class="flex items-start gap-2">
        <i class="pi pi-directions mt-1 text-gray-500"></i>
        <div>
          <p class="text-sm font-medium text-gray-700">Ver en Google Maps</p>
          <a
            :href="camp.googleMapsUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="text-sm text-blue-600 hover:underline"
          >
            Abrir en Google Maps
            <i class="pi pi-external-link ml-1 text-xs"></i>
          </a>
        </div>
      </div>

      <!-- Rating (si existe) -->
      <div v-if="camp.googleRating" class="flex items-start gap-2">
        <i class="pi pi-star-fill mt-1 text-yellow-500"></i>
        <div>
          <p class="text-sm font-medium text-gray-700">Valoración Google</p>
          <p class="text-sm text-gray-600">
            {{ camp.googleRating.toFixed(1) }}
            <span class="text-gray-500">({{ camp.googleRatingCount }} valoraciones)</span>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
```

#### 3. Componente: CampPhotoGallery

**Archivo:** `frontend/src/components/camps/CampPhotoGallery.vue`

```vue
<script setup lang="ts">
import { ref } from 'vue'
import Image from 'primevue/image'
import type { CampPhoto } from '@/types/camp'

interface Props {
  photos: CampPhoto[]
}

const props = defineProps<Props>()

const getPhotoUrl = (photo: CampPhoto): string => {
  // Si tenemos photoUrl almacenada, usarla
  if (photo.photoUrl) {
    return photo.photoUrl
  }

  // Si es de Google Places, construir URL con Photo API
  if (photo.isOriginal && photo.photoReference) {
    // Esta URL se construiría en el backend o con un servicio
    return `/api/places/photo?reference=${photo.photoReference}&maxwidth=${photo.width}`
  }

  // Placeholder si no hay foto
  return '/placeholder-camp.jpg'
}

const primaryPhoto = props.photos.find(p => p.isPrimary) || props.photos[0]
const otherPhotos = props.photos.filter(p => !p.isPrimary).slice(0, 5)
</script>

<template>
  <div class="rounded-lg border border-gray-200 bg-white p-6">
    <h3 class="mb-4 text-lg font-semibold text-gray-900">Galería de Fotos</h3>

    <div v-if="photos && photos.length > 0" class="space-y-4">
      <!-- Primary Photo -->
      <div class="relative aspect-video overflow-hidden rounded-lg">
        <Image
          :src="getPhotoUrl(primaryPhoto)"
          :alt="`${primaryPhoto.attributionName}`"
          preview
          class="h-full w-full object-cover"
        />
        <div class="absolute bottom-2 right-2 rounded bg-black bg-opacity-50 px-2 py-1 text-xs text-white">
          Foto: {{ primaryPhoto.attributionName }}
        </div>
      </div>

      <!-- Thumbnail Grid -->
      <div v-if="otherPhotos.length > 0" class="grid grid-cols-3 gap-2 sm:grid-cols-4 md:grid-cols-5">
        <div
          v-for="photo in otherPhotos"
          :key="photo.id"
          class="relative aspect-square overflow-hidden rounded"
        >
          <Image
            :src="getPhotoUrl(photo)"
            :alt="`${photo.attributionName}`"
            preview
            class="h-full w-full object-cover"
          />
        </div>
      </div>

      <!-- Google Attribution -->
      <div v-if="photos.some(p => p.isOriginal)" class="text-xs text-gray-500">
        <i class="pi pi-info-circle mr-1"></i>
        Fotografías de Google Maps
      </div>
    </div>

    <div v-else class="rounded-lg border-2 border-dashed border-gray-300 p-8 text-center">
      <i class="pi pi-images mb-2 text-3xl text-gray-400"></i>
      <p class="text-sm text-gray-500">No hay fotografías disponibles</p>
    </div>
  </div>
</template>
```

#### 4. Actualización de CampLocationDetailPage

**Archivo:** `frontend/src/views/camps/CampLocationDetailPage.vue`

Añadir los nuevos componentes:

```vue
<script setup lang="ts">
// ... imports existentes
import CampContactInfo from '@/components/camps/CampContactInfo.vue'
import CampPhotoGallery from '@/components/camps/CampPhotoGallery.vue'
</script>

<template>
  <div class="container mx-auto p-4">
    <!-- ... Header existente ... -->

    <div class="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <!-- Columna Principal -->
      <div class="lg:col-span-2 space-y-6">
        <!-- Galería de Fotos (nuevo) -->
        <CampPhotoGallery v-if="camp.photos && camp.photos.length > 0" :photos="camp.photos" />

        <!-- Descripción existente -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-3 text-lg font-semibold text-gray-900">Descripción</h2>
          <p class="text-gray-700">{{ camp.description || 'Sin descripción' }}</p>
        </div>

        <!-- Mapa existente -->
        <div class="rounded-lg border border-gray-200 bg-white p-6">
          <h2 class="mb-4 text-lg font-semibold text-gray-900">Ubicación</h2>
          <CampLocationMap :locations="[...]" />
        </div>
      </div>

      <!-- Columna Lateral -->
      <div class="space-y-6">
        <!-- Información de Contacto (nuevo) -->
        <CampContactInfo :camp="camp" />

        <!-- Precios existente -->
        <!-- ... -->

        <!-- Metadata existente -->
        <!-- ... -->
      </div>
    </div>
  </div>
</template>
```

## Integración con Google Places Autocomplete

Esta feature se integra perfectamente con la spec de Google Places Autocomplete:

1. **Cuando el usuario selecciona un lugar:**
   - Se obtienen los detalles completos vía Place Details API
   - Se mapean TODOS los campos (básicos + extendidos)
   - Se auto-rellenan en el formulario
   - Se almacenan al crear el campamento

2. **Actualización manual:**
   - Todos los campos extendidos son opcionales
   - El usuario puede editarlos manualmente si es necesario

3. **Sincronización periódica:**
   - Opcionalmente, implementar job que actualice información de Google Places
   - Usar `googlePlaceId` para obtener datos actualizados
   - Actualizar rating, fotos, horarios, etc.

## Consideraciones

### Almacenamiento de Fotos

**Opción A: Solo Referencias (Recomendado para MVP)**
- Almacenar solo `photoReference` de Google Places
- Construir URLs dinámicamente usando Google Places Photo API
- Pros: Sin almacenamiento, siempre actualizado
- Contras: Dependencia de Google API, coste por solicitud

**Opción B: Descarga y Almacenamiento**
- Descargar fotos de Google Places
- Almacenar en Azure Blob Storage / AWS S3
- Pros: Control total, sin dependencias externas
- Contras: Almacenamiento, posibles problemas de copyright

**Opción Recomendada:** Opción A inicialmente, migrar a B si es necesario

### Validación

Todos los campos extendidos deben ser **opcionales**:
- Permitir crear campamentos sin Google Places
- Permitir crear campamentos manualmente
- Solo requerir campos básicos (nombre, descripción, coordenadas, precios)

### GDPR y Privacidad

- Fotos de Google Places: Atribución obligatoria
- Reviews: NO almacenar (solo mostrar en tiempo real si es necesario)
- Información personal: Ninguna en estos campos

## Criterios de Aceptación

- [ ] Modelo Camp actualizado con campos extendidos
- [ ] Migración de base de datos ejecutada correctamente
- [ ] Entidad CampPhoto creada y relacionada
- [ ] DTOs actualizados para incluir nuevos campos
- [ ] Servicio de mapeo Google Places → Camp implementado
- [ ] Frontend types actualizados
- [ ] Componente CampContactInfo implementado y funcional
- [ ] Componente CampPhotoGallery implementado y funcional
- [ ] CampLocationDetailPage muestra información extendida
- [ ] Formulario de creación permite campos opcionales
- [ ] Google Places autocomplete rellena campos extendidos
- [ ] Todos los campos extendidos son opcionales (no rompen funcionalidad existente)
- [ ] Tests unitarios para mapeo de datos
- [ ] Tests E2E para visualización de datos extendidos

## Fases de Implementación

### Fase 1: Backend - Modelo y Migración
1. Actualizar entidad Camp
2. Crear entidad CampPhoto
3. Crear y ejecutar migración
4. Actualizar DTOs
5. Tests unitarios

### Fase 2: Backend - Servicio de Mapeo
1. Crear GooglePlacesMapperService
2. Integrar con PlacesController existente
3. Actualizar CampsController para manejar campos extendidos
4. Tests de integración

### Fase 3: Frontend - Types y Composables
1. Actualizar types de Camp y CampPhoto
2. Actualizar useCamps composable
3. Actualizar useGooglePlaces para incluir mapeo

### Fase 4: Frontend - Componentes de Visualización
1. Crear CampContactInfo component
2. Crear CampPhotoGallery component
3. Actualizar CampLocationDetailPage
4. Actualizar CampLocationCard (mostrar rating si existe)

### Fase 5: Testing y Refinamiento
1. Tests E2E
2. Validación de datos
3. Optimización de performance
4. Documentación

## Próximos Pasos

1. Aprobar esta especificación
2. Decidir estrategia de almacenamiento de fotos
3. Implementar Fase 1 (Backend - Modelo)
4. Implementar Fase 2 (Backend - Mapeo)
5. Implementar Fases 3-4 (Frontend)
6. Testing completo

## Sincronización Periódica de Datos (Feature Adicional)

### Contexto

La información de los campamentos en Google Places puede cambiar con el tiempo:
- ⭐ Ratings y número de valoraciones
- 📞 Números de teléfono
- 🌐 Sitios web
- 📸 Nuevas fotografías
- 🏢 Estado del negocio (operativo, cerrado temporalmente, cerrado permanentemente)
- ⏰ Horarios de apertura

Es necesario implementar un sistema de **sincronización periódica** (recomendado: anual) para mantener la información actualizada.

### Casos de Uso

#### 1. Sincronización Manual Individual

- Usuario Board/Admin accede a detalle de campamento
- Ve fecha de última sincronización
- Puede hacer clic en "Actualizar desde Google Places"
- Sistema obtiene datos actualizados y los compara con los existentes
- Usuario revisa y aprueba cambios

**Cuándo usar:** Para actualizar información de un campamento específico cuando se detectan cambios o errores.

#### 2. Sincronización Manual en Bloque (Principal) ⭐

Este es el **flujo principal** para la actualización anual de campamentos:

1. Usuario Board/Admin accede a página de gestión de campamentos
2. Ve botón prominente "Sincronizar Campamentos con Google Places"
3. Se muestra diálogo con opciones:
   - **Opción por defecto:** Sincronizar solo campamentos activos
   - **Opción alternativa:** Incluir también inactivos para revisar su estado
   - Muestra cantidad de campamentos que se sincronizarán
4. Usuario confirma inicio del proceso
5. Sistema procesa en background todos los campamentos:
   - Obtiene datos actualizados de Google Places para cada uno
   - Compara con datos actuales
   - Genera reporte de cambios
6. Al finalizar:
   - Notificación al usuario que inició el proceso
   - Email con reporte detallado de cambios
   - Dashboard con resumen de cambios detectados
7. Usuario Board/Admin revisa cambios:
   - Ve lista de campamentos con cambios
   - Puede revisar cambios uno por uno
   - Puede aplicar todos los cambios de golpe o selectivamente

**Cuándo usar:** Actualización anual de información de todos los campamentos (recomendado: inicio de año antes de preparar nueva temporada).

### Especificación Técnica - Sincronización

#### 1. Nuevo Endpoint: Sync Individual

**Backend - CampsController.cs**

```csharp
[HttpPost("{id}/sync")]
[Authorize(Roles = "Board")]
public async Task<ActionResult<SyncResultDto>> SyncCampWithGooglePlaces(
    Guid id,
    [FromQuery] bool autoApprove = false)
{
    var camp = await _campService.GetByIdAsync(id);
    if (camp == null)
        return NotFound();

    if (string.IsNullOrEmpty(camp.GooglePlaceId))
        return BadRequest("Este campamento no tiene un Google Place ID asociado");

    try
    {
        var syncResult = await _campSyncService.SyncCampAsync(id, autoApprove);
        return Ok(syncResult);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error syncing camp {CampId}", id);
        return StatusCode(500, "Error al sincronizar con Google Places");
    }
}
```

#### 2. Nuevo Servicio: CampSyncService

**Interface:**

```csharp
public interface ICampSyncService
{
    Task<SyncResultDto> SyncCampAsync(Guid campId, bool autoApprove = false);
    Task<BatchSyncResultDto> SyncMultipleCampsAsync(List<Guid> campIds);
    Task<BatchSyncResultDto> SyncAllCampsAsync(bool includeInactive = false);
}
```

**Implementación:**

```csharp
public class CampSyncService : ICampSyncService
{
    private readonly ICampRepository _campRepository;
    private readonly IGooglePlacesService _placesService;
    private readonly IGooglePlacesMapperService _mapperService;
    private readonly ILogger<CampSyncService> _logger;

    public async Task<SyncResultDto> SyncCampAsync(Guid campId, bool autoApprove)
    {
        var camp = await _campRepository.GetByIdAsync(campId);
        if (camp == null)
            throw new NotFoundException("Camp not found");

        if (string.IsNullOrEmpty(camp.GooglePlaceId))
            throw new InvalidOperationException("Camp has no Google Place ID");

        // 1. Obtener datos actuales de Google Places
        var placeDetails = await _placesService.GetPlaceDetailsAsync(camp.GooglePlaceId);

        // 2. Mapear datos de Google Places
        var updatedInfo = _mapperService.MapPlaceDetailsToCamp(placeDetails);
        var updatedPhotos = _mapperService.MapPlacePhotos(placeDetails);

        // 3. Comparar datos actuales vs nuevos
        var changes = CompareData(camp, updatedInfo, updatedPhotos);

        // 4. Si autoApprove, aplicar cambios inmediatamente
        if (autoApprove && changes.HasChanges)
        {
            await ApplyChangesAsync(camp, updatedInfo, updatedPhotos);
        }

        // 5. Actualizar LastGoogleSyncAt
        camp.LastGoogleSyncAt = DateTime.UtcNow;
        await _campRepository.UpdateAsync(camp);

        return new SyncResultDto
        {
            CampId = campId,
            CampName = camp.Name,
            SyncedAt = DateTime.UtcNow,
            Changes = changes,
            Applied = autoApprove
        };
    }

    private ChangeDetectionDto CompareData(
        Camp existing,
        CampExtendedInfo updated,
        List<CampPhotoDto> updatedPhotos)
    {
        var changes = new ChangeDetectionDto();

        // Comparar cada campo
        if (existing.FormattedAddress != updated.FormattedAddress)
            changes.AddChange("FormattedAddress", existing.FormattedAddress, updated.FormattedAddress);

        if (existing.PhoneNumber != updated.PhoneNumber)
            changes.AddChange("PhoneNumber", existing.PhoneNumber, updated.PhoneNumber);

        if (existing.WebsiteUrl != updated.WebsiteUrl)
            changes.AddChange("WebsiteUrl", existing.WebsiteUrl, updated.WebsiteUrl);

        if (existing.GoogleRating != updated.GoogleRating)
            changes.AddChange("GoogleRating", existing.GoogleRating?.ToString(), updated.GoogleRating?.ToString());

        if (existing.GoogleRatingCount != updated.GoogleRatingCount)
            changes.AddChange("GoogleRatingCount", existing.GoogleRatingCount?.ToString(), updated.GoogleRatingCount?.ToString());

        if (existing.BusinessStatus != updated.BusinessStatus)
            changes.AddChange("BusinessStatus", existing.BusinessStatus, updated.BusinessStatus);

        // Comparar fotos
        var newPhotos = updatedPhotos
            .Where(up => !existing.Photos.Any(ep => ep.PhotoReference == up.PhotoReference))
            .ToList();

        if (newPhotos.Any())
            changes.NewPhotosCount = newPhotos.Count;

        return changes;
    }

    private async Task ApplyChangesAsync(
        Camp camp,
        CampExtendedInfo updatedInfo,
        List<CampPhotoDto> updatedPhotos)
    {
        // Aplicar cambios a campos
        camp.FormattedAddress = updatedInfo.FormattedAddress;
        camp.StreetAddress = updatedInfo.StreetAddress;
        camp.Locality = updatedInfo.Locality;
        camp.AdministrativeArea = updatedInfo.AdministrativeArea;
        camp.PostalCode = updatedInfo.PostalCode;
        camp.Country = updatedInfo.Country;
        camp.PhoneNumber = updatedInfo.PhoneNumber;
        camp.NationalPhoneNumber = updatedInfo.NationalPhoneNumber;
        camp.WebsiteUrl = updatedInfo.WebsiteUrl;
        camp.GoogleMapsUrl = updatedInfo.GoogleMapsUrl;
        camp.GoogleRating = updatedInfo.GoogleRating;
        camp.GoogleRatingCount = updatedInfo.GoogleRatingCount;
        camp.BusinessStatus = updatedInfo.BusinessStatus;
        camp.PlaceTypes = updatedInfo.PlaceTypes;

        // Añadir nuevas fotos (no eliminar las existentes)
        var newPhotos = updatedPhotos
            .Where(up => !camp.Photos.Any(ep => ep.PhotoReference == up.PhotoReference))
            .Select(p => new CampPhoto
            {
                Id = Guid.NewGuid(),
                CampId = camp.Id,
                PhotoReference = p.PhotoReference,
                Width = p.Width,
                Height = p.Height,
                AttributionName = p.AttributionName,
                AttributionUrl = p.AttributionUrl,
                IsOriginal = true,
                IsPrimary = !camp.Photos.Any(),
                DisplayOrder = camp.Photos.Count + p.DisplayOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        foreach (var photo in newPhotos)
        {
            camp.Photos.Add(photo);
        }

        await _campRepository.UpdateAsync(camp);
    }

    public async Task<BatchSyncResultDto> SyncAllCampsAsync(bool includeInactive = false)
    {
        // Obtener campamentos con Google Place ID
        // Por defecto solo activos, opcionalmente incluir inactivos
        var campsWithPlaceId = await _campRepository.GetCampsWithGooglePlaceIdAsync(includeInactive);

        var results = new List<SyncResultDto>();
        var errors = new List<string>();

        _logger.LogInformation(
            "Starting batch sync for {Count} camps (includeInactive: {IncludeInactive})",
            campsWithPlaceId.Count,
            includeInactive);

        foreach (var camp in campsWithPlaceId)
        {
            try
            {
                var result = await SyncCampAsync(camp.Id, autoApprove: false);
                results.Add(result);

                // Delay para no exceder rate limits de Google
                await Task.Delay(200); // 5 requests/second max
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing camp {CampId}", camp.Id);
                errors.Add($"{camp.Name}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "Batch sync completed. Processed: {Total}, Changes: {Changes}, Errors: {Errors}",
            results.Count,
            results.Count(r => r.Changes.HasChanges),
            errors.Count);

        return new BatchSyncResultDto
        {
            TotalProcessed = results.Count,
            SuccessCount = results.Count(r => !r.Changes.HasChanges || r.Applied),
            ChangesDetected = results.Count(r => r.Changes.HasChanges),
            Errors = errors,
            Results = results
        };
    }
}
```

#### 3. DTOs para Sincronización

```csharp
public record SyncResultDto(
    Guid CampId,
    string CampName,
    DateTime SyncedAt,
    ChangeDetectionDto Changes,
    bool Applied // true si los cambios se aplicaron automáticamente
);

public record ChangeDetectionDto
{
    public List<FieldChangeDto> FieldChanges { get; set; } = new();
    public int NewPhotosCount { get; set; }

    public bool HasChanges => FieldChanges.Any() || NewPhotosCount > 0;

    public void AddChange(string fieldName, string? oldValue, string? newValue)
    {
        if (oldValue != newValue)
        {
            FieldChanges.Add(new FieldChangeDto(fieldName, oldValue, newValue));
        }
    }
}

public record FieldChangeDto(
    string FieldName,
    string? OldValue,
    string? NewValue
);

public record BatchSyncResultDto(
    int TotalProcessed,
    int SuccessCount,
    int ChangesDetected,
    List<string> Errors,
    List<SyncResultDto> Results
);
```

#### 4. Endpoint: Sincronización en Bloque

**Backend - CampsController.cs**

```csharp
[HttpPost("batch-sync")]
[Authorize(Roles = "Board,Admin")]
public async Task<ActionResult<BatchSyncResultDto>> BatchSyncCamps(
    [FromBody] BatchSyncRequest request)
{
    try
    {
        BatchSyncResultDto result;

        if (request.CampIds != null && request.CampIds.Any())
        {
            // Sincronizar campamentos específicos
            result = await _campSyncService.SyncMultipleCampsAsync(request.CampIds);
        }
        else
        {
            // Sincronizar todos según filtros
            result = await _campSyncService.SyncAllCampsAsync(
                includeInactive: request.IncludeInactive
            );
        }

        // Enviar email con reporte
        if (result.ChangesDetected > 0)
        {
            await _emailService.SendSyncReportAsync(result, User.Identity.Name);
        }

        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in batch sync");
        return StatusCode(500, "Error al sincronizar campamentos");
    }
}

[HttpGet("sync-preview")]
[Authorize(Roles = "Board,Admin")]
public async Task<ActionResult<SyncPreviewDto>> GetSyncPreview(
    [FromQuery] bool includeInactive = false)
{
    var campsToSync = await _campRepository.GetCampsWithGooglePlaceIdAsync(includeInactive);

    return Ok(new SyncPreviewDto
    {
        TotalCamps = campsToSync.Count,
        ActiveCamps = campsToSync.Count(c => c.Status == CampStatus.Active),
        InactiveCamps = campsToSync.Count(c => c.Status == CampStatus.Inactive),
        CampsWithoutPlaceId = await _campRepository.CountCampsWithoutGooglePlaceIdAsync(),
        EstimatedDurationMinutes = Math.Ceiling(campsToSync.Count * 0.2 / 60.0) // 200ms por camp
    });
}
```

**Nuevos DTOs:**

```csharp
public record BatchSyncRequest(
    List<Guid>? CampIds, // Si es null, sincroniza todos
    bool IncludeInactive = false
);

public record SyncPreviewDto(
    int TotalCamps,
    int ActiveCamps,
    int InactiveCamps,
    int CampsWithoutPlaceId,
    double EstimatedDurationMinutes
);
```

### Frontend - Interfaz de Sincronización

#### 1. Componente: CampBatchSyncDialog (Principal)

**Archivo:** `frontend/src/components/camps/CampBatchSyncDialog.vue`

Este componente implementa el flujo principal de sincronización en bloque.

```vue
<script setup lang="ts">
import { ref, computed } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import Checkbox from 'primevue/checkbox'
import ProgressBar from 'primevue/progressbar'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import { useCampSync } from '@/composables/useCampSync'
import { useToast } from 'primevue/usetoast'

const visible = defineModel<boolean>('visible')
const toast = useToast()

const { getSyncPreview, batchSyncCamps, loading } = useCampSync()

const includeInactive = ref(false)
const preview = ref<SyncPreview | null>(null)
const syncResult = ref<BatchSyncResult | null>(null)
const currentStep = ref<'preview' | 'syncing' | 'results'>('preview')

const loadPreview = async () => {
  preview.value = await getSyncPreview(includeInactive.value)
}

const handleStartSync = async () => {
  if (!preview.value) return

  currentStep.value = 'syncing'

  const result = await batchSyncCamps({
    campIds: null, // null = todos
    includeInactive: includeInactive.value
  })

  if (result) {
    syncResult.value = result
    currentStep.value = 'results'

    toast.add({
      severity: 'success',
      summary: 'Sincronización Completada',
      detail: `${result.changesDetected} campamentos con cambios detectados`,
      life: 5000
    })
  }
}

const campsWithChanges = computed(() => {
  return syncResult.value?.results.filter(r => r.changes.hasChanges) || []
})

watch(visible, (newVal) => {
  if (newVal) {
    currentStep.value = 'preview'
    syncResult.value = null
    loadPreview()
  }
})

watch(includeInactive, () => {
  if (currentStep.value === 'preview') {
    loadPreview()
  }
})
</script>

<template>
  <Dialog
    v-model:visible="visible"
    header="Sincronización Masiva con Google Places"
    modal
    :closable="currentStep !== 'syncing'"
    class="w-full max-w-4xl"
  >
    <!-- Step 1: Preview -->
    <div v-if="currentStep === 'preview'">
      <Message severity="info" :closable="false" class="mb-4">
        Este proceso actualizará la información de los campamentos desde Google Places
        (dirección, teléfono, web, rating, fotos, etc.)
      </Message>

      <div v-if="preview" class="mb-6">
        <div class="mb-4 rounded-lg border border-gray-200 bg-gray-50 p-4">
          <h4 class="mb-3 font-semibold">Campamentos a Sincronizar</h4>
          <div class="grid grid-cols-2 gap-4 md:grid-cols-4">
            <div class="text-center">
              <p class="text-2xl font-bold text-blue-600">{{ preview.totalCamps }}</p>
              <p class="text-sm text-gray-600">Total</p>
            </div>
            <div class="text-center">
              <p class="text-2xl font-bold text-green-600">{{ preview.activeCamps }}</p>
              <p class="text-sm text-gray-600">Activos</p>
            </div>
            <div class="text-center">
              <p class="text-2xl font-bold text-gray-600">{{ preview.inactiveCamps }}</p>
              <p class="text-sm text-gray-600">Inactivos</p>
            </div>
            <div class="text-center">
              <p class="text-2xl font-bold text-orange-600">
                ~{{ Math.ceil(preview.estimatedDurationMinutes) }}
              </p>
              <p class="text-sm text-gray-600">Minutos estimados</p>
            </div>
          </div>
        </div>

        <div class="mb-4 flex items-center gap-2">
          <Checkbox v-model="includeInactive" binary input-id="include-inactive" />
          <label for="include-inactive" class="cursor-pointer">
            Incluir campamentos inactivos (para revisar su estado actual)
          </label>
        </div>

        <Message v-if="preview.campsWithoutPlaceId > 0" severity="warn" :closable="false">
          Hay {{ preview.campsWithoutPlaceId }} campamentos sin Google Place ID que no se
          sincronizarán
        </Message>
      </div>

      <div class="flex justify-end gap-2">
        <Button label="Cancelar" severity="secondary" outlined @click="visible = false" />
        <Button
          label="Iniciar Sincronización"
          icon="pi pi-sync"
          :loading="loading"
          :disabled="!preview || preview.totalCamps === 0"
          @click="handleStartSync"
        />
      </div>
    </div>

    <!-- Step 2: Syncing -->
    <div v-else-if="currentStep === 'syncing'" class="py-8 text-center">
      <i class="pi pi-spin pi-spinner mb-4 text-4xl text-blue-600"></i>
      <h3 class="mb-2 text-lg font-semibold">Sincronizando campamentos...</h3>
      <p class="mb-4 text-gray-600">
        Este proceso puede tomar varios minutos. Por favor, no cierre esta ventana.
      </p>
      <ProgressBar mode="indeterminate" class="mb-2" />
      <p class="text-sm text-gray-500">
        Procesando {{ preview?.totalCamps }} campamentos...
      </p>
    </div>

    <!-- Step 3: Results -->
    <div v-else-if="currentStep === 'results' && syncResult">
      <Message
        v-if="syncResult.changesDetected === 0"
        severity="success"
        :closable="false"
        class="mb-4"
      >
        ✅ No se detectaron cambios. Toda la información está actualizada.
      </Message>

      <div v-else>
        <Message severity="success" :closable="false" class="mb-4">
          Sincronización completada: {{ syncResult.totalProcessed }} procesados,
          {{ syncResult.changesDetected }} con cambios detectados
        </Message>

        <div v-if="syncResult.errors.length > 0" class="mb-4">
          <Message severity="error" :closable="false">
            {{ syncResult.errors.length }} errores durante la sincronización
          </Message>
          <ul class="mt-2 list-inside list-disc text-sm text-red-600">
            <li v-for="(error, idx) in syncResult.errors.slice(0, 5)" :key="idx">
              {{ error }}
            </li>
            <li v-if="syncResult.errors.length > 5" class="text-gray-500">
              ... y {{ syncResult.errors.length - 5 }} errores más
            </li>
          </ul>
        </div>

        <!-- Tabla de cambios -->
        <div class="mb-4">
          <h4 class="mb-2 font-semibold">Campamentos con Cambios Detectados</h4>
          <DataTable
            :value="campsWithChanges"
            paginator
            :rows="10"
            striped-rows
            class="text-sm"
          >
            <Column field="campName" header="Campamento" sortable />
            <Column header="Cambios">
              <template #body="{ data }">
                <span class="rounded bg-blue-100 px-2 py-1 text-xs font-medium text-blue-800">
                  {{ data.changes.fieldChanges.length }} campos
                </span>
                <span
                  v-if="data.changes.newPhotosCount > 0"
                  class="ml-1 rounded bg-green-100 px-2 py-1 text-xs font-medium text-green-800"
                >
                  {{ data.changes.newPhotosCount }} fotos
                </span>
              </template>
            </Column>
            <Column field="syncedAt" header="Sincronizado">
              <template #body="{ data }">
                {{ new Date(data.syncedAt).toLocaleString('es-ES') }}
              </template>
            </Column>
          </DataTable>
        </div>

        <Message severity="info" :closable="false">
          Los cambios han sido detectados pero NO aplicados automáticamente. Revisa cada
          campamento individualmente para aplicar los cambios que consideres apropiados.
        </Message>
      </div>

      <div class="flex justify-end gap-2">
        <Button label="Cerrar" @click="visible = false" />
        <Button
          label="Ver Reporte Completo"
          icon="pi pi-file-pdf"
          severity="secondary"
          outlined
        />
      </div>
    </div>
  </Dialog>
</template>
```

#### 2. Componente: CampSyncButton (Individual)

**Archivo:** `frontend/src/components/camps/CampSyncButton.vue`

```vue
<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import Dialog from 'primevue/dialog'
import Message from 'primevue/message'
import { useCampSync } from '@/composables/useCampSync'
import type { Camp, SyncResult } from '@/types/camp'

interface Props {
  camp: Camp
}

const props = defineProps<Props>()
const emit = defineEmits<{
  synced: [camp: Camp]
}>()

const { syncCamp, loading, error } = useCampSync()

const showDialog = ref(false)
const syncResult = ref<SyncResult | null>(null)

const canSync = computed(() => !!props.camp.googlePlaceId)

const lastSyncDate = computed(() => {
  if (!props.camp.lastGoogleSyncAt) return 'Nunca'
  return new Date(props.camp.lastGoogleSyncAt).toLocaleDateString('es-ES', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  })
})

const handleSync = async () => {
  const result = await syncCamp(props.camp.id)
  if (result) {
    syncResult.value = result
    showDialog.value = true
  }
}

const handleApplyChanges = async () => {
  // Aplicar cambios
  const result = await syncCamp(props.camp.id, true)
  if (result) {
    emit('synced', result.camp)
    showDialog.value = false
  }
}
</script>

<template>
  <div>
    <div class="flex items-center gap-2">
      <Button
        :disabled="!canSync || loading"
        :loading="loading"
        label="Sincronizar con Google"
        icon="pi pi-sync"
        outlined
        size="small"
        @click="handleSync"
      />
      <span v-if="camp.lastGoogleSyncAt" class="text-xs text-gray-500">
        Última sincronización: {{ lastSyncDate }}
      </span>
    </div>

    <Dialog
      v-model:visible="showDialog"
      header="Resultado de Sincronización"
      modal
      class="w-full max-w-2xl"
    >
      <div v-if="syncResult">
        <Message
          v-if="!syncResult.changes.hasChanges"
          severity="success"
          :closable="false"
          class="mb-4"
        >
          ✅ No se detectaron cambios. La información está actualizada.
        </Message>

        <div v-else>
          <Message severity="info" :closable="false" class="mb-4">
            Se detectaron {{ syncResult.changes.fieldChanges.length }} cambios en los datos
            <span v-if="syncResult.changes.newPhotosCount > 0">
              y {{ syncResult.changes.newPhotosCount }} nuevas fotografías
            </span>
          </Message>

          <!-- Lista de cambios -->
          <div class="mb-4 rounded border border-gray-200">
            <div class="border-b border-gray-200 bg-gray-50 px-4 py-2">
              <h4 class="font-semibold">Cambios Detectados</h4>
            </div>
            <div class="divide-y divide-gray-200">
              <div
                v-for="change in syncResult.changes.fieldChanges"
                :key="change.fieldName"
                class="px-4 py-3"
              >
                <p class="mb-1 text-sm font-medium text-gray-700">
                  {{ change.fieldName }}
                </p>
                <div class="grid grid-cols-2 gap-2 text-sm">
                  <div>
                    <p class="text-gray-500">Valor actual:</p>
                    <p class="text-gray-900">{{ change.oldValue || '(vacío)' }}</p>
                  </div>
                  <div>
                    <p class="text-gray-500">Nuevo valor:</p>
                    <p class="font-semibold text-green-700">{{ change.newValue || '(vacío)' }}</p>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Botones de acción -->
          <div class="flex justify-end gap-2">
            <Button
              label="Cancelar"
              severity="secondary"
              outlined
              @click="showDialog = false"
            />
            <Button
              label="Aplicar Cambios"
              icon="pi pi-check"
              @click="handleApplyChanges"
            />
          </div>
        </div>
      </div>
    </Dialog>
  </div>
</template>
```

#### 3. Composable: useCampSync

**Archivo:** `frontend/src/composables/useCampSync.ts`

```typescript
import { ref } from 'vue'
import { api } from '@/utils/api'
import type { SyncResult, BatchSyncResult, SyncPreview, BatchSyncRequest } from '@/types/camp'

export function useCampSync() {
  const loading = ref(false)
  const error = ref<string | null>(null)

  const getSyncPreview = async (includeInactive = false): Promise<SyncPreview | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.get<ApiResponse<SyncPreview>>(
        `/camps/sync-preview?includeInactive=${includeInactive}`
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      return null
    } catch (err: unknown) {
      error.value = 'Error al obtener vista previa de sincronización'
      console.error('Failed to get sync preview:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const syncCamp = async (
    campId: string,
    autoApprove = false
  ): Promise<SyncResult | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<SyncResult>>(
        `/camps/${campId}/sync?autoApprove=${autoApprove}`
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      return null
    } catch (err: unknown) {
      error.value = 'Error al sincronizar con Google Places'
      console.error('Failed to sync camp:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  const batchSyncCamps = async (
    request: BatchSyncRequest
  ): Promise<BatchSyncResult | null> => {
    loading.value = true
    error.value = null

    try {
      const response = await api.post<ApiResponse<BatchSyncResult>>(
        '/camps/batch-sync',
        request
      )

      if (response.data.success && response.data.data) {
        return response.data.data
      }

      return null
    } catch (err: unknown) {
      error.value = 'Error al sincronizar campamentos'
      console.error('Failed to batch sync camps:', err)
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    error,
    getSyncPreview,
    syncCamp,
    batchSyncCamps
  }
}
```

**Nuevos Types (agregar a camp.ts):**

```typescript
export interface SyncPreview {
  totalCamps: number
  activeCamps: number
  inactiveCamps: number
  campsWithoutPlaceId: number
  estimatedDurationMinutes: number
}

export interface BatchSyncRequest {
  campIds: string[] | null
  includeInactive: boolean
}

export interface BatchSyncResult {
  totalProcessed: number
  successCount: number
  changesDetected: number
  errors: string[]
  results: SyncResult[]
}

export interface SyncResult {
  campId: string
  campName: string
  syncedAt: string
  changes: ChangeDetection
  applied: boolean
}

export interface ChangeDetection {
  fieldChanges: FieldChange[]
  newPhotosCount: number
  hasChanges: boolean
}

export interface FieldChange {
  fieldName: string
  oldValue: string | null
  newValue: string | null
}
```

### Políticas de Sincronización

#### Campos que SE Actualizan

Estos campos se actualizan automáticamente desde Google Places:

- ✅ `formattedAddress`, `streetAddress`, `locality`, etc. (dirección)
- ✅ `phoneNumber`, `nationalPhoneNumber` (teléfonos)
- ✅ `websiteUrl` (sitio web)
- ✅ `googleMapsUrl` (enlace Maps)
- ✅ `googleRating`, `googleRatingCount` (valoraciones)
- ✅ `businessStatus` (estado del negocio)
- ✅ `placeTypes` (tipos)
- ✅ Añadir nuevas `photos` (nunca eliminar las existentes)

#### Campos que NO se Actualizan

Estos campos se mantienen como los editó el usuario:

- ❌ `name` (nombre del campamento - puede ser diferente al de Google)
- ❌ `description` (descripción personalizada)
- ❌ `latitude`, `longitude` (coordenadas - fijas)
- ❌ `basePriceAdult`, `basePriceChild`, `basePriceBaby` (precios propios)
- ❌ `status` (estado interno del campamento en nuestra app)
- ❌ Fotos subidas manualmente (`isOriginal = false`)

### Consideraciones

1. **Rate Limiting:**
   - Google Places API tiene límites de requests
   - Implementar delay entre sincronizaciones (200ms recomendado)
   - Para sincronización masiva, procesar en batches

2. **Conflictos:**
   - Si usuario editó manualmente un campo que Google actualiza: ¿qué hacer?
   - Recomendación: Mostrar diálogo comparativo, usuario decide
   - Opción avanzada: Flags `userEdited` por campo para preservar ediciones manuales

3. **Logging:**
   - Registrar todas las sincronizaciones
   - Guardar qué cambios se aplicaron y cuándo
   - Útil para auditoría y debugging

4. **Notificaciones:**
   - Email a administradores cuando sincronización automática detecta cambios
   - Notificaciones en app para cambios críticos (ej: campamento cerrado)

5. **Rollback:**
   - Considerar implementar historial de versiones
   - Permitir revertir sincronizaciones si es necesario

### Ubicación en la UI

**Sincronización en Bloque:**
- Botón prominente en `CampLocationsPage.vue` (vista principal de campamentos)
- Solo visible para usuarios Board/Admin
- Ubicación sugerida: Toolbar superior, junto a "Nuevo Campamento"
- Icono: `pi pi-sync` con label "Sincronizar con Google Places"

**Sincronización Individual:**
- Botón en `CampLocationDetailPage.vue` (vista de detalle)
- Muestra última fecha de sincronización
- Solo visible si el campamento tiene `googlePlaceId`

### Criterios de Aceptación - Sincronización

**Backend:**
- [ ] Endpoint `POST /camps/{id}/sync` implementado (sincronización individual)
- [ ] Endpoint `POST /camps/batch-sync` implementado (sincronización en bloque)
- [ ] Endpoint `GET /camps/sync-preview` implementado (vista previa)
- [ ] Servicio CampSyncService con lógica de comparación de datos
- [ ] DTOs de sincronización (SyncResultDto, ChangeDetectionDto, BatchSyncRequest, SyncPreviewDto)
- [ ] Método `GetCampsWithGooglePlaceIdAsync(includeInactive)` en repositorio
- [ ] Logs de todas las sincronizaciones guardados
- [ ] Rate limiting implementado (200ms entre requests)
- [ ] Email de notificación con reporte enviado a administradores

**Frontend:**
- [ ] Componente CampBatchSyncDialog implementado y funcional
- [ ] Componente CampSyncButton para sincronización individual
- [ ] Composable useCampSync con todos los métodos (preview, sync, batch)
- [ ] Types de TypeScript actualizados (SyncPreview, BatchSyncRequest, etc.)
- [ ] Botón de sincronización en bloque visible en CampLocationsPage (solo Board/Admin)
- [ ] Preview muestra cantidad de campamentos y tiempo estimado
- [ ] Checkbox para incluir/excluir inactivos funciona correctamente
- [ ] Diálogo muestra progreso durante sincronización
- [ ] Tabla de resultados muestra campamentos con cambios
- [ ] Usuario puede ver cambios detectados por campamento
- [ ] Toast notifications de éxito/error

**Testing:**
- [ ] Tests unitarios de lógica de comparación de datos
- [ ] Tests de integración de endpoints de sincronización
- [ ] Tests E2E de flujo completo de sincronización en bloque
- [ ] Tests E2E de sincronización individual
- [ ] Verificar que rate limiting funciona correctamente

### Fase de Implementación - Sincronización

**Fase 6: Sincronización (Post-MVP)**

1. Backend - Servicio de sincronización
2. Backend - Background job
3. Frontend - Composable y componente
4. Testing y optimización
5. Documentación de proceso

## Referencias

- Google Places API - Photo: https://developers.google.com/maps/documentation/places/web-service/photos
- Google Places API - Rate Limits: https://developers.google.com/maps/documentation/places/web-service/usage-and-billing
- Ejemplo de respuesta: `ai-specs/changes/feat-google-places-camps/examples/google-places-response.json`
- Spec relacionada: `ai-specs/changes/feat-camps-definition/google-places-autocomplete.md`
