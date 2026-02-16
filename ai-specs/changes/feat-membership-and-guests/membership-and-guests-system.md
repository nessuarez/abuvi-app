# Feature Spec: Sistema de Socios y Invitados

**Estado:** Pendiente de Implementación
**Prioridad:** Media
**Versión:** 1.0
**Fecha Creación:** 2026-02-15

---

## Contexto

Durante la implementación de la feature de Family Units, se identificó la necesidad de distinguir entre:

1. **Socios de la asociación**: Miembros familiares que son socios activos de Abuvi (pagan cuota anual)
2. **Miembros no-socios**: Personas en la unidad familiar que no son socios
3. **Invitados/Amigos**: Personas que no son familiares pero están vinculadas a una familia para asistir a campamentos

### Decisión Arquitectónica Importante

❌ **NO** añadir "Friend" al enum `FamilyRelationship`

**Razón**: Los invitados/amigos NO son miembros de la familia. El enum `FamilyRelationship` debe representar únicamente relaciones familiares reales (Parent, Child, Sibling, Spouse, Other).

---

## Problema a Resolver

### Limitaciones Actuales

1. **No se distingue entre socios y no-socios**
   - Todos los FamilyMembers se tratan igual
   - No hay forma de saber quién es socio de la asociación
   - No se gestiona el estado de pago de cuotas

2. **No hay sistema para invitados**
   - Las familias invitan amigos a campamentos
   - No hay forma de registrar invitados que no son familiares
   - Los invitados no deben tener derechos de socio ni usuario

3. **Impacto en inscripciones a campamentos**
   - Las inscripciones futuras necesitarán diferenciar socios vs no-socios (precios diferentes)
   - Necesidad de validar que los socios estén al corriente de pago

---

## Solución Propuesta

### Feature 1: Sistema de Socios (Membership)

#### Objetivo

Permitir marcar qué `FamilyMembers` son socios de la asociación y gestionar su estado de pago de cuotas anuales.

#### Modelo de Datos

**Nueva entidad: `Membership`**

```csharp
public class Membership
{
    public Guid Id { get; set; }
    public Guid FamilyMemberId { get; set; }  // FK a FamilyMember
    public DateTime StartDate { get; set; }    // Fecha de inicio de la membresía
    public DateTime? EndDate { get; set; }      // Fecha de fin (nullable para activos)
    public bool IsActive { get; set; }          // ¿Es socio activo?

    // Navegación
    public FamilyMember FamilyMember { get; set; } = null!;
    public ICollection<MembershipFee> Fees { get; set; } = new List<MembershipFee>();

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Nueva entidad: `MembershipFee` (Cuota de Socio)**

```csharp
public class MembershipFee
{
    public Guid Id { get; set; }
    public Guid MembershipId { get; set; }     // FK a Membership
    public int Year { get; set; }               // Año de la cuota (ej. 2026)
    public decimal Amount { get; set; }         // Monto de la cuota
    public FeeStatus Status { get; set; }       // Pendiente, Pagada, Vencida
    public DateTime? PaidDate { get; set; }     // Fecha de pago (nullable)
    public string? PaymentReference { get; set; } // Referencia del pago

