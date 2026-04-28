# Ticket D — Listados de salida de asignación

## Resumen

Nueva pestaña **"Listados"** en la vista `AccommodationAssignmentView` que permite a la Junta Directiva generar dos vistas impresas/exportables de la propuesta de asignación activa:

1. **Listado por alojamiento** — agrupado por zona y tipo; muestra las familias asignadas a cada alojamiento.
2. **Listado por familia** — lista alfabética de todas las familias inscritas con su alojamiento asignado.

Cada listado incluye un botón **"Exportar CSV"** que genera el fichero directamente en el navegador (sin nuevo endpoint). Además, el `TODO: Export to CSV/Excel` existente en `AccommodationReportsPanel.vue` se resuelve añadiendo el mismo mecanismo de exportación a los informes de ocupación ya existentes.

---

## Contexto del sistema actual

### Vista y pestañas existentes

`AccommodationAssignmentView.vue` ya tiene tres pestañas: **Asignar** / **Resumen** / **Informes**. Esta tarea añade una cuarta pestaña **Listados** con el valor `"listings"`.

### Endpoints existentes (reutilizados)

| Endpoint | Uso en Ticket D |
|---|---|
| `GET /camps/editions/{editionId}/assignment-proposals/{proposalId}/reports/by-type` | Datos ya disponibles pero orientados a ocupación. **No se reutiliza** para el listado de salida. |
| `GET /camps/editions/{editionId}/assignment-proposals/{proposalId}/reports/by-zone` | Ídem. |
| `GET /camps/editions/{editionId}/assignment-proposals/{proposalId}/reports/unassigned` | Devuelve `AssignmentFamilyResponse[]`. Se reutiliza para el listado por familia (familias sin asignar). |

El estado completo de asignación (`ProposalAssignmentStateResponse`) ya está cargado en el composable `useAccommodationAssignment` con `assignments`, `accommodations` y `families`. El listado por familia puede derivarse de estos datos en el frontend sin llamada adicional.

### Modelos C# relevantes ya definidos

```csharp
// CampsModels.cs — records existentes
public record AssignmentReportFamilyRow(
    Guid RegistrationId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    string? AccommodationName,
    string? ZoneName
);

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
    IReadOnlyList<AccommodationPreferenceItem> AccommodationPreferences
);
```

`CampEditionAccommodation` dispone de `Name`, `AccommodationType`, `Description`, `Capacity`, `ZoneId` y navegación a `Zone`. No tiene campo "notas internas" propio; se usa `Description` para ese fin en el listado.

---

## Nuevo endpoint backend

### `GET /api/camps/editions/{campEditionId}/assignment-proposals/{proposalId}/reports/by-accommodation`

Devuelve los alojamientos de la edición agrupados por zona, cada uno con las familias asignadas en esa propuesta. Los alojamientos sin asignaciones se incluyen igualmente (vacíos). Permite renderizar el listado de llegada al campamento.

**Autorización:** Admin o Board.

**Parámetros de ruta:**

| Parámetro | Tipo | Descripción |
|---|---|---|
| `campEditionId` | `Guid` | Identificador de la edición del campamento |
| `proposalId` | `Guid` | Identificador de la propuesta de asignación |

**Respuesta exitosa (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "zoneId": "...",
      "zoneName": "Edificio Arcs",
      "accommodationType": "Lodge",
      "accommodations": [
        {
          "accommodationId": "...",
          "accommodationName": "Habitación 101",
          "description": "Orientación norte, cama individual + litera",
          "capacity": 4,
          "assignedFamilies": [
            {
              "registrationId": "...",
              "familyName": "García Fernández",
              "representativeName": "Juan García",
              "memberCount": 3,
              "internalNotes": null
            }
          ]
        },
        {
          "accommodationId": "...",
          "accommodationName": "Habitación 102",
          "description": null,
          "capacity": 2,
          "assignedFamilies": []
        }
      ]
    },
    {
      "zoneId": null,
      "zoneName": "Sin zona",
      "accommodationType": "Caravan",
      "accommodations": [...]
    }
  ]
}
```

**Errores:**

- `404 Not Found` — la propuesta no pertenece a la edición indicada (`NOT_FOUND`).

#### DTO C# nuevos

```csharp
// Añadir a CampsModels.cs

public record AssignedFamilySlimResponse(
    Guid RegistrationId,
    string FamilyName,
    string RepresentativeName,
    int MemberCount,
    string? InternalNotes        // descripción de la relación familia↔alojamiento; null por ahora
);

