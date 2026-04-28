# feat-zone-accommodation-units — Gestión de unidades de alojamiento dentro de zonas

## Problema

El modelo actual define `CampEditionAccommodation` como la unidad individual asignable a una familia, con un `ZoneId` opcional para agruparla en una zona. Sin embargo:

1. **`CountByFamily` está hard-codeado** en `AccommodationAssignmentsRepository.cs` por tipo de alojamiento (`Caravan` y `Tent` = por familia; `Lodge`, `Bungalow`, `Motorhome` = por persona). No existe como campo en la entidad ni se puede configurar por unidad.
2. **No hay flujo de gestión de unidades dentro de una zona.** La Board debe crear las habitaciones/parcelas desde el panel global de "Alojamientos" y luego vincularlas manualmente a zonas, lo que no refleja la jerarquía natural: Zona → Unidades.
3. **Los distintos modelos de ocupación** (habitaciones de lodge = personas, parcelas de tienda/caravana = familias o unidades, parcelas de autocaravana = unidades) no son configurables ni visibles en la UI de gestión.

### Ejemplos concretos

| Tipo de zona | Ejemplo de zona | Ejemplo de unidad | Modelo de ocupación |
|---|---|---|---|
| Lodge | "Edificio 1 – Planta alta" | "Habitación 101" — 4 pax, baño propio, accesible | Por personas |
| Tienda | "Explanada junto piscina" | "Parcela T-12" — 1 tienda, punto de luz, baños a 100m, sombra | Por familia/tienda |
| Caravana/Autocaravana | "Zona junto Edificio 1" | "Parcela AC-03" — hasta 6 pax, 3,5 kW, baño en edificio 1, sol | Por familia/vehículo |

---

## Solución

Añadir `CountByFamily: bool` como campo explícito en la entidad `CampEditionAccommodation` (con migración) y exponerlo en los DTOs de gestión. Añadir un sub-panel de "Unidades" dentro de cada zona en `AccommodationZonePanel`, desde el que se puedan crear, editar y gestionar las unidades individuales de esa zona.

---

## Cambios de modelo de datos

### Entidad `CampEditionAccommodation` — nuevo campo

```csharp
public bool CountByFamily { get; set; } = false;
```

**Regla de negocio**: Para `Tent` y `Caravan`, el valor por defecto al crear es `true`; para el resto, `false`. El usuario puede sobreescribir manualmente.

### Configuración EF Core (`CampEditionAccommodationConfiguration`)

```csharp
builder.Property(a => a.CountByFamily)
    .HasColumnName("count_by_family")
    .HasDefaultValue(false);
```

### Migración

Nueva migración EF Core: `AddCountByFamilyToAccommodations`

```sql
ALTER TABLE camp_edition_accommodations
ADD count_by_family BOOLEAN NOT NULL DEFAULT FALSE;
```

---

## Cambios backend

### `CampsModels.cs`

**`CampEditionAccommodation` (entidad):**
- Añadir `public bool CountByFamily { get; set; } = false;`

**`CampEditionAccommodationResponse`:**
```csharp
public record CampEditionAccommodationResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,      // ← nuevo
    bool IsActive,
    int SortOrder,
    int CurrentPreferenceCount,
    int FirstChoiceCount,
    Guid? ZoneId,
    string? ZoneName,
    IReadOnlyList<AccommodationFeatureResponse> Features,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**`CreateCampEditionAccommodationRequest`:**
```csharp
public record CreateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily = false,  // ← nuevo (default según tipo en service)
    Guid? ZoneId = null,
    int SortOrder = 0
);
```

**`UpdateCampEditionAccommodationRequest`:**
```csharp
public record UpdateCampEditionAccommodationRequest(
    string Name,
    AccommodationType AccommodationType,
    string? Description,
    int? Capacity,
    bool CountByFamily,  // ← nuevo
    bool IsActive,
    Guid? ZoneId,
    int SortOrder
);
```

### `CampEditionAccommodationsService.cs`

En `CreateAsync`: asignar `CountByFamily = request.CountByFamily`. Si el valor no se especifica, aplicar default inteligente:
```csharp
CountByFamily = request.CountByFamily
    ?? (request.AccommodationType is AccommodationType.Tent or AccommodationType.Caravan),
