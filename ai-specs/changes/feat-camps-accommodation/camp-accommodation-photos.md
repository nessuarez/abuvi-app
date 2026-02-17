# Feature: Camp Accommodation Capacity & Photos

## 🎯 Overview

Esta feature extiende el modelo de Campamentos para incluir:

1. **Capacidades de alojamiento detalladas** (habitaciones, bungalows, tiendas, etc.)
2. **Gestión de fotografías** del campamento
3. **Actualización automática del template** desde ediciones

## 🔴 TDD Approach - CRITICAL

**Every ticket MUST follow Red-Green-Refactor:**

1. ✍️ Write failing tests FIRST
2. ✅ Implement minimum code to pass
3. 🔧 Refactor while keeping tests green

---

## 📋 Business Requirements

### Contexto

Los campamentos tienen diferentes tipos de alojamiento que influyen en:

- Capacidad total del campamento
- Asignación de participantes a habitaciones
- Pricing diferenciado por tipo de alojamiento
- Toma de decisiones de la junta

### Tipos de Alojamiento

1. **Habitaciones Privadas**
   - Con baño propio
   - Con baño compartido

2. **Habitaciones Compartidas**
   - Múltiples configuraciones posibles
   - Ejemplo: "2 hab con 8 literas, baño compartido, sin ducha"
   - Ejemplo: "8 hab con 2 literas, baño privado con ducha"
   - Necesitan flexibilidad pero también capacidad calculable

3. **Bungalows/Casetas**
   - Unidades individuales

4. **Tiendas**
   - Tiendas propias del campamento (cantidad específica)
   - Espacio para tiendas de socios (m² o capacidad estimada)

5. **Autocaravanas**
   - Plazas disponibles

### Fotografías

- URLs a imágenes (blob storage en feature futura)
- Descripción por foto
- Orden de visualización
- Foto principal destacada

### Actualización de Template

**Comportamiento automático:**

- Cuando se actualiza `AccommodationCapacityJson` en una `CampEdition`
- El campo `AccommodationCapacityJson` del `Camp` se actualiza automáticamente
- Esto mantiene el template actualizado con la última información conocida
- Transparente para el usuario (no diferencia entre Camp y CampEdition)

---

## 🏗️ Technical Design

### 1. Data Model - Accommodation Capacity (JSON)

**Estructura JSON almacenada en columna `accommodation_capacity_json`:**

```json
{
  "privateRoomsWithBathroom": 5,
  "privateRoomsSharedBathroom": 3,
  "sharedRooms": [
    {
      "quantity": 2,
      "bedsPerRoom": 8,
      "hasBathroom": false,
      "hasShower": false,
      "notes": "8 literas"
    },
    {
      "quantity": 8,
      "bedsPerRoom": 2,
      "hasBathroom": true,
      "hasShower": true,
      "notes": "2 literas"
    },
    {
      "quantity": 2,
      "bedsPerRoom": 2,
      "hasBathroom": true,
      "hasShower": false,
      "notes": "2 camas de 90cm"
    }
  ],
  "bungalows": 10,
  "campOwnedTents": 15,
  "memberTentAreaSquareMeters": 500,
  "memberTentCapacityEstimate": 30,
  "motorhomeSpots": 10,
  "notes": "Texto libre para información adicional"
}
```

**Clases C# para serialización:**

