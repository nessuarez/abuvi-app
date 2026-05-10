# Ticket B — Etiquetado de necesidades de alojamiento por la Junta

## Resumen

La Junta Directiva necesita poder enriquecer manualmente cada inscripción con señales estructuradas de alojamiento antes de usar el tablero de asignación (Ticket C — Encaje de Bolillos). Esto incluye:

1. Etiquetar necesidades estructuradas usando el catálogo de características de alojamiento (Ticket A).
2. Añadir notas internas de alojamiento (solo visibles para Admin/Board).
3. Vincular explícitamente familias amigas a otras inscripciones de la misma edición (enlace estructurado para el algoritmo de asignación).

Estos datos deben exponerse en el endpoint de estado de asignación para que el tablero de Ticket C los consuma.

---

## Dependencias

| Ticket | Estado requerido |
|--------|-----------------|
| Ticket A — Catálogo de características (`AccommodationFeature`) | Debe estar implementado. Este ticket consume las características del catálogo. |
| Ticket C — Tablero de asignación (Encaje de Bolillos) | Consume los datos producidos por este ticket. |

---

## Contexto: campos existentes en `Registration`

Los siguientes campos **ya existen** en la entidad `Registration` y deben mostrarse en la UI sin modificación del modelo:

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `SpecialNeeds` | `string?` | Texto libre de preferencias/necesidades de alojamiento indicado por la familia en la inscripción. |
| `CampatesPreference` | `string?` | Texto libre de familias amigas indicado por la familia en la inscripción. |

---

## Cambios en el modelo de datos

### Nueva entidad: `RegistrationAccommodationNeed`

Almacena la relación entre una inscripción y una característica del catálogo (Ticket A). Representa las necesidades estructuradas etiquetadas manualmente por la Junta.

**Tabla:** `registration_accommodation_needs`

**Campos:**

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `Id` | `Guid` | PK, `gen_random_uuid()` |
| `RegistrationId` | `Guid` | FK → `registrations`, CASCADE delete, indexed |
| `AccommodationFeatureId` | `Guid` | FK → `accommodation_features` (Ticket A), RESTRICT delete |
| `TaggedByUserId` | `Guid` | FK → `users`, SET NULL on delete |
| `CreatedAt` | `datetime UTC` | `now() at time zone 'utc'` |

**Restricciones:**

- Índice único en `(registration_id, accommodation_feature_id)`: una característica sólo puede etiquetarse una vez por inscripción.
- `AccommodationFeatureId` no puede eliminarse si alguna inscripción lo referencia (RESTRICT).

**Entidad C#** (añadir en `Features/Registrations/RegistrationsModels.cs`):

```csharp
public class RegistrationAccommodationNeed
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid AccommodationFeatureId { get; set; }
    public Guid? TaggedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Registration Registration { get; set; } = null!;
    public AccommodationFeature AccommodationFeature { get; set; } = null!;
}
```

**Configuración EF** (`Data/Configurations/RegistrationAccommodationNeedConfiguration.cs`):

```csharp
builder.ToTable("registration_accommodation_needs");
builder.HasKey(n => n.Id);
builder.Property(n => n.Id).HasDefaultValueSql("gen_random_uuid()");
builder.Property(n => n.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.HasOne(n => n.Registration)
    .WithMany(r => r.AccommodationNeeds)
    .HasForeignKey(n => n.RegistrationId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasOne(n => n.AccommodationFeature)
    .WithMany()
    .HasForeignKey(n => n.AccommodationFeatureId)
    .OnDelete(DeleteBehavior.Restrict);
builder.HasIndex(n => new { n.RegistrationId, n.AccommodationFeatureId }).IsUnique();
builder.HasIndex(n => n.RegistrationId);
```

Añadir `ICollection<RegistrationAccommodationNeed> AccommodationNeeds` a `Registration`.

---

### Extensión de `Registration`: notas internas de alojamiento

Añadir el campo `AccommodationInternalNotes` a la entidad `Registration` existente.

