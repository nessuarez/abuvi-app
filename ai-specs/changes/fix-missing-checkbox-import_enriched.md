# Fix: Missing Checkbox in RegistrationMemberSelector

## Contexto

En el componente `RegistrationMemberSelector.vue`, el checkbox para marcar "No todas las personas asistentes tienen la misma estancia" no se renderiza. Solo aparece el texto como label pero sin el control interactivo.

## Causa raíz

El componente `Checkbox` de PrimeVue se utiliza en el template (línea 307) pero **no está importado** en la sección `<script setup>`. Vue ignora silenciosamente los componentes no registrados, por lo que el `<Checkbox>` no se renderiza y solo queda visible el `<label>` asociado.

## Solución

Agregar el import faltante:

```typescript
import Checkbox from 'primevue/checkbox'
```

### Archivo modificado

- `frontend/src/components/registrations/RegistrationMemberSelector.vue` — Agregar import de `Checkbox` desde `primevue/checkbox`.

## Verificación

1. Seleccionar al menos un miembro en el wizard de inscripción.
2. Confirmar que la sección "Estancia" muestra el checkbox interactivo junto al texto "No todas las personas asistentes tienen la misma estancia".
3. Confirmar que al marcar el checkbox se despliegan los selectores individuales de periodo por cada miembro seleccionado.
4. Confirmar que al desmarcar el checkbox se vuelve al modo global (todos con el mismo periodo).

## Impacto

- Solo afecta al frontend.
- Sin cambios en backend, API ni base de datos.
- Sin riesgo de regresión: es un import faltante que restaura funcionalidad existente.
