# Enriched Bug Report: Membership Enrollment & Activation Issues

**Status:** Ready for Implementation
**Priority:** High
**Architecture:** Vertical Slice — `src/Abuvi.API/Features/Memberships/`

---

## Context & Problem Summary

There are two interrelated issues with the membership system:

1. **Registro bloqueado cuando no hay membresía activa** — Al intentar inscribirse al campamento, el sistema comprueba que exista una cuota pagada (`MembershipFee.Status == Paid`) para el año en curso. Si no existe cuota pagada (o si el registro de `MembershipFee` directamente no existe), la inscripción lanza `BusinessRuleException` con el mensaje "La cuota de membresía del año en curso no está pagada." Esto es correcto por diseño, pero **falta el flujo que permite crear/cobrar esa cuota para una membresía ya activa**.

2. **"El miembro ya tiene una membresía activa" al intentar activar una familia no al corriente** — Cuando una familia tiene membresías con `IsActive=true` pero sin cuota pagada para el año en curso, el frontend muestra los miembros como "sin alta de socio/a" (o permite iniciar la activación). Al llamar a `POST /membership` el backend lanza la excepción porque el miembro ya tiene un registro `Membership` con `IsActive=true`. El sistema no puede crear un duplicado (constraint único en `FamilyMemberId`), y además **no existe ningún endpoint para generar la cuota anual manualmente**.

---

## Root Cause Analysis

### Root Cause 1 — Membership creation does not auto-generate first-year fee

