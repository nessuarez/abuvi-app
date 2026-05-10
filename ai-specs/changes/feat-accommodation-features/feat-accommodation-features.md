# Feature: Características de Alojamiento Configurables — Spec Enriquecida

## Contexto

El tablero de asignación de alojamiento (_encaje de bolillos_) necesita señales de compatibilidad entre las necesidades de las familias y las características de cada alojamiento o zona. Esta feature añade un **catálogo extensible de características** (etiquetas descriptivas con icono/emoji) gestionable desde la interfaz de la Junta Directiva, y los mecanismos de asociación many-to-many a `CampEditionAccommodation` y a `Zone` (entidad nueva). Además, reutiliza el módulo `MediaItems` existente para adjuntar fotos y planos.

> **Leer antes de implementar:**
>
> - `CampEditionAccommodation` ya existe en `CampsModels.cs`. Esta feature extiende esa entidad con una relación many-to-many, **sin alterar su estructura base**.
> - El módulo `MediaItems` ya existe en `src/Abuvi.API/Features/MediaItems/`. Se reutiliza añadiendo dos nuevas columnas FK opcionales (`AccommodationId`, `ZoneId`) al modelo, siguiendo el mismo patrón que `MemoryId` y `CampLocationId`.
> - El endpoint de estado de asignación (`GET /api/camp-editions/{editionId}/assignment-status`, definido en la spec del tablero _encaje de bolillos_) debe incluir las características en su respuesta. Esta spec **no define** ese endpoint, pero sí documenta qué campos exponer.

---

## TDD Approach — OBLIGATORIO

**Cada ticket: Rojo → Verde → Refactor.**

---

## Requisitos de negocio

### Catálogo de características

La Junta Directiva necesita mantener un catálogo de etiquetas descriptivas que se pueden adjuntar a alojamientos concretos y/o a zonas. Ejemplos: _"Sin litera"_, _"Baño propio"_, _"Accesible"_, _"Planta baja"_.

- Cada característica tiene: `nombre`, `icono` (emoji o nombre de icono), `descripción` (opcional) y `nivel de aplicación` (_applicability level_).
- El nivel de aplicación determina si la característica es apta para zonas, para alojamientos individuales, para tipos de alojamiento, o para cualquier combinación.
- La Junta puede crear, editar y eliminar características. Si una característica está en uso (tiene asociaciones activas) no se puede eliminar directamente — se debe desactivar.
- Las características se asocian a `CampEditionAccommodation` o a `Zone` mediante relaciones many-to-many.
- El tablero de asignación puede filtrar/visualizar alojamientos por características.

### Zonas de alojamiento

Para organizar el espacio del campamento se introduce una entidad `Zone` (zona/área). Una zona agrupa alojamientos y puede tener sus propias características y medios adjuntos.

- Cada zona pertenece a una edición de campamento (`CampEdition`).
- Un `CampEditionAccommodation` puede pertenecer opcionalmente a una zona.
- Las zonas tienen nombre, descripción y orden de visualización.

### Adjunto de fotos y planos

Se reutiliza el módulo `MediaItems` existente para que la Junta pueda adjuntar fotos y planos tanto a alojamientos individuales como a zonas:

- Se añaden las FK opcionales `AccommodationId` y `ZoneId` a `MediaItem`.
- El tipo `Document` ya existe en `MediaItemType` y sirve para planos.
- No se requiere flujo de aprobación para medios adjuntos a alojamientos/zonas (son de uso interno de la Junta); `IsApproved = true` y `IsPublished = false` por defecto.

### Exposición en el tablero

Las características deben estar disponibles en la respuesta del endpoint de estado de asignación para que el tablero pueda renderizar las _señales de compatibilidad_ sin llamadas adicionales.

---

## Modelo de datos

### 1. Nueva entidad `AccommodationFeature` (catálogo)

**Tabla:** `accommodation_features`

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `id` | `UUID` PK | `gen_random_uuid()` |
| `name` | `varchar(100)` | requerido, único |
| `icon` | `varchar(100)` | requerido (emoji o nombre de icono, ej: `"🛏"`, `"accessible"`) |
| `description` | `text` | opcional |
| `applicabilityLevel` | `enum` | requerido: `Zone`, `Accommodation`, `AccommodationType`, `Any` |
| `isActive` | `boolean` | requerido, default `true` |
| `sortOrder` | `integer >= 0` | requerido, default `0` |
| `createdAt` | `timestamp UTC` | auto |
| `updatedAt` | `timestamp UTC` | auto |

**Reglas de validación:**

- `name`: requerido, max 100 caracteres, único en toda la tabla (case-insensitive).
- `icon`: requerido, max 100 caracteres.
- `description`: opcional, max 500 caracteres.
- `applicabilityLevel`: enum `Zone | Accommodation | AccommodationType | Any`.
- `sortOrder`: >= 0.
- Una característica con `isActive = false` no se puede asociar a nuevos alojamientos o zonas, pero mantiene las asociaciones existentes.
- No se puede eliminar (hard delete) si tiene al menos una asociación activa en `AccommodationFeatureAssignment` o `ZoneFeatureAssignment`. En ese caso se devuelve `409 Conflict` con código `FEATURE_IN_USE`. La alternativa es desactivar.

**Relaciones:**

- Una `AccommodationFeature` puede estar en muchos `AccommodationFeatureAssignment` (many-to-many con `CampEditionAccommodation`).
- Una `AccommodationFeature` puede estar en muchos `ZoneFeatureAssignment` (many-to-many con `Zone`).

---

### 2. Nueva entidad de unión `AccommodationFeatureAssignment`

**Tabla:** `accommodation_feature_assignments`

Tabla de unión many-to-many entre `CampEditionAccommodation` y `AccommodationFeature`.

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `accommodationId` | `UUID` FK → `CampEditionAccommodation` | ON DELETE CASCADE |
| `featureId` | `UUID` FK → `AccommodationFeature` | ON DELETE RESTRICT |
| `createdAt` | `timestamp UTC` | auto |

**PK compuesta:** `(accommodationId, featureId)`.

**Reglas:**

