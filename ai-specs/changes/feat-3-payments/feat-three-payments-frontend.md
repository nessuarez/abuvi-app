# Plan Frontend: Edición de Inscripción + Desglose de 3 Plazos

## Dependencia con el plan de backend

Este plan depende de `feat-three-payments-system.md` (sistema de 3 plazos de pago).

**Lo que el backend aporta a este plan**:

- `GET /registrations/:id/payments` ya devuelve todos los pagos incluido P3 cuando existe (`InstallmentNumber == 3`)
- `PUT /registrations/:id/members` y `POST /registrations/:id/extras` ya retornan error 422 si hay un justificante subido
- P3 se crea/actualiza/elimina automáticamente al llamar `setExtras()`
- No se requieren nuevas llamadas API desde el frontend

**Bloqueo**: El formulario de edición debe **desactivarse** si algún pago tiene `proofFileUrl != null`. El backend devuelve 422 si la familia intenta editar en ese caso, pero la UI también debe ocultarlo proactivamente para buena UX.

---

## Estado actual

- `RegistrationDetailPage.vue` es **solo lectura** — no tiene modo edición
- Los componentes de selección de miembros (`RegistrationMemberSelector`) y extras (`RegistrationExtrasSelector`) **ya existen** y se usan en el wizard de creación
- Los métodos API `updateMembers()` y `setExtras()` **ya existen** en `useRegistrations`
- `RegistrationPricingBreakdown` ya muestra el desglose de importe (miembros + extras + total)
- `PaymentInstallmentCard` ya renderiza un pago — P3 funcionará automáticamente si el backend lo devuelve

---

## Contexto de archivos clave

| Archivo | Rol |
|---------|-----|
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | **MODIFICAR** — Añadir modo edición y desglose de plazos |
| `frontend/src/components/registrations/RegistrationMemberSelector.vue` | REUTILIZAR (sin cambios) |
| `frontend/src/components/registrations/RegistrationExtrasSelector.vue` | REUTILIZAR (sin cambios) |
| `frontend/src/components/registrations/RegistrationPricingBreakdown.vue` | REUTILIZAR para desglose |
| `frontend/src/composables/useRegistrations.ts` | REUTILIZAR — `updateMembers()`, `setExtras()` ya existen |
| `frontend/src/types/registration.ts` | LEER — tipos existentes |
| `frontend/src/types/payment.ts` | LEER — `PaymentResponse` (ya tiene `dueDate`, `installmentNumber`) |

---

## Features a implementar

### Feature 1 — Indicador de estado de edición

Computed `canEdit` que determina si la familia puede editar:

```typescript
const canEdit = computed(() => {
  if (!registration.value) return false
  const status = registration.value.status
  // Solo Pending o Draft (admin modificó algo)
  if (status !== 'Pending' && status !== 'Draft') return false
  // Solo el representante puede editar
  if (!isRepresentative.value) return false
  // Bloqueado si algún pago tiene justificante subido
  return !installments.value.some(p => p.proofFileUrl != null)
})
```

Mostrar un `Message severity="info"` contextual cuando `isDraft` para indicar al representante que el admin ha modificado la inscripción.

### Feature 2 — Modo edición de miembros

Botón "Editar inscripción" visible solo si `canEdit`. Al pulsarlo, muestra `RegistrationMemberSelector` con los miembros actuales pre-cargados.

**Inicializar estado desde `registration.value.members`**:

```typescript
const memberSelections = ref<WizardMemberSelection[]>(
  registration.value?.members.map(m => ({
    memberId: m.id,
    attendancePeriod: m.attendancePeriod,
    visitStartDate: m.visitStartDate ?? null,
    visitEndDate: m.visitEndDate ?? null,
    guardianName: m.guardianName ?? null,
    guardianDocumentNumber: m.guardianDocumentNumber ?? null,
  })) ?? []
)
```

**Guardar**:

