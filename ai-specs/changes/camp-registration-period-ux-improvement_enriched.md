# Mejora UX: Selección global de estancia en inscripción de campamento

## Contexto

Tras el fix del bug del dropdown de periodo (cambio de `<label>` a `<div>` en `RegistrationMemberSelector.vue`), se ha detectado una inconsistencia de usabilidad: al hacer clic en el checkbox de un miembro, el comportamiento difiere del clic en la tarjeta completa. El checkbox no dispara correctamente la aparición del selector de estancia, lo que resulta confuso.

Además, el caso de uso más frecuente es que **todos los miembros de la familia asistan con la misma estancia** (campamento completo, primera semana, etc.). El diseño actual obliga al usuario a configurar la estancia miembro por miembro, lo cual es innecesariamente repetitivo.

## Solución propuesta

Reorganizar la selección de estancia en dos fases dentro del **Paso 1 (Participantes)** del wizard:

1. **Fase 1 — Selección de miembros:** El usuario marca/desmarca qué miembros asisten. Las tarjetas de miembro solo muestran el checkbox, nombre, relación y fecha de nacimiento. **No se muestra selector de estancia individual.**

2. **Fase 2 — Selección de estancia:** Una vez hay al menos un miembro seleccionado y la edición permite múltiples periodos, aparece debajo de la lista de miembros:
   - Un **selector global de estancia** (`Select` de PrimeVue) que aplica a todos los miembros seleccionados. Valor por defecto: `Complete`.
   - Si se elige `WeekendVisit`, aparecen los date pickers de llegada/salida globales (aplican a todos).
   - Un **checkbox** con texto _"No todos los asistentes tienen la misma estancia"_. Al marcarlo:
     - Se muestran selectores de estancia individuales **por cada miembro seleccionado**, pre-rellenados con el valor global.
     - Cada miembro puede cambiar su estancia individualmente.
     - Si un miembro elige `WeekendVisit`, aparecen sus date pickers individuales.
   - Al desmarcar el checkbox, se vuelve al modo global y se sobreescriben las estancias individuales con el valor global.

Los datos de **tutor** para menores siguen apareciendo inline dentro de cada tarjeta de miembro (sin cambios en esa lógica).

## Archivo principal afectado

| Archivo | Cambios |
|---|---|
| [RegistrationMemberSelector.vue](frontend/src/components/registrations/RegistrationMemberSelector.vue) | Reestructurar template y lógica según las fases descritas |

## Cambios concretos en el componente

### 1. Nuevas variables de estado local

```ts
const globalPeriod = ref<AttendancePeriod>('Complete')
const globalVisitStartDate = ref<string | null>(null)
const globalVisitEndDate = ref<string | null>(null)
const hasDifferentPeriods = ref(false)
```

### 2. Simplificar las tarjetas de miembro

Eliminar el selector de estancia individual (`Select`) y los date pickers de `WeekendVisit` de dentro de cada tarjeta. Solo mantener:

- Checkbox + nombre + relación + fecha de nacimiento.
- Sección de datos del tutor para menores (sin cambios).

### 3. Nueva sección de estancia (fuera de las tarjetas)

Debajo de la cuadrícula de miembros, renderizar condicionalmente (cuando `selectedMembers.length > 0 && showPeriodSelector`):

```vue
<!-- Sección de estancia global -->
<div v-if="hasSelectedMembers && showPeriodSelector" class="mt-4 space-y-3 rounded-lg border border-gray-200 bg-white p-4">
  <h3 class="text-sm font-semibold text-gray-700">Estancia</h3>

  <!-- Selector global -->
  <Select v-model="globalPeriod" :options="periodOptions" option-label="label"
    option-value="value" placeholder="Periodo" class="w-full text-sm"
    data-testid="global-period-select" />

  <!-- Date pickers globales para WeekendVisit -->
  <template v-if="globalPeriod === 'WeekendVisit' && !hasDifferentPeriods">
    <!-- Llegada / Salida (misma estructura actual) -->
  </template>

  <!-- Checkbox de estancias diferentes -->
  <div class="flex items-center gap-2 pt-1">
    <Checkbox v-model="hasDifferentPeriods" :binary="true"
      input-id="different-periods" data-testid="different-periods-checkbox" />
    <label for="different-periods" class="cursor-pointer text-sm text-gray-600">
      No todos los asistentes tienen la misma estancia
    </label>
  </div>

  <!-- Selectores individuales (si hasDifferentPeriods) -->
  <div v-if="hasDifferentPeriods" class="space-y-3 border-t border-gray-100 pt-3">
    <div v-for="sel in modelValue" :key="sel.memberId" class="flex items-center gap-3">
      <span class="min-w-[120px] text-sm text-gray-700">{{ getMemberName(sel.memberId) }}</span>
      <Select :model-value="sel.attendancePeriod" :options="periodOptions"
        option-label="label" option-value="value" class="flex-1 text-sm"
        :data-testid="`period-select-${sel.memberId}`"
        @update:model-value="(p: AttendancePeriod) => updatePeriod(sel.memberId, p)" />
    </div>
    <!-- Date pickers individuales para miembros con WeekendVisit -->
  </div>
</div>
```

### 4. Lógica de sincronización

