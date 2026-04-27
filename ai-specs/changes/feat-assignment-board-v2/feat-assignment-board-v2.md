# Especificación técnica: Mejoras del tablero de asignación v2

## Resumen

Este ticket añade señales de compatibilidad enriquecidas al tablero de asignación, filtros en los paneles de familias y alojamientos, mejoras al servicio de auto-asignación y trazabilidad de última modificación en la barra de propuestas.

**Dependencias previas obligatorias:**

- **Ticket A (feat-accommodation-features):** Las entidades `CampEditionAccommodation` deben tener características (`features`/`tags`) requeridas por la familia.
- **Ticket B (familia amiga / etiquetado):** Las familias deben tener vínculos de amistad (`FriendlyFamilyLink`) y el campo `specialNeeds` del `Registration` debe estar activo.

Si los Tickets A y B no están implementados, esta especificación no puede desarrollarse. Se asume que ambos ya existen en `dev` cuando se abra la rama de este ticket.

---

## Contexto

### Flujo existente

El tablero actual (`AccommodationAssignmentPanel.vue` + `AccommodationSlotCard.vue`) ya:

- Muestra señales de borde según preferencia declarada por la familia (verde = 1ª elección, ámbar = 2ª/3ª).
- Marca en rojo cuando la capacidad está superada.
- Agrupa las tarjetas por tipo y zona.

### Lo que falta (objetivo de este ticket)

| Área | Problema actual | Mejora |
|------|-----------------|--------|
| Señales de compatibilidad | Solo preferencia declarada y capacidad | Añadir compatibilidad por características, familia amiga y disponibilidad real |
| Panel de familias | Sin filtros | Toggle "Solo con necesidades especiales" |
| Panel de alojamientos | Sin filtros | Por tipo, por zona, toggle "Solo disponibles" |
| Auto-asignación | Greedy sin bonificaciones por calidad | +5/característica cubierta, +15/familia amiga en mismo alojamiento, +10/familia amiga en misma zona |
| ProposalSelectorBar | Sin trazabilidad | Mostrar "Última modificación por {usuario}" |

---

## Dependencias del modelo de datos (Tickets A y B)

Este ticket **lee** —no escribe— los campos introducidos por los tickets anteriores. No requiere nuevas migraciones de base de datos.

### Campos de Ticket A que se consumen

En `CampEditionAccommodation` (tabla `camp_edition_accommodations`):

```csharp
// Suponer que Ticket A añadió algo como:
public ICollection<AccommodationFeature> Features { get; set; } = [];
```

En `Registration` (ya existe):

```csharp
public string? SpecialNeeds { get; set; }   // ya en el modelo base
```

Se asume que la familia puede declarar características requeridas. Este ticket asume que el campo equivalente en `Registration` o `FamilyUnit` se llama `RequiredAccommodationFeatures` (lista de IDs o etiquetas de características). Si el nombre real difiere en la implementación de Ticket A, ajustar en consecuencia.

### Campos de Ticket B que se consumen

Entidad nueva (Ticket B):

```csharp
// FriendlyFamilyLink (o similar)
public record FriendlyFamilyLink(Guid FamilyUnitIdA, Guid FamilyUnitIdB);
```

Se asume que el servicio puede resolver "¿qué familias son amigas de la familia X?" mediante una consulta al repositorio o una lista precargada en el estado de la propuesta.

---

## Cambios en la API / DTOs del backend

### 1. `AssignmentFamilyResponse` — añadir campos nuevos

**Archivo:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

**Cambio:** Ampliar el record existente con:

```csharp
public record AssignmentFamilyResponse(
    Guid RegistrationId,
    Guid FamilyUnitId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    int AdultCount,
    int ChildCount,
    bool HasPet,
    string? SpecialNeeds,
    string? CampatesPreference,
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences,
    // --- NUEVO ---
    bool HasSpecialNeeds,                                       // true si SpecialNeeds != null && != ""
    IReadOnlyList<string> RequiredFeatures,                     // IDs o slugs de características exigidas (de Ticket A)
    IReadOnlyList<Guid> FriendlyFamilyUnitIds                   // IDs de familias amigas (de Ticket B)
);
```

**Notas de implementación:**

