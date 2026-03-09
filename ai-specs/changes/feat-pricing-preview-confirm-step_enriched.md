# Mostrar desglose de precios en el paso de confirmación de inscripción

## Contexto

Actualmente, el wizard de inscripción (`RegisterForCampPage.vue`) tiene 5 pasos: Participantes → Extras → Alojamiento → **Confirmar** → Pago. En el paso de confirmación (step 4) se muestra un resumen de participantes, extras, alojamiento y notas, pero **no se muestra ningún precio ni importe total**. El usuario confirma su inscripción "a ciegas" y solo ve los precios después de que la inscripción ya está creada (en el paso de pago).

## Objetivo

Añadir un desglose de precios **estimado** en el paso de confirmación, antes de que el usuario pulse "Confirmar inscripción". El desglose debe mostrar el precio de cada participante y extra seleccionado, con un total general.

## Enfoque técnico recomendado: Cálculo client-side

Toda la información necesaria para calcular los precios ya está disponible en el frontend durante el wizard:

- **`edition`** (`CampEdition`): contiene `pricePerAdult`, `pricePerChild`, `pricePerBaby`, `pricePerAdultWeek`, `pricePerChildWeek`, `pricePerBabyWeek`, `pricePerAdultWeekend`, `pricePerChildWeekend`, `pricePerBabyWeekend`, `startDate`, `endDate`, `halfDate`, `weekendStartDate`, `weekendEndDate`, `useCustomAgeRanges`, `customBabyMaxAge`, `customChildMinAge`, `customChildMaxAge`, `customAdultMinAge`.
- **`familyMembers`** (`FamilyMemberResponse[]`): contiene `dateOfBirth` de cada miembro.
- **`selectedMembers`** (`WizardMemberSelection[]`): contiene `memberId` y `attendancePeriod` elegido.
- **`extrasSelections`** (`WizardExtrasSelection[]`): contiene `unitPrice`, `quantity`, `name`.
- **`campExtras`** (`CampEditionExtra[]`): contiene `price`, `pricingType`, `pricingPeriod`.

**No se necesita un nuevo endpoint backend.** Se puede replicar la lógica de cálculo de `RegistrationPricingService` en el frontend, creando un composable/utilidad que calcule los precios a partir de los datos ya disponibles.

> **Nota:** El precio mostrado es una **estimación local**. El precio definitivo lo calcula el backend al crear la inscripción. Si los rangos de edad globales (`age_ranges` en `association_settings`) difieren de los custom de la edición, podría haber una mínima discrepancia, pero en la práctica las ediciones con `useCustomAgeRanges = true` ya tienen todos los rangos en el objeto `edition`.

## Archivos a modificar

### Frontend

#### 1. Nuevo: `frontend/src/utils/registration-pricing.ts`

Utilidad de cálculo de precios client-side. Funciones:

```typescript
// Calcula la edad a fecha del campamento
function calculateAgeAtCamp(dateOfBirth: string, campStartDate: string): number

// Determina categoría de edad según rangos de la edición
function getAgeCategory(age: number, edition: CampEdition): AgeCategory

// Obtiene el precio para una categoría y periodo
function getPriceForCategory(category: AgeCategory, period: AttendancePeriod, edition: CampEdition): number

// Calcula días de asistencia para un periodo
function getPeriodDays(period: AttendancePeriod, edition: CampEdition, visitStart?: string | null, visitEnd?: string | null): number

// Calcula importe de un extra
function calculateExtraAmount(extra: CampEditionExtra, quantity: number, campDurationDays: number, pricingType: string, pricingPeriod: string): number

// Función principal: genera un PricingBreakdown completo a partir del estado del wizard
function calculatePricingPreview(
  edition: CampEdition,
  familyMembers: FamilyMemberResponse[],
  selectedMembers: WizardMemberSelection[],
  extrasSelections: WizardExtrasSelection[],
  campExtras: CampEditionExtra[]
): PricingBreakdown
```

La lógica debe replicar fielmente la del backend (`RegistrationPricingService.cs`):
- Edad = edad a fecha `edition.startDate`
- Categoría: si `edition.useCustomAgeRanges` → usar rangos custom; sino usar rangos por defecto (para el frontend, usar los rangos de la edición disponibles o unos defaults razonables como Baby ≤ 2, Child 3–14, Adult ≥ 15)
- Precio miembro = `getPriceForCategory(category, period, edition)`
- Extras: `PerPerson` → `price × quantity`; `PerFamily` → `price`; si `PerDay` → multiplicar por `campDurationDays`
- `campDurationDays` = días de asistencia del periodo Complete (total days)

#### 2. Modificar: `frontend/src/views/registrations/RegisterForCampPage.vue`

En el paso de confirmación (líneas ~353-477, `<StepPanel :value="confirmStepValue">`):

