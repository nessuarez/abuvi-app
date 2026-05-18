# Encaje de Bolillos — UX Fixes 2

## Contexto

Mejoras visuales y de usabilidad sobre la pantalla de distribución de alojamientos (`AccommodationAssignmentView`), detectadas en revisión de uso real.

---

## Cambio 1: "Preferencia no cubierta" muestra UUIDs en lugar de nombres legibles

### Descripción

Al seleccionar una inscripción con características requeridas, el badge **"Preferencia no cubierta"** muestra los IDs en bruto:

> Preferencia no cubierta: 30f43d31-ea53-4904-9d2b-b75294bb6430, d815ccb3-6659-45b1-9542-0e8d4008302f

### Causa raíz

- `AssignmentFamilyResponse.requiredFeatures` es `string[]` de UUIDs.
- `AssignmentAccommodationResponse.availableFeatures` es `string[]` de UUIDs.
- `AccommodationSlotCard.vue` calcula `missingFeatures` como la diferencia, pero no tiene acceso a `ProposalAssignmentStateResponse.allFeatures` (`AccommodationFeatureSummary[]`) para resolver `id → name`.

### Solución

**`frontend/src/components/camps/AccommodationSlotCard.vue`**

1. Añadir prop `featureMap: Map<string, string>` (mapping `id → name`).
2. Crear computed `missingFeatureNames` que mapee cada ID de `missingFeatures` al nombre usando `featureMap`, con fallback al propio ID.
3. En la plantilla, mostrar `missingFeatureNames.join(', ')` en lugar de `missingFeatures.join(', ')`.

**`frontend/src/components/camps/AccommodationAssignmentPanel.vue`**

4. Construir `featureMap` computed desde `props.state.allFeatures`:
   ```ts
   const featureMap = computed(() => {
     const map = new Map<string, string>()
     ;(props.state.allFeatures ?? []).forEach((f) => map.set(f.id, f.name))
     return map
   })
   ```
5. Pasar `:feature-map="featureMap"` a cada `<AccommodationSlotCard />`.

### Resultado esperado

> Preferencia no cubierta: Acceso adaptado, Baño privado

---

## Cambio 2: Acortar el contador de ocupación en tarjetas de alojamiento

### Descripción

El contador "2 / 6 pers." ocupa espacio innecesario. Se abrevia a "2 / 6 p".

### Solución

**`frontend/src/components/camps/AccommodationSlotCard.vue`** — template, sección header (~líneas 120–127):

```html
<!-- Antes: -->
{{ accommodation.countByFamily ? 'fam.' : 'pers.' }}

<!-- Después: -->
{{ accommodation.countByFamily ? 'f.' : 'p.' }}
```

El texto informativo alternativo (cuando la familia no cabe) también se abrevia:

```html
<!-- Antes: -->
Necesitan {{ accommodation.countByFamily ? '1 plaza' : `${selectedFamily.memberCount} pers.` }},
quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }}

<!-- Después: -->
Necesitan {{ accommodation.countByFamily ? '1' : selectedFamily.memberCount }},
quedan {{ Math.max(0, (accommodation.capacity ?? 0) - occupiedUnits) }} p.
```

---

## Cambio 3: Filtro por rango de capacidad en la barra de filtros de alojamientos

### Descripción

Se añade un filtro de rango de capacidad (mínimo y máximo) en la barra de filtros del panel derecho, para que el usuario pueda ocultar alojamientos que no se adecúen al tamaño de las familias que está distribuyendo.

El rango va de **1 a 10+** (donde "10+" significa ≥ 10, sin límite superior).

### Solución

**`frontend/src/components/camps/AccommodationAssignmentPanel.vue`**

**Estado reactivo (sección `<script setup>`):**

```ts
const filterCapacityMin = ref<number | null>(null)
const filterCapacityMax = ref<number | null>(null)
```

**Opciones para los selects:**

