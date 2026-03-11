# feat: Añadir campo "viene con mascota" a la inscripción

## Resumen

Añadir un campo booleano `HasPet` a la entidad `Registration` para registrar si la unidad familiar asistirá al campamento con mascota. El campo se muestra como un checkbox en el formulario de inscripción (Step 2 - Extras), junto a "Necesidades especiales" y "Preferencia de acampantes".

## Justificación

- La organización necesita saber de antemano cuántas mascotas habrá en el campamento para planificar logística (zonas, normas, etc.).
- Es un dato a nivel de inscripción (familia), no de miembro individual.

## Modelo de datos

### Entidad `Registration`

Añadir campo:

```csharp
public bool HasPet { get; set; } = false;
```

### Configuración EF (`RegistrationConfiguration.cs`)

```csharp
builder.Property(r => r.HasPet)
    .HasDefaultValue(false)
    .HasColumnName("has_pet");
```

### Migración

Nueva migración que añade columna `has_pet` (boolean, not null, default false) a la tabla `registrations`.

```csharp
migrationBuilder.AddColumn<bool>(
    name: "has_pet",
    table: "registrations",
    type: "boolean",
    nullable: false,
    defaultValue: false);
```

## DTOs (Request/Response)

### `CreateRegistrationRequest`

Añadir campo:

```csharp
public record CreateRegistrationRequest(
    Guid CampEditionId,
    Guid FamilyUnitId,
    List<MemberAttendanceRequest> Members,
    string? Notes,
    string? SpecialNeeds,
    string? CampatesPreference,
    bool HasPet = false               // NUEVO
);
```

### `AdminEditRegistrationRequest`

Añadir campo:

```csharp
bool? HasPet                          // NUEVO (nullable para edición parcial)
```

### `RegistrationResponse`

Añadir campo:

```csharp
bool HasPet                           // NUEVO
```

## Validación

No se requiere validación adicional para un booleano. El valor por defecto es `false`.

## API Endpoints afectados

| Endpoint | Método | Cambio |
|---|---|---|
| `POST /api/registrations/` | Create | Acepta `hasPet` en el body |
| `GET /api/registrations/{id}` | Detail | Devuelve `hasPet` en la respuesta |
| `PUT /api/registrations/{id}/admin-edit` | Admin Edit | Acepta `hasPet` opcional |

No se necesitan nuevos endpoints.

## Frontend

### TypeScript Types (`frontend/src/types/registration.ts`)

```typescript
// En CreateRegistrationRequest
hasPet: boolean

// En RegistrationResponse
hasPet: boolean
```

### Formulario de inscripción (`RegisterForCampPage.vue`)

- Añadir `const hasPet = ref<boolean>(false)` junto a las refs existentes.
- En Step 2 (Extras), después de "Necesidades especiales", añadir:

```vue
<div class="mb-5 mt-4 flex items-center gap-2">
  <Checkbox v-model="hasPet" :binary="true" input-id="has-pet" data-testid="has-pet" />
  <label for="has-pet" class="text-sm font-medium text-gray-700">
    ¿Viene con mascota?
  </label>
</div>
```

- En `handleConfirm()`, incluir `hasPet: hasPet.value` en el payload.

### Detalle de inscripción (`RegistrationDetailPage.vue`)

- En la sección "Información adicional", añadir:

```vue
<div v-if="registration.hasPet" class="flex flex-col gap-0.5">
  <dt class="font-medium text-gray-600">Mascota</dt>
  <dd class="text-gray-800">Sí, asiste con mascota</dd>
</div>
```

- Actualizar la condición del `v-if` del bloque contenedor para incluir `registration.hasPet`.

### Admin edit (si aplica)

- Si existe un formulario de edición admin, incluir el checkbox de mascota.

## Archivos a modificar

### Backend

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` | Añadir `HasPet` a entity, DTOs request/response |
| `src/Abuvi.API/Data/Configurations/RegistrationConfiguration.cs` | Configurar columna `has_pet` |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Mapear `HasPet` en create y admin-edit |
| `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` | Verificar que el mapeo a response incluya el campo |
| `src/Abuvi.API/Migrations/` | Nueva migración `AddHasPetToRegistrations` |

### Frontend

| Archivo | Cambio |
|---|---|
| `frontend/src/types/registration.ts` | Añadir `hasPet` a interfaces |
| `frontend/src/views/registrations/RegisterForCampPage.vue` | Checkbox en Step 2 + ref + payload |
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Mostrar indicador de mascota |

## Criterios de aceptación

- [ ] El formulario de inscripción muestra un checkbox "¿Viene con mascota?" en el Step 2 (Extras).
- [ ] Por defecto el checkbox está desmarcado (false).
- [ ] Al crear una inscripción con el checkbox marcado, el valor `hasPet: true` se persiste en la base de datos.
- [ ] La vista de detalle de inscripción muestra "Sí, asiste con mascota" cuando `hasPet` es `true`.
- [ ] El endpoint de admin-edit permite modificar el campo `hasPet`.
- [ ] Las inscripciones existentes conservan `has_pet = false` por defecto (migración con default).
- [ ] El campo aparece en la respuesta del GET de detalle de inscripción.

## Requisitos no funcionales

- La migración debe ser retrocompatible (default false, not null).
- No se requiere índice adicional para este campo booleano.