- `HasSpecialNeeds` es derivado: `SpecialNeeds is { Length: > 0 }`. Calculado en el mapper, no almacenado.
- `RequiredFeatures` viene de la relación que establece Ticket A en `Registration` o `FamilyUnit`. Si no existe aún, devolver lista vacía.
- `FriendlyFamilyUnitIds` viene de Ticket B. Si no existe la relación, devolver lista vacía.
- El servicio `AccommodationAssignmentsService` (o el método `GetProposalAssignmentStateAsync`) debe enriquecer las familias con estos datos al construir el `ProposalAssignmentStateResponse`.

### 2. `AssignmentAccommodationResponse` — añadir características del alojamiento

**Archivo:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

**Cambio:** Ampliar el record existente:

```csharp
public record AssignmentAccommodationResponse(
    Guid Id,
    string Name,
    AccommodationType Type,
    int? Capacity,
    bool CountByFamily,
    Guid? ZoneId,
    string? ZoneName,
    int SortOrder,
    // --- NUEVO ---
    IReadOnlyList<string> AvailableFeatures   // IDs o slugs de características que tiene este alojamiento (de Ticket A)
);
```

**Notas de implementación:**

- `AvailableFeatures` se carga desde la relación `CampEditionAccommodation.Features` introducida por Ticket A.
- Si Ticket A no está mergeado, devolver lista vacía y el sistema seguirá funcionando (sin señales de características).

### 3. `AccommodationAssignmentProposalSummaryResponse` — añadir trazabilidad

**Archivo:** `src/Abuvi.API/Features/Camps/CampsModels.cs`

**Cambio:**

```csharp
public record AccommodationAssignmentProposalSummaryResponse(
    Guid Id,
    Guid CampEditionId,
    string Name,
    string? Notes,
    bool IsActive,
    int AssignmentCount,
    int UnassignedCount,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // --- NUEVO ---
    string? LastModifiedByUserName   // "Nombre Apellido" del último usuario que modificó la propuesta
);
```

**Notas de implementación:**

- `LastModifiedByUserName` se obtiene haciendo JOIN con la tabla `users` usando `AccommodationAssignment.AssignedByUserId` de la asignación más reciente, o bien llevando un campo `LastModifiedByUserId` en `AccommodationAssignmentProposal` (opción preferida: más simple y consistente).
- **Opción recomendada:** Añadir campo `LastModifiedByUserId Guid?` a `AccommodationAssignmentProposal` y actualizarlo en cada operación de escritura (assign, unassign, auto-assign). Hacer JOIN en la query de listado de propuestas para obtener `FirstName + " " + LastName`.
- No requiere migración si se añade `LastModifiedByUserId` como columna nullable en `accommodation_assignment_proposals`. Sí requiere una migración pequeña adicional de esta columna.

---

## Cambios en el backend — `AutoAssignService`

**Archivo:** `src/Abuvi.API/Features/Camps/AutoAssignService.cs`

### Algoritmo actual (de referencia)

El método `Compute` hace:

1. Ordena familias de mayor a menor tamaño.
2. Para cada familia sin asignar, intenta en orden de preferencia declarada.
3. Si ninguna preferencia tiene hueco, busca el alojamiento con menor capacidad restante que encaje (tightest fit).

### Cambio: sistema de puntuación

Sustituir la selección de fallback "tightest fit" por una función de **scoring** que también se aplique al ordenar las preferencias declaradas (respetando el orden de preferencia como criterio primario).

**Nueva firma del método de scoring:**

```csharp
private static int ScoreAccommodation(
    AssignmentAccommodationResponse acc,
    AssignmentFamilyResponse family,
    Dictionary<Guid, List<Guid>> occupancy,       // accId -> lista de registrationIds asignados
    Dictionary<Guid, int> sizeMap,                 // registrationId -> memberCount
    IReadOnlyDictionary<Guid, Guid> registrationToFamilyUnit,  // registrationId -> familyUnitId
    ProposalAssignmentStateResponse state)
```

**Reglas de puntuación (acumulativas):**

| Condición | Puntos |
|-----------|--------|
| Por cada característica requerida por la familia cubierta por el alojamiento | +5 |
| Una familia amiga ya asignada a **este mismo** alojamiento | +15 |
| Una familia amiga asignada a un alojamiento de **la misma zona** (distinto alojamiento) | +10 |
| Alojamiento es primera preferencia declarada | Usar como criterio primario (no como puntos); dentro del mismo nivel de preferencia, desempatar por score |

**Cambios concretos en `AutoAssignService.Compute`:**

