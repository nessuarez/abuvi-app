# Traducción al Español — Sección Gestión de Usuarios

## Resumen

Traducir al español todos los textos visibles para el usuario en la sección de gestión de usuarios, incluyendo la página de listado (`UsersPage.vue`), la página de detalle (`UserDetailPage.vue`) y sus componentes relacionados. Este cambio es parte del esfuerzo global de traducción de la app ([ver spec base](../merged/feat-spanish-texts/feat-spanish-texts-enriched.md)), y está acotado a los archivos del módulo de usuarios.

## Contexto de negocio

La app es usada principalmente por familias hispanohablantes. Los textos en inglés en la gestión de usuarios generan fricción al personal administrativo que gestiona socios, juntas y administradores. El objetivo es que toda la interfaz de esta sección esté en español de forma consistente con el resto de la app.

## Estado actual — textos en inglés identificados

A continuación se listan **todos** los textos en inglés encontrados, por archivo:

### `frontend/src/pages/UsersPage.vue`

| Inglés | Español |
|---|---|
| `"User Management"` (h1) | `"Gestión de Usuarios"` |
| `"Create User"` (botón) | `"Crear Usuario"` |
| `"Retry"` (botón error) | `"Reintentar"` |
| Column header `"Name"` | `"Nombre"` |
| Column header `"Email"` | `"Correo electrónico"` |
| Column header `"Role"` | `"Rol"` |
| Column header `"Phone"` | `"Teléfono"` |
| Column header `"Status"` | `"Estado"` |
| Column header `"Created"` | `"Alta"` |
| Column header `"Actions"` | `"Acciones"` |
| `"Active"` / `"Inactive"` (Tag) | `"Activo"` / `"Inactivo"` |
| `aria-label="View Details"` | `aria-label="Ver detalles"` |
| Dialog header `"Create New User"` | `"Crear nuevo usuario"` |
| Toast summary `"Role Updated"` | `"Rol actualizado"` |
| Toast detail `` `${name}'s role has been updated to ${role}` `` | `` `El rol de ${name} ha sido actualizado a ${roleLabel}` `` |

### `frontend/src/pages/UserDetailPage.vue`

| Inglés | Español |
|---|---|
| `"Back to Users"` (botón) | `"Volver a usuarios"` |
| `"User Details"` (h1) | `"Detalles del usuario"` |
| `"Go Back"` (botón error) | `"Volver"` |
| `"Edit"` (botón) | `"Editar"` |
| Field label `"Email"` | `"Correo electrónico"` |
| Field label `"Role"` | `"Rol"` |
| Field label `"Phone"` | `"Teléfono"` |
| Field label `"Status"` | `"Estado"` |
| Field label `"Created"` | `"Alta"` |
| Field label `"Last Updated"` | `"Última actualización"` |
| `"Active"` / `"Inactive"` (Tag) | `"Activo"` / `"Inactivo"` |
| Card title `"Edit User"` | `"Editar usuario"` |

### `frontend/src/components/users/UserForm.vue`

| Inglés | Español |
|---|---|
| Label `"Email *"` | `"Correo electrónico *"` |
| Label `"Password *"` | `"Contraseña *"` |
| Placeholder `"Minimum 8 characters"` | `"Mínimo 8 caracteres"` |
| Label `"First Name *"` | `"Nombre *"` |
| Placeholder `"John"` | Eliminar o poner `"María"` |
| Label `"Last Name *"` | `"Apellidos *"` |
| Placeholder `"Doe"` | Eliminar o poner `"García"` |
| Label `"Phone (optional)"` | `"Teléfono (opcional)"` |
| Label `"Role *"` | `"Rol *"` |
| Placeholder `"Select a role"` | `"Seleccionar un rol"` |
| Role labels: `'Member'`, `'Board'`, `'Admin'` | `'Socio'`, `'Junta Directiva'`, `'Administrador'` |
| Label `"Active"` (switch) | `"Activo"` |
| Button `"Create User"` | `"Crear usuario"` |
| Button `"Update User"` | `"Actualizar usuario"` |
| Button `"Cancel"` | `"Cancelar"` |
| Validation `'Email is required'` | `'El correo electrónico es obligatorio'` |
| Validation `'Email must be valid'` | `'El formato del correo electrónico no es válido'` |
| Validation `'Password is required'` | `'La contraseña es obligatoria'` |
| Validation `'Password must be at least 8 characters'` | `'La contraseña debe tener al menos 8 caracteres'` |
| Validation `'First name is required'` | `'El nombre es obligatorio'` |
| Validation `'Last name is required'` | `'Los apellidos son obligatorios'` |