```

En `UpdateAsync`: asignar `accommodation.CountByFamily = request.CountByFamily;`

En `ToResponse`: incluir `a.CountByFamily` en el record.

### `AccommodationAssignmentsRepository.cs`

Eliminar el set hard-codeado `ByFamilyTypes` y usar el campo de la entidad:
```csharp
// ELIMINAR:
private static readonly HashSet<AccommodationType> ByFamilyTypes = [AccommodationType.Caravan, AccommodationType.Tent];

// CAMBIAR en la proyección de AssignmentAccommodationResponse:
ByFamilyTypes.Contains(a.AccommodationType)  →  a.CountByFamily
```

### Validación (`CampEditionAccommodationsValidators.cs` o inline)

Ningún cambio de validación específico necesario — `CountByFamily` es un booleano sin restricciones.

### Tests

**Unit test** (`CampEditionAccommodationsServiceTests.cs`):
- `CreateAsync_WithTentType_DefaultsCountByFamilyTrue`
- `CreateAsync_WithLodgeType_DefaultsCountByFamilyFalse`
- `CreateAsync_WithExplicitCountByFamily_UsesProvidedValue`
- `UpdateAsync_UpdatesCountByFamily`

**Integration test** (`AccommodationFeaturesIntegrationTests.cs` o nuevo archivo):
- Verificar que `AccommodationAssignmentsRepository` usa `CountByFamily` del campo en lugar del tipo

---

## Cambios frontend

### `frontend/src/types/camp-edition.ts`

```typescript
export interface CampEditionAccommodation {
  // ... campos existentes ...
  countByFamily: boolean  // ← nuevo
}

export interface CreateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily?: boolean  // ← nuevo
  zoneId?: string | null   // ← ya existe, verificar
  sortOrder?: number
}

export interface UpdateCampEditionAccommodationRequest {
  name: string
  accommodationType: AccommodationType
  description?: string
  capacity?: number
  countByFamily: boolean  // ← nuevo
  isActive: boolean
  zoneId?: string | null
  sortOrder: number
}
```

### `frontend/src/components/camps/CampEditionAccommodationDialog.vue`

**Nuevas props opcionales:**
```typescript
defineProps<{
  visible: boolean
  editionId: string
  accommodation?: CampEditionAccommodation
  prefilledZoneId?: string | null      // ← nuevo: pre-asigna a zona
  prefilledType?: AccommodationType    // ← nuevo: bloquea tipo
}>()
```

**Campo nuevo en formulario:**
```
[ ] Ocupación por familia / unidad
    Activa cuando 1 tienda, caravana o autocaravana ocupa una plaza
    independientemente del número de personas.
```
Implementado como `ToggleSwitch` con etiqueta dinámica:
- `countByFamily = true` → "Por familia/unidad"
- `countByFamily = false` → "Por personas (usar capacidad numérica)"

**Default inteligente** al abrir en modo crear:
```typescript
countByFamily.value = prefilledType
  ? ['Tent', 'Caravan', 'Motorhome'].includes(prefilledType)
  : false
```

**Tipo bloqueado**: cuando se pasa `prefilledType`, el `Select` de tipo aparece deshabilitado.

**`zoneId`** se incluye en el body de create y update (ya en la lógica actual).

### `frontend/src/components/camps/CampEditionAccommodationsPanel.vue`

- Añadir indicador `countByFamily` en cada fila:
  ```html
  <Tag v-if="acc.countByFamily" value="Por unidad" severity="warn" class="text-xs" />
  <Tag v-else value="Por personas" severity="info" class="text-xs" />
  ```

### `frontend/src/components/camps/AccommodationZonePanel.vue`

**Añadir sub-panel "Unidades" por zona.**

Usar `useCampAccommodations(editionId)` (ya disponible vía prop o composable) filtrado client-side por `acc.zoneId === zone.id`.

**Estructura visual:**

```
DataTable de zonas (existente)
└── Expandible por zona (PrimeVue DataTable row expansion, o panel colapsable debajo de cada fila)
    └── [Sub-lista de unidades en esa zona]
        ├── Columnas: Nombre | Cap. | Ocupación | Características | Activo | Acciones
        ├── [+ Nueva unidad] button → abre CampEditionAccommodationDialog con prefilledZoneId + prefilledType
        └── Row actions: Editar | Activar/Desactivar | Características | Eliminar