```csharp
public class AccommodationCapacity
{
    public int? PrivateRoomsWithBathroom { get; set; }
    public int? PrivateRoomsSharedBathroom { get; set; }
    public List<SharedRoomInfo>? SharedRooms { get; set; }
    public int? Bungalows { get; set; }
    public int? CampOwnedTents { get; set; }
    public int? MemberTentAreaSquareMeters { get; set; }
    public int? MemberTentCapacityEstimate { get; set; }
    public int? MotorhomeSpots { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Calcula la capacidad total de camas del campamento
    /// </summary>
    public int CalculateTotalBedCapacity()
    {
        int total = 0;

        // Habitaciones privadas (asumiendo 2 camas por habitación)
        total += (PrivateRoomsWithBathroom ?? 0) * 2;
        total += (PrivateRoomsSharedBathroom ?? 0) * 2;

        // Habitaciones compartidas (suma de quantity * bedsPerRoom)
        total += SharedRooms?.Sum(r => r.Quantity * r.BedsPerRoom) ?? 0;

        return total;
    }
}

public class SharedRoomInfo
{
    public int Quantity { get; set; }
    public int BedsPerRoom { get; set; }
    public bool HasBathroom { get; set; }
    public bool HasShower { get; set; }
    public string? Notes { get; set; }
}
```

### 2. Data Model - Camp Photos

**Nueva entidad:**

```csharp
public class CampPhoto
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsMainPhoto { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Camp Camp { get; set; } = null!;
}
```

**EF Core Configuration:**

```csharp
public class CampPhotoConfiguration : IEntityTypeConfiguration<CampPhoto>
{
    public void Configure(EntityTypeBuilder<CampPhoto> builder)
    {
        builder.ToTable("camp_photos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url)
            .HasColumnName("url")
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(p => p.DisplayOrder)
            .HasColumnName("display_order");

        builder.Property(p => p.IsMainPhoto)
            .HasColumnName("is_main_photo");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.HasOne(p => p.Camp)
            .WithMany(c => c.Photos)
            .HasForeignKey(p => p.CampId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.CampId, p.DisplayOrder });
    }
}
```

### 3. Entity Changes

**Camp.cs:**

```csharp
// Nuevo campo
public string? AccommodationCapacityJson { get; set; }

// Nueva navegación
public List<CampPhoto> Photos { get; set; } = new();

// Helper method para obtener capacidad deserializada
public AccommodationCapacity? GetAccommodationCapacity()
{
    if (string.IsNullOrWhiteSpace(AccommodationCapacityJson))
        return null;

    return JsonSerializer.Deserialize<AccommodationCapacity>(
        AccommodationCapacityJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    );
}

// Helper method para setear capacidad serializada
public void SetAccommodationCapacity(AccommodationCapacity? capacity)
{
    AccommodationCapacityJson = capacity == null
        ? null
        : JsonSerializer.Serialize(capacity, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
}
```

**CampEdition.cs:**

```csharp
// Nuevo campo (nullable - si es null, usa el del Camp)
public string? AccommodationCapacityJson { get; set; }

// Métodos helpers similares a Camp
public AccommodationCapacity? GetAccommodationCapacity()
{
    if (string.IsNullOrWhiteSpace(AccommodationCapacityJson))
        return null;

    return JsonSerializer.Deserialize<AccommodationCapacity>(
        AccommodationCapacityJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    );
}

public void SetAccommodationCapacity(AccommodationCapacity? capacity)
{
    AccommodationCapacityJson = capacity == null
        ? null
        : JsonSerializer.Serialize(capacity, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
}
```

### 4. DTOs

**CreateCampRequest / UpdateCampRequest:**

```csharp
public record CreateCampRequest(
    // ... campos existentes ...
    AccommodationCapacity? AccommodationCapacity
);

public record UpdateCampRequest(
    // ... campos existentes ...
    AccommodationCapacity? AccommodationCapacity
);
```

**CampResponse:**

```csharp
public record CampResponse(
    // ... campos existentes ...
    AccommodationCapacity? AccommodationCapacity,
    int? CalculatedTotalBedCapacity, // Capacidad calculada
    List<CampPhotoResponse> Photos
);
```

**Camp Photos DTOs:**

```csharp
public record CreateCampPhotoRequest(
    Guid CampId,
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsMainPhoto
);

public record UpdateCampPhotoRequest(
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsMainPhoto
);

public record CampPhotoResponse(
    Guid Id,
    Guid CampId,
    string Url,
    string? Description,
    int DisplayOrder,
    bool IsMainPhoto,
    DateTime CreatedAt
);
```