```typescript
const savingMembers = ref(false)
const handleSaveMembers = async () => {
  savingMembers.value = true
  const result = await updateMembers(registrationId.value, { members: memberSelections.value })
  savingMembers.value = false
  if (result) {
    registration.value = result
    await refreshInstallments()
    isEditingMembers.value = false
    toast.add({ severity: 'success', summary: 'Éxito', detail: 'Miembros actualizados', life: 3000 })
  } else {
    toast.add({ severity: 'error', summary: 'Error', detail: error.value, life: 5000 })
  }
}
```

### Feature 3 — Modo edición de extras

Misma mecánica que Feature 2 pero con `RegistrationExtrasSelector` y `setExtras()`.

**Diferencia clave**: Tras guardar, refrescar los plazos (`installments`) porque P3 puede haberse creado, actualizado o eliminado.

### Feature 4 — Desglose de plazos (P1, P2, P3)

Sección debajo de `RegistrationPricingBreakdown` que muestra cómo se distribuyen los plazos:

```
Plazo 1 — Primer pago            €X    [vence: dd/mm/yyyy]  [estado]
Plazo 2 — Segundo pago           €X    [vence: dd/mm/yyyy]  [estado]
Plazo 3 — Extras (si existe P3)  €X    [vence: dd/mm/yyyy]  [estado]
```

Los plazos ya vienen de `installments.value` (array `PaymentResponse[]`). Solo hay que renderizarlos ordenados por `installmentNumber`. `PaymentInstallmentCard` ya existe — usarla directamente.

**Etiqueta de P3**: Si `installmentNumber === 3`, mostrar "Pago de extras" como label adicional.

### Feature 5 — Limpieza de bugs existentes en RegistrationDetailPage.vue

Hay código duplicado en el archivo actual (detectado al explorar):

- `RegistrationDeleteDialog` importado dos veces
- `showDeleteDialog` y `deleting` declarados dos veces
- `handleDelete` definido dos veces

Limpiar al tocar el archivo.

---

## Flujo UX propuesto

```
RegistrationDetailPage (modo lectura)
  ├── Header: nombre del campamento, estado badge
  ├── [si Draft] Message info: "El administrador ha modificado tu inscripción. Revísala."
  ├── RegistrationPricingBreakdown (desglose miembros + extras)
  ├── Sección Plazos de pago:
  │     ├── PaymentInstallmentCard (P1)
  │     ├── PaymentInstallmentCard (P2)
  │     └── PaymentInstallmentCard (P3) ← solo si existe
  ├── [si canEdit] Botón "Editar miembros" → isEditingMembers = true
  ├── [si isEditingMembers] RegistrationMemberSelector + Guardar/Cancelar
  ├── [si canEdit] Botón "Editar extras" → isEditingExtras = true
  ├── [si isEditingExtras] RegistrationExtrasSelector + Guardar/Cancelar
  └── Botones: Cancelar inscripción, Eliminar
```

---

## Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `frontend/src/views/registrations/RegistrationDetailPage.vue` | Añadir `canEdit`, `isEditingMembers`, `isEditingExtras`, Feature 2-4, limpiar duplicados |

No se crean nuevos componentes ni rutas.

---

## Dependencias de datos

La `RegistrationResponse` que devuelve el backend ya incluye:

- `members[]` con `attendancePeriod`, `visitStartDate`, `visitEndDate`, `guardianName`, `guardianDocumentNumber`
- `extras[]` con `campEditionExtraId`, `quantity`, `userInput`
- `status`

Si alguno de estos campos falta en el tipo TypeScript actual, añadirlos a `frontend/src/types/registration.ts`.

`PaymentResponse` ya incluye `dueDate`, `installmentNumber`, `proofFileUrl`, `status` — suficiente para todo.

---

## Verificación

```bash
# Frontend: compilación limpia
cd frontend && npm run build

# Probar manualmente:
# 1. Abrir inscripción Pending sin justificantes → botón "Editar miembros" visible
# 2. Cambiar miembros → P1 y P2 se actualizan (refrescar plazos)
# 3. Añadir extras → P3 aparece en la sección de plazos
# 4. Quitar todos los extras → P3 desaparece
# 5. Subir justificante de P1 → botón "Editar" desaparece
# 6. Inscripción Draft → mensaje informativo visible + edición disponible (sin justificantes)
# 7. Inscripción Confirmed → botón editar no visible
```