- La `AccommodationFeature` referenciada debe estar activa al crear la asociación.
- Al eliminar una `CampEditionAccommodation`, se eliminan en cascada sus asignaciones.
- Al intentar eliminar una `AccommodationFeature` con asignaciones existentes, se devuelve `409 Conflict`.

---

### 3. Nueva entidad `Zone`

**Tabla:** `zones`

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `id` | `UUID` PK | `gen_random_uuid()` |
| `campEditionId` | `UUID` FK → `CampEdition` | ON DELETE CASCADE, requerido |
| `name` | `varchar(200)` | requerido |
| `description` | `text` | opcional |
| `sortOrder` | `integer >= 0` | requerido, default `0` |
| `isActive` | `boolean` | requerido, default `true` |
| `createdAt` | `timestamp UTC` | auto |
| `updatedAt` | `timestamp UTC` | auto |

**Reglas de validación:**

- `name`: requerido, max 200 caracteres. Único dentro de la misma edición de campamento.
- `sortOrder`: >= 0.

**Relaciones:**

- Una `Zone` pertenece a una `CampEdition` (ON DELETE CASCADE).
- Una `Zone` tiene muchos `ZoneFeatureAssignment`.
- Una `Zone` puede tener muchos `CampEditionAccommodation` (FK opcional en `CampEditionAccommodation`).
- Una `Zone` puede tener muchos `MediaItem` (FK opcional en `MediaItem`).

---

### 4. Nueva entidad de unión `ZoneFeatureAssignment`

**Tabla:** `zone_feature_assignments`

Tabla de unión many-to-many entre `Zone` y `AccommodationFeature`.

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `zoneId` | `UUID` FK → `Zone` | ON DELETE CASCADE |
| `featureId` | `UUID` FK → `AccommodationFeature` | ON DELETE RESTRICT |
| `createdAt` | `timestamp UTC` | auto |

**PK compuesta:** `(zoneId, featureId)`.

**Reglas:** idénticas a `AccommodationFeatureAssignment`.

---

### 5. Cambios en `CampEditionAccommodation`

**Archivo:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Añadir a la clase existente `CampEditionAccommodation`:

```csharp
// Zona a la que pertenece este alojamiento (opcional)
public Guid? ZoneId { get; set; }

// Navigation properties
public Zone? Zone { get; set; }
public ICollection<AccommodationFeatureAssignment> FeatureAssignments { get; set; } = [];
```

---

### 6. Cambios en `MediaItem`

**Archivo:** `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs`

Añadir a la clase existente `MediaItem`:

```csharp
// Enlace opcional a alojamiento específico de edición
public Guid? AccommodationId { get; set; }

// Enlace opcional a zona
public Guid? ZoneId { get; set; }

// Navigation
public CampEditionAccommodation? Accommodation { get; set; }
public Zone? Zone { get; set; }
```

Actualizar `CreateMediaItemRequest` para aceptar los nuevos campos:

```csharp
public record CreateMediaItemRequest(
    string FileUrl,
    string? ThumbnailUrl,
    MediaItemType Type,
    string Title,
    string? Description,
    int? Year,
    Guid? MemoryId,
    Guid? CampLocationId,
    Guid? AccommodationId,   // nuevo
    Guid? ZoneId,            // nuevo
    string? Context);
```

Actualizar `MediaItemResponse` para exponer los nuevos campos:

```csharp
public record MediaItemResponse(
    Guid Id,
    Guid UploadedByUserId,
    string UploadedByName,
    string FileUrl,
    string? ThumbnailUrl,
    string Type,
    string Title,
    string? Description,
    int? Year,
    string? Decade,
    Guid? MemoryId,
    Guid? AccommodationId,   // nuevo
    Guid? ZoneId,            // nuevo
    string? Context,
    bool IsPublished,
    bool IsApproved,
    DateTime CreatedAt);
```

---

### 7. DTOs nuevos

**Archivo:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesModels.cs` _(nuevo)_

```csharp
// ── AccommodationFeature ──────────────────────────────────────────────────────

public enum FeatureApplicabilityLevel
{
    Zone,
    Accommodation,
    AccommodationType,
    Any
}

public class AccommodationFeature
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FeatureApplicabilityLevel ApplicabilityLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<AccommodationFeatureAssignment> AccommodationAssignments { get; set; } = [];
    public ICollection<ZoneFeatureAssignment> ZoneAssignments { get; set; } = [];
}

public class AccommodationFeatureAssignment
{
    public Guid AccommodationId { get; set; }
    public Guid FeatureId { get; set; }
    public DateTime CreatedAt { get; set; }

    public CampEditionAccommodation Accommodation { get; set; } = null!;
    public AccommodationFeature Feature { get; set; } = null!;
}

// ── Zone ─────────────────────────────────────────────────────────────────────

public class Zone
{
    public Guid Id { get; set; }
    public Guid CampEditionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CampEdition CampEdition { get; set; } = null!;
    public ICollection<CampEditionAccommodation> Accommodations { get; set; } = [];
    public ICollection<ZoneFeatureAssignment> FeatureAssignments { get; set; } = [];
    public ICollection<MediaItem> MediaItems { get; set; } = [];
}

public class ZoneFeatureAssignment
{
    public Guid ZoneId { get; set; }
    public Guid FeatureId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Zone Zone { get; set; } = null!;
    public AccommodationFeature Feature { get; set; } = null!;
}

// ── Request / Response DTOs ──────────────────────────────────────────────────