**Camp Editions DTOs:**

```csharp
public record ProposeCampEditionRequest(
    // ... campos existentes ...
    AccommodationCapacity? AccommodationCapacity
);

public record CampEditionResponse(
    // ... campos existentes ...
    AccommodationCapacity? AccommodationCapacity,
    int? CalculatedTotalBedCapacity
);
```

---

## 📦 Implementation Plan

### Phase 1: Database & Core Models

#### Ticket 1: Add Accommodation Capacity Models (TDD)

**Priority:** P1
**Estimated Effort:** 2-3 hours

##### TDD Steps

**1. RED - Write Tests First:**

`tests/Abuvi.Tests/Unit/Features/Camps/AccommodationCapacityTests.cs`

```csharp
[Fact]
public void CalculateTotalBedCapacity_WithAllTypes_ReturnsCorrectSum()
{
    // Arrange
    var capacity = new AccommodationCapacity
    {
        PrivateRoomsWithBathroom = 5,
        PrivateRoomsSharedBathroom = 3,
        SharedRooms = new List<SharedRoomInfo>
        {
            new() { Quantity = 2, BedsPerRoom = 8 },
            new() { Quantity = 8, BedsPerRoom = 2 }
        }
    };

    // Act
    var total = capacity.CalculateTotalBedCapacity();

    // Assert
    // (5 + 3) * 2 + (2*8 + 8*2) = 16 + 32 = 48
    total.Should().Be(48);
}

[Fact]
public void CalculateTotalBedCapacity_WithNullValues_HandlesGracefully()
{
    // Test null handling
}

[Fact]
public void CalculateTotalBedCapacity_WithEmptySharedRooms_ReturnsCorrectSum()
{
    // Test empty collections
}
```

**2. GREEN - Implement:**

- Create `AccommodationCapacity` class in `CampsModels.cs`
- Create `SharedRoomInfo` class
- Implement `CalculateTotalBedCapacity()` method

**3. REFACTOR:**

- Ensure JSON serialization works correctly
- Add XML docs

##### Acceptance Criteria

- [x] All calculation tests pass
- [x] Handles null/empty values gracefully
- [x] JSON serialization/deserialization works

---

#### Ticket 2: Database Migration - Add Accommodation & Photos (TDD)

**Priority:** P1
**Estimated Effort:** 3-4 hours

##### TDD Steps

**1. RED - Write Integration Tests:**

`tests/Abuvi.Tests/Integration/Features/Camps/CampAccommodationTests.cs`

```csharp
[Fact]
public async Task Camp_WithAccommodationJson_CanBeSavedAndRetrieved()
{
    // Arrange
    var capacity = new AccommodationCapacity
    {
        PrivateRoomsWithBathroom = 5,
        SharedRooms = new List<SharedRoomInfo>
        {
            new() { Quantity = 2, BedsPerRoom = 8, HasBathroom = false, Notes = "Literas" }
        }
    };

    var camp = new Camp
    {
        Name = "Test Camp",
        // ... otros campos ...
    };
    camp.SetAccommodationCapacity(capacity);

    // Act
    await _context.Camps.AddAsync(camp);
    await _context.SaveChangesAsync();

    var retrieved = await _context.Camps.FindAsync(camp.Id);
    var retrievedCapacity = retrieved.GetAccommodationCapacity();

    // Assert
    retrievedCapacity.Should().NotBeNull();
    retrievedCapacity.PrivateRoomsWithBathroom.Should().Be(5);
    retrievedCapacity.SharedRooms.Should().HaveCount(1);
}

[Fact]
public async Task CampPhoto_CanBeCreatedAndLinkedToCamp()
{
    // Test photo creation
}

[Fact]
public async Task Camp_CanHaveMultiplePhotos_WithCorrectOrder()
{
    // Test multiple photos with ordering
}
```