```ts
const CAPACITY_OPTIONS = [
  { label: '1', value: 1 },
  { label: '2', value: 2 },
  { label: '3', value: 3 },
  { label: '4', value: 4 },
  { label: '5', value: 5 },
  { label: '6', value: 6 },
  { label: '7', value: 7 },
  { label: '8', value: 8 },
  { label: '9', value: 9 },
  { label: '10+', value: 10 },
]
```

**Lógica de filtrado en `groupedAccommodations`** — añadir tras los filtros existentes:

```ts
if (filterCapacityMin.value !== null) {
  const cap = acc.capacity ?? Infinity
  if (cap < filterCapacityMin.value) continue
}
if (filterCapacityMax.value !== null) {
  const cap = acc.capacity ?? Infinity
  // "10+" en el máximo significa sin límite superior efectivo (≥10)
  if (filterCapacityMax.value < 10 && cap > filterCapacityMax.value) continue
}
```

> Nota: alojamientos con `capacity === null` (ilimitados) pasan el filtro siempre.

**Template** — añadir en la fila "Zone + availability filters" (~línea 361), junto al Select de zona y el toggle de disponibilidad:

```html
<!-- Capacity range filter -->
<div class="flex items-center gap-1 text-xs text-gray-600">
  <span>Cap.</span>
  <Select
    v-model="filterCapacityMin"
    :options="CAPACITY_OPTIONS"
    option-label="label"
    option-value="value"
    placeholder="Mín"
    show-clear
    class="w-20"
    size="small"
  />
  <span>–</span>
  <Select
    v-model="filterCapacityMax"
    :options="CAPACITY_OPTIONS"
    option-label="label"
    option-value="value"
    placeholder="Máx"
    show-clear
    class="w-20"
    size="small"
  />
</div>
```

---

## Cambio 4: Icono de mascota — reemplazar corazón por etiqueta "Con mascotas"

### Descripción

El icono `pi-heart-fill` / `pi-heart` para mascota no es intuitivo. Se reemplaza por texto claro.

### Solución

**`frontend/src/components/camps/AccommodationAssignmentPanel.vue`** (panel de familia seleccionada, ~línea 288):

```html
<!-- Antes: -->
<span v-if="selectedFamily.hasPet" class="font-medium text-amber-600">
  <i class="pi pi-heart-fill mr-0.5" />Mascota
</span>

<!-- Después: -->
<span v-if="selectedFamily.hasPet" class="font-medium text-amber-600">
  Con mascotas
</span>
```

**`frontend/src/components/camps/FamilyAssignmentCard.vue`** (icono compacto en tarjeta de lista, ~línea 51):

```html
<!-- Antes: -->
<i
  v-if="family.hasPet"
  class="pi pi-heart text-xs text-amber-500"
  v-tooltip.top="'Viaja con mascota'"
  aria-label="Viaja con mascota"
/>

<!-- Después: -->
<span
  v-if="family.hasPet"
  class="rounded-full border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[10px] text-amber-700"
>
  Con mascotas
</span>
```

---

## Archivos afectados

| Archivo | Cambios |
| --- | --- |
| `frontend/src/components/camps/AccommodationSlotCard.vue` | Prop `featureMap`, nombres legibles en badge, abreviatura del contador |
| `frontend/src/components/camps/AccommodationAssignmentPanel.vue` | `featureMap`, filtro de capacidad (estado + lógica + template), texto "Con mascotas" |
| `frontend/src/components/camps/FamilyAssignmentCard.vue` | Chip "Con mascotas" en lugar de icono |

## Tests

- Badge "Preferencia no cubierta" muestra nombres legibles, no UUIDs.
- Contador de ocupación muestra "2 / 6 p." en lugar de "2 / 6 pers."
- El filtro de capacidad mínima oculta alojamientos con `capacity < min`.
- El filtro "10+" en máximo no oculta alojamientos con `capacity >= 10`.
- Alojamientos con `capacity === null` no son ocultados por el filtro de capacidad.
- Familias con mascota muestran "Con mascotas" en tarjeta de lista y en panel de detalle.

## No funcional

- Sin cambios en backend ni en tipos TypeScript.
- `featureMap` y `CAPACITY_OPTIONS` son constante/computed cacheados, sin impacto de rendimiento.