**Campo nuevo:**

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `AccommodationInternalNotes` | `string?` | `text`, max 4000 caracteres. Nunca se expone a roles `Member`. Solo visible para `Admin` y `Board`. |

**Cambio en entidad C#** (modificar `Registration` en `RegistrationsModels.cs`):

```csharp
// Añadir junto a SpecialNeeds y CampatesPreference:
public string? AccommodationInternalNotes { get; set; }
```

**Configuración EF** (añadir en `RegistrationConfiguration.cs`):

```csharp
builder.Property(r => r.AccommodationInternalNotes).HasMaxLength(4000);
```

---

### Nueva entidad: `RegistrationFriendLink`

Enlace estructurado explícito entre dos inscripciones de la misma edición como "familias amigas". Completa el texto libre de `CampatesPreference` con un vínculo resolvible para el algoritmo de asignación.

**Tabla:** `registration_friend_links`

**Campos:**

| Campo | Tipo | Restricciones |
|-------|------|---------------|
| `Id` | `Guid` | PK, `gen_random_uuid()` |
| `RegistrationId` | `Guid` | FK → `registrations`, CASCADE delete, indexed |
| `LinkedRegistrationId` | `Guid` | FK → `registrations`, CASCADE delete |
| `CreatedByUserId` | `Guid` | FK → `users`, SET NULL on delete |
| `CreatedAt` | `datetime UTC` | `now() at time zone 'utc'` |

**Restricciones:**

- Índice único en `(registration_id, linked_registration_id)`.
- `RegistrationId != LinkedRegistrationId` (check constraint: no autoenlace).
- Ambas inscripciones deben pertenecer a la misma `CampEditionId` (validado en servicio, no en BD).
- El enlace es **direccional** desde el punto de vista del registro, pero la UI los muestra como bidireccionales (se crean dos filas, una por dirección, o la UI consulta en ambas direcciones — ver decisión en §API).

**Entidad C#** (añadir en `RegistrationsModels.cs`):

```csharp
public class RegistrationFriendLink
{
    public Guid Id { get; set; }
    public Guid RegistrationId { get; set; }
    public Guid LinkedRegistrationId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Registration Registration { get; set; } = null!;
    public Registration LinkedRegistration { get; set; } = null!;
}
```

**Configuración EF** (`Data/Configurations/RegistrationFriendLinkConfiguration.cs`):

```csharp
builder.ToTable("registration_friend_links");
builder.HasKey(l => l.Id);
builder.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");
builder.Property(l => l.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
builder.HasOne(l => l.Registration)
    .WithMany(r => r.FriendLinks)
    .HasForeignKey(l => l.RegistrationId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasOne(l => l.LinkedRegistration)
    .WithMany()
    .HasForeignKey(l => l.LinkedRegistrationId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasIndex(l => new { l.RegistrationId, l.LinkedRegistrationId }).IsUnique();
builder.HasIndex(l => l.RegistrationId);
builder.HasCheckConstraint("ck_registration_friend_links_no_self",
    "registration_id <> linked_registration_id");
```

Añadir a `Registration`:

```csharp
public ICollection<RegistrationFriendLink> FriendLinks { get; set; } = [];
```

---

### Migración

```bash
dotnet ef migrations add AddRegistrationAccommodationNeedsAndFriendLinks --project src/Abuvi.API
```

La migración debe:
- Crear tabla `registration_accommodation_needs`.
- Crear tabla `registration_friend_links`.
- Añadir columna `accommodation_internal_notes` (text, nullable) a `registrations`.

---

## Endpoints de API

### Convención de autorización

| Rol | Acceso |
|-----|--------|
| `Admin` | Lectura y escritura completa |
| `Board` | Lectura y escritura completa |
| `Member` | Lectura parcial (ver §Visibilidad) |

`AccommodationInternalNotes` **nunca** se incluye en respuestas para `Member`.

---

### 1. Gestión de necesidades estructuradas (etiquetas)

