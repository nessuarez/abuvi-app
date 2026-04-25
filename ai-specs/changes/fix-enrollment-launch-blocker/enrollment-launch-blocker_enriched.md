# Especificación Enriquecida: Desbloquear Inscripciones en Lanzamiento

**Status:** Análisis & Propuesta  
**Prioridad:** CRÍTICA — Bloquea lanzamiento de aplicación  
**Contexto:** App en fase de lanzamiento. Muchos usuarios no pueden completar inscripciones por validación de cuota.

---

## Pregunta del Usuario

> _Algunos usuarios no consiguen finalizar la inscripción y todos reciben el mismo mensaje: "La cuota de membresía del año en curso no está pagada. Contacte con el administrador"._
>
> **¿Son estas las condiciones correctas?**
> - Membresía debe estar activa
> - Cuota 2026 pagada para **todos** los miembros de la unidad familiar
>
> **¿Qué hacemos en lanzamiento?** ¿Aceptamos inscripciones marcando familias como pendientes de confirmar membresía y cobro de cuota?

---

## Análisis de Condiciones de Inscripción (Actual)

### Validaciones Actuales en `RegistrationsService.cs:36-41`

```csharp
var hasPaidCurrentYearFee = await membershipsRepo
    .HasPaidCurrentYearFeeForFamilyAsync(request.FamilyUnitId, ct);
if (!hasPaidCurrentYearFee)
    throw new BusinessRuleException(
        "La cuota de membresía del año en curso no está pagada. Contacte al administrador.");
```

### Definición de `HasPaidCurrentYearFeeForFamilyAsync`

La consulta requiere:
1. **Al menos un miembro de la familia** con membresía (`Membership.IsActive == true`)
2. **Cuota pagada** (`MembershipFee.Status == Paid`)
3. **Año actual** (`MembershipFee.Year == DateTime.UtcNow.Year`)

### ⚠️ PROBLEMA CRÍTICO

La validación actual es **"al menos un miembro con cuota pagada"**, pero el mensaje ("*La cuota* ... no está pagada") puede inducir a error, sugiriendo que todos los miembros necesitan cuota pagada.

**Respuesta a tu pregunta:**

| Afirmación | ¿Correcto? | Aclaración |
|-----------|-----------|-----------|
| Membresía debe estar activa | ✅ Sí | Pero solo **un miembro** de la familia necesita membresía activa |
| Cuota 2026 pagada para **todos** los miembros | ❌ No | Solo se requiere **cuota pagada para al menos un miembro** |

---

## Root Cause: Por qué los usuarios no pueden inscribirse

### Escenario 1: La familia fue creada DESPUÉS del 1 enero 2026

- ✅ Membresías se crearon exitosamente para todos/algunas personas
- ❌ **Ninguna `MembershipFee` se creó automáticamente** (el `AnnualFeeGenerationService` solo corre el 1 de enero)
- ❌ Usuario intenta inscribirse → validación falla porque no hay cuota pagada para ningún año

