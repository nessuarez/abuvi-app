# Bug: Inscripciones existentes en BD no se muestran en la web

## Contexto

El usuario reporta que tiene 2 registros de inscripciones en la base de datos pero no consigue listarlas en la pagina "Mis Inscripciones".

## Analisis de causa raiz

Se identificaron **dos posibles causas** que deben verificarse:

### Causa 1 (mas probable): Filtro frontend excluye status `Draft`

**Archivo:** `frontend/src/views/registrations/RegistrationsPage.vue:15-21`

```typescript
const sortedRegistrations = computed<RegistrationResponse[]>(() => {
  const active = registrations.value.filter(
    (r) => r.status === 'Pending' || r.status === 'Confirmed'
  )
  const cancelled = registrations.value.filter((r) => r.status === 'Cancelled')
  return [...active, ...cancelled]
})
```

Este computed **solo incluye** `Pending`, `Confirmed` y `Cancelled`. Si las inscripciones estan en status `Draft` (por ejemplo, porque un Admin las edito via `PUT /api/registrations/{id}/admin-edit`), quedan excluidas de la lista.

**Efecto adicional:** El empty state (linea 56) verifica `registrations.value.length === 0` (datos crudos de la API), pero la lista renderiza `sortedRegistrations`. Si las 2 inscripciones son Draft:

- `registrations.length = 2` -> no muestra el mensaje "Todavia no tienes inscripciones"
- `sortedRegistrations.length = 0` -> no renderiza ninguna tarjeta
- **Resultado:** el usuario ve un area vacia sin mensaje explicativo

### Causa 2: La API no devuelve datos (problema de family unit)

**Archivo:** `src/Abuvi.API/Features/Registrations/RegistrationsService.cs:522-525`

```csharp
var familyUnit = await familyUnitsRepo.GetFamilyUnitByRepresentativeIdAsync(userId, ct);
if (familyUnit is null) return [];
```

Si el usuario autenticado no es el representante de la family unit asociada a las inscripciones, la API devuelve una lista vacia. Esto podria pasar si:

- El usuario logueado no es el representante de la unidad familiar
- La relacion entre usuario y family unit se rompio o nunca se establecio correctamente

## Pasos de verificacion

1. **Verificar el status de las 2 inscripciones en BD:**

   ```sql
   SELECT id, family_unit_id, status, created_at FROM registrations ORDER BY created_at DESC LIMIT 5;
   ```

2. **Verificar que el usuario logueado es representante de la family unit:**

   ```sql
   SELECT fu.id, fu.name, fu.representative_user_id, u.email
   FROM family_units fu
   JOIN users u ON u.id = fu.representative_user_id
   WHERE fu.id = '<family_unit_id_from_step_1>';
   ```

3. **Probar la API directamente:** Hacer GET a `/api/registrations` con el token del usuario y verificar si la respuesta contiene datos.

## Solucion propuesta

### Si la causa es el filtro de Draft (Causa 1)

**Archivo a modificar:** `frontend/src/views/registrations/RegistrationsPage.vue`

Incluir `Draft` en el computed `sortedRegistrations`:

```typescript
const sortedRegistrations = computed<RegistrationResponse[]>(() => {
  const active = registrations.value.filter(
    (r) => r.status === 'Pending' || r.status === 'Confirmed' || r.status === 'Draft'
  )
  const cancelled = registrations.value.filter((r) => r.status === 'Cancelled')
  return [...active, ...cancelled]
})
```

Adicionalmente, verificar que el componente `RegistrationCard` maneja correctamente el status `Draft` (badge, colores, acciones disponibles).

**Archivo a revisar:** `frontend/src/components/registrations/RegistrationCard.vue`

### Si la causa es la API (Causa 2)

Investigar por que `GetFamilyUnitByRepresentativeIdAsync` no encuentra la family unit del usuario y corregir la relacion en BD o el query.

## Archivos involucrados

| Archivo | Rol |
|---------|-----|
| `frontend/src/views/registrations/RegistrationsPage.vue` | Vista principal - filtro `sortedRegistrations` |
| `frontend/src/composables/useRegistrations.ts` | Composable - llamada API `fetchMyRegistrations` |
| `frontend/src/components/registrations/RegistrationCard.vue` | Componente tarjeta - manejo visual de status |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Servicio - `GetByFamilyUnitAsync` |
| `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs` | Repositorio - query sin filtro de status |
| `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs` | Endpoint `GET /api/registrations/` |

## Criterios de aceptacion

- [ ] Las inscripciones en cualquier status (Pending, Confirmed, Draft, Cancelled) se muestran en "Mis Inscripciones"
- [ ] Las inscripciones en Draft muestran un badge/indicador visual claro de su estado
- [ ] Si no hay inscripciones en ningun status, se muestra el empty state correctamente
- [ ] El orden de visualizacion es: activas (Pending, Confirmed, Draft) primero, canceladas al final