#### `PUT /api/registrations/{registrationId}/accommodation-needs`

Reemplaza la lista completa de necesidades estructuradas de una inscripción. Operación idempotente.

**Autorización:** Admin o Board

**Path params:**
- `registrationId` (GUID): ID de la inscripción.

**Request body:**

```json
{
  "featureIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "9bb12c33-1234-5678-abcd-ef0011223344"
  ]
}
```

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `featureIds` | `Guid[]` | Sí | Lista de IDs de `AccommodationFeature`. Puede ser vacía (elimina todas las etiquetas). Max 20 elementos. |

**Reglas de validación:**
- Todos los `featureIds` deben existir en `accommodation_features`.
- La inscripción debe existir.
- Sin duplicados en la lista.

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "registrationId": "...",
    "needs": [
      {
        "featureId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "featureName": "Habitación privada",
        "featureCategory": "Accessibility",
        "taggedByUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "createdAt": "2026-04-25T10:00:00Z"
      }
    ]
  }
}
```

**Error responses:**
- `400 Bad Request` — IDs de características no encontrados o lista inválida (`VALIDATION_ERROR`).
- `403 Forbidden` — Rol insuficiente.
- `404 Not Found` — Inscripción no encontrada (`NOT_FOUND`).

---

#### `GET /api/registrations/{registrationId}/accommodation-needs`

Devuelve la lista de necesidades estructuradas etiquetadas para una inscripción.

**Autorización:** Admin o Board

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "featureId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "featureName": "Habitación privada",
      "featureCategory": "Accessibility",
      "taggedByUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "createdAt": "2026-04-25T10:00:00Z"
    }
  ]
}
```

**Error responses:**
- `403 Forbidden` — Rol insuficiente.
- `404 Not Found` — Inscripción no encontrada.

---

### 2. Notas internas de alojamiento

#### `PATCH /api/registrations/{registrationId}/accommodation-notes`

Actualiza las notas internas de alojamiento de una inscripción.

**Autorización:** Admin o Board

**Request body:**

```json
{
  "accommodationInternalNotes": "La familia necesita habitaciones contiguas. Ana tiene movilidad reducida."
}
```

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `accommodationInternalNotes` | `string?` | Sí | Texto libre, max 4000 caracteres. `null` o cadena vacía elimina las notas. |

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "registrationId": "...",
    "accommodationInternalNotes": "La familia necesita habitaciones contiguas. Ana tiene movilidad reducida.",
    "updatedAt": "2026-04-25T10:15:00Z"
  }
}
```

**Error responses:**
- `400 Bad Request` — Texto supera 4000 caracteres (`VALIDATION_ERROR`).
- `403 Forbidden` — Rol insuficiente.
- `404 Not Found` — Inscripción no encontrada.

---

### 3. Vínculos de familias amigas (estructurado)

#### `PUT /api/registrations/{registrationId}/friend-links`

Reemplaza la lista completa de vínculos de familias amigas de una inscripción. Operación idempotente. La creación de un vínculo desde A→B crea automáticamente el vínculo recíproco B→A en la misma transacción (el enlace es bidireccional simétricamente en BD, pero se gestiona como uno desde la UI).

**Autorización:** Admin o Board

**Path params:**
- `registrationId` (GUID): ID de la inscripción origen.

**Request body:**

```json
{
  "linkedRegistrationIds": [
    "7fa85f64-5717-4562-b3fc-2c963f66afa6"
  ]
}
```

| Campo | Tipo | Obligatorio | Descripción |
|-------|------|-------------|-------------|
| `linkedRegistrationIds` | `Guid[]` | Sí | IDs de inscripciones a vincular. Puede ser vacía (elimina todos los vínculos). Max 10 elementos. |

**Reglas de validación:**
- Todas las `linkedRegistrationIds` deben existir.
- Todas deben pertenecer a la **misma edición** que `registrationId` (`SAME_EDITION_REQUIRED`).
- No puede incluir el propio `registrationId` (`NO_SELF_LINK`).
- Sin duplicados en la lista.
- Al reemplazar, elimina sólo los vínculos que ya no aparecen; crea los nuevos; mantiene los que siguen presentes. La bidireccionalidad se mantiene: si se elimina A→B, también se elimina B→A.

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "registrationId": "...",
    "friendLinks": [
      {
        "linkedRegistrationId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
        "linkedFamilyName": "Martínez Family",
        "createdByUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "createdAt": "2026-04-25T10:20:00Z"
      }
    ]
  }
}
```