public record AccommodationWithAssignmentsResponse(
    Guid AccommodationId,
    string AccommodationName,
    string? Description,
    int? Capacity,
    IReadOnlyList<AssignedFamilySlimResponse> AssignedFamilies
);

public record AccommodationZoneListingResponse(
    Guid? ZoneId,
    string ZoneName,
    string AccommodationType,       // valor del enum como string
    IReadOnlyList<AccommodationWithAssignmentsResponse> Accommodations
);
```

#### Implementación en `AccommodationAssignmentReportsService`

Añadir método `GetByAccommodationAsync`:

```csharp
public async Task<List<AccommodationZoneListingResponse>> GetByAccommodationAsync(
    Guid campEditionId,
    Guid proposalId,
    CancellationToken ct = default)
{
    await EnsureProposalBelongsToEditionAsync(proposalId, campEditionId, ct);

    // 1. Cargar todos los alojamientos activos de la edición (con zona)
    // 2. Cargar las asignaciones de la propuesta con registrations y family units
    // 3. Para cada alojamiento, proyectar las familias asignadas
    // 4. Agrupar por zona (null → "Sin zona"), ordenar por sortOrder
    // 5. Retornar lista de AccommodationZoneListingResponse
}
```

Registrar el endpoint en `CampsEndpoints.cs` dentro del grupo `reportsGroup` existente:

```csharp
reportsGroup.MapGet("/by-accommodation", GetReportByAccommodation)
    .WithName("GetAccommodationAssignmentReportByAccommodation")
    .WithSummary("Detailed listing per accommodation with assigned families")
    .Produces<ApiResponse<List<AccommodationZoneListingResponse>>>()
    .Produces(StatusCodes.Status404NotFound);
```

> **Nota de diseño:** `InternalNotes` en `AssignedFamilySlimResponse` queda `null` por ahora. En el futuro podría provenir de un campo `AssignmentNotes` en `AccommodationAssignment`. No se añade ese campo en este ticket (out of scope).

---

## Frontend — componentes a crear

### Árbol de ficheros nuevos

```
frontend/src/
  components/camps/
    AssignmentListingsPanel.vue          ← panel raíz de la pestaña "Listados"
    AssignmentListingByAccommodation.vue ← sub-vista: listado por alojamiento
    AssignmentListingByFamily.vue        ← sub-vista: listado por familia
  utils/
    csv-export.ts                        ← utilidad genérica de exportación CSV
```

---

### `csv-export.ts`

Utilidad cliente ligera sin dependencias externas. Genera un fichero `.csv` y lanza la descarga.

```typescript
// frontend/src/utils/csv-export.ts

/**
 * Exports a list of plain objects to a UTF-8 CSV file and triggers a browser download.
 *
 * @param headers  Column headers in display order (Spanish labels).
 * @param rows     Data rows. Each value is coerced to string; undefined/null → empty string.
 * @param filename Target filename (without extension).
 */