### `frontend/src/components/users/UserRoleCell.vue`

| Inglés | Español |
|---|---|
| `aria-label="Edit role"` | `aria-label="Editar rol"` |
| `{{ user.role }}` (mostrado directamente) | Usar helper `getRoleLabel(user.role)` |

### `frontend/src/components/users/UserRoleDialog.vue`

| Inglés | Español |
|---|---|
| Dialog header `` `Update Role: ${name}` `` | `` `Actualizar rol: ${name}` `` |
| Message `"You cannot change your own role"` | `"No puedes cambiar tu propio rol"` |
| Label `"Current Role"` | `"Rol actual"` |
| Label `"New Role *"` | `"Nuevo rol *"` |
| Placeholder `"Select new role"` | `"Seleccionar nuevo rol"` |
| Role labels: `'Member'`, `'Board'`, `'Admin'` | `'Socio'`, `'Junta Directiva'`, `'Administrador'` |
| Label `"Reason (optional)"` | `"Motivo (opcional)"` |
| Placeholder reason | `"Indica el motivo del cambio de rol (queda registrado para auditoría)"` |
| `"characters"` (contador) | `"caracteres"` |
| Button `"Cancel"` | `"Cancelar"` |
| Button `"Update Role"` | `"Actualizar rol"` |

### `frontend/src/components/users/UserCard.vue`

| Inglés | Español |
|---|---|
| `'Active'` / `'Inactive'` | `'Activo'` / `'Inactivo'` |
| `user.role` (mostrado directamente) | Usar helper `getRoleLabel(user.role)` |

---

## Requisitos técnicos

### 1. Helper de traducción de roles

Los roles del sistema son enums del backend (`Admin`, `Board`, `Member`). Los valores no deben cambiar (se envían y reciben de la API tal cual). Se necesita un helper reutilizable para mostrarlos en español:

**Archivo:** `frontend/src/utils/userUtils.ts` (nuevo o añadir a utils existente)

```typescript
export const roleLabels: Record<string, string> = {
  Admin: 'Admin',
  Board: 'Junta',
  Member: 'Socio/a',
}

export const getRoleLabel = (role: string): string => {
  return roleLabels[role] ?? role
}
```

Este helper debe usarse en:

- `UserRoleCell.vue` — en lugar de `{{ user.role }}`
- `UserCard.vue` — en lugar de `{{ user.role }}`
- `UserForm.vue` — en el array `roleOptions` (campo `label`)
- `UserRoleDialog.vue` — en el computed `availableRoles` (campo `label`)
- `UsersPage.vue` — en el toast detail (para mostrar el nombre del rol en español)

### 2. Archivos a modificar

```
frontend/src/pages/
  ├── UsersPage.vue                          ❌ Traducir
  └── UserDetailPage.vue                     ❌ Traducir

frontend/src/components/users/
  ├── UserForm.vue                           ❌ Traducir labels, placeholders y validaciones
  ├── UserRoleCell.vue                       ❌ Traducir aria-label, usar getRoleLabel()
  ├── UserRoleDialog.vue                     ❌ Traducir todos los textos
  └── UserCard.vue                           ❌ Traducir 'Active'/'Inactive', usar getRoleLabel()

frontend/src/utils/userUtils.ts             ✨ Crear helper getRoleLabel()
```

**No cambiar:**

- `frontend/src/router/index.ts` — Los meta titles `/users` ya están en español (`"ABUVI | Gestión de Usuarios"`, `"ABUVI | Detalle de Usuario"`)
- Valores de los enums de rol en ningún caso (siguen siendo `'Admin'`, `'Board'`, `'Member'` en la API)

### 3. Patrón de traducción para roles en templates

```vue
<!-- ❌ Antes (en UserCard.vue, UserRoleCell.vue) -->
<span>{{ user.role }}</span>

<!-- ✅ Después -->
<script setup lang="ts">
import { getRoleLabel } from '@/utils/userUtils'
</script>
<template>
  <span>{{ getRoleLabel(user.role) }}</span>
</template>
```

---

## Tests a actualizar

### `frontend/src/components/users/__tests__/UserForm.test.ts`

Los tests que buscan texto en inglés deben actualizarse:

```typescript
// Antes
const cancelButton = wrapper.findAll('button').find((b) => b.text() === 'Cancel')
const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Create User'))

// Después
const cancelButton = wrapper.findAll('button').find((b) => b.text() === 'Cancelar')
const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Crear usuario'))
```

Si se añaden tests de validación, deben usar los mensajes en español:

```typescript
expect(wrapper.text()).toContain('El correo electrónico es obligatorio')
expect(wrapper.text()).toContain('La contraseña es obligatoria')
```

### `frontend/src/components/users/__tests__/UserCard.test.ts`

Si el test verifica `'Active'` o `'Inactive'`, actualizar a `'Activo'` / `'Inactivo'`. El test actual no verifica estos strings, pero si se añaden deben ser en español.

El test `should render user information` verifica `'Member'` — si se traduce el rol en la card, actualizar a `'Socio'`. **Verificar si la tag del rol en UserCard usa el value del backend o el label traducido** al implementar.

---

## Criterios de aceptación

1. ✅ `UsersPage.vue` muestra todos los textos en español (título, botones, columnas, tags, mensajes de toast)
2. ✅ `UserDetailPage.vue` muestra todos los textos en español (título, botones, labels de campos, tags)
3. ✅ `UserForm.vue` muestra labels, placeholders y mensajes de validación en español
4. ✅ `UserRoleCell.vue` muestra el rol traducido y el aria-label en español
5. ✅ `UserRoleDialog.vue` muestra todos los textos del diálogo en español
6. ✅ `UserCard.vue` muestra el estado (Activo/Inactivo) y el rol en español
7. ✅ Helper `getRoleLabel()` centraliza la traducción de roles y es reutilizado en todos los componentes
8. ✅ Los valores de los roles enviados a la API no cambian (`Admin`, `Board`, `Member`)
9. ✅ Los tests de `UserForm.test.ts` actualizados y pasando con textos en español
10. ✅ No quedan textos en inglés visibles al navegar por `/users` ni `/users/:id`
11. ✅ El código (variables, funciones, comentarios) permanece en inglés según los estándares del proyecto

---

## Pasos de implementación (TDD)

> **Recordatorio TDD**: escribir el test primero (RED), luego implementar (GREEN), luego refactorizar.

### Paso 1 — Helper `getRoleLabel`

1. Crear `frontend/src/utils/userUtils.ts` con el helper
2. Verificar manualmente que `getRoleLabel('Admin') === 'Administrador'` etc.

### Paso 2 — `UserForm.vue`

1. Actualizar test: cambiar `'Cancel'` → `'Cancelar'`, `'Create User'` → `'Crear usuario'`
2. Ejecutar tests → deben fallar (RED)
3. Actualizar todos los textos del componente + usar `getRoleLabel` en `roleOptions`
4. Ejecutar tests → deben pasar (GREEN)

### Paso 3 — `UserRoleCell.vue` y `UserCard.vue`

1. Actualizar (o crear) tests que verifiquen los textos en español
2. Implementar cambios en los componentes

### Paso 4 — `UserRoleDialog.vue`

1. Traducir todos los textos del diálogo
2. Verificar manualmente el flujo de cambio de rol

### Paso 5 — `UsersPage.vue` y `UserDetailPage.vue`

1. Traducir todos los textos de las páginas
2. Verificar manualmente: listar usuarios, ver detalle, crear usuario, editar usuario

### Paso 6 — Verificación final

1. `npm run test` — todos los tests deben pasar
2. Revisión manual completa de `/users` y `/users/:id`

---

## Notas de implementación

- **Género gramatical**: Respetar concordancia de género en mensajes:
  - "El correo electrónico **es obligatorio**" (masculino)
  - "La contraseña **es obligatoria**" (femenino)
  - "Los apellidos **son obligatorios**" (plural masculino)
- **Tono**: Informal ("tú"), amigable, consistente con el resto de la app
- **Valores de API**: NUNCA traducir los valores de los enums que se envían al backend
- **Accesibilidad**: Traducir también los atributos `aria-label`

---

## Dependencias

- Ninguna dependencia externa
- No requiere cambios de backend
- No requiere cambios de router (los meta titles ya están en español)

---

**Estado:** Listo para desarrollo
**Directorio de feature:** `ai-specs/changes/feat-i18n-user-management/`
**Archivos relacionados:** [feat-spanish-texts-enriched.md](../merged/feat-spanish-texts/feat-spanish-texts-enriched.md)