```csharp
// Nuevo: construir mapa registrationId → familyUnitId para resolver familias amigas
var registrationToFamilyUnit = state.Families.ToDictionary(
    f => f.RegistrationId, f => f.FamilyUnitId);

// Nuevo: construir mapa accId → zoneId para señales de zona
var accToZone = state.Accommodations.ToDictionary(acc => acc.Id, acc => acc.ZoneId);

foreach (var family in unassigned)
{
    // Fase 1: preferencias declaradas (orden prioritario), desempate por score
    var preferenceAccommodations = family.AccommodationPreferences
        .OrderBy(p => p.PreferenceOrder)
        .Select(p => state.Accommodations.FirstOrDefault(acc => acc.Id == p.AccommodationId))
        .Where(acc => acc is not null && HasCapacity(acc!, occupancy[acc!.Id], family.MemberCount, sizeMap))
        .Cast<AssignmentAccommodationResponse>()
        .ToList();

    // Fase 2: fallback — todos los alojamientos con hueco, ordenados por score DESC
    var fallbackAccommodations = state.Accommodations
        .Where(acc => HasCapacity(acc, occupancy[acc.Id], family.MemberCount, sizeMap))
        .OrderByDescending(acc => ScoreAccommodation(acc, family, occupancy, sizeMap, registrationToFamilyUnit, state))
        .ThenBy(acc => GetRemainingCapacity(acc, occupancy[acc.Id], sizeMap))  // tightest fit como desempate final
        .ToList();

    var target = preferenceAccommodations.FirstOrDefault() ?? fallbackAccommodations.FirstOrDefault();
    // ... asignar igual que antes
}
```

**Implementación de `ScoreAccommodation`:**

```csharp
private static int ScoreAccommodation(
    AssignmentAccommodationResponse acc,
    AssignmentFamilyResponse family,
    Dictionary<Guid, List<Guid>> occupancy,
    Dictionary<Guid, int> sizeMap,
    IReadOnlyDictionary<Guid, Guid> registrationToFamilyUnit,
    ProposalAssignmentStateResponse state)
{
    var score = 0;

    // +5 por cada característica requerida cubierta
    var coveredFeatures = family.RequiredFeatures
        .Count(req => acc.AvailableFeatures.Contains(req));
    score += coveredFeatures * 5;

    // Familia amiga ya asignada a este alojamiento (+15) o misma zona (+10)
    var assignedRegIds = occupancy[acc.Id];
    foreach (var assignedRegId in assignedRegIds)
    {
        if (!registrationToFamilyUnit.TryGetValue(assignedRegId, out var assignedFamilyUnitId))
            continue;
        if (!family.FriendlyFamilyUnitIds.Contains(assignedFamilyUnitId)) continue;

        // Familia amiga en mismo alojamiento
        score += 15;
    }

    // Familia amiga en misma zona (otro alojamiento de la misma zona)
    if (acc.ZoneId.HasValue)
    {
        var sameZoneAccIds = state.Accommodations
            .Where(a => a.ZoneId == acc.ZoneId && a.Id != acc.Id)
            .Select(a => a.Id)
            .ToHashSet();

        foreach (var sameZoneAccId in sameZoneAccIds)
        {
            if (!occupancy.TryGetValue(sameZoneAccId, out var sameZoneRegIds)) continue;
            foreach (var regId in sameZoneRegIds)
            {
                if (!registrationToFamilyUnit.TryGetValue(regId, out var fuId)) continue;
                if (!family.FriendlyFamilyUnitIds.Contains(fuId)) continue;
                score += 10;
                break; // una sola bonificación por zona, independientemente de cuántas familias amigas haya
            }
        }
    }

    return score;
}
```

**Tests unitarios a añadir en `AutoAssignServiceTests.cs`:**

```
ScoreAccommodation_AllFeaturesMatch_Returns5PerFeature
ScoreAccommodation_FriendlyFamilyInSameAccommodation_Returns15
ScoreAccommodation_FriendlyFamilyInSameZone_Returns10
ScoreAccommodation_NoMatchingFeatures_Returns0
Compute_PrefersAccommodationWithFriendlyFamily
Compute_PrefersAccommodationCoveringRequiredFeatures
```

---

## Cambios en el frontend

### 1. `AccommodationSlotCard.vue` — señales de compatibilidad enriquecidas

**Archivo:** `frontend/src/components/camps/AccommodationSlotCard.vue`

#### Nuevas props (ningún cambio de firma existente — solo adición)

No se cambia la interfaz `AccommodationAssignmentResponse`. Los nuevos campos llegan ya en el objeto `selectedFamily` y `accommodation` como resultado de la ampliación del DTO descrita arriba.