export function exportToCsv(
  headers: string[],
  rows: (string | number | boolean | null | undefined)[][],
  filename: string
): void {
  const escape = (value: string | number | boolean | null | undefined): string => {
    const str = value == null ? '' : String(value)
    // Wrap in double-quotes if the value contains commas, newlines or double-quotes
    if (str.includes(',') || str.includes('\n') || str.includes('"')) {
      return `"${str.replace(/"/g, '""')}"`
    }
    return str
  }

  const csvLines = [
    headers.map(escape).join(','),
    ...rows.map(row => row.map(escape).join(','))
  ]

  const blob = new Blob(['﻿' + csvLines.join('\r\n')], {
    type: 'text/csv;charset=utf-8;'
  })

  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${filename}.csv`
  link.click()
  URL.revokeObjectURL(url)
}
```

> El BOM (`﻿`) garantiza que Excel (Windows) interpreta el fichero como UTF-8.

---

### `AssignmentListingsPanel.vue`

Panel contenedor de la pestaña "Listados". Carga el nuevo endpoint `/by-accommodation` y reutiliza `assignmentState` del composable padre (pasado como prop) para construir el listado por familia. Ofrece dos sub-pestañas internas: **Por alojamiento** / **Por familia**.

**Props:**

```typescript
interface Props {
  proposalId: string
  campEditionId: string
  // Estado completo ya cargado en la vista padre — evita doble fetch
  assignmentState: ProposalAssignmentStateResponse | null
}
```

**Responsabilidades:**

- Llama a `GET /by-accommodation` al montar (o cuando cambie `proposalId`).
- Deriva el listado por familia a partir de `assignmentState.families`, `assignmentState.assignments` y `assignmentState.accommodations` (sin llamada adicional al servidor).
- Pasa datos a los dos sub-componentes.
- Muestra `ProgressSpinner` mientras carga; `Message` severity="error" en caso de fallo.

**Estado interno:**

```typescript
const byAccommodationData = ref<AccommodationZoneListingResponse[]>([])
const loading = ref(false)
const error = ref<string | null>(null)
const activeSubTab = ref<'by-accommodation' | 'by-family'>('by-accommodation')
```

**Template:** Tabs PrimeVue con dos TabPanel.

---

### `AssignmentListingByAccommodation.vue`

Muestra el listado de salida agrupado por zona.

**Props:**

```typescript
interface Props {
  data: AccommodationZoneListingResponse[]
}
```

**Renderizado:**

- Itera `data` por zona. Cada zona es un bloque con título `zoneName` + badge del tipo de alojamiento.
- Dentro de cada zona, lista los alojamientos (`accommodations`):
  - **Con familias asignadas** → estilo normal. Muestra tabla compacta: Familia | Representante | Personas | Notas internas.
  - **Sin familias asignadas** (`assignedFamilies.length === 0`) → fondo gris (`bg-gray-50`), texto `text-gray-400`, indicador "Vacío".
- Botón **"Exportar CSV"** (PrimeVue `Button`, icon `pi-download`) en la cabecera del panel.

**Cabeceras CSV:** `Zona`, `Tipo`, `Alojamiento`, `Capacidad`, `Familia`, `Representante`, `Personas`, `Notas`

**Filas CSV:** una fila por familia asignada. Alojamientos vacíos generan una fila con los campos de familia en blanco.

**Nombre de fichero:** `listado-por-alojamiento-{proposalId-short}.csv`

---

### `AssignmentListingByFamily.vue`

Muestra el listado de salida ordenado alfabéticamente por nombre de familia.

**Props:**

```typescript
interface Props {
  assignmentState: ProposalAssignmentStateResponse | null
}
```

**Lógica de composición (en el componente):**

```typescript
// Construido a partir de assignmentState, no requiere llamada a la API
const assignmentsMap = computed<Map<string, string>>(() =>
  new Map(assignmentState.value?.assignments.map(a => [a.registrationId, a.accommodationId]) ?? [])
)

const accommodationsMap = computed<Map<string, AssignmentAccommodationResponse>>(() =>
  new Map(assignmentState.value?.accommodations.map(a => [a.id, a]) ?? [])
)

const rows = computed<FamilyListingRow[]>(() => {
  const families = assignmentState.value?.families ?? []
  return [...families]
    .sort((a, b) => a.familyName.localeCompare(b.familyName, 'es'))
    .map(f => {
      const accId = assignmentsMap.value.get(f.registrationId) ?? null
      const acc = accId ? accommodationsMap.value.get(accId) ?? null : null
      return {
        familyName: f.familyName,
        representativeName: f.representativeName,
        memberCount: f.memberCount,
        accommodationName: acc?.name ?? null,
        zoneName: acc?.zoneName ?? null,
        accommodationType: acc?.type ?? null,
        isAssigned: accId !== null
      }
    })
})
```

**Renderizado:**

- Tabla PrimeVue DataTable con columnas: Familia | Representante | Personas | Alojamiento | Zona | Tipo.
- Familias sin asignar: fondo ámbar (`bg-amber-50`), texto `text-amber-700`), aparecen al final (ya que el sort por apellido no las separa — se puede añadir criterio secundario `!isAssigned` para ordenarlas al final).
- Botón **"Exportar CSV"** en la cabecera.

**Cabeceras CSV:** `Familia`, `Representante`, `Personas`, `Alojamiento`, `Zona`, `Tipo`, `Estado`

**Columna "Estado":** `Asignada` / `Sin asignar`

**Nombre de fichero:** `listado-por-familia-{proposalId-short}.csv`

---

## Componentes existentes a modificar

### `AccommodationAssignmentView.vue`

1. **Importar** `AssignmentListingsPanel`.
2. **Añadir cuarta pestaña** en `TabList`:

```html
<Tab value="listings">
  <i class="pi pi-list mr-2" />Listados
</Tab>
```

3. **Añadir `TabPanel`** correspondiente:

```html
<TabPanel value="listings" class="overflow-y-auto">
  <AssignmentListingsPanel
    v-if="selectedProposalId && assignmentState"
    :proposal-id="selectedProposalId"
    :camp-edition-id="campEditionId"
    :assignment-state="assignmentState"
  />
  <div v-else class="flex h-full items-center justify-center text-gray-400 p-8">
    Selecciona una propuesta para ver los listados
  </div>
</TabPanel>
```

### `AccommodationReportsPanel.vue`

Resolver el `TODO: Export to CSV/Excel` existente en la línea 171. Añadir dos botones de exportación (uno por sub-pestaña) usando la misma utilidad `exportToCsv`.

**Cabeceras CSV — "Por tipo de alojamiento":** `Tipo`, `Familia`, `Representante`, `Personas`, `Alojamiento`, `Zona`

**Cabeceras CSV — "Por zona":** `Zona`, `Familia`, `Representante`, `Personas`, `Alojamiento`

El botón se coloca junto a los TabList del componente, alineado a la derecha. Exporta solo los datos de la sub-pestaña actualmente visible.

---

## Tipos TypeScript nuevos

Añadir a `frontend/src/types/accommodation-assignment.ts`:

```typescript
// --- Listado por alojamiento (nuevo endpoint by-accommodation) ---

export interface AssignedFamilySlimResponse {
  registrationId: string
  familyName: string
  representativeName: string
  memberCount: number
  internalNotes: string | null
}

export interface AccommodationWithAssignmentsResponse {
  accommodationId: string
  accommodationName: string
  description: string | null
  capacity: number | null
  assignedFamilies: AssignedFamilySlimResponse[]
}

export interface AccommodationZoneListingResponse {
  zoneId: string | null
  zoneName: string
  accommodationType: AccommodationTypeValue
  accommodations: AccommodationWithAssignmentsResponse[]
}

// --- Listado por familia (derivado en frontend) ---

export interface FamilyListingRow {
  familyName: string
  representativeName: string
  memberCount: number
  accommodationName: string | null
  zoneName: string | null
  accommodationType: AccommodationTypeValue | null
  isAssigned: boolean
}
```

---

## Flujo de datos completo

```
AccommodationAssignmentView
  │
  ├── useAccommodationAssignment (composable)
  │     └── assignmentState: ProposalAssignmentStateResponse   ← ya cargado
  │
  └── AssignmentListingsPanel (nueva pestaña)
        │
        ├── GET /by-accommodation  ──→  AssignmentListingByAccommodation
        │     (nuevo endpoint)              └── exportToCsv()
        │
        └── assignmentState (prop)  ──→  AssignmentListingByFamily
              (sin fetch extra)             └── exportToCsv()
```

---

## Tests backend

Nuevos tests de integración en `Abuvi.Tests/Integration/Features/Camps/`:

- `AccommodationAssignmentListingsTests.cs`
  - `GetByAccommodation_ReturnsAllAccommodationsIncludingEmpty` — verifica que alojamientos sin asignaciones aparecen en el resultado.
  - `GetByAccommodation_GroupsByZoneCorrectly` — agrupa por zona con nombre correcto.
  - `GetByAccommodation_WithWrongProposalId_Returns404` — valida ownership.
  - `GetByAccommodation_AssignedFamiliesArePopulated` — los campos de familia son correctos.

---

## Consideraciones de diseño

| Decisión | Justificación |
|---|---|
| Nuevo endpoint `by-accommodation` en lugar de reutilizar `by-type` | `by-type` agrupa por tipo y sólo devuelve filas asignadas. El listado de salida necesita todos los alojamientos (incluyendo vacíos) con estructura zona → alojamiento → familias. |
| Listado por familia derivado en frontend | Los datos necesarios (`families`, `assignments`, `accommodations`) ya están en `assignmentState`. Un endpoint adicional duplicaría trabajo sin beneficio. |
| CSV generado en cliente | El tamaño del dataset (decenas de familias) es pequeño. No justifica una ruta backend. Mantiene la lógica de presentación en el frontend. |
| BOM UTF-8 en CSV | Compatibilidad con Excel en Windows sin que el usuario tenga que importar manualmente. |
| `InternalNotes` como campo preparado (null) | El ticket menciona "notas internas de alojamiento". El modelo `AccommodationAssignment` no tiene campo de notas todavía. Se proyecta `null` ahora y queda el nombre correcto para cuando se añada en un ticket futuro. |

---

## Out of scope

- Envío de email de confirmación de alojamiento a familias.
- Exportación a PDF.
- Filtros dentro de los listados (búsqueda por familia, etc.).
- Campo `AssignmentNotes` en `AccommodationAssignment` (requeriría migración de BD).