1. **Importar** `calculatePricingPreview` y el componente `RegistrationPricingBreakdown`.
2. **Añadir computed** `pricingPreview`:
   ```typescript
   const pricingPreview = computed<PricingBreakdown | null>(() => {
     if (!edition.value || selectedMembers.value.length === 0) return null
     return calculatePricingPreview(
       edition.value,
       familyMembers.value,
       selectedMembers.value,
       extrasSelections.value,
       campExtras.value
     )
   })
   ```
3. **Insertar el componente** `RegistrationPricingBreakdown` en el template del paso de confirmación, justo **después** de los bloques de resumen existentes (extras, alojamiento) y **antes** del bloque de notas adicionales:
   ```html
   <!-- Desglose de precios estimado -->
   <div v-if="pricingPreview" class="mb-4">
     <RegistrationPricingBreakdown :pricing="pricingPreview" />
     <p class="mt-2 text-xs text-gray-400">
       * Precios estimados. El importe definitivo se confirmará tras completar la inscripción.
     </p>
   </div>
   ```

#### 3. Reutilizar: `frontend/src/components/registrations/RegistrationPricingBreakdown.vue`

Este componente **ya existe** y muestra exactamente lo solicitado:
- Tabla de participantes con nombre, categoría, periodo e importe individual
- Subtotal participantes
- Tabla de extras con concepto, cálculo e importe
- Subtotal extras
- **Total inscripción** en barra destacada

No necesita modificaciones. Recibe un `PricingBreakdown` como prop.

### Backend

**No se requieren cambios en el backend.** La lógica de precios definitiva sigue ejecutándose server-side al crear la inscripción. El frontend solo muestra una estimación.

## Campos del desglose

### Participantes
| Campo | Origen |
|---|---|
| Nombre completo | `familyMembers[].firstName + lastName` |
| Categoría de edad | Calculada: `getAgeCategory(ageAtCamp, edition)` |
| Edad en campamento | Calculada: `calculateAgeAtCamp(dateOfBirth, startDate)` |
| Periodo de asistencia | `selectedMembers[].attendancePeriod` |
| Días de asistencia | Calculado: `getPeriodDays(period, edition, visitStart, visitEnd)` |
| Importe individual | Calculado: `getPriceForCategory(category, period, edition)` |

### Extras
| Campo | Origen |
|---|---|
| Nombre | `extrasSelections[].name` |
| Precio unitario | `campExtras[].price` (cruzado por `campEditionExtraId`) |
| Tipo (PerPerson/PerFamily) | `campExtras[].pricingType` |
| Periodo (OneTime/PerDay) | `campExtras[].pricingPeriod` |
| Cantidad | `extrasSelections[].quantity` |
| Cálculo (texto descriptivo) | Generado: ej. "2 × 5,00 € × 7 días" |
| Importe total | Calculado |

### Totales
| Campo | Cálculo |
|---|---|
| Subtotal participantes | Suma de importes individuales |
| Subtotal extras | Suma de importes de extras |
| **Total inscripción** | Subtotal participantes + Subtotal extras |

## Criterios de aceptación

- [ ] En el paso "Revisa y confirma" del wizard de inscripción, se muestra un desglose de precios con participantes, extras y total
- [ ] Cada participante muestra su nombre, categoría de edad, periodo y precio individual
- [ ] Cada extra seleccionado muestra nombre, cálculo y precio
- [ ] Se muestra el total general de la inscripción de forma destacada
- [ ] Se indica visualmente que los precios son estimados (texto aclaratorio)
- [ ] El componente `RegistrationPricingBreakdown` existente se reutiliza sin duplicar lógica de presentación
- [ ] Los precios estimados coinciden con los que calcula el backend (salvo edge cases de rangos de edad globales vs custom)
- [ ] La UI es responsive y se integra visualmente con el estilo del resumen existente
- [ ] No se realizan llamadas adicionales al backend; el cálculo es puramente client-side

## Tests

- [ ] Unit tests para `calculatePricingPreview` en `frontend/src/utils/__tests__/registration-pricing.test.ts`:
  - Miembros con diferentes categorías de edad (Baby, Child, Adult)
  - Diferentes periodos (Complete, FirstWeek, SecondWeek, WeekendVisit)
  - Extras con diferentes combinaciones de `pricingType` y `pricingPeriod`
  - Caso sin extras seleccionados
  - Caso con rangos de edad custom de la edición

## Requisitos no funcionales

- **Rendimiento**: El cálculo es una computed property reactiva; se recalcula solo cuando cambian los inputs. No hay llamadas de red adicionales.
- **Mantenibilidad**: Si se modifica la lógica de precios en el backend (`RegistrationPricingService.cs`), hay que actualizar también `registration-pricing.ts`. Documentar esta dependencia con un comentario en ambos archivos.
- **UX**: El disclaimer de "precios estimados" evita confusiones si hay una mínima diferencia con el precio final.