- **Al cambiar `globalPeriod`:** Si `hasDifferentPeriods` es `false`, actualizar el `attendancePeriod` de todos los miembros seleccionados al nuevo valor global. Resetear `visitStartDate`/`visitEndDate` a `null`.
- **Al cambiar las fechas globales de WeekendVisit:** Si `hasDifferentPeriods` es `false`, propagar a todos los miembros con `WeekendVisit`.
- **Al marcar `hasDifferentPeriods`:** Los miembros mantienen el valor global como inicial, el usuario puede modificarlos individualmente.
- **Al desmarcar `hasDifferentPeriods`:** Sobreescribir todos los miembros seleccionados con `globalPeriod` y las fechas globales (si aplica).
- **Al añadir un nuevo miembro (toggle):** Asignarle `globalPeriod` como periodo por defecto (en vez del hardcoded `'Complete'`). Si `hasDifferentPeriods` es `false`, usar también las fechas globales.

### 5. Ajustar `toggleMember`

Cambiar la línea que asigna `attendancePeriod: 'Complete'` por `attendancePeriod: globalPeriod.value`. Si `globalPeriod` es `WeekendVisit` y `!hasDifferentPeriods`, también copiar las fechas globales.

### 6. Helper `getMemberName`

Añadir una función helper para obtener el nombre de un miembro por su ID:

```ts
const getMemberName = (memberId: string): string => {
  const m = props.members.find((m) => m.id === memberId)
  return m ? `${m.firstName} ${m.lastName}` : ''
}
```

## Cambios en el wizard padre

**Archivo:** [RegisterForCampPage.vue](frontend/src/views/registrations/RegisterForCampPage.vue)

No requiere cambios estructurales. El contrato del componente (`v-model` de `WizardMemberSelection[]`) se mantiene idéntico. El paso de confirmación sigue mostrando la estancia por miembro como hasta ahora.

## Tipos

**Archivo:** [registration.ts](frontend/src/types/registration.ts)

No requiere cambios. `WizardMemberSelection` ya incluye `attendancePeriod` por miembro, que es lo que se envía al backend.

## Tests a actualizar

**Archivo:** [RegistrationMemberSelector.test.ts](frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts)

### Tests existentes a adaptar

- **"should show period selector when member is selected and edition allows multiple periods":** Adaptar para verificar que aparece el selector _global_ (no dentro de la tarjeta del miembro).
- **"should not show period selector for unselected members":** Adaptar para verificar que la sección global no aparece cuando no hay miembros seleccionados.
- **"should show WeekendVisit date pickers when WeekendVisit period is selected":** Adaptar para verificar los date pickers globales.
- **"should not deselect member when clicking on the period selector area":** Ya no aplica (el selector está fuera de la tarjeta). Eliminar o adaptar.

### Nuevos tests recomendados

1. **Selector global visible:** Cuando hay miembros seleccionados y la edición permite múltiples periodos, se muestra el selector global de estancia.
2. **Selector global aplica a todos los miembros:** Cambiar el periodo global y verificar que todos los `WizardMemberSelection` emitidos tienen el nuevo periodo.
3. **Checkbox "estancias diferentes":** Al marcarlo, aparecen selectores individuales pre-rellenados con el periodo global.
4. **Desmarcar checkbox sobreescribe individuales:** Al desmarcar, todos los miembros vuelven al periodo global.
5. **Nuevo miembro hereda periodo global:** Al seleccionar un miembro nuevo, su `attendancePeriod` es el periodo global actual (no siempre `Complete`).
6. **WeekendVisit global con fechas:** Al seleccionar `WeekendVisit` globalmente, los date pickers aparecen y las fechas se propagan a todos los miembros.

## Criterios de aceptación

- [ ] El usuario puede seleccionar/deseleccionar miembros haciendo clic en la tarjeta o en el checkbox.
- [ ] Cuando hay miembros seleccionados y la edición permite múltiples periodos, aparece un selector de estancia global debajo de las tarjetas.
- [ ] Al cambiar la estancia global, todos los miembros seleccionados se actualizan con el nuevo periodo.
- [ ] Si se selecciona `WeekendVisit` globalmente, aparecen los date pickers de llegada/salida globales.
- [ ] Un checkbox permite indicar que no todos los asistentes tienen la misma estancia.
- [ ] Al marcar dicho checkbox, aparecen selectores individuales por miembro, pre-rellenados con el valor global.
- [ ] Al desmarcar el checkbox, se sobreescriben las estancias individuales con el valor global.
- [ ] Los miembros nuevos que se seleccionan heredan el periodo global (no siempre "Completo").
- [ ] Los datos del tutor para menores siguen funcionando correctamente.
- [ ] Los campos de tutor se pueden editar sin deseleccionar al miembro.
- [ ] Si la edición solo permite "Completo", no aparece la sección de estancia (comportamiento actual).
- [ ] Los tests unitarios existentes pasan (adaptados) y se añaden tests para la nueva funcionalidad.

## Requisitos no funcionales

- No se requieren cambios en el backend.
- No se requieren migraciones de base de datos.
- No hay impacto en rendimiento.
- Accesibilidad: los nuevos controles deben ser navegables por teclado. Los `label` con `for` deben estar correctamente vinculados a los inputs.
