# Feature: Family IBAN for Direct Debit + Board Notification on Change

## Summary

Families need a place to store the IBAN used by the association to charge direct debits (*domiciliaciones* — annual membership fees and camp installments). Today the only IBAN in the system is the **association's own** account (`PaymentSettings.Iban`, used for inbound bank transfers); there is no field for the **family's** account.

This feature adds:

1. A per-family IBAN stored **encrypted at rest** on `FamilyUnit`, editable by the family representative.
2. An **email notification to the board** (`Resend:BoardBccEmail`) every time the field is created, changed or removed, so the Junta can update its banking records.
3. A **liability warning to families**: if the IBAN is missing or out of date, a direct debit may be returned by the bank and the family must bear the cost of the resulting surcharge (*recargo por devolución*). The warning is shown persistently and must be explicitly acknowledged when saving.

---

## Business Context

- The Junta processes direct debits outside the platform (manually / via the bank's portal). The platform is the **source of truth for the mandate data**, not the payment rail. No SEPA file generation is in scope.
- A returned direct debit generates a bank surcharge. The association's policy is that the surcharge is borne by the family whose bank details were wrong or stale. The platform must make that policy visible and record the family's acknowledgement.
- The board must learn about a change **without polling** the family list — hence the email notification.

---

## Current Behavior

- `FamilyUnit` has no banking fields (`FamilyUnitsModels.cs:6-16`).
- `PaymentSettings.Iban` exists but is the association's inbound account, rendered in `BankTransferInstructions.vue` and in payment-instruction emails. Unrelated to this feature — **do not reuse or overload it**.
- `FamilyUnitsService` has no `IEmailService` dependency (`FamilyUnitsService.cs:12-17`).
- No surcharge concept exists anywhere in the codebase. The warning introduced here is **informational and contractual only** — it does not compute or charge anything.

---

## Expected Behavior

- The representative sees an "Datos bancarios" section on the family page showing the IBAN **masked** (`ES** **** **** **** **** 1234`) plus the date it was last updated.
- Saving a new/changed IBAN requires ticking a surcharge-liability checkbox; the acceptance timestamp is stored.
- On every successful create/update/delete of the IBAN, an email is sent to the representative with the board in `Bcc`, stating what changed (masked values only).
- When no IBAN is stored, a persistent orange warning appears on the family page.
- Admin/Board see, in the family units admin list, whether each family has an IBAN, and can retrieve the full IBAN through a dedicated, logged, board-only endpoint.

---

## Scope

### In scope

1. `FamilyUnit` entity: encrypted `Iban`, `IbanLast4`, `IbanUpdatedAt`, `IbanUpdatedByUserId`, `IbanSurchargeTermsAcceptedAt` + EF configuration + migration.
2. Endpoints: `PUT` / `DELETE` `/api/family-units/{id}/iban` (representative), `GET /api/family-units/{id}/iban` (Admin/Board, returns plaintext, logged).
3. Additive fields on `FamilyUnitResponse`: `hasIban`, `ibanMasked`, `ibanUpdatedAt`.
4. Shared IBAN validation helper (format + mod-97 checksum) in `Common`, reused by the new validator.
5. `IEmailService.SendFamilyIbanUpdatedAsync` + `ResendEmailService` implementation + `FamilyIbanUpdatedEmailData` DTO.
6. Frontend: `FamilyIbanForm.vue`, IBAN card + warning banner in `FamilyUnitPage.vue`, types, composable methods, `hasIban` column in `FamilyUnitsAdminPanel.vue`.
7. Unit tests (backend service + validator + email service; frontend component + composable).
8. Documentation updates in `ai-specs/specs/data-model.md` and `ai-specs/specs/api-endpoints.md`.

### Out of scope

- SEPA XML (pain.008) generation, bank file export, or any actual direct-debit execution.
- Automatic surcharge calculation, charging or invoicing.
- Admin/Board **editing** a family's IBAN (write stays representative-only so the audit trail is unambiguous). Board editing belongs to `feat-admin-edit-profiles`.
- Periodic "confirm your IBAN is still valid" staleness prompt (see *Follow-up* below).
- Non-Spanish IBANs (only `ES` is accepted, consistent with `PaymentSettingsRequestValidator`).

### Follow-up (documented, not built here)

A staleness review flow: association setting `family_iban_review_months` (default 24) plus `POST /api/family-units/{id}/iban/confirm` that bumps `IbanUpdatedAt` without changing the value, and a softer "revisa tus datos bancarios" prompt. `IbanUpdatedAt` is introduced here precisely so this can be added without a further migration.

---

## Affected Files

### Backend

| File | Change |
| --- | --- |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs` | Add 5 IBAN fields to `FamilyUnit`. Add `UpdateFamilyIbanRequest`, `FamilyIbanResponse`. Extend `FamilyUnitResponse` + `ToResponse()` with `hasIban`, `ibanMasked`, `ibanUpdatedAt`. Extend `FamilyUnitAdminProjection` / `FamilyUnitListItemResponse` with `HasIban`. |
| `src/Abuvi.API/Features/FamilyUnits/UpdateFamilyIbanValidator.cs` | **New.** FluentValidation for `UpdateFamilyIbanRequest`. |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Map `PUT`/`DELETE` `/{id:guid}/iban` on `group`; map `GET /{id:guid}/iban` on `adminGroup`. |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs` | Inject `IEmailService`. Add `UpdateIbanAsync`, `DeleteIbanAsync`, `GetIbanAsync`. |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs` | Include `HasIban` in `GetAllPagedAsync` projection (`fu.IbanLast4 != null`). |
| `src/Abuvi.API/Common/Validation/IbanValidator.cs` | **New.** `Normalize(string)` + `IsValidSpanishIban(string)` (regex + mod-97) + `Mask(string)`. |
| `src/Abuvi.API/Common/Services/IEmailService.cs` | Add `FamilyIbanUpdatedEmailData` record + `SendFamilyIbanUpdatedAsync`. |
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Implement `SendFamilyIbanUpdatedAsync` (To = representative, Bcc = `_boardBccEmail`). |
| `src/Abuvi.API/Data/Configurations/FamilyUnitConfiguration.cs` | Map the 5 new columns. |
| `src/Abuvi.API/Migrations/` | `AddFamilyUnitIbanFields` migration. |

### Frontend

| File | Change |
| --- | --- |
| `frontend/src/types/family-unit.ts` | Add `hasIban`, `ibanMasked`, `ibanUpdatedAt` to `FamilyUnitResponse`; add `UpdateFamilyIbanRequest`, `FamilyIbanResponse`. |
| `frontend/src/components/family-units/FamilyIbanForm.vue` | **New.** IBAN input + surcharge warning + mandatory acknowledgement checkbox. |
| `frontend/src/views/FamilyUnitPage.vue` | New "Datos bancarios" `Card` with masked IBAN, last-updated date, edit/remove actions; orange `Message` banner when `!hasIban`. |
| `frontend/src/composables/useFamilyUnits.ts` | `updateFamilyIban`, `deleteFamilyIban` (+ `getFamilyIban` for Admin/Board). |
| `frontend/src/components/admin/FamilyUnitsAdminPanel.vue` | "IBAN" column with a `Tag` (`Sí` / `No`). |
| `frontend/src/components/family-units/__tests__/FamilyIbanForm.test.ts` | **New.** Component tests. |

### Tests (backend)

| File | Change |
| --- | --- |
| `src/Abuvi.Tests/Unit/Features/FamilyUnits/FamilyUnitsServiceTests.cs` | Tests for `UpdateIbanAsync` / `DeleteIbanAsync` / `GetIbanAsync`. |
| `src/Abuvi.Tests/Unit/Features/FamilyUnits/UpdateFamilyIbanValidatorTests.cs` | **New.** Validator tests. |
| `src/Abuvi.Tests/Unit/Common/IbanValidatorTests.cs` | **New.** Checksum/normalisation/masking tests. |

---

## Data Model Changes

### `FamilyUnit` entity — new fields

```csharp
// Family bank account used by the association for direct debits (domiciliaciones).
// Stored AES-256 encrypted via IEncryptionService — same treatment as FamilyMember.MedicalNotes.
public string? Iban { get; set; }

// Last 4 digits in plaintext, so the UI and the admin list can show/filter
// without decrypting. Never enough to reconstruct the account.
public string? IbanLast4 { get; set; }

// When the IBAN was last set or changed (null when no IBAN stored).
public DateTime? IbanUpdatedAt { get; set; }

// Which user performed the last change (representative). Audit trail.
public Guid? IbanUpdatedByUserId { get; set; }

// When the family last accepted the returned-direct-debit surcharge liability.
public DateTime? IbanSurchargeTermsAcceptedAt { get; set; }
```

**Invariants**

- `Iban`, `IbanLast4`, `IbanUpdatedAt`, `IbanUpdatedByUserId` are all null or all non-null.
- Deleting the IBAN nulls all five fields (including the acceptance timestamp).

### EF configuration (`FamilyUnitConfiguration`)

```csharp
builder.Property(fu => fu.Iban)
    .HasMaxLength(512)                 // ciphertext, not the 24-char plaintext
    .HasColumnName("iban_encrypted");

builder.Property(fu => fu.IbanLast4)
    .HasMaxLength(4)
    .HasColumnName("iban_last4");

builder.Property(fu => fu.IbanUpdatedAt).HasColumnName("iban_updated_at");
builder.Property(fu => fu.IbanUpdatedByUserId).HasColumnName("iban_updated_by_user_id");
builder.Property(fu => fu.IbanSurchargeTermsAcceptedAt).HasColumnName("iban_surcharge_terms_accepted_at");
```

No index and no FK on `iban_updated_by_user_id` (audit reference only, must survive user deletion).

### Data model doc

Add to `ai-specs/specs/data-model.md` → `### FamilyUnit` → **Fields**:

- `iban`: Family bank account for direct debits, **AES-256 encrypted at rest** (optional)
- `ibanLast4`: Last 4 digits in plaintext for display/filtering (optional, 4 chars)
- `ibanUpdatedAt`: Timestamp of the last IBAN change (optional)
- `ibanUpdatedByUserId`: User who performed the last IBAN change (optional, audit only, no FK)
- `ibanSurchargeTermsAcceptedAt`: When the family accepted liability for returned-direct-debit surcharges (optional)

And under **Validation rules**: *IBAN must be a Spanish IBAN (`ES` + 22 digits) with a valid mod-97 checksum; it is never exposed in plaintext except through the Admin/Board-only retrieval endpoint.*

---

## API Contract

### `PUT /api/family-units/{id}/iban`

Sets or replaces the family's direct-debit IBAN.

**Authorization**: representative of the family unit only (403 otherwise, including Admin/Board).

**Request:**

```json
{
  "iban": "ES9121000418450200051332",
  "surchargeTermsAccepted": true
}
```

- `iban` is normalised server-side: whitespace stripped, upper-cased, before validation and encryption.

**Response 200 OK** — `ApiResponse<FamilyUnitResponse>` with the new masked value.

**Errors:**

- `400 Bad Request` — invalid format or checksum, or `surchargeTermsAccepted` is `false`.
- `403 Forbidden` — caller is not the representative.
- `404 Not Found` — family unit does not exist.

### `DELETE /api/family-units/{id}/iban`

Clears all IBAN fields. Sends the notification with `Action = Removed`.

**Authorization**: representative only.
**Response**: `204 No Content`. `404` if the family unit does not exist; **idempotent** — clearing an already-empty IBAN returns `204` and sends no email.

### `GET /api/family-units/{id}/iban`

Returns the decrypted IBAN so the board can set up the direct debit at the bank.

**Authorization**: `Admin`, `Board` (mapped on the existing `adminGroup`).

**Response 200 OK:**

```json
{
  "success": true,
  "data": {
    "familyUnitId": "…",
    "iban": "ES9121000418450200051332",
    "updatedAt": "2026-08-24T10:12:00Z"
  }
}
```

- `404 Not Found` when the family unit does not exist **or** has no IBAN stored.
- Every successful call logs at `Information`: `FamilyUnitId`, requesting `UserId`, `IbanLast4`. **The plaintext IBAN is never logged.**

### `FamilyUnitResponse` — additive fields

```json
{
  "hasIban": true,
  "ibanMasked": "ES** **** **** **** **** 1332",
  "ibanUpdatedAt": "2026-08-24T10:12:00Z"
}
```

`ibanMasked` is built from `IbanLast4` only — the ciphertext is never decrypted to build a list response.

### `FamilyUnitListItemResponse` — additive field

```json
{ "hasIban": false }
```

---

## Validation

### `Common/Validation/IbanValidator.cs` (new)

```csharp
public static class IbanValidator
{
    /// <summary>Strips whitespace and upper-cases. Returns "" for null/blank.</summary>
    public static string Normalize(string? raw);

    /// <summary>ES + 22 digits AND valid ISO 13616 mod-97 checksum.</summary>
    public static bool IsValidSpanishIban(string? raw);

    /// <summary>"ES9121000418450200051332" → "ES** **** **** **** **** 1332".</summary>
    public static string Mask(string last4);
}
```

Mod-97: move the first 4 chars to the end, map letters to digits (`A`=10 … `Z`=35), compute the remainder of the resulting numeric string modulo 97 in chunks (never parse the whole string into a numeric type), and require `== 1`.

### `UpdateFamilyIbanValidator`

```csharp
RuleFor(x => x.Iban)
    .NotEmpty().WithMessage("El IBAN es obligatorio.")
    .Must(iban => IbanValidator.IsValidSpanishIban(iban))
    .WithMessage("El IBAN no es válido. Debe ser un IBAN español (ES + 22 dígitos).");

RuleFor(x => x.SurchargeTermsAccepted)
    .Equal(true)
    .WithMessage("Debes aceptar las condiciones sobre el recargo por devolución de recibos.");
```

Wired through the existing `ValidationFilter<UpdateFamilyIbanRequest>` endpoint filter.

---

## Board Notification Email

### DTO (`IEmailService.cs`)

```csharp
public enum FamilyIbanChangeAction { Added, Updated, Removed }

public record FamilyIbanUpdatedEmailData
{
    public required string ToEmail { get; init; }            // representative
    public required string RecipientFirstName { get; init; }
    public required string FamilyName { get; init; }
    public int? FamilyNumber { get; init; }
    public required FamilyIbanChangeAction Action { get; init; }
    public string? PreviousIbanMasked { get; init; }         // null when Action == Added
    public string? NewIbanMasked { get; init; }              // null when Action == Removed
    public required DateTime ChangedAtUtc { get; init; }
}

Task SendFamilyIbanUpdatedAsync(FamilyIbanUpdatedEmailData data, CancellationToken ct);
```

**Only masked values ever reach the email.** The board retrieves the full number through `GET /api/family-units/{id}/iban`.

### Implementation notes (`ResendEmailService`)

Follows the established pattern in this file: `IsTestAddress` early-return, `From = $"{_fromName} <{_fromEmail}>"`, `To = data.ToEmail`, `Bcc = [_boardBccEmail]`. Sending to the representative with the board in `Bcc` satisfies the board-notification requirement **and** gives the family a change-confirmation, which is the standard fraud-detection safeguard for bank-detail changes.

**Subject:** `Datos bancarios actualizados — {FamilyName}`

**Body:**

> Hola, {RecipientFirstName}:
>
> Se han **{added: registrado | updated: actualizado | removed: eliminado}** los datos bancarios de domiciliación de la familia **{FamilyName}**{ (nº {FamilyNumber})}.
>
> | | |
> | --- | --- |
> | IBAN anterior | {PreviousIbanMasked ?? "—"} |
> | IBAN actual | {NewIbanMasked ?? "— (eliminado)"} |
> | Fecha del cambio | {ChangedAtUtc:dd/MM/yyyy HH:mm} |
>
> *Recuerda: si el IBAN no está actualizado, el recibo puede ser devuelto por el banco y **el coste del recargo por devolución correrá a cargo de la familia**.*
>
> Si no has sido tú quien ha realizado este cambio, ponte en contacto con la junta lo antes posible.
>
> Saludos cordiales, El equipo de Abuvi

Use the existing `BuildBoardNotesHtml`-style inline HTML conventions already present in the file (inline styles, `max-width: 600px` wrapper).

---

## Service Logic (`FamilyUnitsService`)

`FamilyUnitsService` must gain an `IEmailService emailService` constructor parameter (primary-constructor style, `FamilyUnitsService.cs:12`).

```csharp
public async Task<FamilyUnitResponse> UpdateIbanAsync(
    Guid userId, Guid familyUnitId, UpdateFamilyIbanRequest request, CancellationToken ct)
{
    var familyUnit = await repository.GetFamilyUnitByIdAsync(familyUnitId, ct)
        ?? throw new NotFoundException("Unidad familiar", familyUnitId);

    if (familyUnit.RepresentativeUserId != userId)
        throw new ForbiddenException("Solo el representante puede modificar los datos bancarios");

    var normalized = IbanValidator.Normalize(request.Iban);
    var previousLast4 = familyUnit.IbanLast4;

    // No-op guard: same IBAN re-submitted → refresh the acceptance timestamp, no email.
    var isSameIban = previousLast4 is not null
        && encryptionService.Decrypt(familyUnit.Iban!) == normalized;

    familyUnit.Iban = encryptionService.Encrypt(normalized);
    familyUnit.IbanLast4 = normalized[^4..];
    familyUnit.IbanUpdatedAt = DateTime.UtcNow;
    familyUnit.IbanUpdatedByUserId = userId;
    familyUnit.IbanSurchargeTermsAcceptedAt = DateTime.UtcNow;

    await repository.UpdateFamilyUnitAsync(familyUnit, ct);

    logger.LogInformation(
        "Family unit {FamilyUnitId} IBAN updated by {UserId} (last4 {Last4})",
        familyUnitId, userId, familyUnit.IbanLast4);

    if (!isSameIban)
        await SendIbanNotificationSafeAsync(familyUnit, previousLast4, familyUnit.IbanLast4, ct);

    return familyUnit.ToResponse();
}
```

`DeleteIbanAsync` mirrors it: representative check, return early (no email) when `IbanLast4 is null`, null all five fields, notify with `Action = Removed`.

`SendIbanNotificationSafeAsync` wraps the email call in `try/catch`, logs the failure and **never propagates** — an email outage must not roll back the IBAN change (same isolation rule as `ChangeStatusAsync` in `RegistrationsService`).

---

## Frontend

### `FamilyIbanForm.vue` (new)

- `InputText` for the IBAN, `maxlength="34"`, auto-normalised on input (strip spaces, upper-case) and rendered in 4-char groups for readability.
- Client-side mirror of `IbanValidator.IsValidSpanishIban` for immediate feedback; the server remains authoritative.
- Persistent `Message severity="warn" :closable="false"` above the field:

  > Los recibos se domiciliarán en esta cuenta. **Si el IBAN no está actualizado y el recibo es devuelto, el coste del recargo generado por la devolución correrá a cargo de la familia.**

- Mandatory `Checkbox` bound to `surchargeTermsAccepted` (same interaction pattern as the existing consent checkbox in `FamilyUnitForm.vue:103-114`):

  > He comprobado que el IBAN es correcto y acepto asumir el coste del recargo si el recibo resulta devuelto por datos bancarios incorrectos o desactualizados.

- Submit disabled until the IBAN is valid **and** the checkbox is ticked.
- The form never receives the current full IBAN — editing always means typing the complete new number.

### `FamilyUnitPage.vue`

New "Datos bancarios" `Card`, placed after the existing family-unit card:

- **With IBAN**: masked value in monospace, `Actualizado el {{ formatDate(ibanUpdatedAt) }}`, `Editar` and `Eliminar` buttons (delete behind a `ConfirmDialog`).
- **Without IBAN**:

```html
<Message severity="warn" :closable="false" data-testid="iban-missing-warning">
  No has registrado el IBAN para las domiciliaciones. Sin él no podemos girar los
  recibos, y si el recibo se devuelve por datos bancarios incorrectos o
  desactualizados, <strong>el coste del recargo correrá a cargo de la familia</strong>.
</Message>
```

Both the card and the banner are visible to the representative only; Admin/Board viewing the page see the masked value read-only.

### `FamilyUnitsAdminPanel.vue`

New sortable-by-value "IBAN" column rendering `<Tag :severity="data.hasIban ? 'success' : 'warn'" :value="data.hasIban ? 'Sí' : 'No'" />`, so the board can see at a glance which families still need to provide bank details.

---

## Implementation Steps (TDD)

1. **`IbanValidator` (tests first)** — valid ES IBAN passes; wrong checksum fails; lowercase/spaced input normalises; non-ES prefix fails; `Mask("1332")` → `"ES** **** **** **** **** 1332"`.
2. **Entity + EF config + migration** — add the 5 fields, map columns, generate `AddFamilyUnitIbanFields`, verify it applies against a clean database.
3. **DTOs + mapping** — `UpdateFamilyIbanRequest`, `FamilyIbanResponse`; extend `FamilyUnitResponse` and `ToResponse()`; extend the admin projection and its repository query.
4. **`UpdateFamilyIbanValidator` (tests first)** — invalid IBAN rejected; `surchargeTermsAccepted: false` rejected.
5. **Service (tests first)**
   - `UpdateIbanAsync` by a non-representative → `ForbiddenException`.
   - `UpdateIbanAsync` on a first-time set → `Iban` encrypted, `IbanLast4` = last 4, timestamps set, `SendFamilyIbanUpdatedAsync` called with `Action = Added` and `PreviousIbanMasked = null`.
   - `UpdateIbanAsync` replacing an existing IBAN → `Action = Updated`, previous masked value passed.
   - `UpdateIbanAsync` with the identical IBAN → persisted, **no email sent**.
   - `DeleteIbanAsync` → all five fields null, `Action = Removed`.
   - `DeleteIbanAsync` when no IBAN stored → no email, no exception.
   - Email throws → IBAN change still persists, exception swallowed and logged.
   - `GetIbanAsync` on a family with no IBAN → `NotFoundException`.
6. **Email service** — `FamilyIbanUpdatedEmailData` + `SendFamilyIbanUpdatedAsync`; test with the mocked `IResendClient` that `Bcc` contains the board address and that the body contains **no** unmasked IBAN.
7. **Endpoints** — map the three routes with the correct groups, filters and `Produces` metadata; extract `userId` from claims exactly as the sibling endpoints do.
8. **Frontend types + composable** — `updateFamilyIban`, `deleteFamilyIban`, `getFamilyIban`; refresh `familyUnit.value` from the response.
9. **`FamilyIbanForm.vue` (tests first)** — submit disabled until valid + acknowledged; invalid IBAN shows an error; submit emits the normalised value.
10. **`FamilyUnitPage.vue`** — bank-details card, missing-IBAN banner, delete confirmation.
11. **`FamilyUnitsAdminPanel.vue`** — IBAN column.
12. **Docs** — update `data-model.md` (FamilyUnit fields) and `api-endpoints.md` (three endpoints + the additive response fields, in the existing Family Units section around line 759-820).

---

## Non-functional Requirements

- **Security — encryption at rest**: the IBAN is stored AES-256 encrypted via `IEncryptionService`, exactly like `FamilyMember.MedicalNotes` (`FamilyUnitsService.cs:184-190`). Plaintext exists only in memory during a request.
- **Security — exposure**: no list or detail response ever returns the plaintext IBAN. The single plaintext path is the Admin/Board `GET /api/family-units/{id}/iban`.
- **Security — logging**: never log the plaintext IBAN, in any log level, in any service, or in Sentry breadcrumbs. Log `IbanLast4` only.
- **Security — authorization**: writes are representative-only; Admin/Board get `403` on write. Reads of the plaintext are Admin/Board-only via `adminGroup`.
- **Auditability**: every write records `IbanUpdatedByUserId` + `IbanUpdatedAt`; every plaintext read emits an `Information` log with the requesting user id.
- **GDPR**: the IBAN is personal financial data. It is removed with the family unit on `AdminDeleteFamilyUnit` (hard delete) and must be excluded from, or masked in, any data-export or support tooling.
- **Error isolation**: email failures never roll back the IBAN change and never surface as a `5xx`.
- **No breaking change**: all API response fields are additive; existing clients are unaffected.
- **Backwards compatibility**: existing family units have `hasIban: false` and see the warning banner immediately after deployment — that is the intended behaviour, not a regression.
- **Test coverage**: every new service path, the validator, the IBAN helper, and the new Vue component are covered; no regressions in the existing suites.

---

## Acceptance Criteria

- [ ] A representative can save a valid Spanish IBAN from the family page; an invalid format or checksum is rejected with a clear Spanish message.
- [ ] Saving is impossible without ticking the surcharge acknowledgement; the acceptance timestamp is persisted in `iban_surcharge_terms_accepted_at`.
- [ ] The stored IBAN is encrypted in the database — a direct `SELECT iban_encrypted FROM family_units` shows ciphertext, never `ES…`.
- [ ] `GET /api/family-units/me` returns `hasIban`, `ibanMasked` and `ibanUpdatedAt`, and **never** the full IBAN.
- [ ] Creating, changing or removing the IBAN sends an email to the representative with `Resend:BoardBccEmail` in `Bcc`, stating the action and masked previous/new values.
- [ ] Re-submitting the identical IBAN does not send an email.
- [ ] A Resend failure leaves the IBAN change persisted and returns `200`/`204`.
- [ ] A non-representative (including Admin/Board) gets `403` on `PUT`/`DELETE` `/api/family-units/{id}/iban`.
- [ ] `GET /api/family-units/{id}/iban` returns the plaintext IBAN for Admin/Board, `403` for a Member, and `404` when no IBAN is stored; the access is logged.
- [ ] A family with no IBAN sees the orange warning banner naming the surcharge liability on the family page.
- [ ] The admin family units list shows an IBAN `Sí`/`No` tag per family.
- [ ] `data-model.md` and `api-endpoints.md` are updated.
- [ ] All new and existing unit tests pass.

---

## ⚠️ Decision Gate: Formal SEPA Mandate Fields

**Status: pending confirmation by the Junta. Do not start implementation until this is resolved.**

The spec above stores the IBAN as a plain data field. A legally valid direct debit under SEPA Core is not a bank account number — it is a **mandate** (*orden de domiciliación*) signed by the debtor, and the creditor must be able to produce it if the debit is disputed. If the Junta debits as a registered SEPA creditor (rather than the bank keying in transfers on their behalf), the fields below are not optional extras: they are what makes the debit enforceable.

Deciding this **before** implementation matters more than usual, for two reasons.

### Why this cannot be bolted on later

1. **Changing an IBAN is a mandate amendment, not an overwrite.** The current design (`UpdateIbanAsync`) overwrites `Iban` in place and keeps no history. Under SEPA, when a debtor changes account the creditor must send the next collection with amendment data — the original mandate reference and the previous account — or restart the sequence as `FRST`. An overwrite destroys exactly the data the amendment needs. If mandates are adopted, `UpdateIbanAsync` must **supersede** the old mandate (keep it, mark it `Amended`) instead of overwriting, which changes the entity shape from "field on `FamilyUnit`" to "`FamilyUnit` has many `SepaMandate`, one active".
2. **The signature evidence must be captured at the moment of acceptance.** A mandate signed digitally needs the accepted text, the timestamp and the acting user recorded when the family clicks. That evidence cannot be reconstructed retroactively for families who already saved an IBAN — they would all have to re-accept.

The existing `IbanSurchargeTermsAcceptedAt` covers the *surcharge* acknowledgement, which is an internal association policy. It is **not** a SEPA mandate signature; the two are separate legal acts and should stay separate fields even if both are ticked in the same form.

### Option A — IBAN only (spec as written above)

Appropriate when the Junta enters the debits manually in the bank's portal and the bank holds the signed paper mandates. No further fields. Lowest cost, no legal weight in the platform.

### Option B — Full SEPA mandate

Additional fields, as a `SepaMandate` entity rather than more columns on `FamilyUnit`:

| Field | Type | Notes |
| --- | --- | --- |
| `MandateReference` (RUM) | string, max 35 | Unique per creditor. Generated by the platform (e.g. `ABUVI-{FamilyNumber:D5}-{seq}`) unless the bank assigns it. |
| `DebtorName` | string, max 140 | **Account holder — not necessarily the representative.** This is the most commonly missed field; a debit against an account whose holder name does not match is a frequent rejection cause. |
| `DebtorAddress` | string, optional | Required by some banks; SEPA-optional when the IBAN is Spanish. |
| `Bic` | string, max 11, optional | Optional for intra-SEPA since Feb 2016; some entities still ask. |
| `SignedAt` | DateTime | Date of signature — the legal anchor of the mandate. |
| `SignaturePlace` | string, optional | *Localidad* — printed on the standard Spanish mandate form. |
| `MandateType` | enum | `Recurrent` (RCUR — annual membership fee) / `OneOff` (OOFF — a single camp installment). Likely both are needed, which is a second argument for a mandate collection rather than one field set. |
| `Status` | enum | `Active` / `Amended` / `Cancelled` / `Expired`. |
| `AcceptanceEvidence` | value object | Accepted text version, UTC timestamp, acting user id — the digital-signature audit trail. |
| `LastCollectionAt` | DateTime? | Needed to enforce the SEPA rule that a mandate lapses **36 months** after the last collection. |

Creditor-side data (`CreditorIdentifier`, e.g. `ES##ZZZ` + CIF, and the creditor name) belongs on the association's `PaymentSettings`, not per family — one value for the whole organisation.

### Consultation for the Junta (Spanish, ready to send)

1. ¿La asociación gira los recibos como **acreedor SEPA registrado**, con identificador de acreedor propio (`ES##ZZZ` + CIF)? Si es así, ¿cuál es?
2. ¿Cómo se recogen hoy los mandatos? ¿Papel firmado que custodia el banco, o no hay mandato formal?
3. ¿Se aceptaría que la **firma digital en la plataforma** (marca de tiempo + texto aceptado + usuario) sustituya al mandato en papel, o el banco exige documento firmado?
4. La **referencia única del mandato (RUM)**, ¿la genera la asociación o la asigna el banco?
5. ¿Los cobros son **recurrentes** (cuota anual) y además **puntuales** (plazos de campamento), o solo uno de los dos?
6. ¿El **titular de la cuenta** es siempre el representante de la familia, o puede ser otra persona? (Determina si hace falta pedir el nombre del titular por separado.)
7. ¿El banco pide **BIC** y/o **domicilio del titular**?
8. ¿Necesitáis que la plataforma genere el **fichero SEPA (pain.008)** para subirlo al banco, o los recibos se introducen a mano?
9. **Recargo por devolución**: ¿importe fijo por recibo devuelto? ¿Se quiere que el aviso a las familias indique la cifra concreta?

Answers to 1–3 decide Option A vs B. Answers to 5–7 decide the exact field list. Answer to 8 decides whether a pain.008 export becomes a follow-up ticket. Answer to 9 closes Open Question 3 below.

### If Option B is chosen

This ticket should be re-enriched before implementation, not patched: the entity becomes `SepaMandate` (one active per family, history preserved), `UpdateIbanAsync` becomes `CreateOrAmendMandateAsync`, the board notification reports the mandate action (`Created` / `Amended` / `Cancelled`) plus the RUM, and the admin `GET .../iban` endpoint becomes a mandate-retrieval endpoint. Everything else in this spec — encryption at rest, masked exposure, board notification, surcharge warning, authorization model — carries over unchanged.

---

## Open Questions

1. **SEPA mandate** — see the Decision Gate above. Blocking.
2. **Non-Spanish IBANs** — any member families with foreign accounts? Current spec accepts `ES` only.
3. **Surcharge amount** — should the warning quote a concrete figure (e.g. "aprox. X €") or stay generic as specified here? (Question 9 of the consultation.)