#### Nuevos computed

Añadir los siguientes computed **por encima** del `signalClass` existente:

```typescript
// Todas las características requeridas por la familia están cubiertas por este alojamiento
const allFeaturesMatch = computed(() => {
  if (!props.selectedFamily) return false
  const required = props.selectedFamily.requiredFeatures
  if (required.length === 0) return false
  return required.every((feat) => props.accommodation.availableFeatures.includes(feat))
})

// Features requeridas que NO están cubiertas
const missingFeatures = computed(() => {
  if (!props.selectedFamily) return []
  return props.selectedFamily.requiredFeatures.filter(
    (feat) => !props.accommodation.availableFeatures.includes(feat)
  )
})

// Una familia amiga ya está asignada a ESTE alojamiento
const hasFriendlyFamilyHere = computed(() => {
  if (!props.selectedFamily || props.selectedFamily.friendlyFamilyUnitIds.length === 0) return false
  return props.assignedFamilies.some((f) =>
    props.selectedFamily!.friendlyFamilyUnitIds.includes(f.familyUnitId)
  )
})

// Una familia amiga está en OTRO alojamiento de LA MISMA ZONA (prop nueva necesaria)
// El padre (AccommodationAssignmentPanel) debe calcular y pasar este bool
// Ver sección de AccommodationAssignmentPanel más abajo
```

**Nueva prop `hasFriendlyFamilyInZone`:**

```typescript
defineProps<{
  accommodation: AssignmentAccommodationResponse
  assignedFamilies: AssignmentFamilyResponse[]
  selectedFamily: AssignmentFamilyResponse | null
  hasFriendlyFamilyInZone: boolean   // NUEVO — calculado por el padre
}>()
```

#### `signalClass` — reescribir para incorporar todas las señales

Reemplazar el computed `signalClass` completo:

```typescript
const signalClass = computed(() => {
  if (!props.selectedFamily) return 'border-gray-200'

  // Rojo: no caben (capacidad insuficiente)
  const needed = props.accommodation.countByFamily ? 1 : props.selectedFamily.memberCount
  const remaining = props.accommodation.capacity === null
    ? Infinity
    : props.accommodation.capacity - occupiedUnits.value
  if (remaining < needed) return 'border-red-500 ring-1 ring-red-400'

  // Verde: preferencia 1ª O cumple todas las características O familia amiga ya aquí
  const prefs = props.selectedFamily.accommodationPreferences
  const pref = prefs.find((p) => p.accommodationId === props.accommodation.id)
  if (pref?.preferenceOrder === 1 || allFeaturesMatch.value || hasFriendlyFamilyHere.value) {
    return 'border-green-400 ring-1 ring-green-300'
  }

  // Azul: familia amiga en misma zona
  if (props.hasFriendlyFamilyInZone) return 'border-blue-400 ring-1 ring-blue-300'

  // Ámbar: preferencia 2ª/3ª O falta alguna característica requerida
  if (pref?.preferenceOrder === 2 || pref?.preferenceOrder === 3 || missingFeatures.value.length > 0) {
    return 'border-amber-400 ring-1 ring-amber-300'
  }

  return 'border-blue-200'
})
```

**Prioridad de señales (de mayor a menor):**

1. Rojo (no caben)
2. Verde (1ª preferencia, o cumple todas las características, o familia amiga ya aquí)
3. Azul (familia amiga en misma zona)
4. Ámbar (2ª/3ª preferencia, o falta alguna característica)
5. Azul claro (sin señal especial, pero hay familia seleccionada)

#### Nuevos badges de señal en el template

Dentro del `<div>` del header de la tarjeta (después del contador de ocupación), añadir una sección de badges informativos condicionales. Estos badges son visibles solo cuando hay una familia seleccionada:

```html
<!-- Badges de compatibilidad (solo cuando hay familia seleccionada) -->
<div v-if="selectedFamily" class="mt-1 flex flex-wrap gap-1">
  <!-- Verde: Cumple todas las preferencias -->
  <span
    v-if="allFeaturesMatch"
    class="rounded bg-green-100 px-1.5 py-0.5 text-xs text-green-700"
    title="El alojamiento tiene todas las características requeridas"
  >
    Cumple todas las preferencias
  </span>

  <!-- Verde: Familia amiga ya aquí -->
  <span
    v-if="hasFriendlyFamilyHere"
    class="rounded bg-green-100 px-1.5 py-0.5 text-xs text-green-700"
  >
    Familia amiga ya aquí
  </span>

  <!-- Azul: Familia amiga en misma zona -->
  <span
    v-if="hasFriendlyFamilyInZone && !hasFriendlyFamilyHere"
    class="rounded bg-blue-100 px-1.5 py-0.5 text-xs text-blue-700"
  >
    Familia amiga en misma zona
  </span>

  <!-- Ámbar: Falta alguna característica -->
  <span
    v-if="missingFeatures.length > 0"
    class="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-700"
    :title="`Faltan: ${missingFeatures.join(', ')}`"
  >
    Preferencia no cubierta: {{ missingFeatures.join(', ') }}
  </span>
</div>
```

#### Mensaje mejorado para "no caben"

En el contador de ocupación (esquina superior derecha de la tarjeta), mejorar el mensaje de capacidad insuficiente:

```html
<!-- Reemplazar el span de conteo existente -->
<span
  class="text-xs"
  :class="isOverCapacity ? 'font-bold text-red-600' : canFitSelectedFamily ? 'text-gray-500' : 'font-medium text-red-500'"
>
  <!-- Caso normal: X / Y pers. o fam. -->
  <template v-if="!selectedFamily || canFitSelectedFamily || isOverCapacity">
    {{ occupiedUnits }} / {{ accommodation.capacity ?? '∞' }}
    {{ accommodation.countByFamily ? 'fam.' : 'pers.' }}
  </template>
  <!-- Caso "no caben": Necesitan X, quedan Y plazas -->
  <template v-else>
    Necesitan {{ accommodation.countByFamily ? '1 plaza' : `${selectedFamily.memberCount} pers.` }},
    quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }}
  </template>
</span>
```

**Nuevo computed `canFitSelectedFamily`:**

```typescript
const canFitSelectedFamily = computed(() => {
  if (!props.selectedFamily || props.accommodation.capacity === null) return true
  const needed = props.accommodation.countByFamily ? 1 : props.selectedFamily.memberCount
  return (props.accommodation.capacity - occupiedUnits.value) >= needed
})
```

---

### 2. `AccommodationAssignmentPanel.vue` — cálculo de `hasFriendlyFamilyInZone` y nuevos filtros

**Archivo:** `frontend/src/components/camps/AccommodationAssignmentPanel.vue`

#### Nuevo computed: `friendlyFamilyInZoneMap`

El panel debe calcular, para cada `accommodationId`, si hay una familia amiga del `selectedFamily` asignada en algún alojamiento de la misma zona:

```typescript
// Mapa: accommodationId → boolean (¿hay familia amiga en la misma zona?)
const friendlyFamilyInZoneMap = computed((): Map<string, boolean> => {
  const map = new Map<string, boolean>()
  if (!selectedFamily.value || selectedFamily.value.friendlyFamilyUnitIds.length === 0) {
    props.state.accommodations.forEach((acc) => map.set(acc.id, false))
    return map
  }

  // Agrupar accommodations por zona
  const zoneToAccIds = new Map<string | null, string[]>()
  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    if (!zoneToAccIds.has(zoneKey)) zoneToAccIds.set(zoneKey, [])
    zoneToAccIds.get(zoneKey)!.push(acc.id)
  }

  // Para cada accommodation, ver si alguna familia amiga está en la misma zona (pero no en este mismo acc)
  for (const acc of props.state.accommodations) {
    const zoneKey = acc.zoneId ?? null
    const sameZoneAccIds = (zoneToAccIds.get(zoneKey) ?? []).filter((id) => id !== acc.id)

    const hasFriendlyInZone = sameZoneAccIds.some((sameZoneAccId) => {
      const familiesHere = assignedFamiliesFor(sameZoneAccId)
      return familiesHere.some((f) =>
        selectedFamily.value!.friendlyFamilyUnitIds.includes(f.familyUnitId)
      )
    })

    map.set(acc.id, hasFriendlyInZone)
  }
  return map
})
```

#### Pasar la prop `hasFriendlyFamilyInZone` a cada `AccommodationSlotCard`

```html
<AccommodationSlotCard
  v-for="acc in accommodations"
  :key="acc.id"
  :accommodation="acc"
  :assigned-families="assignedFamiliesFor(acc.id)"
  :selected-family="selectedFamily"
  :has-friendly-family-in-zone="friendlyFamilyInZoneMap.get(acc.id) ?? false"
  @assign="handleAssign"
  @unassign="$emit('unassign', $event)"
/>
```

