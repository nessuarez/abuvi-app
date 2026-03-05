# Bug: El dropdown de periodo no se puede desplegar en el selector de miembros del campamento

## Contexto

Al inscribirse en un campamento, el paso de selección de asistentes muestra tarjetas por cada miembro de la unidad familiar. Cada tarjeta permite:

1. Seleccionar/deseleccionar al miembro (checkbox).
2. Elegir el periodo de asistencia (dropdown `Select` de PrimeVue).
3. Introducir datos del tutor si es menor.

## Problema

Al hacer clic en el dropdown de periodo (`Select`), en lugar de abrirse el listado de opciones, se dispara la selección/deselección del miembro. Esto impide elegir un periodo diferente a "Completo" (valor por defecto).

## Causa raíz

El componente [RegistrationMemberSelector.vue](frontend/src/components/registrations/RegistrationMemberSelector.vue) utiliza un elemento `<label>` nativo (línea 165) como contenedor de toda la tarjeta del miembro. Dentro del `<label>` está el `<Checkbox>` con su `input-id`.

El comportamiento nativo del navegador hace que **cualquier clic dentro de un `<label>` se propague al input asociado** (el checkbox), independientemente de `@click.stop` en Vue. El `@click.stop` (línea 189) detiene la propagación de eventos DOM, pero **no impide la asociación nativa label→input** del navegador, que funciona a un nivel diferente.

Cuando el usuario hace clic en el `<Select>`:

1. El navegador asocia el clic al `<Checkbox>` por estar dentro del `<label>` → se deselecciona el miembro.
2. Al deseleccionarse el miembro, `isSelected(member.id)` pasa a `false` → el `v-if` oculta el selector de periodo.
3. El dropdown del `Select` nunca llega a abrirse.

## Archivo afectado

| Archivo | Líneas clave |
|---|---|
| [RegistrationMemberSelector.vue](frontend/src/components/registrations/RegistrationMemberSelector.vue) | L165-241 (template) |

## Solución propuesta

Cambiar el elemento `<label>` por un `<div>` y manejar la selección de forma explícita. De esta forma los elementos internos (Select, DatePicker, InputText) no interferirán con la lógica de selección.

### Cambios concretos

**1. Cambiar `<label>` por `<div>` (línea 165 y 241)**

```vue
<!-- ANTES -->
<label v-for="member in members" :key="member.id"
  class="flex cursor-pointer items-start gap-3 rounded-lg border border-gray-200 bg-white p-3 transition hover:border-blue-300 hover:bg-blue-50"
  :class="{ 'border-blue-400 bg-blue-50': isSelected(member.id) }"
  :data-testid="`member-label-${member.id}`">
  ...
</label>

<!-- DESPUÉS -->
<div v-for="member in members" :key="member.id"
  class="flex cursor-pointer items-start gap-3 rounded-lg border border-gray-200 bg-white p-3 transition hover:border-blue-300 hover:bg-blue-50"
  :class="{ 'border-blue-400 bg-blue-50': isSelected(member.id) }"
  :data-testid="`member-label-${member.id}`"
  @click="toggleMember(member.id)">
  ...
</div>
```

**2. Evitar que el clic en el Checkbox dispare doble toggle**

Como ahora el `<div>` tiene su propio `@click`, el clic en el Checkbox se propagaría al div y ejecutaría `toggleMember` dos veces. Hay que evitarlo:

```vue
<!-- ANTES -->
<Checkbox :model-value="isSelected(member.id)" :binary="true"
  @update:model-value="toggleMember(member.id)"
  :input-id="`member-${member.id}`" data-testid="member-checkbox" />

<!-- DESPUÉS -->
<Checkbox :model-value="isSelected(member.id)" :binary="true"
  :input-id="`member-${member.id}`" data-testid="member-checkbox"
  @click.stop />
```

Se elimina el handler `@update:model-value` del Checkbox porque el `@click` del `<div>` padre ya se encarga de llamar a `toggleMember`. El `@click.stop` impide que el clic en el checkbox burbujee al div.

**3. Mantener `@click.stop` en las zonas interactivas internas**

Los `@click.stop` existentes en las líneas 189 y 229 ya son correctos y deben permanecer, ya que evitan que el clic en el Select, DatePicker o InputText dispare `toggleMember` desde el div padre.

## Tests a actualizar

**Archivo:** [RegistrationMemberSelector.test.ts](frontend/src/components/registrations/__tests__/RegistrationMemberSelector.test.ts)

- Verificar que los tests existentes de selección/deselección de miembros siguen pasando (el trigger cambia de checkbox a div).
- Verificar que el test de visibilidad del selector de periodo sigue funcionando.
- **Nuevo test recomendado:** Abrir el dropdown de periodo en un miembro seleccionado y confirmar que el miembro sigue seleccionado después del clic.

## Criterios de aceptación

- [ ] El usuario puede seleccionar un miembro haciendo clic en cualquier parte de la tarjeta.
- [ ] Una vez seleccionado, el dropdown de periodo se abre correctamente al hacer clic en él.
- [ ] Hacer clic en el dropdown NO deselecciona al miembro.
- [ ] Los DatePicker de visita de fin de semana funcionan correctamente sin deseleccionar al miembro.
- [ ] Los campos de tutor (InputText) se pueden editar sin deseleccionar al miembro.
- [ ] Los tests unitarios existentes pasan correctamente.
- [ ] Se añade al menos un test que valide que el dropdown de periodo es interactuable.

## Requisitos no funcionales

- No se requieren cambios en el backend.
- No se requieren migraciones de base de datos.
- No hay impacto en rendimiento.
- Accesibilidad: al cambiar de `<label>` a `<div>`, se debe asegurar que el componente siga siendo navegable por teclado (el Checkbox de PrimeVue ya es accesible por sí solo y el `role` implícito del label no es imprescindible al tener el `data-testid` y el checkbox con `input-id`).
