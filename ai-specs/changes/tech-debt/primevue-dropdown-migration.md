# PrimeVue Dropdown to Select Component Migration

## Contexto

PrimeVue v4+ ha deprecado el componente `Dropdown` en favor del nuevo componente `Select`. Aunque ambos componentes funcionan de manera similar, el uso de `Dropdown` genera warnings de deprecación en la consola del navegador.

**Warning actual:**

```
Deprecated since v4. Use Select component instead.
```

## Estado Actual

### ✅ Componentes Actualizados

#### Camps Feature
- `frontend/src/views/camps/CampLocationsPage.vue` - Actualizado
- `frontend/src/components/camps/CampLocationForm.vue` - Actualizado

#### Users Feature
- `frontend/src/components/users/UserRoleDialog.vue` - ✅ **Actualizado**
- `frontend/src/components/users/UserForm.vue` - ✅ **Actualizado**

### 🎉 Estado: Migración Completada

Todos los componentes han sido migrados de `Dropdown` a `Select`. No quedan warnings de deprecación en el proyecto.

## Especificación de Cambios

### Patrón de Migración

#### 1. Actualizar Import Statement

**Antes:**

```typescript
import Dropdown from 'primevue/dropdown'
```

**Después:**

```typescript
import Select from 'primevue/select'
```

#### 2. Actualizar Uso en Template

**Antes:**

```vue
<Dropdown
  v-model="selectedValue"
  :options="options"
  option-label="label"
  option-value="value"
  placeholder="Seleccione una opción"
  class="w-full"
/>
```

**Después:**

```vue
<Select
  v-model="selectedValue"
  :options="options"
  option-label="label"
  option-value="value"
  placeholder="Seleccione una opción"
  class="w-full"
/>
```

### Compatibilidad de Props

El componente `Select` mantiene la misma API que `Dropdown`, por lo que los props principales son compatibles:

- ✅ `v-model` - Funciona igual
- ✅ `:options` - Array de opciones
- ✅ `option-label` - Campo para mostrar el label
- ✅ `option-value` - Campo para el value
- ✅ `placeholder` - Texto placeholder
- ✅ `class` - Clases CSS
- ✅ `disabled` - Estado deshabilitado
- ✅ `filter` - Filtro de búsqueda
- ✅ Event handlers (`@change`, etc.)

## Archivos a Modificar

### 1. UserRoleDialog.vue

**Ubicación:** `frontend/src/components/users/UserRoleDialog.vue`

**Cambios necesarios:**

1. Cambiar import de `Dropdown` a `Select`
2. Reemplazar todas las instancias de `<Dropdown>` por `<Select>` en el template
3. Reemplazar todas las instancias de `</Dropdown>` por `</Select>` en el template

**Uso esperado:** Selector de roles de usuario

### 2. UserForm.vue

**Ubicación:** `frontend/src/components/users/UserForm.vue`

**Cambios necesarios:**

1. Cambiar import de `Dropdown` a `Select`
2. Reemplazar todas las instancias de `<Dropdown>` por `<Select>` en el template
3. Reemplazar todas las instancias de `</Dropdown>` por `</Select>` en el template

**Uso esperado:** Formulario de creación/edición de usuarios (posiblemente para seleccionar roles, estado, etc.)

## Testing Post-Migración

Después de realizar los cambios, verificar:

1. **Funcionalidad:**
   - [ ] Los selectors se renderizan correctamente
   - [ ] La selección de valores funciona como antes
   - [ ] El binding v-model funciona correctamente
   - [ ] Los eventos se emiten correctamente

2. **Visual:**
   - [ ] El estilo se mantiene consistente
   - [ ] El placeholder se muestra correctamente
   - [ ] Las opciones se despliegan correctamente
   - [ ] El estado disabled funciona (si aplica)

3. **Console:**
   - [ ] No hay warnings de deprecación
   - [ ] No hay errores de consola

## Comandos de Búsqueda Útiles

Para encontrar todas las instancias de Dropdown en el proyecto:

```bash
# Buscar imports de Dropdown
npx grep -r "from 'primevue/dropdown'" frontend/src

# Buscar uso de componente Dropdown en templates
npx grep -r "<Dropdown" frontend/src
```

## Referencias

- [PrimeVue Select Component Documentation](https://primevue.org/select/)
- [PrimeVue Migration Guide](https://primevue.org/migration/)

## Prioridad

**Prioridad:** Media
**Esfuerzo estimado:** 15-30 minutos
**Impacto:** Eliminar warnings de deprecación, preparar código para futuras versiones de PrimeVue

## Notas Adicionales

- Este cambio es puramente cosmético en términos de funcionalidad
- No requiere cambios en la lógica de negocio
- Es un cambio seguro que no debería romper funcionalidad existente
- Se recomienda hacer este cambio antes de actualizar a versiones futuras de PrimeVue donde Dropdown podría ser completamente removido