#### Nuevos filtros en el panel de familias (izquierda)

Añadir debajo del `IconField` de búsqueda:

```typescript
// Nuevo estado de filtro
const filterSpecialNeeds = ref(false)
```

```html
<!-- Toggle: Solo con necesidades especiales -->
<div class="mt-2 flex items-center gap-2">
  <ToggleSwitch v-model="filterSpecialNeeds" input-id="filter-special-needs" size="small" />
  <label for="filter-special-needs" class="cursor-pointer text-xs text-gray-600">
    Solo con necesidades especiales
  </label>
</div>
```

**Actualizar `filteredFamilies` computed:**

```typescript
const filteredFamilies = computed(() => {
  const q = searchQuery.value.toLowerCase()
  return sortedFamilies.value.filter((f) => {
    const matchesSearch =
      !q ||
      f.familyName.toLowerCase().includes(q) ||
      f.representativeName.toLowerCase().includes(q)
    const matchesSpecialNeeds = !filterSpecialNeeds.value || f.hasSpecialNeeds
    return matchesSearch && matchesSpecialNeeds
  })
})
```

#### Nuevos filtros en el panel de alojamientos (derecha)

Añadir una barra de filtros encima del grid de alojamientos:

```typescript
// Nuevos estados de filtro
const filterType = ref<string | null>(null)          // AccommodationTypeValue o null (todos)
const filterZone = ref<string | null>(null)           // zoneName o null (todas)
const filterOnlyAvailable = ref(false)
```

```html
<!-- Barra de filtros de alojamientos -->
<div class="mb-4 flex flex-wrap items-center gap-2">
  <!-- Filtro por tipo -->
  <Select
    v-model="filterType"
    :options="availableTypeOptions"
    option-label="label"
    option-value="value"
    placeholder="Todos los tipos"
    show-clear
    class="w-44"
    size="small"
  />

  <!-- Filtro por zona -->
  <Select
    v-model="filterZone"
    :options="availableZoneOptions"
    option-label="label"
    option-value="value"
    placeholder="Todas las zonas"
    show-clear
    class="w-44"
    size="small"
  />

  <!-- Toggle: Solo disponibles -->
  <div class="flex items-center gap-1.5">
    <ToggleSwitch v-model="filterOnlyAvailable" input-id="filter-available" size="small" />
    <label for="filter-available" class="cursor-pointer text-xs text-gray-600">
      Solo disponibles
    </label>
  </div>
</div>
```

**Computed para las opciones de filtro:**

```typescript
const availableTypeOptions = computed(() => {
  const types = [...new Set(props.state.accommodations.map((a) => a.type))]
  return types.map((t) => ({ label: ACCOMMODATION_TYPE_LABELS[t as AccommodationTypeValue], value: t }))
})

const availableZoneOptions = computed(() => {
  const zones = [...new Set(
    props.state.accommodations.map((a) => a.zoneName).filter((z): z is string => z !== null)
  )]
  return zones.map((z) => ({ label: z, value: z }))
})
```

**Actualizar `groupedAccommodations` para aplicar los filtros:**

```typescript
const groupedAccommodations = computed((): Map<string, Map<string, AssignmentAccommodationResponse[]>> => {
  const byType = new Map<string, Map<string, AssignmentAccommodationResponse[]>>()
  const sorted = [...props.state.accommodations].sort((a, b) => a.sortOrder - b.sortOrder)

  for (const acc of sorted) {
    // Aplicar filtro por tipo
    if (filterType.value && acc.type !== filterType.value) continue

    // Aplicar filtro por zona
    if (filterZone.value && acc.zoneName !== filterZone.value) continue

    // Aplicar filtro "Solo disponibles"
    if (filterOnlyAvailable.value) {
      const families = assignedFamiliesFor(acc.id)
      const used = acc.countByFamily
        ? families.length
        : families.reduce((sum, f) => sum + f.memberCount, 0)
      if (acc.capacity !== null && used >= acc.capacity) continue
    }

    if (!byType.has(acc.type)) byType.set(acc.type, new Map())
    const byZone = byType.get(acc.type)!
    const zoneKey = acc.zoneName ?? 'Sin zona'
    if (!byZone.has(zoneKey)) byZone.set(zoneKey, [])
    byZone.get(zoneKey)!.push(acc)
  }
  return byType
})
```

**Importaciones adicionales necesarias en `AccommodationAssignmentPanel.vue`:**