```

**Implementación recomendada**: PrimeVue `DataTable` con `expandedRows` para mostrar el sub-panel de unidades al expandir una zona, evitando navegación adicional.

**Props nuevas de `AccommodationZonePanel`:**
```typescript
defineProps<{
  campEditionId: string
  accommodations: CampEditionAccommodation[]  // ya existe
  availableFeatures: AccommodationFeature[]   // ya existe
  editionId: string  // ← nuevo, necesario para useCampAccommodations dentro del panel
}>()
```

**Notas de implementación:**
- Las unidades de cada zona son el subconjunto de `accommodations` prop filtrado por `zoneId`
- No requiere llamada API adicional; usa los datos que el padre ya tiene
- El padre (`CampEditionDetailPage`) ya pasa `accommodations` como `[]` — debe cambiarse para pasar `accommodations` reales de `useCampAccommodations`

### `frontend/src/views/camps/CampEditionDetailPage.vue`

- Instanciar `useCampAccommodations(edition.value.id)` en el nivel de la página para compartir los alojamientos entre `CampEditionAccommodationsPanel` y `AccommodationZonePanel`
- Pasar `accommodations` (la lista real) a `AccommodationZonePanel` en lugar del `[]` actual
- Pasar `editionId` a `AccommodationZonePanel`

---

## Flujo de usuario completo

### Crear habitaciones en un albergue

1. Board accede a Edición → Tab "Alojamientos"
2. En el panel de Zonas, expande la zona "Edificio 1 – Planta alta"
3. Hace clic en "+ Nueva unidad"
4. Se abre el dialog con Tipo = "Lodge" bloqueado, Zona pre-asignada
5. Introduce: Nombre = "Habitación 101", Capacidad = 4, Ocupación = Por personas
6. Añade características: "Baño propio", "Accesible"
7. Guarda → la habitación aparece en el sub-panel de la zona

### Crear parcelas en zona de tiendas

1. Expande zona "Explanada junto piscina" (Tipo: Tent)
2. Clic "+ Nueva unidad"
3. Dialog con Tipo = "Tent" bloqueado, Ocupación = Por familia/unidad (default automático)
4. Introduce: Nombre = "Parcela T-12", Capacidad = 1
5. Características: "Punto de luz", "Baños a 100m", "Sombra"
6. Guarda

---

## Criterios de aceptación

- [ ] `CountByFamily` persiste en base de datos como campo propio (no derivado del tipo)
- [ ] `AccommodationAssignmentsRepository` usa `a.CountByFamily` del campo, no el set `ByFamilyTypes`
- [ ] `CampEditionAccommodationDialog` tiene toggle de ocupación visible en create y edit
- [ ] Al abrir el dialog desde la zona, el tipo está pre-rellenado y bloqueado
- [ ] Sub-panel de unidades en `AccommodationZonePanel` muestra las unidades filtradas por zona
- [ ] Crear una unidad desde el sub-panel la asocia automáticamente a la zona correcta
- [ ] `npx vue-tsc --noEmit` pasa sin errores
- [ ] `dotnet test` pasa los nuevos tests de unidad

## No incluido en este ticket

- Gestión de media (fotos de habitación) dentro del sub-panel de unidades — ya cubierto por `feat-accommodation-features` con `zoneId`/`accommodationId` en MediaItem
- Reordenación drag-and-drop de unidades dentro de una zona