**2. GREEN - Implement:**

- Add `AccommodationCapacityJson` column to `camps` table
- Add `AccommodationCapacityJson` column to `camp_editions` table
- Create `camp_photos` table
- Update `CampConfiguration.cs`
- Update `CampEditionConfiguration.cs`
- Create `CampPhotoConfiguration.cs`
- Create migration: `AddAccommodationAndPhotos`
- Add helper methods to `Camp` and `CampEdition` entities

**3. REFACTOR:**

- Verify indexes are optimal
- Test cascade deletes work correctly

##### Acceptance Criteria

- [x] Migration applies successfully
- [x] JSON can be saved and retrieved from database
- [x] Photos can be created and linked to camps
- [x] All integration tests pass

---

### Phase 2: Service Layer

#### Ticket 3: Camp Service - Accommodation Capacity (TDD)

**Priority:** P1
**Estimated Effort:** 3-4 hours

##### TDD Steps

**1. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`

```csharp
[Fact]
public async Task CreateAsync_WithAccommodationCapacity_SavesCorrectly()
{
    // Arrange
    var request = new CreateCampRequest(
        Name: "Camp with Capacity",
        // ... otros campos ...
        AccommodationCapacity: new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 10,
            SharedRooms = new List<SharedRoomInfo>
            {
                new() { Quantity = 5, BedsPerRoom = 4, HasBathroom = true }
            }
        }
    );

    // Act
    var result = await _service.CreateAsync(request);

    // Assert
    result.AccommodationCapacity.Should().NotBeNull();
    result.CalculatedTotalBedCapacity.Should().Be(40); // (10*2) + (5*4)
}

[Fact]
public async Task UpdateAsync_WithAccommodationCapacity_UpdatesCorrectly()
{
    // Test update
}

[Fact]
public async Task GetByIdAsync_ReturnsAccommodationCapacity()
{
    // Test retrieval includes capacity
}
```

**2. GREEN - Implement:**

- Update `CampsService.CreateAsync()` to handle `AccommodationCapacity`
- Update `CampsService.UpdateAsync()` to handle `AccommodationCapacity`
- Update response mapping to include calculated capacity
- Add JSON serialization in service layer

**3. REFACTOR:**

- Extract JSON serialization to helper methods
- Add proper error handling for invalid JSON

##### Acceptance Criteria

- [x] All service tests pass
- [x] Capacity is correctly serialized/deserialized
- [x] Calculated capacity is returned in responses

---

#### Ticket 4: Camp Editions Service - Auto-update Template (TDD)

**Priority:** P1
**Estimated Effort:** 4-5 hours

##### TDD Steps

**1. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/Camps/CampEditionsServiceTests.cs`

```csharp
[Fact]
public async Task ProposeAsync_WithAccommodationCapacity_UpdatesCampTemplate()
{
    // Arrange
    var camp = CreateTestCamp();
    camp.SetAccommodationCapacity(new AccommodationCapacity
    {
        PrivateRoomsWithBathroom = 5
    });

    var request = new ProposeCampEditionRequest(
        CampId: camp.Id,
        // ... otros campos ...
        AccommodationCapacity: new AccommodationCapacity
        {
            PrivateRoomsWithBathroom = 10 // NUEVO VALOR
        }
    );

    _campsRepository.GetByIdAsync(camp.Id).Returns(camp);

    // Act
    await _service.ProposeAsync(request);

    // Assert
    await _campsRepository.Received(1).UpdateAsync(
        Arg.Is<Camp>(c =>
            c.GetAccommodationCapacity()?.PrivateRoomsWithBathroom == 10
        ),
        Arg.Any<CancellationToken>()
    );
}

[Fact]
public async Task PromoteToDraftAsync_WithModifiedCapacity_UpdatesCampTemplate()
{
    // Test promotion also updates template
}

[Fact]
public async Task ProposeAsync_WithNullCapacity_DoesNotUpdateTemplate()
{
    // Test null handling
}
```

