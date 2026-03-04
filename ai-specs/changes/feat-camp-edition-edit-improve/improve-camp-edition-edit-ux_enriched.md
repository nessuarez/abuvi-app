# Mejorar UX de Edición de CampEdition

## Resumen

La vista de edición de una edición de campamento (`CampEditionUpdateDialog.vue`) es actualmente un diálogo modal que resulta poco usable para la cantidad de campos que contiene. Se necesita convertirla en una página dedicada con mejor organización visual, dando protagonismo a la descripción (texto principal del campamento del año), mejorando la UX de las fechas y clarificando la fecha de corte semanal.

## Problemas Actuales

### 1. Modal demasiado pequeño para el formulario (UX - Principal)

El formulario de edición de edición se renderiza dentro de un `<Dialog>` PrimeVue (`CampEditionUpdateDialog.vue`) con `max-w-2xl`. Contiene ~20 campos agrupados en 6 secciones (fechas, precios, capacidad, asistencia parcial, fin de semana, rangos de edad personalizados, notas, descripción). Esto obliga a hacer scroll dentro del modal, lo cual es incómodo y oculta contexto.

**Solución**: Reemplazar el modal por una página dedicada en `/camps/editions/:id/edit` con layout espacioso y secciones visuales bien definidas.

### 2. Las fechas de inicio/fin no tienen valores por defecto útiles

Al crear o editar una edición, las fechas de inicio y fin se inicializan vacías (o desde la edición existente). El campamento siempre se celebra en la misma franja: **15 de agosto a 30 de agosto**. No tener estos defaults obliga al usuario a seleccionarlos manualmente cada vez.

**Solución**: Al crear una nueva edición o cuando las fechas están vacías, pre-rellenar con 15/08 y 30/08 del año correspondiente.

### 3. La fecha de corte semanal no indica a qué semana pertenece

Cuando se activa "Permitir inscripción por semanas", el usuario establece una `halfDate` (fecha de corte). Actualmente es solo un DatePicker sin contexto. El usuario no sabe si esa fecha se incluye en la 1ª o 2ª semana, lo cual genera confusión al procesar inscripciones.

**Solución**: Añadir una indicación visual clara debajo del DatePicker de `halfDate` que muestre:

- **1ª semana**: desde `startDate` hasta `halfDate` (incluida)
- **2ª semana**: desde el día siguiente a `halfDate` hasta `endDate`

### 4. La descripción tiene una presencia secundaria (UX)

La descripción es actualmente un `<Textarea>` al final del formulario, con la misma importancia visual que las notas internas. Sin embargo, la descripción es **el texto principal de la edición del año**: explica actividades, novedades, información pública. Debería ser lo primero que el usuario ve y edita.

**Solución**: Mover la descripción a la parte superior de la página con un estilo prominente (tamaño mayor, label descriptivo, más filas).

---

## Diseño de la Nueva Página de Edición

### Layout propuesto

```
┌─────────────────────────────────────────────────────────┐
│ ← Volver          Editar Edición 2026            Estado │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  📝 Descripción de la edición                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │ Textarea grande (8-10 filas)                     │   │
│  │ "Describe las actividades, novedades y datos     │   │
│  │  relevantes de esta edición..."                  │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│  ┌──────────────────────┐  ┌──────────────────────┐    │
│  │ 📅 Fechas            │  │ 💰 Precios           │    │
│  │ Inicio: [15/08/2026] │  │ Adulto:  [___] €     │    │
│  │ Fin:    [30/08/2026] │  │ Niño:    [___] €     │    │
│  │                      │  │ Bebé:    [___] €     │    │
│  └──────────────────────┘  └──────────────────────┘    │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ 👥 Capacidad                                     │  │
│  │ Máxima (opcional): [___]                         │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ 📅 Inscripción por semanas  [toggle]             │  │
│  │                                                    │ │
│  │ Fecha de corte: [22/08/2026]                      │ │
│  │ ┌──────────────────────────────────────────────┐  │ │
│  │ │ 1ª semana: 15/08 → 22/08 (incluido)         │  │ │
│  │ │ 2ª semana: 23/08 → 30/08                    │  │ │
│  │ └──────────────────────────────────────────────┘  │ │
│  │                                                    │ │
│  │ Precios por semana:                               │ │
│  │ Adulto: [___] €  Niño: [___] €  Bebé: [___] €   │ │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ 🏕️ Visitas fin de semana  [toggle]               │  │
│  │ ... (fechas, precios, capacidad fds)              │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ 🔢 Rangos de edad personalizados  [toggle]       │  │
│  │ ... (bebé max, niño min/max, adulto min)          │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │ 📝 Notas internas                                │  │
│  │ [Textarea 3 filas]                                │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│                              [Cancelar]  [Guardar]      │
└─────────────────────────────────────────────────────────┘
```

