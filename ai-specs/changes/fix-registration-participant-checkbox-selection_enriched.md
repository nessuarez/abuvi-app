# Bug: Al hacer clic directamente en el checkbox de un participante, no se selecciona en la inscripción

## Contexto

Al inscribirse en un campamento, el primer paso muestra tarjetas por cada miembro de la unidad familiar. Cada tarjeta es clickable (el `<div>` tiene `@click="toggleMember"`) y contiene un `Checkbox` de PrimeVue cuyo valor está controlado por `:model-value="isSelected(member.id)"`.

Este componente es el resultado de una refactorización anterior (ver [`camp-registration-dropdown-bug_enriched.md`](camp-registration-dropdown-bug_enriched.md)) que cambió el contenedor de `<label>` a `<div>` y añadió `@click.stop` al `Checkbox` para evitar el doble-toggle.

## Problema

Cuando el usuario hace clic **directamente sobre el cuadro del checkbox** (en lugar de hacer clic en cualquier otra zona de la tarjeta), el miembro **no se añade a `selectedMembers`**, aunque visualmente el checkbox aparece como marcado. Al llegar al paso de confirmación, esos miembros no aparecen en la lista de participantes seleccionados.

El usuario describe: "solo llegan algunas \[al último paso\], sospecho que las que el checkbox está marcado internamente".

## Causa raíz

En [RegistrationMemberSelector.vue](frontend/src/components/registrations/RegistrationMemberSelector.vue) línea 244:

```vue
<Checkbox :model-value="isSelected(member.id)" :binary="true"
  :input-id="`member-${member.id}`" data-testid="member-checkbox" @click.stop />
```

El `@click.stop` detiene la propagación del evento al `<div>` padre, **pero no llama a `toggleMember`**. El comportamiento es:

1. El usuario hace clic en el checkbox.
2. `@click.stop` detiene la propagación → el `<div>` padre NO llama a `toggleMember`.
3. El elemento `<input type="checkbox">` nativo dentro del componente PrimeVue **sí alterna su estado interno de checked** (comportamiento nativo del navegador, independiente del prop `:model-value`).
4. El checkbox aparece visualmente marcado (estado nativo), pero `selectedMembers` **no se actualiza**.
5. En el paso de confirmación, `selectedMemberDetails` se computa filtrando por `selectedMembers.value.some(s => s.memberId === m.id)`, por lo que esos miembros **no aparecen**.

Este bug fue introducido al aplicar la corrección del bug anterior: se añadió `@click.stop` al checkbox correctamente para evitar el doble-toggle, pero se olvidó mantener la llamada a `toggleMember`.

## Archivos afectados

| Archivo | Línea clave |
|---|---|
| [RegistrationMemberSelector.vue](frontend/src/components/registrations/RegistrationMemberSelector.vue) | L244 |
| [RegistrationMemberSelector.test.ts](frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts) | A añadir tests |

## Solución propuesta

Añadir la llamada a `toggleMember` directamente en el evento `@click.stop` del `Checkbox`:

```vue
<!-- ANTES (línea 244) -->
<Checkbox :model-value="isSelected(member.id)" :binary="true"
  :input-id="`member-${member.id}`" data-testid="member-checkbox" @click.stop />

<!-- DESPUÉS -->
<Checkbox :model-value="isSelected(member.id)" :binary="true"
  :input-id="`member-${member.id}`" data-testid="member-checkbox"
  @click.stop="toggleMember(member.id)" />
```

Esto garantiza que:
- El clic en el checkbox **no se propaga** al `<div>` padre (sin doble-toggle).
- `toggleMember` **sí se llama**, actualizando `selectedMembers` correctamente.
- El estado controlado de `:model-value` se vuelve a evaluar desde el padre, resolviendo también el estado nativo interno.

No se requiere ningún cambio en el backend ni en el modelo de datos.

## Tests a actualizar

**Archivo:** [RegistrationMemberSelector.test.ts](frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts)

Añadir los siguientes tests en el bloque `describe('RegistrationMemberSelector')`:

1. **Selección directa por checkbox**: Hacer clic en el `[data-testid="member-checkbox"]` del miembro 1 y verificar que se emite `update:modelValue` con ese miembro incluido.
2. **Deselección directa por checkbox**: Con el miembro 1 pre-seleccionado, hacer clic en su checkbox y verificar que se emite `update:modelValue` con array vacío.

Ejemplo orientativo:

```ts
it('should emit update:modelValue when clicking directly on checkbox to select', async () => {
  const wrapper = mountComponent([])
  const checkboxEl = wrapper.find('[data-testid="member-label-member-1"] [data-testid="member-checkbox"]')
  await checkboxEl.trigger('click')
  const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
  expect(emitted).toBeTruthy()
  const emittedSelections = emitted[emitted.length - 1][0]
  expect((emittedSelections as unknown as WizardMemberSelection[]).some(s => s.memberId === 'member-1')).toBe(true)
})

it('should emit update:modelValue when clicking directly on checkbox to deselect', async () => {
  const preSelected: WizardMemberSelection[] = [
    { memberId: 'member-1', attendancePeriod: 'Complete', visitStartDate: null, visitEndDate: null, guardianName: null, guardianDocumentNumber: null }
  ]
  const wrapper = mountComponent(preSelected)
  const checkboxEl = wrapper.find('[data-testid="member-label-member-1"] [data-testid="member-checkbox"]')
  await checkboxEl.trigger('click')
  const emitted = wrapper.emitted('update:modelValue') as WizardMemberSelection[][]
  const emittedSelections = emitted[emitted.length - 1][0]
  expect((emittedSelections as unknown as WizardMemberSelection[]).some(s => s.memberId === 'member-1')).toBe(false)
})
```

## Criterios de aceptación

- [ ] Hacer clic directamente en el cuadro del checkbox selecciona al miembro correctamente.
- [ ] Hacer clic directamente en el cuadro del checkbox sobre un miembro ya seleccionado, lo deselecciona.
- [ ] Hacer clic en cualquier parte de la tarjeta (fuera del checkbox) sigue funcionando correctamente — sin doble-toggle.
- [ ] Los miembros seleccionados mediante clic en checkbox aparecen en el paso de confirmación.
- [ ] Los miembros seleccionados se envían correctamente al backend al confirmar la inscripción.
- [ ] Los tests existentes en `RegistrationMemberSelector.test.ts` siguen pasando.
- [ ] Se añaden los tests de clic directo en checkbox (selección y deselección).

## Requisitos no funcionales

- No se requieren cambios en el backend ni en el modelo de datos.
- No hay impacto en rendimiento.
- Sin cambios en accesibilidad: el `Checkbox` de PrimeVue ya gestiona `aria-checked` internamente basándose en `:model-value`, que seguirá siendo correcto una vez que el estado se actualice.
