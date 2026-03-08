# Homogeneizar campos de fecha con entrada por teclado

## Problema

Los campos de fecha actuales (PrimeVue `DatePicker`) presentan varios problemas de usabilidad:

1. **No se intuye el formato**: El usuario no sabe si debe escribir `DD/MM/YYYY`, `MM/DD/YYYY` u otro formato. No hay placeholder visible cuando el campo está vacío.
2. **No se pueden introducir solo dígitos**: Si el usuario escribe `15032000`, el componente no lo interpreta correctamente. Necesita escribir `15/03/2000` con los separadores manualmente.
3. **En móviles no se ofrece entrada por teclado**: El DatePicker abre directamente el calendario, lo que es lento para fechas como fechas de nacimiento (hay que navegar muchos meses/años hacia atrás). No hay opción de escribir la fecha directamente con el teclado numérico.

## Solución propuesta

Reemplazar el `DatePicker` nativo de PrimeVue por un componente wrapper reutilizable `DateInput` que:

- Use un `InputMask` (de PrimeVue) con máscara `99/99/9999` para guiar la entrada
- Muestre un placeholder `DD/MM/AAAA` cuando está vacío
- Permita al usuario introducir solo dígitos (las barras se insertan automáticamente)
- Mantenga opcionalmente un botón de calendario para selección visual (usando DatePicker en modo popup)
- Funcione correctamente en móviles con teclado numérico (`inputmode="numeric"`)

## Componentes afectados (7 archivos)

| Componente | Campos de fecha |
|---|---|
| `frontend/src/components/family-units/FamilyMemberForm.vue` | `dateOfBirth` |
| `frontend/src/components/guests/GuestForm.vue` | `dateOfBirth` |
| `frontend/src/components/camps/CampEditionUpdateDialog.vue` | `startDate`, `endDate`, `halfDate`, `weekendStartDate`, `weekendEndDate` |
| `frontend/src/components/camps/CampEditionProposeDialog.vue` | `startDate`, `endDate`, `halfDate`, `weekendStartDate`, `weekendEndDate` |
| `frontend/src/components/memberships/PayFeeDialog.vue` | `paidDate` |
| `frontend/src/components/registrations/RegistrationMemberSelector.vue` | `visitStartDate`, `visitEndDate` |
| `frontend/src/components/admin/PaymentsAllList.vue` | `dateFrom`, `dateTo` |

## Diseño técnico

### 1. Crear componente compartido `DateInput.vue`

**Ubicación**: `frontend/src/components/shared/DateInput.vue`

**Props**:
| Prop | Tipo | Default | Descripción |
|---|---|---|---|
| `modelValue` | `Date \| null` | `null` | Valor de fecha (v-model) |
| `invalid` | `boolean` | `false` | Estado de error visual |
| `disabled` | `boolean` | `false` | Desactivar entrada |
| `minDate` | `Date` | — | Fecha mínima permitida |
| `maxDate` | `Date` | — | Fecha máxima permitida |
| `showCalendar` | `boolean` | `true` | Mostrar botón de calendario |
| `id` | `string` | — | ID del input para el label |
| `placeholder` | `string` | `'DD/MM/AAAA'` | Texto placeholder |

**Emits**: `update:modelValue`, `blur`

**Comportamiento**:
- Renderiza un `InputMask` de PrimeVue con `mask="99/99/9999"` y `placeholder="DD/MM/AAAA"`
- Añade atributo `inputmode="numeric"` para que los móviles muestren teclado numérico
- Opcionalmente renderiza un botón de icono de calendario que abre un `DatePicker` en modo overlay/popup
- Al completar la máscara (8 dígitos introducidos), parsea `DD/MM/YYYY` → `Date` y emite `update:modelValue`
- Si la fecha parseada es inválida (ej. 31/02/2000), no emite y muestra el campo en estado de error
- Si el usuario selecciona una fecha del calendario popup, rellena el InputMask con `DD/MM/YYYY` formateado
- Valida contra `minDate`/`maxDate` si están definidos

**Conversión interna**:
```typescript
// Texto de máscara → Date
function parseInputToDate(masked: string): Date | null {
  const match = masked.match(/^(\d{2})\/(\d{2})\/(\d{4})$/)
  if (!match) return null
  const [, dd, mm, yyyy] = match
  const date = new Date(+yyyy, +mm - 1, +dd)
  // Validar que la fecha es real (no 31/02)
  if (date.getDate() !== +dd || date.getMonth() !== +mm - 1) return null
  return date
}

// Date → texto de máscara
function formatDateToInput(date: Date): string {
  const dd = String(date.getDate()).padStart(2, '0')
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const yyyy = String(date.getFullYear())
  return `${dd}/${mm}/${yyyy}`
}
```

### 2. Actualizar los 7 componentes

En cada componente:
1. Reemplazar `import DatePicker from 'primevue/datepicker'` → `import DateInput from '@/components/shared/DateInput.vue'`
2. Reemplazar `<DatePicker ... dateFormat="dd/mm/yy" showIcon ...>` → `<DateInput ...>`
3. Eliminar las props específicas de DatePicker (`dateFormat`, `showIcon`) ya que `DateInput` las maneja internamente
4. Mantener las props comunes: `v-model`, `:invalid`, `:disabled`, `:maxDate`, `:minDate`, `@blur`, `id`, `class`

### 3. Dependencias

- `InputMask` de PrimeVue ya está disponible (incluido en PrimeVue 4.5.4), solo hay que importarlo
- No se necesitan nuevas dependencias

## Criterios de aceptación

- [ ] El campo de fecha muestra el placeholder `DD/MM/AAAA` cuando está vacío
- [ ] Al escribir dígitos, las barras `/` se insertan automáticamente (ej. escribir `15032000` produce `15/03/2000`)
- [ ] En dispositivos móviles se muestra el teclado numérico, no el alfabético
- [ ] El botón de calendario sigue disponible para selección visual
- [ ] Al seleccionar una fecha del calendario, el campo de texto se actualiza con el formato `DD/MM/AAAA`
- [ ] Fechas inválidas (ej. `31/02/2025`) no se aceptan y muestran error visual
- [ ] Las restricciones `minDate`/`maxDate` se siguen respetando
- [ ] Los 7 componentes afectados usan el nuevo `DateInput` de forma homogénea
- [ ] El componente funciona correctamente al editar (pre-rellenar) valores existentes
- [ ] Las utilidades `formatDateLocal()` y `parseDateLocal()` siguen usándose para la comunicación con la API (sin cambios en el backend)

## Requisitos no funcionales

- **Rendimiento**: El componente `DateInput` no debe introducir re-renders innecesarios. Usar `computed` y `watch` con cuidado.
- **Accesibilidad**: El `inputmode="numeric"` y el `placeholder` mejoran la accesibilidad. El `label` sigue vinculado por `id`.
- **Tests**: Crear test unitario `frontend/src/components/shared/__tests__/DateInput.test.ts` cubriendo:
  - Renderizado con placeholder
  - Entrada de dígitos y auto-formateo
  - Parseo correcto de fechas válidas
  - Rechazo de fechas inválidas (31/02, 00/00/0000)
  - Respeto de minDate/maxDate
  - Pre-rellenado con valor existente
  - Selección desde calendario popup

## Notas de implementación

- El formato interno sigue siendo `YYYY-MM-DD` para la API. La conversión `DD/MM/YYYY` ↔ `Date` ↔ `YYYY-MM-DD` se hace en capas separadas.
- No se modifica nada en el backend.
- El `DatePicker` de PrimeVue se sigue usando internamente en el popup del calendario, pero el input de texto principal es un `InputMask`.