---

## Plan de Implementación

### Paso 1: Crear nueva ruta para la página de edición

**Archivo**: `frontend/src/router/index.ts`

Añadir ruta nueva después de `camp-edition-detail`:

```typescript
{
  path: '/camps/editions/:id/edit',
  name: 'camp-edition-edit',
  component: () => import('@/views/camps/CampEditionEditPage.vue'),
  meta: {
    title: 'ABUVI | Editar Edición',
    requiresAuth: true,
    requiresBoard: true
  }
}
```

### Paso 2: Crear página `CampEditionEditPage.vue`

**Archivo nuevo**: `frontend/src/views/camps/CampEditionEditPage.vue`

Estructura:

- Carga la edición completa con `getEditionById(route.params.id)`
- Formulario completo con secciones en cards (similar al layout de `CampEditionDetailPage.vue` pero editable)
- Reutiliza toda la lógica de validación y submit de `CampEditionUpdateDialog.vue` (extraída al propio componente)
- La descripción va en la parte superior con estilo prominente
- El indicador de semanas calcula dinámicamente los rangos a partir de `startDate`, `halfDate` y `endDate`
- Las fechas se pre-rellenan con 15/08 y 30/08 del año de la edición si están vacías

Secciones del formulario (en orden):

1. **Header**: Título "Editar Edición {year}", botón volver, badge de estado
2. **Descripción** (prominente): `<Textarea>` grande (8-10 rows), label "Descripción de la edición", placeholder descriptivo
3. **Fechas + Precios** (grid 2 columnas en desktop): DatePickers + InputNumbers de precios
4. **Capacidad**: InputNumber
5. **Inscripción por semanas** (collapsible con toggle): `halfDate` + indicador visual de semanas + precios semanales
6. **Visitas fin de semana** (collapsible con toggle): fechas fds + precios fds + capacidad fds
7. **Rangos de edad personalizados** (collapsible con toggle): 4 campos edad
8. **Notas internas**: Textarea pequeño (3 rows)
9. **Footer**: Botones Cancelar / Guardar

### Paso 3: Indicador visual de semanas

Dentro de la sección "Inscripción por semanas", añadir un `computed` que calcule y muestre los rangos:

```typescript
const weekRanges = computed(() => {
  if (!form.startDate || !form.endDate || !form.halfDate) return null
  const fmt = (d: Date) => new Intl.DateTimeFormat('es-ES', { day: '2-digit', month: '2-digit' }).format(d)
  const nextDay = new Date(form.halfDate)
  nextDay.setDate(nextDay.getDate() + 1)
  return {
    week1: `${fmt(form.startDate)} → ${fmt(form.halfDate)} (incluido)`,
    week2: `${fmt(nextDay)} → ${fmt(form.endDate)}`
  }
})
```

Renderizar como un panel informativo con fondo suave:

```html
<div v-if="weekRanges" class="rounded-lg bg-blue-50 p-3 text-sm text-blue-800">
  <p><strong>1ª semana:</strong> {{ weekRanges.week1 }}</p>
  <p><strong>2ª semana:</strong> {{ weekRanges.week2 }}</p>
</div>
```

### Paso 4: Defaults de fechas (15/08 - 30/08)

En `initializeForm()`, si las fechas de la edición están vacías, pre-rellenar:

```typescript
const year = props.edition?.year ?? new Date().getFullYear()
form.startDate = props.edition.startDate
  ? new Date(props.edition.startDate)
  : new Date(year, 7, 15) // 15 de agosto (mes 7 = agosto, 0-indexed)
form.endDate = props.edition.endDate
  ? new Date(props.edition.endDate)
  : new Date(year, 7, 30) // 30 de agosto
```

### Paso 5: Actualizar navegación desde páginas existentes

**Archivo**: `frontend/src/views/camps/CampEditionsPage.vue`

Cambiar `handleEdit()` para navegar a la nueva ruta en lugar de abrir el modal:

```typescript
const handleEdit = (edition: CampEdition) => {
  router.push({ name: 'camp-edition-edit', params: { id: edition.id } })
}
```

Eliminar:

- `showEditDialog` ref
- `<CampEditionUpdateDialog>` del template
- Import de `CampEditionUpdateDialog`
- `handleEditionSaved` handler (el toast se maneja en la nueva página)

**Archivo**: `frontend/src/views/camps/CampEditionDetailPage.vue`

Añadir botón "Editar" en el header para usuarios Board:

```html
<Button
  v-if="isBoard && edition.status !== 'Closed' && edition.status !== 'Completed'"
  label="Editar"
  icon="pi pi-pencil"
  @click="router.push({ name: 'camp-edition-edit', params: { id: edition.id } })"
/>
```

### Paso 6: Deprecar el componente modal (opcional)

**Archivo**: `frontend/src/components/camps/CampEditionUpdateDialog.vue`

Tras la migración, este componente ya no se usa. Se puede:

- Eliminar el archivo
- O mantenerlo temporalmente con un comentario `// @deprecated — usar CampEditionEditPage`

**Recomendación**: Eliminarlo para evitar confusión.

---

## Endpoints Utilizados

| Método | URL | Propósito |
|--------|-----|-----------|
| `GET` | `/api/camps/editions/{id}` | Cargar datos de la edición para editar |
| `PUT` | `/api/camps/editions/{id}` | Guardar cambios de la edición |

No se requieren cambios en el backend. La API actual ya soporta todos los campos necesarios.

## Archivos a Modificar

### Frontend

| Archivo | Cambios |
|---------|---------|
| `frontend/src/router/index.ts` | Añadir ruta `/camps/editions/:id/edit` → `CampEditionEditPage.vue` |
| `frontend/src/views/camps/CampEditionEditPage.vue` | **NUEVO** — Página completa de edición con formulario reorganizado |
| `frontend/src/views/camps/CampEditionsPage.vue` | Cambiar `handleEdit()` para navegar a la nueva ruta; eliminar `CampEditionUpdateDialog` |
| `frontend/src/views/camps/CampEditionDetailPage.vue` | Añadir botón "Editar" que navega a `/camps/editions/:id/edit` |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | Eliminar (reemplazado por la nueva página) |

### Backend

No se requieren cambios en el backend.

## Criterios de Aceptación

- [ ] La edición de una edición de campamento se realiza en una página dedicada (`/camps/editions/:id/edit`), no en un modal
- [ ] La descripción aparece en la parte superior del formulario con mayor prominencia (textarea grande, label descriptivo)
- [ ] Las fechas de inicio/fin se pre-rellenan con 15/08 y 30/08 del año de la edición cuando están vacías
- [ ] Al activar "Inscripción por semanas" y seleccionar una fecha de corte, se muestra un indicador visual que aclara: "1ª semana: DD/MM → DD/MM (incluido)" y "2ª semana: DD/MM → DD/MM"
- [ ] El botón "Editar" en la tabla de ediciones (`CampEditionsPage`) navega a la nueva página en lugar de abrir un modal
- [ ] El botón "Editar" en la página de detalle (`CampEditionDetailPage`) navega a la nueva página de edición
- [ ] El formulario respeta las restricciones por estado (campos deshabilitados si la edición está Open)
- [ ] El componente `CampEditionUpdateDialog.vue` se elimina del codebase
- [ ] Tras guardar cambios exitosamente, se redirige al detalle de la edición con un toast de confirmación
- [ ] El botón "Cancelar" navega de vuelta sin guardar cambios

## Requisitos No Funcionales

- Seguir las convenciones existentes: Vue 3 Composition API, PrimeVue, Tailwind CSS
- Mantener la validación de formulario existente (todos los validators actuales de `CampEditionUpdateDialog`)
- La página debe ser responsive (grid 1 col en móvil, 2 cols en desktop para fechas/precios)
- Mantener los `data-testid` existentes para testing
- No romper el contrato de API backend existente

## Notas Técnicas

- El `Message` de restricción para ediciones Open se mantiene como banner informativo al inicio de la página
- Los toggles de secciones opcionales (semanas, fds, rangos edad) funcionan como acordeones: al activar el toggle se muestran los campos adicionales
- La lógica de `formatDateToIso()` y `validate()` se migra directamente del dialog a la nueva página
- El composable `useCampEditions().updateEdition()` se reutiliza sin cambios