**Lógica actual en [MembershipsService.cs:48](src/Abuvi.API/Features/Memberships/MembershipsService.cs#L48):**
- `CreateAsync` crea el registro `Membership` pero **NO** crea automáticamente `MembershipFee` para 2026
- El admin tendría que ir a "Administración → Membresías → Crear Cuota Anual" pero **ese endpoint no existe todavía**

### Escenario 2: La familia existía pero no tenía cuota pagada

- Membresías existen y están activas
- Cuota existe pero está en estado `Pending` (no pagada)
- Usuario intenta inscribirse → falla porque `Status != Paid`

### Escenario 3: Intento de "reactivar" membresía desactivada

- Admin intenta crear membresía para un miembro que ya tiene `Membership.IsActive=false`
- Backend lanza error único constraint (no puede crear duplicado)
- No existe endpoint para reactivar

---

## Propuesta de Solución para Lanzamiento (CORREGIDA)

Tu enfoque es **mucho mejor**. El sistema ya tiene toda la infraestructura lista:

- ✅ `RegistrationStatus.Pending` existe en el modelo
- ✅ Inscripciones se crean como `Pending` (línea 154 de RegistrationsService.cs)
- ✅ `SendCampRegistrationConfirmationAsync` ya envía email de confirmación
- ✅ `SendPaymentReceiptAsync` ya notifica pagos

### OPCIÓN RECOMENDADA ⭐: Inscripción Pendiente con Validación Asíncrona

**Flujo propuesto:**

1. **Usuario se inscribe sin validación de cuota pagada**
   - Modificar [RegistrationsService.cs:36-41](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L36-L41): cambiar de `throw` a `log warning`
   - Inscripción se crea con `Status = Pending` (ya es así por defecto)
   - Se procede con creación de pagos y todo lo demás

2. **Email de confirmación CON DISCLAIMER**
   - Modificar plantilla en [ResendEmailService.cs:217-268](src/Abuvi.API/Common/Services/ResendEmailService.cs#L217-L268)
   - Agregar sección destacada:
     ```html
     <div style='background-color: #fef3c7; border: 2px solid #f59e0b; border-radius: 8px; padding: 20px; margin: 25px 0;'>
         <h3 style='color: #92400e; margin-top: 0;'>⚠️ INSCRIPCIÓN PENDIENTE DE VERIFICACIÓN</h3>
         <p>Tu inscripción está registrada pero aún está pendiente de validación 
            por parte de la organización. Verificaremos que tu membresía y cuota 
            de 2026 estén al día en las próximas horas.</p>
         <p><strong>¿Qué ocurre si algo no está en orden?</strong></p>
         <ul>
            <li>Te enviaremos un email notificándote qué falta</li>
            <li>Podrás completar los pagos pendientes desde tu cuenta</li>
            <li>Una vez validado, tu inscripción estará confirmada</li>
         </ul>
     </div>
     ```

3. **Panel de Admin: "Inscripciones Pendientes"**
   - Nueva vista mostrando inscripciones con `Status = Pending`
   - Para cada inscripción: mostrar estado de membresía + cuota de cada miembro
   - Acciones:
     - ✅ **Confirmar** → cambiar a `Confirmed` (si todo está en orden)
     - ❌ **Rechazar** → cambiar a `Cancelled` con mensaje al usuario
     - 📧 **Enviar recordatorio** → email con qué falta completar

4. **Auto-verificación (opcional, más tarde)**
   - Background job que revisa inscripciones `Pending`
   - Si membresía + cuota pagada para todos → cambia a `Confirmed` automáticamente
   - Si no → marca como requiriendo intervención manual

5. **Notificaciones de pago**
   - Ya existe `SendPaymentReceiptAsync` en [ResendEmailService.cs:467](src/Abuvi.API/Common/Services/ResendEmailService.cs#L467)
   - Se envía cuando pago se marca como `Completed`
   - **Mejora:** Agregar notificación cuando inscripción pasa de `Pending` → `Confirmed`

**Ventajas:**
- ✅ Lanzamiento sin bloqueos — usuarios se inscriben inmediatamente
- ✅ Control de admin — verifica después en lote o automáticamente
- ✅ Experiencia usuario mejorada — aviso claro + recordatorio
- ✅ Cumple modelo de negocio — inscripciones verificadas antes de confirmar
- ✅ Auditable — registro completo de cuándo y por qué se confirmó cada inscripción
- ✅ Escalable — se puede automatizar después con background job

**Desventajas:**
- Requiere panelo admin nueva (pero simple)
- Requiere cambio en plantilla email

---

## RECOMENDACIÓN

Implementar en dos fases:

### Fase 1: URGENTE (hoy) — 1.5h
Desbloquear inscripciones en lanzamiento:

1. **Modificar validación en [RegistrationsService.cs:36-41](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L36-L41):**
   - Cambiar `throw new BusinessRuleException(...)` a `_logger.LogWarning(...)`
   - Continuar con flujo de inscripción normalmente
   - Agregar nota en `registration.Notes`: "Inscripción creada pendiente de validación de cuota"

2. **Actualizar email de confirmación en [ResendEmailService.cs](src/Abuvi.API/Common/Services/ResendEmailService.cs)**
   - Agregar sección de disclaimer sobre inscripción pendiente

3. **Auto-crear `MembershipFee` al crear membresía** (si aún no existe)
   - Agregar en [MembershipsService.cs:CreateAsync](src/Abuvi.API/Features/Memberships/MembershipsService.cs#L48)

### Fase 2: Semana 2 — 3h
Panel de admin + automatización:

1. Crear vista admin "Inscripciones Pendientes" con filtros y acciones
2. Background job para auto-confirmar inscripciones cuando cumplen condiciones
3. Email de notificación cuando pasa de Pending → Confirmed

**Estimado de esfuerzo:** 1.5h (Fase 1) + 3h (Fase 2) = 4.5h total

---

## Cambios Técnicos Requeridos

### Fix 1: Auto-crear `MembershipFee` en membresía (URGENTE)

**Archivo:** [src/Abuvi.API/Features/Memberships/MembershipsService.cs](src/Abuvi.API/Features/Memberships/MembershipsService.cs)

**Ubicación:** Dentro de `CreateAsync`, después de `await repository.AddAsync(membership, ct);`

```csharp
// Auto-create initial fee for current year
var currentYear = DateTime.UtcNow.Year;
var fee = new MembershipFee
{
    Id = Guid.NewGuid(),
    MembershipId = membership.Id,
    Year = currentYear,
    Amount = 0m,  // Admin actualiza si es necesario
    Status = FeeStatus.Pending,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
await repository.AddFeeAsync(fee, ct);
```

Aplicar el mismo cambio en `BulkActivateAsync` (después del loop).

**Tests a agregar:**
- `CreateAsync_ShouldAutoCreatePendingFeeForCurrentYear`
- `BulkActivateAsync_ShouldAutoCreateFeeForEachMember`

---

### Fix 2: Endpoint para marcar cuota como pagada (sin pago real)

**Nuevo endpoint:**
```
POST /api/fees/{feeId}/mark-paid
Body: {} (vacío)
Response: 200 OK
```

**Autorización:** Solo Admin o Board  
**Validación:**
- Si fee no existe → 404
- Si fee ya está `Paid` → 409 Conflict
- Si fee está `Pending` → cambiar a `Paid`

**Archivo a crear:**
- [src/Abuvi.API/Features/Memberships/MarkMembershipFeeAsPaidRequest.cs](src/Abuvi.API/Features/Memberships/)
- [src/Abuvi.API/Features/Memberships/MarkMembershipFeeAsPaidHandler.cs](src/Abuvi.API/Features/Memberships/) (o agregar a `MembershipsService`)

**Archivos a modificar:**
- [MembershipsEndpoints.cs](src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs) — registrar endpoint

**Lógica en MembershipsService:**
```csharp
public async Task MarkFeeAsPaidAsync(Guid feeId, CancellationToken ct)
{
    var fee = await repository.GetFeeByIdAsync(feeId, ct)
        ?? throw new NotFoundException("Cuota", feeId);
    
    if (fee.Status == FeeStatus.Paid)
        throw new BusinessRuleException("La cuota ya está marcada como pagada");
    
    fee.Status = FeeStatus.Paid;
    fee.UpdatedAt = DateTime.UtcNow;
    await repository.UpdateFeeAsync(fee, ct);
}
```

---

### Fix 3: Mensaje de error más claro

**Archivo:** [src/Abuvi.API/Features/Registrations/RegistrationsService.cs:40-41](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L40-L41)

**Cambio:**
```csharp
throw new BusinessRuleException(
    "La unidad familiar no tiene cuota de membresía pagada para 2026. " +
    "El administrador puede marcarla como pagada en la interfaz de administración.");
```

---

## Acceptance Criteria

- [ ] `MembershipsService.CreateAsync` auto-genera `MembershipFee(Pending)` para el año actual al crear membresía
- [ ] `MembershipsService.BulkActivateAsync` auto-genera `MembershipFee(Pending)` para cada miembro activado
- [ ] Endpoint `POST /api/fees/{feeId}/mark-paid` existe y cambia estado de `Pending` → `Paid`
- [ ] Solo Admin/Board pueden llamar al endpoint de marcar como pagada
- [ ] Mensaje de error en inscripción es más específico y guía al usuario
- [ ] Unit tests para auto-generación de cuota
- [ ] Integration test: crear membresía → auto-genera fee pending → marcar como pagada → inscribirse exitosamente

---

## Propuesta de Flujo Post-Lanzamiento

Una vez que el lanzamiento esté estable:

1. Implementar los fixes descriptos en [fix-membership-enrollment_enriched.md](ai-specs/changes/fix-membership-enrollment/fix-membership-enrollment_enriched.md):
   - Endpoint para crear cuota anual manualmente (`POST /api/memberships/{id}/fees`)
   - Endpoint para reactivar membresía (`POST /api/memberships/{id}/reactivate`)
   - Mejorar UI para mostrar estado de cuota por miembro

2. Integración con sistema de pagos real (si no está ya integrado)

3. Documentación para usuarios finales sobre cuotas y membresía

---

## Timeline Propuesto

| Etapa | Duración | Acción |
|-------|----------|--------|
| **HOY** | 2h | Implementar Fix 1 + Fix 2 (auto-crear fee + marcar como pagada) |
| **Mañana** | 1h | Testing + deploy a staging |
| **Lanzamiento** | N/A | Monitor de inscripciones; admin manualmente marca cuotas como pagadas |
| **Post-lanzamiento (Semana 2)** | 4h | Implementar fixes completos de [fix-membership-enrollment_enriched.md](ai-specs/changes/fix-membership-enrollment/fix-membership-enrollment_enriched.md) |

---

## Resumen Ejecutivo

**Tu pregunta:** ¿Aceptar inscripciones pendientes de verificación de cuota?

**Respuesta:** No necesario. Con los fixes mínimos de hoy:
1. Membresías auto-generan cuota al crear
2. Admin marca como pagada en 1 clic
3. Usuario se inscribe inmediatamente

Esto es más simple que implementar un sistema completo de "inscripciones pendientes" y mantiene el control de admin sobre el proceso de cobro.