```typescript
import Select from 'primevue/select'
import ToggleSwitch from 'primevue/toggleswitch'
```

---

### 3. `ProposalSelectorBar.vue` — última modificación por usuario

**Archivo:** `frontend/src/components/camps/ProposalSelectorBar.vue`

#### Cambio de prop

La prop `proposals` ya contiene objetos `AccommodationAssignmentProposalSummaryResponse`. El nuevo campo `lastModifiedByUserName` (nullable `string | null`) llega ya en esos objetos gracias al cambio del DTO del backend.

#### Cambio en el tipo TypeScript

**Archivo:** `frontend/src/types/accommodation-assignment.ts`

```typescript
export interface AccommodationAssignmentProposalSummaryResponse {
  id: string
  campEditionId: string
  name: string
  notes: string | null
  isActive: boolean
  assignmentCount: number
  unassignedCount: number
  createdByUserId: string
  createdAt: string
  updatedAt: string
  lastModifiedByUserName: string | null   // NUEVO
}
```

#### Cambio en el template

Añadir el texto de trazabilidad en la barra, a la derecha de las estadísticas:

```html
<!-- Stats: X sin asignar (ya existe) -->
<span v-if="selectedProposal" class="ml-auto flex flex-col items-end text-right">
  <span class="text-sm text-gray-500">
    {{ selectedProposal.unassignedCount }} sin asignar · {{ selectedProposal.assignmentCount }} asignadas
  </span>
  <!-- NUEVO: última modificación -->
  <span
    v-if="selectedProposal.lastModifiedByUserName"
    class="text-xs text-gray-400"
  >
    Última modificación por {{ selectedProposal.lastModifiedByUserName }}
  </span>
</span>
```

---

## Cambios en los tipos TypeScript

**Archivo:** `frontend/src/types/accommodation-assignment.ts`

Resumen de todos los cambios en las interfaces:

```typescript
// Ampliar AssignmentFamilyResponse
export interface AssignmentFamilyResponse {
  registrationId: string
  familyUnitId: string
  familyName: string
  representativeName: string
  memberCount: number
  adultCount: number
  childCount: number
  hasPet: boolean
  specialNeeds: string | null
  campatesPreference: string | null
  accommodationPreferences: AccommodationPreferenceItem[]
  // NUEVO:
  hasSpecialNeeds: boolean
  requiredFeatures: string[]
  friendlyFamilyUnitIds: string[]
}

// Ampliar AssignmentAccommodationResponse
export interface AssignmentAccommodationResponse {
  id: string
  name: string
  type: AccommodationTypeValue
  capacity: number | null
  countByFamily: boolean
  zoneId: string | null
  zoneName: string | null
  sortOrder: number
  // NUEVO:
  availableFeatures: string[]
}

// Ampliar AccommodationAssignmentProposalSummaryResponse (ver sección ProposalSelectorBar)
```

---

## Tests unitarios frontend

**Archivo:** `frontend/src/components/camps/__tests__/AccommodationSlotCard.test.ts`

Añadir los siguientes casos de test a los ya existentes:

```
showsGreenBadge_whenAllFeaturesMatch
showsGreenBadge_whenFriendlyFamilyIsAlreadyAssignedHere
showsBlueBadge_whenFriendlyFamilyIsInSameZone_butNotSameAccommodation
showsAmberBadge_whenSomeRequiredFeaturesAreMissing
showsMissingFeaturesList_inAmberBadge
showsImprovedCapacityMessage_whenFamilyDoesNotFit
signalClass_isGreen_whenAllFeaturesMatchEvenWithNoPreference
signalClass_priority_redBeatsGreen
```

**Archivo:** `frontend/src/composables/__tests__/useAccommodationAssignment.test.ts`

No requiere nuevos tests de composable (los nuevos campos son solo datos; la lógica nueva está en los componentes).

---

## Tests unitarios backend

**Archivo:** `src/Abuvi.Tests/Unit/Features/Camps/AutoAssignServiceTests.cs`

Añadir (además de los ya listados en la sección de `AutoAssignService`):

```
ScoreAccommodation_MultipleFeatures_AccumulatesPoints
ScoreAccommodation_MultipleFriendlyFamiliesInSameAcc_OnlyCountsOnce  // o decide si acumula
ScoreAccommodation_FriendlyFamilyInZone_NoBonusIfAlreadyInSameAcc
Compute_WithFeaturesAndFriendlyFamilies_SelectsHighestScoredFallback
```