**2. GREEN - Implement:**

- Update `CampEditionsService.ProposeAsync()` to update camp template
- Update `CampEditionsService.PromoteToDraftAsync()` to update camp template
- Add logic to synchronize `AccommodationCapacityJson` from edition to camp
- Ensure atomic updates (edition + camp in same transaction)

**3. REFACTOR:**

- Extract template update logic to private method
- Add logging for template updates

##### Acceptance Criteria

- [x] Edition updates automatically update camp template
- [x] Updates are atomic (transaction boundary)
- [x] Null capacity doesn't break functionality

---

#### Ticket 5: Camp Photos Service (TDD)

**Priority:** P2
**Estimated Effort:** 4-5 hours

##### TDD Steps

**1. RED - Write Service Tests:**

`tests/Abuvi.Tests/Unit/Features/Camps/CampPhotosServiceTests.cs`

```csharp
[Fact]
public async Task AddPhotoAsync_WithValidData_CreatesPhoto()
{
    // Test photo creation
}

[Fact]
public async Task UpdatePhotoAsync_UpdatesCorrectly()
{
    // Test update
}

[Fact]
public async Task DeletePhotoAsync_RemovesPhoto()
{
    // Test deletion
}

[Fact]
public async Task SetMainPhotoAsync_UnsetsOtherMainPhotos()
{
    // Test only one main photo per camp
}

[Fact]
public async Task ReorderPhotosAsync_UpdatesDisplayOrder()
{
    // Test reordering
}
```

**2. GREEN - Implement:**

- Create `ICampPhotosRepository` interface
- Create `CampPhotosRepository` implementation
- Create `CampPhotosService` class
- Implement CRUD methods
- Add logic to ensure only one main photo per camp

**3. REFACTOR:**

- Optimize queries with proper includes
- Add validation for URL format

##### Acceptance Criteria

- [x] All photo operations work correctly
- [x] Only one main photo per camp enforced
- [x] Display order is maintained

---

### Phase 3: API Endpoints

#### Ticket 6: Camp Accommodation API Endpoints (TDD)

**Priority:** P1
**Estimated Effort:** 2-3 hours

##### Implementation

- Update existing endpoints to include `AccommodationCapacity` in requests/responses
- Add validators for accommodation capacity
- Update OpenAPI documentation

**Affected Endpoints:**

- `POST /api/camps` - Include accommodation in creation
- `PUT /api/camps/{id}` - Include accommodation in updates
- `GET /api/camps/{id}` - Return accommodation + calculated capacity
- `GET /api/camps` - Return accommodation in list

##### Acceptance Criteria

- [x] All endpoints accept and return accommodation data
- [x] Validation works correctly
- [x] OpenAPI documentation updated

---

#### Ticket 7: Camp Photos API Endpoints (TDD)

**Priority:** P2
**Estimated Effort:** 3-4 hours

##### Implementation

**New Endpoints:**

```csharp
// POST /api/camps/{campId}/photos
editionsGroup.MapPost("/{campId:guid}/photos", AddCampPhoto)
    .WithName("AddCampPhoto")
    .WithSummary("Add a photo to a camp")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .AddEndpointFilter<ValidationFilter<CreateCampPhotoRequest>>()
    .Produces<ApiResponse<CampPhotoResponse>>(201);

// PUT /api/camps/{campId}/photos/{photoId}
editionsGroup.MapPut("/{campId:guid}/photos/{photoId:guid}", UpdateCampPhoto)
    .WithName("UpdateCampPhoto")
    .WithSummary("Update a camp photo")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .Produces<ApiResponse<CampPhotoResponse>>(200);

// DELETE /api/camps/{campId}/photos/{photoId}
editionsGroup.MapDelete("/{campId:guid}/photos/{photoId:guid}", DeleteCampPhoto)
    .WithName("DeleteCampPhoto")
    .WithSummary("Delete a camp photo")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .Produces(204);

// POST /api/camps/{campId}/photos/{photoId}/set-main
editionsGroup.MapPost("/{campId:guid}/photos/{photoId:guid}/set-main", SetMainPhoto)
    .WithName("SetMainPhoto")
    .WithSummary("Set a photo as the main photo")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .Produces<ApiResponse<CampPhotoResponse>>(200);

// POST /api/camps/{campId}/photos/reorder
editionsGroup.MapPost("/{campId:guid}/photos/reorder", ReorderPhotos)
    .WithName("ReorderPhotos")
    .WithSummary("Reorder camp photos")
    .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))
    .Produces(204);
```