**Error responses:**
- `400 Bad Request` — Validación fallida (`VALIDATION_ERROR`).
- `400 Bad Request` — Inscripción vinculada pertenece a otra edición (`SAME_EDITION_REQUIRED`).
- `400 Bad Request` — Autoenlace detectado (`NO_SELF_LINK`).
- `403 Forbidden` — Rol insuficiente.
- `404 Not Found` — Inscripción no encontrada.

---

#### `GET /api/registrations/{registrationId}/friend-links`

Devuelve los vínculos de familias amigas de una inscripción (unión de vínculos salientes + entrantes, deduplicados).

**Autorización:** Admin o Board

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "linkedRegistrationId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
      "linkedFamilyName": "Martínez Family",
      "createdByUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "createdAt": "2026-04-25T10:20:00Z"
    }
  ]
}
```

---

### 4. Extensión del endpoint de detalle de inscripción (admin)

#### `GET /api/registrations/{id}` — Campos añadidos para Admin/Board

El response existente de `RegistrationResponse` se extiende con los campos de este ticket **únicamente** cuando el rol del llamante es `Admin` o `Board`. Para `Member`, estos campos se omiten.

**Campos añadidos al objeto de respuesta:**

```jsonc
{
  // ... campos existentes ...
  "accommodationInternalNotes": "Texto de notas internas (solo Admin/Board, null para Member)",
  "accommodationNeeds": [
    {
      "featureId": "...",
      "featureName": "Habitación privada",
      "featureCategory": "Accessibility",
      "taggedByUserId": "...",
      "createdAt": "2026-04-25T10:00:00Z"
    }
  ],
  "friendLinks": [
    {
      "linkedRegistrationId": "...",
      "linkedFamilyName": "Martínez Family",
      "createdByUserId": "...",
      "createdAt": "2026-04-25T10:20:00Z"
    }
  ]
}
```

> Nota: `accommodationNeeds` y `friendLinks` pueden ser arrays vacíos `[]`. `accommodationInternalNotes` es `null` cuando no hay notas.

**Implementación:** Extender `RegistrationResponse` con tres campos opcionales; el servicio los incluye sólo cuando el usuario tiene rol `Admin` o `Board`. Alternativamente, un record derivado `AdminRegistrationResponse` puede heredar o componer `RegistrationResponse` con los campos adicionales.

---

### 5. Extensión del endpoint de estado de asignación (Ticket C)

El endpoint de estado de asignación del tablero (definido en Ticket C, ruta base `/api/camp-editions/{campEditionId}/assignment-status`) debe incluir, para cada inscripción en su payload:

```jsonc
{
  // ... campos existentes de inscripción ...
  "accommodationNeeds": ["uuid-feature-1", "uuid-feature-2"],
  "accommodationInternalNotes": "Texto de notas internas",
  "friendLinkRegistrationIds": ["uuid-reg-1", "uuid-reg-2"]
}
```

Estos campos se incorporarán en la especificación de Ticket C cuando se desarrolle ese endpoint. La entidad de datos que los provee queda definida en este ticket.

---

## DTOs C# nuevos y modificados

### Nuevos records en `RegistrationsModels.cs`

```csharp
// Request
public record UpdateAccommodationNeedsRequest(List<Guid> FeatureIds);
public record UpdateAccommodationNotesRequest(string? AccommodationInternalNotes);
public record UpdateFriendLinksRequest(List<Guid> LinkedRegistrationIds);