---

## Orden de implementación recomendado

1. **Backend: DTOs** — Ampliar `AssignmentFamilyResponse` y `AssignmentAccommodationResponse` con los campos nuevos (con valores vacíos si Tickets A/B no están listos).
2. **Backend: LastModifiedByUserId** — Añadir columna nullable a `AccommodationAssignmentProposal`, migration, y populate en cada operación de escritura.
3. **Backend: ProposalSummaryResponse** — Incluir `LastModifiedByUserName` en el query de listado.
4. **Backend: AutoAssignService** — Añadir `ScoreAccommodation` y modificar el fallback selection. Añadir tests.
5. **Frontend: tipos** — Actualizar `accommodation-assignment.ts`.
6. **Frontend: AccommodationSlotCard** — Añadir prop `hasFriendlyFamilyInZone`, nuevos computed, badges, mensaje de capacidad mejorado.
7. **Frontend: AccommodationAssignmentPanel** — Añadir `friendlyFamilyInZoneMap`, filtros de familias y alojamientos.
8. **Frontend: ProposalSelectorBar** — Mostrar `lastModifiedByUserName`.
9. **Tests** — Frontend y backend.

---

## Checklist de aceptación

### Señales de compatibilidad

- [ ] Verde "Cumple todas las preferencias" aparece cuando el alojamiento tiene TODAS las características requeridas por la familia seleccionada.
- [ ] Verde "Familia amiga ya aquí" aparece cuando una familia amiga vinculada está asignada a ese alojamiento.
- [ ] Azul "Familia amiga en misma zona" aparece cuando hay familia amiga en otro alojamiento de la misma zona (y no en este mismo).
- [ ] Ámbar "Preferencia no cubierta: {lista}" muestra las características que faltan.
- [ ] Rojo cambia el texto a "Necesitan X, quedan Y plazas" en vez de solo mostrar el contador.
- [ ] Las señales son exclusivamente visibles cuando hay una familia seleccionada en el panel izquierdo.
- [ ] La prioridad de señales (rojo > verde > azul > ámbar) se respeta.

### Filtros

- [ ] Toggle "Solo con necesidades especiales" filtra el panel de familias (izquierda).
- [ ] Select "Tipo" filtra el grid de alojamientos (derecha), mostrando solo el tipo seleccionado.
- [ ] Select "Zona" filtra el grid de alojamientos (derecha), mostrando solo esa zona.
- [ ] Toggle "Solo disponibles" oculta los alojamientos llenos.
- [ ] Los filtros son combinables (tipo + zona + disponibles simultáneamente).
- [ ] Limpiar un select (show-clear) vuelve a mostrar todos.

### Auto-asignación

- [ ] Con Tickets A/B activos: familias con características requeridas son colocadas preferentemente en alojamientos que las tienen.
- [ ] Con Tickets A/B activos: familias amigas tienden a quedar en el mismo alojamiento o zona.
- [ ] Sin Tickets A/B (listas vacías): el comportamiento es idéntico al algoritmo previo.
- [ ] Los nuevos tests de `AutoAssignServiceTests` pasan.

### Colaboración

- [ ] `ProposalSelectorBar` muestra "Última modificación por {nombre apellido}" debajo de las estadísticas.
- [ ] Si nunca se ha modificado (campo nulo), no se muestra el texto.
- [ ] El campo se actualiza al realizar assign, unassign y auto-assign.

---

## Notas técnicas

- **No hay nuevas migraciones de modelo de negocio:** La única migración posible es añadir `last_modified_by_user_id` a `accommodation_assignment_proposals`. Los campos de características y familias amigas vienen de los Tickets A y B.
- **Retrocompatibilidad:** Los nuevos campos en los DTOs son listas vacías por defecto. El tablero funciona igual si Tickets A y B no están desplegados todavía.
- **Rendimiento:** Los `friendlyFamilyUnitIds` y `requiredFeatures` se cargan una sola vez en `ProposalAssignmentStateResponse`. No hay llamadas adicionales al backend durante la interacción.
- **ToggleSwitch:** Usar `primevue/toggleswitch` (ya disponible en el proyecto). No añadir nuevas dependencias npm.
- **Nombres en inglés en código:** Todo el código (variables, métodos, clases, comentarios) en inglés, conforme a `base-standards.mdc`.
- **Rama:** Crear `feature/feat-assignment-board-v2` desde `dev`. PR hacia `dev`, no hacia `main`.