##### Acceptance Criteria

- [x] All photo endpoints implemented
- [x] Authorization required (Board+)
- [x] Validation in place
- [x] OpenAPI documentation complete

---

## 🧪 Testing Strategy

### Unit Tests

- [ ] `AccommodationCapacity.CalculateTotalBedCapacity()` with various scenarios
- [ ] Camp/CampEdition JSON serialization/deserialization
- [ ] CampsService create/update with accommodation
- [ ] CampEditionsService template auto-update logic
- [ ] CampPhotosService CRUD operations
- [ ] Main photo uniqueness enforcement

### Integration Tests

- [ ] Database schema supports JSON storage
- [ ] Photos cascade delete with camps
- [ ] Full workflow: Create camp → Create edition → Update capacity → Verify template updated
- [ ] Photo ordering persists correctly

### API Tests

- [ ] Endpoints accept and return accommodation data
- [ ] Photo upload/update/delete workflow
- [ ] Authorization checks

---

## 📝 Migration Script

```csharp
public partial class AddAccommodationAndPhotos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add accommodation_capacity_json to camps
        migrationBuilder.AddColumn<string>(
            name: "accommodation_capacity_json",
            table: "camps",
            type: "text",
            nullable: true);

        // Add accommodation_capacity_json to camp_editions
        migrationBuilder.AddColumn<string>(
            name: "accommodation_capacity_json",
            table: "camp_editions",
            type: "text",
            nullable: true);

        // Create camp_photos table
        migrationBuilder.CreateTable(
            name: "camp_photos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                camp_id = table.Column<Guid>(type: "uuid", nullable: false),
                url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false),
                is_main_photo = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_camp_photos", x => x.id);
                table.ForeignKey(
                    name: "FK_camp_photos_camps_camp_id",
                    column: x => x.camp_id,
                    principalTable: "camps",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_camp_photos_camp_id_display_order",
            table: "camp_photos",
            columns: new[] { "camp_id", "display_order" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "camp_photos");
        migrationBuilder.DropColumn(name: "accommodation_capacity_json", table: "camps");
        migrationBuilder.DropColumn(name: "accommodation_capacity_json", table: "camp_editions");
    }
}
```

---

## 🎯 Success Criteria

- [x] Accommodation capacity can be stored and retrieved as JSON
- [x] All accommodation types are supported with flexibility
- [x] Shared rooms can have detailed configurations
- [x] Total bed capacity is calculated correctly
- [x] Camp editions auto-update camp template when capacity changes
- [x] Photos can be managed (CRUD)
- [x] Only one main photo per camp
- [x] Photo ordering is maintained
- [x] All tests pass (unit + integration + API)
- [x] OpenAPI documentation is complete
- [x] Authorization is enforced (Board+ only)

---

## 🔮 Future Enhancements

1. **Blob Storage Integration**
   - Replace URLs with actual file uploads
   - Image optimization/resizing
   - CDN integration

2. **Advanced Capacity Management**
   - Room assignment algorithm
   - Availability tracking per edition
   - Capacity vs. registrations reporting

3. **Photo Gallery UI**
   - Drag-and-drop reordering
   - Image cropping/editing
   - Bulk upload

4. **Capacity Analytics**
   - Historical capacity trends
   - Utilization rates
   - Pricing recommendations based on capacity