public record AccommodationFeatureResponse(
    Guid Id,
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    bool IsActive,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateAccommodationFeatureRequest(
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    int SortOrder = 0
);

public record UpdateAccommodationFeatureRequest(
    string Name,
    string Icon,
    string? Description,
    FeatureApplicabilityLevel ApplicabilityLevel,
    bool IsActive,
    int SortOrder
);

public record SetFeatureAssignmentsRequest(
    List<Guid> FeatureIds
);

public record ZoneResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<AccommodationFeatureResponse> Features,
    IReadOnlyList<MediaItemResponse> MediaItems,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateZoneRequest(
    string Name,
    string? Description,
    int SortOrder = 0
);

public record UpdateZoneRequest(
    string Name,
    string? Description,
    bool IsActive,
    int SortOrder
);
```

---

### 8. Cambios en DTOs existentes de `CampEditionAccommodation`

**Archivo:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

Actualizar `CampEditionAccommodationResponse` para incluir:

```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    int SortOrder,
    int CurrentPreferenceCount,
    int FirstChoiceCount,
    Guid? ZoneId,                                   // nuevo
    string? ZoneName,                               // nuevo (desnormalizado para comodidad del frontend)
    IReadOnlyList<AccommodationFeatureResponse> Features,  // nuevo
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

Actualizar `CreateCampEditionAccommodationRequest` y `UpdateCampEditionAccommodationRequest` para aceptar `ZoneId?`:

```csharp
public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    Guid? ZoneId,    // nuevo
    int SortOrder = 0
);

public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool IsActive,
    Guid? ZoneId,    // nuevo
    int SortOrder
);
```

---

## Configuración EF Core

### 9. `AccommodationFeatureConfiguration` — nuevo archivo

**Archivo:** `src/Abuvi.API/Data/Configurations/AccommodationFeatureConfiguration.cs` _(nuevo)_

```csharp
public class AccommodationFeatureConfiguration : IEntityTypeConfiguration<AccommodationFeature>
{
    public void Configure(EntityTypeBuilder<AccommodationFeature> builder)
    {
        builder.ToTable("accommodation_features");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(f => f.Name).IsRequired().HasMaxLength(100).HasColumnName("name");
        builder.HasIndex(f => f.Name).IsUnique();
        builder.Property(f => f.Icon).IsRequired().HasMaxLength(100).HasColumnName("icon");
        builder.Property(f => f.Description).HasColumnType("text").HasColumnName("description");
        builder.Property(f => f.ApplicabilityLevel).IsRequired()
            .HasConversion<string>().HasColumnName("applicability_level");
        builder.Property(f => f.IsActive).IsRequired().HasDefaultValue(true).HasColumnName("is_active");
        builder.Property(f => f.SortOrder).IsRequired().HasDefaultValue(0).HasColumnName("sort_order");
        builder.Property(f => f.CreatedAt).IsRequired().HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).IsRequired().HasColumnName("updated_at");
    }
}
```

### 10. `AccommodationFeatureAssignmentConfiguration` — nuevo archivo

**Archivo:** `src/Abuvi.API/Data/Configurations/AccommodationFeatureAssignmentConfiguration.cs` _(nuevo)_

```csharp
public class AccommodationFeatureAssignmentConfiguration
    : IEntityTypeConfiguration<AccommodationFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<AccommodationFeatureAssignment> builder)
    {
        builder.ToTable("accommodation_feature_assignments");
        builder.HasKey(a => new { a.AccommodationId, a.FeatureId });
        builder.Property(a => a.AccommodationId).HasColumnName("accommodation_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnName("created_at");

        builder.HasOne(a => a.Accommodation)
            .WithMany(acc => acc.FeatureAssignments)
            .HasForeignKey(a => a.AccommodationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Feature)
            .WithMany(f => f.AccommodationAssignments)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### 11. `ZoneConfiguration` — nuevo archivo

**Archivo:** `src/Abuvi.API/Data/Configurations/ZoneConfiguration.cs` _(nuevo)_

```csharp
public class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones");
        builder.HasKey(z => z.Id);
        builder.Property(z => z.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(z => z.CampEditionId).IsRequired().HasColumnName("camp_edition_id");
        builder.Property(z => z.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
        builder.HasIndex(z => new { z.CampEditionId, z.Name }).IsUnique();
        builder.Property(z => z.Description).HasColumnType("text").HasColumnName("description");
        builder.Property(z => z.SortOrder).IsRequired().HasDefaultValue(0).HasColumnName("sort_order");
        builder.Property(z => z.IsActive).IsRequired().HasDefaultValue(true).HasColumnName("is_active");
        builder.Property(z => z.CreatedAt).IsRequired().HasColumnName("created_at");
        builder.Property(z => z.UpdatedAt).IsRequired().HasColumnName("updated_at");

        builder.HasOne(z => z.CampEdition)
            .WithMany()
            .HasForeignKey(z => z.CampEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(z => z.Accommodations)
            .WithOne(a => a.Zone)
            .HasForeignKey(a => a.ZoneId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

### 12. `ZoneFeatureAssignmentConfiguration` — nuevo archivo

**Archivo:** `src/Abuvi.API/Data/Configurations/ZoneFeatureAssignmentConfiguration.cs` _(nuevo)_

```csharp
public class ZoneFeatureAssignmentConfiguration
    : IEntityTypeConfiguration<ZoneFeatureAssignment>
{
    public void Configure(EntityTypeBuilder<ZoneFeatureAssignment> builder)
    {
        builder.ToTable("zone_feature_assignments");
        builder.HasKey(a => new { a.ZoneId, a.FeatureId });
        builder.Property(a => a.ZoneId).HasColumnName("zone_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.CreatedAt).IsRequired().HasColumnName("created_at");

        builder.HasOne(a => a.Zone)
            .WithMany(z => z.FeatureAssignments)
            .HasForeignKey(a => a.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Feature)
            .WithMany(f => f.ZoneAssignments)
            .HasForeignKey(a => a.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### 13. `CampEditionAccommodationConfiguration` — modificar

**Archivo:** `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs`

Añadir la FK opcional de zona:

```csharp
builder.Property(a => a.ZoneId).HasColumnName("zone_id").IsRequired(false);
// La relación HasMany/WithOne ya está configurada en ZoneConfiguration.
```

### 14. `MediaItemConfiguration` — modificar

**Archivo:** `src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs`

Añadir las FK opcionales:

```csharp
builder.Property(m => m.AccommodationId).HasColumnName("accommodation_id").IsRequired(false);
builder.Property(m => m.ZoneId).HasColumnName("zone_id").IsRequired(false);

builder.HasOne(m => m.Accommodation)
    .WithMany()
    .HasForeignKey(m => m.AccommodationId)
    .OnDelete(DeleteBehavior.SetNull);

builder.HasOne(m => m.Zone)
    .WithMany(z => z.MediaItems)
    .HasForeignKey(m => m.ZoneId)
    .OnDelete(DeleteBehavior.SetNull);
```

---

## Migración

**Comando:**

```bash
dotnet ef migrations add AddAccommodationFeaturesAndZones --project src/Abuvi.API
```

**Operaciones esperadas en la migración:**

1. `CreateTable` — `accommodation_features` (7 columnas + timestamps).
2. `CreateTable` — `accommodation_feature_assignments` (PK compuesta + 2 FK + timestamp).
3. `CreateTable` — `zones` (7 columnas + timestamps).
4. `CreateTable` — `zone_feature_assignments` (PK compuesta + 2 FK + timestamp).
5. `AddColumn` — `camp_edition_accommodations.zone_id` (`uuid`, nullable).
6. `AddColumn` — `media_items.accommodation_id` (`uuid`, nullable).
7. `AddColumn` — `media_items.zone_id` (`uuid`, nullable).
8. Índices únicos: `accommodation_features.name`, `zones(camp_edition_id, name)`.

**Archivo generado:** `src/Abuvi.API/Migrations/{timestamp}_AddAccommodationFeaturesAndZones.cs`

> Revisar siempre la migración generada antes de aplicarla. Ejecutar `dotnet ef database update` después.

---

## Validadores

### 15. `AccommodationFeatureValidator` — nuevo archivo

**Archivo:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesValidators.cs` _(nuevo)_

```csharp
public class CreateAccommodationFeatureValidator
    : AbstractValidator<CreateAccommodationFeatureRequest>
{
    public CreateAccommodationFeatureValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la característica es obligatorio")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres");

        RuleFor(x => x.Icon)
            .NotEmpty().WithMessage("El icono es obligatorio")
            .MaximumLength(100).WithMessage("El icono no puede superar los 100 caracteres");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar los 500 caracteres")
            .When(x => x.Description != null);

        RuleFor(x => x.ApplicabilityLevel)
            .IsInEnum().WithMessage("El nivel de aplicación no es válido");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class UpdateAccommodationFeatureValidator
    : AbstractValidator<UpdateAccommodationFeatureRequest>
{
    // Mismas reglas que Create más validación de IsActive (bool, sin restricción adicional)
}

public class SetFeatureAssignmentsValidator
    : AbstractValidator<SetFeatureAssignmentsRequest>
{
    public SetFeatureAssignmentsValidator()
    {
        RuleFor(x => x.FeatureIds)
            .NotNull().WithMessage("La lista de características no puede ser nula");

        RuleForEach(x => x.FeatureIds)
            .NotEmpty().WithMessage("El identificador de característica no puede ser vacío");
    }
}

public class CreateZoneValidator : AbstractValidator<CreateZoneRequest>
{
    public CreateZoneValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la zona es obligatorio")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("El orden debe ser mayor o igual a 0");
    }
}

public class UpdateZoneValidator : AbstractValidator<UpdateZoneRequest>
{
    // Mismas reglas que Create más IsActive
}
```

---

## Capa de servicio

### 16. `AccommodationFeaturesService` — nuevo

**Archivo:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesService.cs` _(nuevo)_

```csharp
public class AccommodationFeaturesService(IAccommodationFeaturesRepository repo)
{
    Task<IReadOnlyList<AccommodationFeatureResponse>> GetAllAsync(bool? activeOnly, CancellationToken ct);
    Task<AccommodationFeatureResponse> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AccommodationFeatureResponse> CreateAsync(CreateAccommodationFeatureRequest request, CancellationToken ct);
    Task<AccommodationFeatureResponse> UpdateAsync(Guid id, UpdateAccommodationFeatureRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

**Reglas de negocio:**

- `CreateAsync`: verifica que `name` sea único (case-insensitive) antes de insertar. Lanza `BusinessRuleException` con código `FEATURE_NAME_EXISTS` si existe.
- `DeleteAsync`: verifica que no haya filas en `accommodation_feature_assignments` ni en `zone_feature_assignments` con ese `featureId`. Si las hay, lanza `BusinessRuleException` con código `FEATURE_IN_USE` y HTTP 409. Si no hay, elimina hard.

### 17. `AccommodationFeatureAssignmentService` — nuevo

**Archivo:** `src/Abuvi.API/Features/Camps/AccommodationFeatureAssignmentService.cs` _(nuevo)_

```csharp
public class AccommodationFeatureAssignmentService(
    IAccommodationFeaturesRepository featuresRepo,
    ICampEditionAccommodationsRepository accommodationsRepo)
{
    // Reemplaza TODAS las asignaciones de un alojamiento con la lista proporcionada
    Task<IReadOnlyList<AccommodationFeatureResponse>> SetAccommodationFeaturesAsync(
        Guid accommodationId, SetFeatureAssignmentsRequest request, CancellationToken ct);

    // Reemplaza TODAS las asignaciones de una zona con la lista proporcionada
    Task<IReadOnlyList<AccommodationFeatureResponse>> SetZoneFeaturesAsync(
        Guid zoneId, SetFeatureAssignmentsRequest request, CancellationToken ct);
}
```

**Reglas de negocio:**

- `SetAccommodationFeaturesAsync`: verifica que el alojamiento exista. Verifica que todas las `FeatureIds` correspondan a features activas; si alguna no existe o está inactiva, lanza `ValidationException`. Elimina asignaciones previas e inserta las nuevas en la misma transacción.
- `SetZoneFeaturesAsync`: misma lógica para zonas.

### 18. `ZonesService` — nuevo

**Archivo:** `src/Abuvi.API/Features/Camps/ZonesService.cs` _(nuevo)_

```csharp
public class ZonesService(IZonesRepository repo)
{
    Task<IReadOnlyList<ZoneResponse>> GetByEditionAsync(Guid campEditionId, CancellationToken ct);
    Task<ZoneResponse> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ZoneResponse> CreateAsync(Guid campEditionId, CreateZoneRequest request, CancellationToken ct);
    Task<ZoneResponse> UpdateAsync(Guid id, UpdateZoneRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
```

**Reglas de negocio:**

- `CreateAsync`: verifica unicidad de `name` dentro de la misma edición.
- `DeleteAsync`: si la zona tiene alojamientos asignados (`CampEditionAccommodation.ZoneId`), pone `ZoneId = null` en esos alojamientos antes de eliminar la zona (EF lo maneja con `OnDelete(DeleteBehavior.SetNull)`, confirmando que la migración genera la FK con `ON DELETE SET NULL`).

---

## Repositorios

### 19. Interfaces de repositorio — nuevos

**Archivo:** `src/Abuvi.API/Features/Camps/IAccommodationFeaturesRepository.cs` _(nuevo)_

```csharp
public interface IAccommodationFeaturesRepository
{
    Task<IReadOnlyList<AccommodationFeature>> GetAllAsync(bool? activeOnly, CancellationToken ct);
    Task<AccommodationFeature?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<AccommodationFeature?> GetByNameAsync(string name, CancellationToken ct);
    Task<AccommodationFeature> AddAsync(AccommodationFeature feature, CancellationToken ct);
    Task<AccommodationFeature> UpdateAsync(AccommodationFeature feature, CancellationToken ct);
    Task DeleteAsync(AccommodationFeature feature, CancellationToken ct);
    Task<bool> HasAssignmentsAsync(Guid featureId, CancellationToken ct);
    Task<IReadOnlyList<AccommodationFeature>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    // Asignaciones a alojamientos
    Task SetAccommodationAssignmentsAsync(Guid accommodationId, IEnumerable<Guid> featureIds, CancellationToken ct);

    // Asignaciones a zonas
    Task SetZoneAssignmentsAsync(Guid zoneId, IEnumerable<Guid> featureIds, CancellationToken ct);
}
```

**Archivo:** `src/Abuvi.API/Features/Camps/IZonesRepository.cs` _(nuevo)_

```csharp
public interface IZonesRepository
{
    Task<IReadOnlyList<Zone>> GetByEditionAsync(Guid campEditionId, CancellationToken ct);
    Task<Zone?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Zone?> GetByNameAsync(Guid campEditionId, string name, CancellationToken ct);
    Task<Zone> AddAsync(Zone zone, CancellationToken ct);
    Task<Zone> UpdateAsync(Zone zone, CancellationToken ct);
    Task DeleteAsync(Zone zone, CancellationToken ct);
}
```

---

## Endpoints API

Todas las rutas requieren autenticación JWT. Los endpoints de escritura (POST/PUT/PATCH/DELETE) requieren rol `Admin` o `Board`.

---

### Catálogo de características

**Archivo:** `src/Abuvi.API/Features/Camps/AccommodationFeaturesEndpoints.cs` _(nuevo)_

Grupo base: `/api/accommodation-features`

---

#### GET /api/accommodation-features

Devuelve el catálogo completo de características.

**Autorización:** Admin o Board

**Query Parameters:**

- `activeOnly` (opcional, boolean): si `true`, filtra sólo las activas (default: `false`, devuelve todas)

**Respuesta exitosa (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Sin litera",
      "icon": "🛏",
      "description": "Camas individuales, sin literas",
      "applicabilityLevel": "Accommodation",
      "isActive": true,
      "sortOrder": 0,
      "createdAt": "2026-04-01T10:00:00Z",
      "updatedAt": "2026-04-01T10:00:00Z"
    }
  ]
}
```

---

#### GET /api/accommodation-features/{id}

Devuelve una característica por ID.

**Autorización:** Admin o Board

**Respuesta exitosa (200 OK):** objeto `AccommodationFeatureResponse`

**Errores:**

- **404 Not Found** — Característica no encontrada

---

#### POST /api/accommodation-features

Crea una nueva característica en el catálogo.

**Autorización:** Admin o Board

**Request Body:**

```json
{
  "name": "Baño propio",
  "icon": "🚿",
  "description": "El alojamiento dispone de baño privado",
  "applicabilityLevel": "Accommodation",
  "sortOrder": 1
}
```

**Validaciones:**

- `name`: requerido, max 100 caracteres, único (case-insensitive)
- `icon`: requerido, max 100 caracteres
- `description`: opcional, max 500 caracteres
- `applicabilityLevel`: requerido, uno de `Zone | Accommodation | AccommodationType | Any`
- `sortOrder`: >= 0

**Respuesta exitosa (201 Created):** `AccommodationFeatureResponse`

**Errores:**

- **400 Bad Request** — Validación fallida (`VALIDATION_ERROR`)
- **409 Conflict** — Nombre duplicado (`FEATURE_NAME_EXISTS`)

---

#### PUT /api/accommodation-features/{id}

Actualiza una característica existente.

**Autorización:** Admin o Board

**Request Body:**

```json
{
  "name": "Baño propio",
  "icon": "🚿",
  "description": "El alojamiento dispone de baño privado",
  "applicabilityLevel": "Accommodation",
  "isActive": true,
  "sortOrder": 1
}
```

**Respuesta exitosa (200 OK):** `AccommodationFeatureResponse` actualizado

**Errores:**

- **400 Bad Request** — Validación fallida
- **404 Not Found** — Característica no encontrada
- **409 Conflict** — Nombre duplicado en otra característica

---

#### DELETE /api/accommodation-features/{id}

Elimina una característica del catálogo. Falla si está en uso.

**Autorización:** Admin o Board

**Respuesta exitosa:** 204 No Content

**Errores:**

- **404 Not Found** — Característica no encontrada
- **409 Conflict** — Característica en uso; desactivar en lugar de eliminar (`FEATURE_IN_USE`)

---

### Asignación de características a alojamientos

Base: `/api/camps/editions/{editionId}/accommodations/{accommodationId}/features`

---

#### GET /api/camps/editions/{editionId}/accommodations/{accommodationId}/features

Devuelve las características asignadas a un alojamiento concreto.

**Autorización:** Admin o Board

**Respuesta exitosa (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Sin litera",
      "icon": "🛏",
      "description": null,
      "applicabilityLevel": "Accommodation",
      "isActive": true,
      "sortOrder": 0,
      "createdAt": "2026-04-01T10:00:00Z",
      "updatedAt": "2026-04-01T10:00:00Z"
    }
  ]
}
```

---

#### PUT /api/camps/editions/{editionId}/accommodations/{accommodationId}/features

Reemplaza por completo el conjunto de características de un alojamiento. Enviar lista vacía elimina todas las asignaciones.

**Autorización:** Admin o Board

**Request Body:**

```json
{
  "featureIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "9cb12a00-1111-2222-3333-444455556666"
  ]
}
```

**Respuesta exitosa (200 OK):** `IReadOnlyList<AccommodationFeatureResponse>` — lista resultante de características asignadas

**Errores:**

- **400 Bad Request** — Algún `featureId` no existe o está inactivo
- **404 Not Found** — Alojamiento no encontrado

---

### Zonas

**Archivo:** `src/Abuvi.API/Features/Camps/ZonesEndpoints.cs` _(nuevo)_

Base: `/api/camps/editions/{editionId}/zones`

---

#### GET /api/camps/editions/{editionId}/zones

Lista todas las zonas de una edición, incluyendo características y medios adjuntos.

**Autorización:** Admin o Board

**Respuesta exitosa (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "a1b2c3d4-...",
      "campEditionId": "3fa85f64-...",
      "name": "Zona A — Refugios",
      "description": "Zona norte con refugios cubiertos",
      "sortOrder": 0,
      "isActive": true,
      "features": [
        { "id": "...", "name": "Cubierto", "icon": "⛺", "description": null, "applicabilityLevel": "Zone", "isActive": true, "sortOrder": 0, "createdAt": "...", "updatedAt": "..." }
      ],
      "mediaItems": [],
      "createdAt": "2026-04-01T10:00:00Z",
      "updatedAt": "2026-04-01T10:00:00Z"
    }
  ]
}
```

---

#### GET /api/camps/editions/{editionId}/zones/{zoneId}

Devuelve una zona por ID.

**Autorización:** Admin o Board

**Respuesta exitosa (200 OK):** `ZoneResponse`

**Errores:**

- **404 Not Found** — Zona no encontrada o no pertenece a la edición

---

#### POST /api/camps/editions/{editionId}/zones

Crea una zona en una edición de campamento.

**Autorización:** Admin o Board

**Request Body:**

```json
{
  "name": "Zona B — Caravanas",
  "description": "Explanada sur para caravanas",
  "sortOrder": 1
}
```

**Validaciones:**

- `name`: requerido, max 200 caracteres, único dentro de la edición
- `sortOrder`: >= 0

**Respuesta exitosa (201 Created):** `ZoneResponse`

**Errores:**

- **400 Bad Request** — Validación fallida
- **404 Not Found** — Edición no encontrada
- **409 Conflict** — Nombre duplicado en la misma edición (`ZONE_NAME_EXISTS`)

---

#### PUT /api/camps/editions/{editionId}/zones/{zoneId}

Actualiza una zona.

**Autorización:** Admin o Board

**Request Body:**

```json
{
  "name": "Zona B — Caravanas",
  "description": "Explanada sur ampliada",
  "isActive": true,
  "sortOrder": 1
}
```

**Respuesta exitosa (200 OK):** `ZoneResponse` actualizado

**Errores:**

- **400 Bad Request** — Validación fallida
- **404 Not Found** — Zona no encontrada
- **409 Conflict** — Nombre duplicado en la misma edición

---

#### DELETE /api/camps/editions/{editionId}/zones/{zoneId}

Elimina una zona. Los alojamientos asignados mantienen su `ZoneId = null` (comportamiento SET NULL de la FK).

**Autorización:** Admin o Board

**Respuesta exitosa:** 204 No Content

**Errores:**

- **404 Not Found** — Zona no encontrada

---

### Asignación de características a zonas

Base: `/api/camps/editions/{editionId}/zones/{zoneId}/features`

---

#### GET /api/camps/editions/{editionId}/zones/{zoneId}/features

Devuelve las características asignadas a una zona.

**Autorización:** Admin o Board

**Respuesta exitosa (200 OK):** `IReadOnlyList<AccommodationFeatureResponse>`

---

#### PUT /api/camps/editions/{editionId}/zones/{zoneId}/features

Reemplaza el conjunto de características de una zona. Enviar lista vacía elimina todas.

**Autorización:** Admin o Board

**Request Body:** igual a `SetFeatureAssignmentsRequest`

**Respuesta exitosa (200 OK):** `IReadOnlyList<AccommodationFeatureResponse>` — lista resultante

**Errores:**

- **400 Bad Request** — Algún `featureId` no existe o está inactivo
- **404 Not Found** — Zona no encontrada

---

### Medios adjuntos a alojamientos y zonas

Se reutiliza **el endpoint existente** `POST /api/media-items` con los nuevos campos `accommodationId` y `zoneId`:

```json
{
  "fileUrl": "https://cdn.abuvi.org/media-items/abc/plano-refugio-norte.pdf",
  "thumbnailUrl": null,
  "type": "Document",
  "title": "Plano Refugio Norte",
  "description": "Plano de planta del refugio norte",
  "year": 2026,
  "accommodationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "zoneId": null,
  "context": "accommodation-plan"
}
```

Para listar medios de un alojamiento o zona se usa el endpoint existente `GET /api/media-items` con el nuevo parámetro de filtro:

**Nuevos query parameters en `GET /api/media-items`:**

- `accommodationId` (opcional, UUID): filtra medios del alojamiento indicado
- `zoneId` (opcional, UUID): filtra medios de la zona indicada

> **Nota de implementación:** Los `MediaItem` creados en contexto de alojamiento/zona tienen `IsApproved = true` y `IsPublished = false` por defecto (uso interno), a diferencia de los medios del archivo histórico que requieren aprobación explícita.

**Nuevo folder de blob storage:** Añadir `"accommodation-media"` a la lista de folders permitidos en `POST /api/blobs/upload`.

---

### Impacto en el endpoint de estado de asignación

El endpoint `GET /api/camp-editions/{editionId}/assignment-status` (definido en la spec del tablero _encaje de bolillos_) debe incluir las características en la respuesta de cada alojamiento y zona. El implementador de ese endpoint debe:

1. Hacer `.Include(a => a.FeatureAssignments).ThenInclude(fa => fa.Feature)` al cargar `CampEditionAccommodation`.
2. Incluir `features: IReadOnlyList<AccommodationFeatureResponse>` dentro de cada ítem de alojamiento en la respuesta.
3. Cargar las zonas con sus características: `.Include(z => z.FeatureAssignments).ThenInclude(fa => fa.Feature)`.
4. Incluir la lista de zonas con sus características en la respuesta raíz del endpoint.

---

## DI Registration

**Archivo:** `src/Abuvi.API/Program.cs`

```csharp
builder.Services.AddScoped<AccommodationFeaturesService>();
builder.Services.AddScoped<AccommodationFeatureAssignmentService>();
builder.Services.AddScoped<ZonesService>();
builder.Services.AddScoped<IAccommodationFeaturesRepository, AccommodationFeaturesRepository>();
builder.Services.AddScoped<IZonesRepository, ZonesRepository>();
```

---

## Archivos a crear o modificar

| Archivo | Acción |
|---------|--------|
| `src/Abuvi.API/Features/Camps/AccommodationFeaturesModels.cs` | **NUEVO** — entidades `AccommodationFeature`, `AccommodationFeatureAssignment`, `Zone`, `ZoneFeatureAssignment` + todos los DTOs |
| `src/Abuvi.API/Features/Camps/AccommodationFeaturesValidators.cs` | **NUEVO** — validadores de características y zonas |
| `src/Abuvi.API/Features/Camps/AccommodationFeaturesService.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/AccommodationFeatureAssignmentService.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/ZonesService.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/IAccommodationFeaturesRepository.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/AccommodationFeaturesRepository.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/IZonesRepository.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/ZonesRepository.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/AccommodationFeaturesEndpoints.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/ZonesEndpoints.cs` | **NUEVO** |
| `src/Abuvi.API/Data/Configurations/AccommodationFeatureConfiguration.cs` | **NUEVO** |
| `src/Abuvi.API/Data/Configurations/AccommodationFeatureAssignmentConfiguration.cs` | **NUEVO** |
| `src/Abuvi.API/Data/Configurations/ZoneConfiguration.cs` | **NUEVO** |
| `src/Abuvi.API/Data/Configurations/ZoneFeatureAssignmentConfiguration.cs` | **NUEVO** |
| `src/Abuvi.API/Features/Camps/CampsModels.cs` | **MODIFICAR** — añadir `ZoneId?` y `FeatureAssignments` a `CampEditionAccommodation`; actualizar `CampEditionAccommodationResponse`, `CreateCampEditionAccommodationRequest`, `UpdateCampEditionAccommodationRequest` |
| `src/Abuvi.API/Features/MediaItems/MediaItemsModels.cs` | **MODIFICAR** — añadir `AccommodationId?` y `ZoneId?` a `MediaItem`, `CreateMediaItemRequest` y `MediaItemResponse` |
| `src/Abuvi.API/Data/Configurations/CampEditionAccommodationConfiguration.cs` | **MODIFICAR** — añadir columna `zone_id` |
| `src/Abuvi.API/Data/Configurations/MediaItemConfiguration.cs` | **MODIFICAR** — añadir columnas `accommodation_id`, `zone_id` y relaciones |
| `src/Abuvi.API/Features/MediaItems/MediaItemsService.cs` | **MODIFICAR** — filtrar por `accommodationId` / `zoneId`; ajustar defaults de aprobación |
| `src/Abuvi.API/Features/MediaItems/MediaItemsEndpoints.cs` | **MODIFICAR** — añadir query params `accommodationId`, `zoneId` a `GET /api/media-items` |
| `src/Abuvi.API/Program.cs` | **MODIFICAR** — registrar nuevos servicios y repositorios |
| `src/Abuvi.API/Migrations/{timestamp}_AddAccommodationFeaturesAndZones.cs` | **NUEVO** — migración generada |

---

## Tests

### Tests unitarios

#### `AccommodationFeaturesServiceTests`

**Archivo:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationFeaturesServiceTests.cs`

```
GetAllAsync_ReturnsAllFeatures
GetAllAsync_WithActiveOnly_ReturnsOnlyActive
GetByIdAsync_WhenExists_ReturnsFeature
GetByIdAsync_WhenNotExists_ThrowsNotFoundException
CreateAsync_WithValidRequest_CreatesFeature
CreateAsync_WithDuplicateName_ThrowsBusinessRuleException
UpdateAsync_WithValidRequest_UpdatesFeature
UpdateAsync_WhenNotExists_ThrowsNotFoundException
DeleteAsync_WhenNoAssignments_DeletesFeature
DeleteAsync_WhenHasAssignments_ThrowsBusinessRuleException
```

#### `AccommodationFeatureAssignmentServiceTests`

**Archivo:** `src/Abuvi.Tests/Unit/Features/Camps/AccommodationFeatureAssignmentServiceTests.cs`

```
SetAccommodationFeaturesAsync_WithValidFeatureIds_ReplacesAssignments
SetAccommodationFeaturesAsync_WithEmptyList_RemovesAllAssignments
SetAccommodationFeaturesAsync_WithInactiveFeature_ThrowsValidationException
SetAccommodationFeaturesAsync_WhenAccommodationNotFound_ThrowsNotFoundException
SetZoneFeaturesAsync_WithValidFeatureIds_ReplacesAssignments
SetZoneFeaturesAsync_WhenZoneNotFound_ThrowsNotFoundException
```

#### `ZonesServiceTests`

**Archivo:** `src/Abuvi.Tests/Unit/Features/Camps/ZonesServiceTests.cs`

```
GetByEditionAsync_ReturnsZonesForEdition
CreateAsync_WithValidRequest_CreatesZone
CreateAsync_WithDuplicateNameInEdition_ThrowsBusinessRuleException
UpdateAsync_WithValidRequest_UpdatesZone
DeleteAsync_SetsZoneIdNullOnAccommodations
DeleteAsync_WhenNotExists_ThrowsNotFoundException
```

### Tests de integración

**Archivo:** `src/Abuvi.Tests/Integration/Features/Camps/AccommodationFeaturesIntegrationTests.cs`

```
AccommodationFeature_CanBeSavedAndRetrievedFromDatabase
AccommodationFeatureAssignment_CascadeDeletesWhenAccommodationDeleted
AccommodationFeatureAssignment_RestrictsDeleteWhenFeatureInUse
Zone_CanBeSavedAndRetrievedFromDatabase
ZoneFeatureAssignment_CascadeDeletesWhenZoneDeleted
MediaItem_WithAccommodationId_CanBeSavedAndFilteredCorrectly
MediaItem_WithZoneId_CanBeSavedAndFilteredCorrectly
```

### Test builders

Añadir en `src/Abuvi.Tests/Helpers/Builders/`:

- `AccommodationFeatureBuilder.cs`
- `ZoneBuilder.cs`

---

## Orden de implementación (TDD por ticket)

### Ticket A1 — Entidades y migración

1. Tests unitarios de serialización y validación de `AccommodationFeature` y `Zone`
2. Crear `AccommodationFeaturesModels.cs` con todas las entidades y DTOs
3. Crear configuraciones EF Core
4. Modificar `CampsModels.cs` y `MediaItemsModels.cs`
5. Modificar configuraciones existentes
6. Generar y aplicar migración `AddAccommodationFeaturesAndZones`
7. Tests de integración verdes (guardado/recuperación en base de datos)

### Ticket A2 — Catálogo de características (CRUD)

1. Tests unitarios de `AccommodationFeaturesService` (rojo)
2. Implementar `IAccommodationFeaturesRepository` + `AccommodationFeaturesRepository`
3. Implementar `AccommodationFeaturesService`
4. Validadores
5. Endpoints `AccommodationFeaturesEndpoints`
6. Registro en DI y `Program.cs`
7. Tests verdes

### Ticket A3 — Asignación de características a alojamientos

1. Tests unitarios de `AccommodationFeatureAssignmentService` (rojo)
2. Implementar métodos de asignación en repositorio
3. Implementar `AccommodationFeatureAssignmentService`
4. Endpoints de asignación bajo `/api/camps/editions/{editionId}/accommodations/{accommodationId}/features`
5. Actualizar `CampEditionAccommodationResponse` con `features`
6. Tests verdes

### Ticket A4 — Zonas (CRUD + asignación de características)

1. Tests unitarios de `ZonesService` (rojo)
2. Implementar `IZonesRepository` + `ZonesRepository`
3. Implementar `ZonesService`
4. Validadores
5. Endpoints `ZonesEndpoints` incluidos los de asignación de features
6. Tests verdes

### Ticket A5 — Medios adjuntos a alojamientos y zonas

1. Tests unitarios de filtrado en `MediaItemsService` (rojo)
2. Actualizar `MediaItemsService` para aceptar y filtrar `accommodationId` / `zoneId`
3. Actualizar `MediaItemsEndpoints` (nuevos query params)
4. Actualizar `MediaItemConfiguration`
5. Añadir folder `accommodation-media` a blob storage
6. Tests verdes

---

## Criterios de aceptación

- [ ] La Junta puede crear, editar y desactivar características del catálogo desde la interfaz
- [ ] No se puede eliminar una característica en uso; el sistema devuelve 409 con código `FEATURE_IN_USE`
- [ ] Una característica puede asociarse a un alojamiento y a una zona (many-to-many)
- [ ] `PUT /api/camps/editions/{editionId}/accommodations/{accommodationId}/features` reemplaza la lista completa de características; lista vacía elimina todas las asignaciones
- [ ] `PUT /api/camps/editions/{editionId}/zones/{zoneId}/features` igual para zonas
- [ ] `GET /api/camps/editions/{editionId}/accommodations` incluye `features: [...]` en cada ítem
- [ ] La Junta puede crear, editar y eliminar zonas en una edición
- [ ] Al eliminar una zona, los alojamientos de esa zona quedan con `zoneId = null` (no se eliminan)
- [ ] Se pueden adjuntar fotos y planos a alojamientos y zonas usando `POST /api/media-items` con los campos `accommodationId` / `zoneId`
- [ ] `GET /api/media-items?accommodationId={id}` devuelve sólo medios de ese alojamiento
- [ ] `GET /api/media-items?zoneId={id}` devuelve sólo medios de esa zona
- [ ] Las características están disponibles en la respuesta del endpoint de estado de asignación (a implementar en la spec del tablero)
- [ ] Todos los endpoints de escritura requieren rol `Admin` o `Board`
- [ ] Los nombres de características son únicos (case-insensitive)
- [ ] Los nombres de zonas son únicos dentro de la misma edición
- [ ] Mensajes de validación en español
- [ ] Todos los tests unitarios e integración pasan
- [ ] Cobertura >= 90 % en código nuevo

---

## Requisitos no funcionales

- **Rendimiento:** Las consultas de alojamientos deben usar `.Include` selectivo (no lazy loading); no cargar todos los `MediaItem` si no son necesarios.
- **Atomicidad:** `SetAccommodationFeaturesAsync` y `SetZoneFeaturesAsync` deben ejecutar DELETE + INSERT en la misma transacción.
- **Seguridad:** Ningún dato sensible. Todos los endpoints de modificación requieren Board+.
- **Consistencia:** Convención de nomenclatura de columnas `snake_case` en PostgreSQL (igual que el resto del proyecto).

---

## Fuera de alcance

- Interfaz de usuario (frontend): cubierto por tickets de frontend separados.
- Filtrado de alojamientos por características en el endpoint de listado general `GET /api/camps/editions/{editionId}/accommodations` — puede añadirse en fase siguiente como query param `featureIds`.
- Historial de cambios (audit log) en características y zonas.
- Importación masiva de características desde CSV.
- Compatibilidad automática entre necesidades de familias y características de alojamiento (lógica de _encaje_) — corresponde a la spec del tablero de asignación.
- Ordenación drag-and-drop de características; el campo `sortOrder` permite hacerlo mediante `PUT` individual.