    // Navegación
    public Membership Membership { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum FeeStatus
{
    Pending,    // Pendiente de pago
    Paid,       // Pagada
    Overdue     // Vencida (no pagada después de fecha límite)
}
```

#### Reglas de Negocio

1. **Socios activos**:
   - Un `FamilyMember` es socio si tiene un `Membership` con `IsActive = true`
   - Solo los socios activos pueden inscribirse a campamentos con descuento de socio

2. **Cuotas anuales**:
   - Se genera automáticamente una cuota anual el 1 de enero de cada año para todos los socios activos
   - La cuota tiene una fecha límite de pago (ej. 31 de marzo)
   - Si no se paga antes de la fecha límite, el estado cambia a `Overdue`

3. **Estado de pago**:
   - Un socio está "al corriente de pago" si la cuota del año actual está en estado `Paid`
   - Los socios NO al corriente de pago no pueden inscribirse a campamentos

4. **Reactivación**:
   - Un socio puede darse de baja (`IsActive = false`)
   - Puede reactivarse posteriormente (crear nuevo registro `Membership`)

#### API Endpoints (Propuestos)

**Memberships**:

- `POST /api/family-units/{familyUnitId}/members/{memberId}/membership` - Activar membresía
- `GET /api/family-units/{familyUnitId}/members/{memberId}/membership` - Obtener membresía
- `DELETE /api/family-units/{familyUnitId}/members/{memberId}/membership` - Desactivar membresía

**Cuotas (Fees)**:

- `GET /api/memberships/{membershipId}/fees` - Listar cuotas
- `POST /api/memberships/{membershipId}/fees/{feeId}/pay` - Marcar cuota como pagada
- `GET /api/memberships/{membershipId}/fees/current` - Obtener cuota del año actual

**Administración (Admin/Board)**:

- `POST /api/admin/memberships/generate-annual-fees` - Generar cuotas anuales para todos los socios
- `GET /api/admin/memberships/overdue` - Listar socios con cuotas vencidas
- `GET /api/admin/memberships/active` - Listar todos los socios activos

#### Permisos

- **Representative**: Puede activar/desactivar membresía de sus propios familiares
- **Admin/Board**: Puede gestionar membresías de todos los socios, marcar pagos, generar cuotas

---

### Feature 2: Sistema de Invitados (Guests)

#### Objetivo

Permitir a las familias registrar invitados (amigos, conocidos) que asistirán a campamentos sin ser miembros de la familia ni socios de la asociación.

#### Modelo de Datos

**Nueva entidad: `Guest`**

```csharp
public class Guest
{
    public Guid Id { get; set; }
    public Guid FamilyUnitId { get; set; }     // FK a FamilyUnit (familia que invita)

    // Datos personales
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? DocumentNumber { get; set; }  // Uppercase alphanumeric
    public string? Email { get; set; }
    public string? Phone { get; set; }           // E.164 format

    // Datos sensibles (ENCRYPTED)
    public string? MedicalNotes { get; set; }    // Encrypted
    public string? Allergies { get; set; }       // Encrypted

    // Estado
    public bool IsActive { get; set; }           // ¿Invitado activo?

    // Navegación
    public FamilyUnit FamilyUnit { get; set; } = null!;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

#### Reglas de Negocio

1. **NO son socios**:
   - Los `Guest` NO pueden ser socios de la asociación
   - NO tienen `Membership`
   - NO tienen descuento de socio en campamentos

2. **NO son usuarios**:
   - Los `Guest` NO tienen cuenta de usuario en la aplicación
   - NO pueden iniciar sesión
   - NO tienen `UserId`

3. **Vinculación a familia**:
   - Un `Guest` está siempre vinculado a una `FamilyUnit`
   - Solo el representante de esa familia puede gestionar al invitado
   - Si se elimina la `FamilyUnit`, se eliminan sus invitados (cascade)

4. **Inscripciones**:
   - Los invitados pueden inscribirse a campamentos (mediante el representante)
   - Pagan precio completo (sin descuento de socio)
   - Sus datos médicos se manejan con la misma seguridad que los de `FamilyMember`

5. **Privacidad**:
   - Mismas reglas RGPD que `FamilyMember`
   - Datos sensibles encriptados
   - Solo el representante y Admin/Board pueden ver datos sensibles

#### API Endpoints (Propuestos)

**Guests**:

- `POST /api/family-units/{familyUnitId}/guests` - Crear invitado
- `GET /api/family-units/{familyUnitId}/guests` - Listar invitados de la familia
- `GET /api/family-units/{familyUnitId}/guests/{guestId}` - Obtener invitado
- `PUT /api/family-units/{familyUnitId}/guests/{guestId}` - Actualizar invitado
- `DELETE /api/family-units/{familyUnitId}/guests/{guestId}` - Eliminar invitado

#### Permisos

- **Representative**: Puede gestionar invitados de su propia familia
- **Admin/Board**: Puede ver/gestionar todos los invitados

---

## Comparación de Entidades

| Característica | FamilyMember | Guest |
|----------------|--------------|-------|
| Relación con familia | Es miembro familiar | Es invitado externo |
| Puede ser socio | ✅ Sí (mediante Membership) | ❌ No |
| Tiene cuenta de usuario | ⚠️ Opcional (UserId nullable) | ❌ No |
| Puede inscribirse a campamentos | ✅ Sí | ✅ Sí (mediante representante) |
| Descuento de socio | ✅ Si es socio activo | ❌ No |
| Datos sensibles encriptados | ✅ Sí | ✅ Sí |
| Gestión | Representante + Admin/Board | Representante + Admin/Board |

---

## Impacto en Código Existente

### Cambios en FamilyMember

**Ningún cambio estructural necesario**

- `FamilyMember` se mantiene igual
- La relación con `Membership` es opcional (uno a uno)
- Un `FamilyMember` puede no ser socio

### Cambios en FamilyUnit

**Agregar navegación**:

```csharp
public class FamilyUnit
{
    // ... campos existentes ...

    // Navegación
    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
    public ICollection<Guest> Guests { get; set; } = new List<Guest>();  // NUEVO
}
```

### Cambios en Camp Registration (Futuro)

Cuando se implemente la inscripción a campamentos:

```csharp
public class CampRegistration
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public Guid FamilyUnitId { get; set; }

    // Puede ser FamilyMember O Guest (mutuamente excluyente)
    public Guid? FamilyMemberId { get; set; }
    public Guid? GuestId { get; set; }

    // Precio calculado según si es socio activo o no
    public decimal Price { get; set; }
    public bool AppliedMemberDiscount { get; set; }

    // Navegación
    public Camp Camp { get; set; } = null!;
    public FamilyUnit FamilyUnit { get; set; } = null!;
    public FamilyMember? FamilyMember { get; set; }
    public Guest? Guest { get; set; }
}
```

---

## Plan de Implementación (Fases)

### Fase 1: Sistema de Socios (Membership)

**Prioridad:** Alta
**Dependencias:** Family Units (completado)

**Entregables**:

1. Entidades `Membership` y `MembershipFee`
2. Migraciones EF Core
3. Repositorio y servicio para membresías
4. API endpoints para gestión de membresías
5. API endpoints para gestión de cuotas
6. Generación automática de cuotas anuales (job programado)
7. Tests unitarios (TDD)
8. Documentación

**Duración estimada:** 3-4 días

### Fase 2: Sistema de Invitados (Guests)

**Prioridad:** Media
**Dependencias:** Family Units (completado)

**Entregables**:

1. Entidad `Guest`
2. Migraciones EF Core
3. Repositorio y servicio para invitados
4. API endpoints para CRUD de invitados
5. Validaciones (similares a FamilyMember)
6. Encriptación de datos sensibles
7. Tests unitarios (TDD)
8. Documentación

**Duración estimada:** 2-3 días

### Fase 3: Integración con Camp Registration

**Prioridad:** Baja (depende de camp registration)
**Dependencias:** Membership, Guests, Camp Registration

**Entregables**:

1. Lógica de cálculo de precios (socios vs no-socios)
2. Validación de estado de pago para inscripciones
3. Permitir inscripción de Guests mediante representante
4. Reportes de inscripciones por tipo (socio/no-socio/invitado)

---

## Consideraciones Técnicas

### Base de Datos

**Nuevas tablas**:

- `memberships`
- `membership_fees`
- `guests`

**Índices recomendados**:

- `memberships.family_member_id` (único, para relación 1-1)
- `membership_fees.membership_id`
- `membership_fees.year` (para queries de cuotas anuales)
- `guests.family_unit_id`

### Encriptación

- `Guest.MedicalNotes` y `Guest.Allergies` deben usar el mismo `IEncryptionService` que FamilyMember
- Nunca exponer datos sensibles en API responses (usar boolean flags)

### Jobs Programados

**Generación de cuotas anuales**:

- Job que corre el 1 de enero a las 00:00
- Crea `MembershipFee` para todos los socios activos
- Notifica a los socios por email

**Detección de cuotas vencidas**:

- Job que corre diariamente
- Marca cuotas como `Overdue` si pasan la fecha límite sin pagar
- Notifica a los socios

### RGPD/GDPR

**Guests**:

- Los invitados tienen los mismos derechos RGPD que los FamilyMembers
- Derecho al olvido: Soft delete con `IsActive = false`
- Datos sensibles encriptados
- Audit logging de accesos

---

## Validaciones

### Membership

**CreateMembershipRequest**:

- `FamilyMemberId`: Requerido, debe existir
- `StartDate`: Requerido, no puede ser futuro
- Validar que el FamilyMember no tenga ya una membresía activa

**PayFeeRequest**:

- `PaymentReference`: Opcional, max 100 caracteres
- `PaidDate`: Requerido, no puede ser futuro

### Guest

**CreateGuestRequest**:

- `FirstName`: Requerido, max 100 caracteres
- `LastName`: Requerido, max 100 caracteres
- `DateOfBirth`: Requerido, debe ser fecha pasada
- `DocumentNumber`: Opcional, uppercase alphanumeric, max 50 caracteres
- `Email`: Opcional, formato válido, max 255 caracteres
- `Phone`: Opcional, formato E.164, max 20 caracteres
- `MedicalNotes`: Opcional, max 2000 caracteres
- `Allergies`: Opcional, max 1000 caracteres

---

## Mensajes de Error (Español)

### Membership

- "El miembro ya tiene una membresía activa"
- "La membresía no existe"
- "La cuota ya está pagada"
- "La cuota no existe"
- "El año de la cuota debe ser el año actual o futuro"

### Guest

- "El invitado no existe"
- "La unidad familiar no existe"
- "No tienes permiso para gestionar invitados de esta familia"

---

## Próximos Pasos

1. **Revisar y aprobar** este spec con el equipo
2. **Crear plan de implementación detallado** para Fase 1 (Membership)
3. **Diseñar UI mockups** para gestión de socios e invitados
4. **Definir modelo de precios** (cuota anual, descuentos en campamentos)
5. **Implementar Fase 1** siguiendo TDD

---

## Notas

- **No mezclar conceptos**: FamilyMember representa familia, Guest representa invitados externos, Membership representa ser socio
- **Separación de responsabilidades**: Cada entidad tiene un propósito claro
- **Escalabilidad**: El sistema permite futura integración con pasarelas de pago para cuotas
- **Flexibilidad**: Se puede agregar más tipos de fees (matrícula, cuota extraordinaria, etc.)

---

**Creado por:** Claude Code
**Fecha:** 2026-02-15
**Versión:** 1.0
**Estado:** Pendiente de Revisión
