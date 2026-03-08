# User Story: Permitir a Admin y Junta ver detalle de Unidades Familiares

## Resumen

Como usuario con rol **Admin** o **Board (Junta Directiva)**, necesito poder acceder al detalle de cualquier unidad familiar desde el panel de administracion, para consultar la informacion completa de la familia y sus miembros.

Actualmente, el panel de administracion (`FamilyUnitsAdminPanel.vue`) muestra la lista de unidades familiares pero el boton "Ver detalle" esta deshabilitado (`disabled`). No existe una ruta `/family-unit/:id` en el frontend, y la pagina `FamilyUnitPage.vue` solo carga la unidad familiar del usuario autenticado via `GET /family-units/me`.

**El backend ya soporta el acceso**: los endpoints `GET /family-units/{id}` y `GET /family-units/{familyUnitId}/members` permiten acceso a Admin/Board. Solo se requieren cambios en el frontend.

---

## Criterios de Aceptacion

### AC1 - Navegacion desde el panel de administracion

- El boton "Ver detalle" (icono ojo) en `FamilyUnitsAdminPanel.vue` deja de estar deshabilitado.
- Al hacer clic, navega a `/family-unit/:id` donde `:id` es el ID de la unidad familiar seleccionada.

### AC2 - Vista de detalle de unidad familiar ajena

- La ruta `/family-unit/:id` carga la unidad familiar por ID usando `GET /family-units/{id}` y sus miembros con `GET /family-units/{familyUnitId}/members`.
- La vista muestra la misma informacion que `FamilyUnitPage.vue`: datos de la familia y tabla de miembros.
- **Modo solo lectura**: cuando el usuario autenticado **no es el representante** de la unidad familiar, se ocultan los botones de edicion/eliminacion/creacion:
  - No se muestra "Editar" ni "Eliminar" en la tarjeta de la unidad familiar.
  - No se muestra "Anadir Miembro".
  - No se muestran las acciones de editar/eliminar en cada fila de miembro.
- **Gestion de membresias**: los botones de "Activar membresia familiar" y "Gestionar membresia" por miembro **si se muestran** para Admin/Board (ya que esto es funcionalidad administrativa existente).

### AC3 - Titulo y contexto de la pagina

- Cuando se accede a `/family-unit/:id` (unidad ajena), el titulo de la pagina debe ser "ABUVI | Unidad Familiar" (no "Mi Unidad Familiar").
- Se muestra un boton o enlace para volver al panel de administracion.

### AC4 - Ruta `/family-unit` sin parametro

- La ruta existente `/family-unit` (sin `:id`) sigue funcionando como siempre: carga la unidad familiar del usuario autenticado via `/family-units/me`.
- No se rompe ninguna funcionalidad existente.

---

## Implementacion Tecnica

### Backend

**No se requieren cambios en el backend.** Los endpoints necesarios ya existen y autorizan correctamente a Admin/Board:

- `GET /family-units/{id}` - Permite acceso al representante O Admin/Board (linea 178-181 de `FamilyUnitsEndpoints.cs`)
- `GET /family-units/{familyUnitId}/members` - Permite acceso al representante O Admin/Board (linea 296-298 de `FamilyUnitsEndpoints.cs`)

### Frontend

#### 1. Nueva ruta: `/family-unit/:id`

**Archivo:** `frontend/src/router/index.ts`

Anadir una nueva ruta parametrizada despues de la ruta existente `/family-unit`:

```typescript
{
  path: "/family-unit/:id",
  name: "family-unit-detail",
  component: () => import("@/views/FamilyUnitPage.vue"),
  meta: {
    requiresAuth: true,
    requiresBoard: true,
    title: "ABUVI | Unidad Familiar"
  }
}
```

**Nota:** `requiresBoard: true` para que solo Admin/Board puedan acceder a la ruta parametrizada. La ruta `/family-unit` (sin parametro) sigue accesible para cualquier usuario autenticado.

#### 2. Modificar `FamilyUnitPage.vue`

**Archivo:** `frontend/src/views/FamilyUnitPage.vue`

Cambios:

1. Importar `useRoute` de `vue-router`.
2. En `onMounted`, comprobar si hay un parametro `id` en la ruta:
   - Si existe `route.params.id`: llamar a `getFamilyUnitById(id)` y luego `getFamilyMembers(id)`.
   - Si no: mantener el comportamiento actual con `getCurrentUserFamilyUnit()`.
3. Crear una computed `isReadOnly` que sea `true` cuando el usuario autenticado no es el representante de la unidad familiar cargada:
   ```typescript
   const isViewingOther = computed(() => !!route.params.id && familyUnit.value?.representativeUserId !== auth.user?.id)
   ```
4. Usar `isViewingOther` para ocultar condicionalmente:
   - Botones "Editar" y "Eliminar" en la tarjeta de unidad familiar.
   - Boton "Anadir Miembro".
   - Props `@edit` y `@delete` en `FamilyMemberList` (o no pasarlos / ocultar las columnas de accion).
5. Cambiar el titulo `<h1>` segun contexto:
   - Si `isViewingOther`: "Unidad Familiar" (sin "Mi").
   - Si no: "Mi Unidad Familiar".
6. Si `isViewingOther`, mostrar un boton "Volver a Administracion" que navegue a `/admin`.

#### 3. Habilitar boton "Ver detalle" en `FamilyUnitsAdminPanel.vue`

**Archivo:** `frontend/src/components/admin/FamilyUnitsAdminPanel.vue`

Cambios:

1. Importar `useRouter` de `vue-router`.
2. Eliminar el atributo `disabled` del boton.
3. Anadir `@click="router.push(`/family-unit/${data.id}`)"` al boton.
4. Pasar `data` al template del boton (ya esta disponible via `#body="{ data }"`).

```vue
<Button
  icon="pi pi-eye"
  text
  rounded
  severity="info"
  aria-label="Ver detalle"
  @click="router.push(`/family-unit/${data.id}`)"
/>
```

#### 4. Modificar `FamilyMemberList.vue` (opcional)

**Archivo:** `frontend/src/components/family-units/FamilyMemberList.vue`

Anadir una prop `readOnly` (booleano, default `false`) que oculte las columnas de accion (editar/eliminar) cuando es `true`.

---

## Archivos a Modificar

| Archivo | Cambio |
|---|---|
| `frontend/src/router/index.ts` | Anadir ruta `/family-unit/:id` con `requiresBoard` |
| `frontend/src/views/FamilyUnitPage.vue` | Soportar carga por ID, modo solo lectura, titulo dinamico |
| `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` | Habilitar boton "Ver detalle" con navegacion |
| `frontend/src/components/family-units/FamilyMemberList.vue` | Prop `readOnly` para ocultar acciones de edicion |

---

## Requisitos No Funcionales

### Seguridad

- La ruta `/family-unit/:id` requiere rol Board o Admin (guard en router).
- El backend ya valida autorizacion por rol en los endpoints de lectura.
- No se exponen datos sensibles (notas medicas, alergias) — solo flags booleanos, como ya esta implementado.

### UX

- El boton "Ver detalle" en el admin panel debe dar feedback visual al hover.
- La vista read-only no debe mostrar botones de accion deshabilitados — simplemente no mostrarlos.
- Incluir breadcrumb o boton de regreso al panel admin.

---

## Definicion de Hecho

1. [ ] Ruta `/family-unit/:id` configurada con guard `requiresBoard`
2. [ ] `FamilyUnitPage.vue` carga unidad familiar por ID cuando hay parametro en la ruta
3. [ ] Vista en modo solo lectura cuando el usuario no es el representante (sin botones de edicion/eliminacion/creacion)
4. [ ] Botones de gestion de membresias visibles para Admin/Board en modo lectura
5. [ ] Titulo de pagina dinamico ("Mi Unidad Familiar" vs "Unidad Familiar")
6. [ ] Boton "Volver a Administracion" visible en modo lectura
7. [ ] Boton "Ver detalle" habilitado y funcional en `FamilyUnitsAdminPanel.vue`
8. [ ] Ruta `/family-unit` (sin parametro) sigue funcionando sin cambios
9. [ ] Prop `readOnly` en `FamilyMemberList.vue` oculta acciones de edicion
10. [ ] Tests de componente actualizados si existen