// Response
public record AccommodationNeedResponse(
    Guid FeatureId,
    string FeatureName,
    string FeatureCategory,
    Guid? TaggedByUserId,
    DateTime CreatedAt
);

public record AccommodationNeedsResponse(
    Guid RegistrationId,
    List<AccommodationNeedResponse> Needs
);

public record AccommodationNotesResponse(
    Guid RegistrationId,
    string? AccommodationInternalNotes,
    DateTime UpdatedAt
);

public record FriendLinkResponse(
    Guid LinkedRegistrationId,
    string LinkedFamilyName,
    Guid? CreatedByUserId,
    DateTime CreatedAt
);

public record FriendLinksResponse(
    Guid RegistrationId,
    List<FriendLinkResponse> FriendLinks
);
```

### Extensión de `RegistrationResponse` (campos opcionales para Admin/Board)

```csharp
// Añadir al final del record RegistrationResponse (o crear AdminRegistrationResponse):
string? AccommodationInternalNotes,           // null para Member
List<AccommodationNeedResponse> AccommodationNeeds,   // [] para Member
List<FriendLinkResponse> FriendLinks                 // [] para Member
```

---

## Archivos a crear / modificar

### Backend

| Acción | Archivo |
|--------|---------|
| Modificar | `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` |
| Modificar | `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` |
| Modificar | `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` |
| Modificar | `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` |
| Crear | `src/Abuvi.API/Data/Configurations/RegistrationAccommodationNeedConfiguration.cs` |
| Crear | `src/Abuvi.API/Data/Configurations/RegistrationFriendLinkConfiguration.cs` |
| Modificar | `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs` (añadir columna `AccommodationInternalNotes`) |
| Modificar | `src/Abuvi.API/Data/AbuviDbContext.cs` (añadir DbSets) |
| Crear | Migración EF: `AddRegistrationAccommodationNeedsAndFriendLinks` |

### Frontend

| Acción | Archivo / Componente |
|--------|---------------------|
| Modificar | `frontend/src/views/registrations/RegistrationDetailAdminView.vue` (o equivalente) |
| Modificar | `frontend/src/components/admin/` — añadir sección de etiquetado y notas en detalle de inscripción |
| Crear | `frontend/src/components/admin/registration-accommodation-needs/RegistrationAccommodationNeeds.vue` |
| Crear | `frontend/src/components/admin/registration-accommodation-needs/RegistrationFriendLinks.vue` |
| Modificar | `frontend/src/types/` — añadir tipos para los nuevos DTOs |

---

## Visibilidad y seguridad

| Dato | Admin | Board | Member |
|------|-------|-------|--------|
| `SpecialNeeds` (ya existe) | Visible | Visible | Visible (propio) |
| `CampatesPreference` (ya existe) | Visible | Visible | Visible (propio) |
| `AccommodationInternalNotes` (nuevo) | Visible y editable | Visible y editable | **Nunca expuesto** |
| `AccommodationNeeds` (etiquetas) | Visible y editable | Visible y editable | **Nunca expuesto** |
| `FriendLinks` (vínculos) | Visible y editable | Visible y editable | **Nunca expuesto** |

**Regla de implementación:** El servicio de registrations debe comprobar el rol del usuario antes de incluir estos campos en el response. El endpoint `GET /api/registrations/{id}` nunca expone `accommodationInternalNotes` a un llamante con rol `Member`, independientemente de si la inscripción le pertenece.

---

## Criterios de aceptación

### Modelo de datos

- [ ] La tabla `registration_accommodation_needs` existe con índice único en `(registration_id, accommodation_feature_id)`.
- [ ] La tabla `registration_friend_links` existe con índice único en `(registration_id, linked_registration_id)` y check constraint de no-autoenlace.
- [ ] La columna `accommodation_internal_notes` existe en `registrations` (text, nullable).
- [ ] La migración `AddRegistrationAccommodationNeedsAndFriendLinks` corre sin errores sobre una BD limpia.

### Etiquetado de necesidades

- [ ] `PUT /api/registrations/{id}/accommodation-needs` reemplaza correctamente la lista de etiquetas.
- [ ] Intentar etiquetar con un `featureId` inexistente devuelve `400`.
- [ ] `GET /api/registrations/{id}/accommodation-needs` devuelve las etiquetas correctas.
- [ ] Un `Member` no puede llamar a estos endpoints (devuelve `403`).

### Notas internas

- [ ] `PATCH /api/registrations/{id}/accommodation-notes` actualiza `accommodationInternalNotes` correctamente.
- [ ] `accommodationInternalNotes` con `null` o cadena vacía borra las notas.
- [ ] Texto de más de 4000 caracteres devuelve `400`.
- [ ] El campo **no aparece** en la respuesta de `GET /api/registrations/{id}` cuando el llamante es `Member`.
- [ ] El campo **sí aparece** cuando el llamante es `Admin` o `Board`.

### Vínculos de familias amigas

- [ ] `PUT /api/registrations/{id}/friend-links` con una lista válida crea los vínculos y sus recíprocos.
- [ ] Intentar vincular una inscripción de otra edición devuelve `400` con código `SAME_EDITION_REQUIRED`.
- [ ] Intentar autoenlace devuelve `400` con código `NO_SELF_LINK`.
- [ ] Al actualizar la lista, los vínculos eliminados también eliminan sus recíprocos.
- [ ] `GET /api/registrations/{id}/friend-links` muestra los vínculos en ambas direcciones de forma deduplicada.

### Integración con el endpoint de detalle (admin)

- [ ] `GET /api/registrations/{id}` para Admin/Board incluye `accommodationInternalNotes`, `accommodationNeeds` y `friendLinks`.
- [ ] Los arrays `accommodationNeeds` y `friendLinks` son `[]` cuando no hay datos (nunca `null`).

### Frontend

- [ ] En la vista de detalle de inscripción (admin), se muestra la sección "Alojamiento (Junta)".
- [ ] La sección muestra `SpecialNeeds` (texto libre, solo lectura).
- [ ] La sección muestra `CampatesPreference` (texto libre, solo lectura).
- [ ] La sección permite multi-select de características del catálogo (Ticket A) y guarda con `PUT accommodation-needs`.
- [ ] La sección muestra un textarea para notas internas y guarda con `PATCH accommodation-notes`.
- [ ] La sección permite buscar y vincular inscripciones de la misma edición como familias amigas y guarda con `PUT friend-links`.
- [ ] La sección de notas internas y etiquetas NO es visible en la vista de la familia (Member).

---

## Notas de diseño

- **Bidireccionalidad de vínculos:** Se almacenan como filas simétricas (A→B y B→A). El PUT gestiona ambas direcciones como unidad atómica. Esto simplifica las consultas del tablero de asignación (Ticket C), que sólo necesita buscar `WHERE registration_id = ?`.
- **Etiquetas como catálogo independiente:** Las `AccommodationFeature` son el catálogo definido en Ticket A. Este ticket no duplica esa entidad; sólo referencia sus IDs.
- **Notas internas vs. `SpecialNeeds`:** `SpecialNeeds` es texto introducido por la familia y ya existe. `AccommodationInternalNotes` es un campo de trabajo interno de la Junta, invisible para las familias.
- **Sin cifrado en reposo para notas internas:** A diferencia de `medicalNotes`/`allergies` (datos sanitarios), las notas internas de alojamiento no son datos sensibles de salud y no requieren AES-256. Basta con el control de acceso por rol.
- **Estrategia de endpoint único vs. sub-recursos:** Se usan sub-rutas dedicadas (`/accommodation-needs`, `/accommodation-notes`, `/friend-links`) en lugar de extender `PUT /admin-edit`, para mantener la granularidad de operaciones y evitar sobreescrituras accidentales durante el etiquetado.