**File:** [MembershipsService.cs:10-48](src/Abuvi.API/Features/Memberships/MembershipsService.cs#L10-L48)

`CreateAsync` crea el registro `Membership` pero no crea ningún `MembershipFee` para el año de inicio. El servicio automatizado (`AnnualFeeGenerationService`) solo ejecuta el 1 de enero. Por tanto:

- Un miembro inscrito en 2026 (después del 1 de enero) tendrá membresía activa pero sin cuota del año 2026.
- No podrá inscribirse al campamento hasta que el admin cree y marque como pagada su cuota para 2026.
- No existe ningún endpoint para crear esa cuota.

### Root Cause 2 — No endpoint to manually create/charge annual fee

**File:** [MembershipsRepository.cs](src/Abuvi.API/Features/Memberships/MembershipsRepository.cs)

`AddFeeAsync` existe en el repositorio pero ningún endpoint del servicio lo expone. El único flujo de creación de cuotas es el background service (`AnnualFeeGenerationService`). No hay endpoint para "cargar cuota anual" a una membresía existente.

### Root Cause 3 — No "reactivate" endpoint for deactivated memberships

**File:** [MembershipsService.cs:21-23](src/Abuvi.API/Features/Memberships/MembershipsService.cs#L21-L23)

`GetByFamilyMemberIdAsync` filtra por `m.IsActive == true`. Si una membresía tiene `IsActive=false`, la query devuelve `null` y `CreateAsync` intenta insertar un nuevo registro. Sin embargo, el modelo tiene una constraint única en `FamilyMemberId` (one-to-one), por lo que la inserción fallaría en la base de datos. No existe endpoint de reactivación.

### Root Cause 4 — Frontend conflates "sin alta" with "no al corriente"

El modal "Activar membresía familiar" muestra "N miembro(s) sin alta de socio/a" pero el conteo puede estar basado en la ausencia de cuota pagada (lógica de `HasPaidCurrentYearFeeForFamilyAsync`) en lugar de la ausencia de un registro `Membership` activo. Esto lleva al admin a intentar crear membresías para miembros que ya las tienen.

---

## Registration Restriction (current behavior)

**File:** [RegistrationsService.cs:36-41](src/Abuvi.API/Features/Registrations/RegistrationsService.cs#L36-L41)

```csharp
// 1c. Validate current year membership fee is paid
var hasPaidCurrentYearFee = await membershipsRepo
    .HasPaidCurrentYearFeeForFamilyAsync(request.FamilyUnitId, ct);
if (!hasPaidCurrentYearFee)
    throw new BusinessRuleException(
        "La cuota de membresía del año en curso no está pagada. Contacte al administrador.");
```

`HasPaidCurrentYearFeeForFamilyAsync` requiere al menos un `MembershipFee` con `Status == Paid`, `Year == currentYear`, y la membresía correspondiente con `IsActive == true`. La restricción es correcta, pero **el flujo para satisfacerla está incompleto**.

---

## Required Changes

### Fix 1 — Auto-create `MembershipFee` on membership creation

**File:** [MembershipsService.cs](src/Abuvi.API/Features/Memberships/MembershipsService.cs)

Al crear una membresía en `CreateAsync`, generar automáticamente la `MembershipFee` para el año de inicio con `Status = Pending` (el admin la marcará como pagada posteriormente). Lo mismo aplica a `BulkActivateAsync`.

**Lógica a agregar** después de `await repository.AddAsync(membership, ct)`:
```csharp
var fee = new MembershipFee
{
    Id = Guid.NewGuid(),
    MembershipId = membership.Id,
    Year = request.Year,
    Amount = 0m,   // El admin actualiza el importe al registrar el pago
    Status = FeeStatus.Pending,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};
await repository.AddFeeAsync(fee, ct);
```

> **Nota:** El campo `Amount` puede quedar en 0 hasta que se configure el importe de cuota. Evaluar si el importe de cuota debe ser configurable a nivel de sistema (actualmente no existe esa configuración).

### Fix 2 — New endpoint: Create annual fee for existing membership (Admin/Board)

**Nuevo endpoint:**
```
POST /api/memberships/{membershipId}/fees
Body: { "year": int, "amount": decimal }
```

**Restricciones:**
- Solo Admin o Board
- `year` debe ser `> 2000` y `<= DateTime.UtcNow.Year`
- No puede existir ya una cuota para ese año en esa membresía (unique constraint `(membershipId, year)`)
- La nueva cuota se crea con `Status = Pending`
- El admin la puede marcar como pagada con el endpoint existente `POST /fees/{feeId}/pay`

**Archivos a modificar:**
- [MembershipsModels.cs](src/Abuvi.API/Features/Memberships/MembershipsModels.cs) — agregar `CreateMembershipFeeRequest(int Year, decimal Amount)`
- [MembershipsService.cs](src/Abuvi.API/Features/Memberships/MembershipsService.cs) — agregar `CreateFeeAsync(Guid membershipId, CreateMembershipFeeRequest, CancellationToken)`
- [MembershipsEndpoints.cs](src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs) — registrar endpoint en `MapMembershipFeeEndpoints`
- Nuevo validador `CreateMembershipFeeValidator.cs`

### Fix 3 — New endpoint: Reactivate deactivated membership (Admin/Board)

**Nuevo endpoint:**
```
POST /api/family-units/{familyUnitId}/members/{memberId}/membership/reactivate
Body: { "year": int }
```

**Lógica:**
- Buscar membresía existente (sin filtrar por `IsActive`) — agregar `GetByFamilyMemberIdIgnoringActiveAsync` al repositorio
- Si no existe → `404 NotFound`
- Si ya está `IsActive=true` → `409 Conflict` con mensaje "El miembro ya tiene una membresía activa"
- Si `IsActive=false` → Setear `IsActive=true`, limpiar `EndDate`, actualizar `UpdatedAt`, y crear `MembershipFee` para el año indicado con `Status=Pending`

**Archivos a modificar:**
- [MembershipsRepository.cs](src/Abuvi.API/Features/Memberships/MembershipsRepository.cs) — agregar `GetByFamilyMemberIdIgnoringActiveAsync`
- [MembershipsModels.cs](src/Abuvi.API/Features/Memberships/MembershipsModels.cs) — agregar `ReactivateMembershipRequest(int Year)`
- [MembershipsService.cs](src/Abuvi.API/Features/Memberships/MembershipsService.cs) — agregar `ReactivateAsync`
- [MembershipsEndpoints.cs](src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs) — registrar endpoint

### Fix 4 — Frontend: Correct "sin alta de socio/a" count in BulkActivate modal

**Causa:** El conteo de "N miembro(s) sin alta de socio/a" debe basarse en miembros sin `Membership` con `IsActive=true`, **no** en miembros sin cuota pagada del año en curso.

**Cambio de comportamiento esperado:**
- "Sin alta" = miembro sin `Membership` activa → se puede activar con BulkActivate
- "No al corriente" = miembro con `Membership` activa pero sin `MembershipFee` pagada este año → solo se soluciona creando/pagando la cuota anual, no activando de nuevo

El endpoint de BulkActivate ya aplica la lógica correcta (skip si tiene membresía activa). Solo falla la presentación de la cuenta previa en el modal y eventualmente el intento de crear membresías para miembros ya activos.

**Recomendación (backend):** Añadir al response de `GET /api/family-units/{familyUnitId}/members` un campo `membershipStatus` por cada miembro con los posibles valores:
- `"None"` — sin membresía
- `"Active"` — membresía activa, cuota pagada el año en curso
- `"ActiveFeePending"` — membresía activa, sin cuota pagada el año en curso
- `"Inactive"` — membresía desactivada

Alternativamente, el frontend puede derivar este estado de la respuesta de membresía por miembro, pero es más eficiente una sola llamada.

---

## Acceptance Criteria

### Backend
- [ ] Al crear una membresía (individual o bulk), se genera automáticamente un `MembershipFee` para el año indicado con `Status = Pending`.
- [ ] Existe endpoint `POST /api/memberships/{membershipId}/fees` (Admin/Board) para crear una cuota anual manualmente.
- [ ] Existe endpoint `POST /api/family-units/{familyUnitId}/members/{memberId}/membership/reactivate` (Admin/Board) para reactivar una membresía desactivada.
- [ ] El endpoint de reactivación lanza `409 Conflict` si la membresía ya está activa.
- [ ] El endpoint de reactivación lanza `404 NotFound` si no existe ningún registro de membresía.
- [ ] Los endpoints de creación de cuota y reactivación requieren rol `Admin` o `Board`.
- [ ] Validación: `year` en crear cuota y reactivar debe ser `> 2000` y `<= DateTime.UtcNow.Year`.

### Frontend
- [ ] El conteo "sin alta de socio/a" en el modal BulkActivate refleja correctamente los miembros sin `Membership` activa (no los que no han pagado la cuota).
- [ ] Existe un flujo en la UI para crear/cargar la cuota anual de una membresía activa.
- [ ] Se diferencia visualmente entre "sin membresía" y "membresía activa sin cuota pagada".

### Tests
- [ ] Unit test: `CreateAsync` genera `MembershipFee` para el año del request.
- [ ] Unit test: `BulkActivateAsync` genera `MembershipFee` para cada miembro activado.
- [ ] Unit test: `CreateFeeAsync` lanza `409` si ya existe cuota para ese año.
- [ ] Unit test: `ReactivateAsync` lanza `409` si membresía ya activa.
- [ ] Unit test: `ReactivateAsync` reactiva correctamente y crea cuota.
- [ ] Integration test: Flujo completo alta → cuota pending → pago → inscripción.

---

## Files to Modify

| File | Change |
|------|--------|
| [MembershipsService.cs](src/Abuvi.API/Features/Memberships/MembershipsService.cs) | Auto-create fee in `CreateAsync` y `BulkActivateAsync`; agregar `CreateFeeAsync`; agregar `ReactivateAsync` |
| [MembershipsRepository.cs](src/Abuvi.API/Features/Memberships/MembershipsRepository.cs) | Agregar `GetByFamilyMemberIdIgnoringActiveAsync` |
| [MembershipsModels.cs](src/Abuvi.API/Features/Memberships/MembershipsModels.cs) | Agregar `CreateMembershipFeeRequest`, `ReactivateMembershipRequest` |
| [MembershipsEndpoints.cs](src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs) | Registrar `POST /fees` y `POST /reactivate` |
| `CreateMembershipFeeValidator.cs` (nuevo) | Validación de año y amount para nueva cuota |
| Frontend: family unit detail / bulk modal | Corregir conteo "sin alta" |

---

## Non-Functional Requirements

- Los nuevos endpoints de Admin/Board deben devolver `403 Forbidden` si el rol es `Member`.
- La creación de `MembershipFee` en `CreateAsync` debe ser transaccional con la creación de `Membership` (misma transacción o manejada dentro del `SaveChangesAsync`).
- El importe de la cuota (`Amount`) en la creación automática puede dejarse en `0m` por ahora hasta que se defina la configuración de importes por año.
