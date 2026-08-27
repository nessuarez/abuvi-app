# Feature: Family IBAN for Direct Debit + Board Notification on Change

## Summary

Families need a place to store the IBAN used by the association to charge direct debits (*domiciliaciones* — annual membership fees and camp installments). Today the only IBAN in the system is the **association's own** account (`PaymentSettings.Iban`, used for inbound bank transfers); there is no field for the **family's** account.

This feature adds:

1. A per-family IBAN stored **encrypted at rest** on `FamilyUnit`, editable by the family representative.
2. The **account holder's identity** — full name, document number and postal address. The holder is **not necessarily the family representative** and may not be a platform user at all (a grandparent, an ex-spouse, a company). This is captured explicitly rather than inferred.
3. An **email notification to the board** (`Resend:BoardBccEmail`) every time the bank details are created, changed or removed, so the Junta can update its banking records.
4. A **liability warning to families**: if the IBAN is missing or out of date, a direct debit may be returned by the bank and the family must bear the cost of the resulting surcharge (*recargo por devolución*). The warning is shown persistently and must be explicitly acknowledged when saving.

The SEPA mandate itself stays **on paper**, managed by the Junta outside the platform — see *Resolved: SEPA Mandate Scope* below.

---

## Business Context

- The Junta processes direct debits outside the platform (manually / via the bank's portal). The platform is the **source of truth for the debtor data the Junta keys into the bank**, not the payment rail. No SEPA file generation is in scope.
- **The SEPA mandate is signed on paper** and is not digitised; the Junta has no plans to digitise it. The platform therefore stores no mandate reference, sequence type or signature evidence — it stores the data a Junta member needs in front of them to fill in the bank's form.
- **The account holder is often not the representative.** Families do not reliably volunteer the holder's exact personal data, and a holder name that does not match the bank's records is one of the most common causes of a rejected debit. The form must ask for it explicitly instead of defaulting to the representative.
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

- The representative sees a "Datos bancarios" section on the family page showing the IBAN **masked** (`ES** **** **** **** **** 1234`), the account holder's name, and the date it was last updated.
- Saving requires naming the account holder — chosen from the family members or entered freely for someone outside the family — and ticking a surcharge-liability + consent checkbox; the acceptance timestamp is stored.
- On every successful create/update/delete of the IBAN, an email is sent to the representative with the board in `Bcc`, stating what changed (masked values only).
- When no IBAN is stored, a persistent orange warning appears on the family page.
- Admin/Board see, in the family units admin list, whether each family has an IBAN, and can retrieve the full IBAN through a dedicated, logged, board-only endpoint.

---

## Scope

### In scope

1. `FamilyUnit` entity: encrypted `Iban`, `IbanLast4`, `IbanUpdatedAt`, `IbanUpdatedByUserId`, `IbanSurchargeTermsAcceptedAt`, plus the account-holder block (`AccountHolderName`, `AccountHolderDocumentNumber`, `AccountHolderStreetAddress`, `AccountHolderPostalCode`, `AccountHolderLocality`, `AccountHolderProvince`, `AccountHolderCountry`) + EF configuration + migration.
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
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs` | Add the 5 IBAN fields + the 7 account-holder fields to `FamilyUnit`. Add `UpdateFamilyIbanRequest`, `FamilyIbanResponse`. Extend `FamilyUnitResponse` + `ToResponse()` with `hasIban`, `ibanMasked`, `ibanUpdatedAt` and the holder block. Extend `FamilyUnitAdminProjection` / `FamilyUnitListItemResponse` with `HasIban`. |
| `src/Abuvi.API/Features/FamilyUnits/UpdateFamilyIbanValidator.cs` | **New.** FluentValidation for `UpdateFamilyIbanRequest`. |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsEndpoints.cs` | Map `PUT`/`DELETE` `/{id:guid}/iban` on `group`; map `GET /{id:guid}/iban` on `adminGroup`. |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs` | Inject `IEmailService`. Add `UpdateIbanAsync`, `DeleteIbanAsync`, `GetIbanAsync`. |
| `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs` | Include `HasIban` in `GetAllPagedAsync` projection (`fu.IbanLast4 != null`). |
| `src/Abuvi.API/Common/Validation/IbanValidator.cs` | **New.** `Normalize(string)` + `IsValidSpanishIban(string)` (regex + mod-97) + `Mask(string)`. |
| `src/Abuvi.API/Common/Services/IEmailService.cs` | Add `FamilyIbanUpdatedEmailData` record + `SendFamilyIbanUpdatedAsync`. |
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Implement `SendFamilyIbanUpdatedAsync` (To = representative, Bcc = `_boardBccEmail`). |
| `src/Abuvi.API/Data/Configurations/FamilyUnitConfiguration.cs` | Map the 12 new columns. |
| `src/Abuvi.API/Migrations/` | `AddFamilyUnitIbanFields` migration. |

### Frontend

| File | Change |
| --- | --- |
| `frontend/src/types/family-unit.ts` | Add `hasIban`, `ibanMasked`, `ibanUpdatedAt` and the holder fields to `FamilyUnitResponse`; add `UpdateFamilyIbanRequest`, `FamilyIbanResponse`. |
| `frontend/src/components/family-units/FamilyIbanForm.vue` | **New.** IBAN input + account-holder sub-section (holder selector, name, document, address) + surcharge/consent acknowledgement checkbox. |
| `frontend/src/views/FamilyUnitPage.vue` | New "Datos bancarios" `Card` with masked IBAN, holder name, last-updated date, edit/remove actions; orange `Message` banner when `!hasIban`. |
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

// ── Account holder (titular de la cuenta) ──────────────────────────────
// NOT necessarily the representative, and not necessarily a platform user.
// Stored in plaintext: this is ordinary contact PII, treated like
// FamilyMember.Email / FamilyMember.Phone. Only the IBAN is encrypted.

// Full name EXACTLY as it appears on the bank account.
public string? AccountHolderName { get; set; }

// NIF/NIE of the holder. Required by most Spanish banks on the mandate form.
public string? AccountHolderDocumentNumber { get; set; }

// Postal address of the holder — printed on the paper mandate.
public string? AccountHolderStreetAddress { get; set; }
public string? AccountHolderPostalCode { get; set; }
public string? AccountHolderLocality { get; set; }
public string? AccountHolderProvince { get; set; }
public string? AccountHolderCountry { get; set; }   // ISO-3166 alpha-2, default "ES"
```

**Invariants**

- `Iban`, `IbanLast4`, `IbanUpdatedAt`, `IbanUpdatedByUserId`, `AccountHolderName` are all null or all non-null — an IBAN without a named holder is not usable at the bank, so the holder name is mandatory whenever an IBAN exists.
- Deleting the IBAN nulls **every** field in both blocks, holder data included.
- `AccountHolderCountry` defaults to `"ES"` when an IBAN is set and no country is supplied.

**Why the holder block is not encrypted**

The IBAN is encrypted because it is directly actionable financial data. The holder's name and address are ordinary contact PII, equivalent to `FamilyMember.Email` and `FamilyMember.Phone`, which this codebase stores in plaintext; encrypting them would force a decrypt on every admin list render for no meaningful gain. They are still PII: excluded from logs, and covered by the GDPR notes below.

**Third-party data consent**

When the holder is not a member of the family unit, the family is supplying a third party's personal data. The acknowledgement checkbox must therefore also cover consent, mirroring the existing pattern in `FamilyUnitForm.vue:107-111` ("confirmo que tengo el consentimiento de cada miembro de la familia"). Exact copy in the *Frontend* section.

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

builder.Property(fu => fu.AccountHolderName)
    .HasMaxLength(140)                 // SEPA debtor-name limit
    .HasColumnName("account_holder_name");

builder.Property(fu => fu.AccountHolderDocumentNumber)
    .HasMaxLength(20)
    .HasColumnName("account_holder_document_number");

builder.Property(fu => fu.AccountHolderStreetAddress)
    .HasMaxLength(200)
    .HasColumnName("account_holder_street_address");

builder.Property(fu => fu.AccountHolderPostalCode)
    .HasMaxLength(10)
    .HasColumnName("account_holder_postal_code");

builder.Property(fu => fu.AccountHolderLocality)
    .HasMaxLength(100)
    .HasColumnName("account_holder_locality");

builder.Property(fu => fu.AccountHolderProvince)
    .HasMaxLength(100)
    .HasColumnName("account_holder_province");

builder.Property(fu => fu.AccountHolderCountry)
    .HasMaxLength(2)
    .HasColumnName("account_holder_country");
```

No index and no FK on `iban_updated_by_user_id` (audit reference only, must survive user deletion).

### Data model doc

Add to `ai-specs/specs/data-model.md` → `### FamilyUnit` → **Fields**:

- `iban`: Family bank account for direct debits, **AES-256 encrypted at rest** (optional)
- `ibanLast4`: Last 4 digits in plaintext for display/filtering (optional, 4 chars)
- `ibanUpdatedAt`: Timestamp of the last IBAN change (optional)
- `ibanUpdatedByUserId`: User who performed the last IBAN change (optional, audit only, no FK)
- `ibanSurchargeTermsAcceptedAt`: When the family accepted liability for returned-direct-debit surcharges (optional)
- `accountHolderName`: Full name of the account holder exactly as registered at the bank — not necessarily the representative (optional, max 140)
- `accountHolderDocumentNumber`: NIF/NIE of the account holder (optional, max 20)
- `accountHolderStreetAddress`, `accountHolderPostalCode`, `accountHolderLocality`, `accountHolderProvince`, `accountHolderCountry`: Postal address of the account holder, printed on the paper SEPA mandate (optional)

And under **Validation rules**:

- *IBAN must be a Spanish IBAN (`ES` + 22 digits) with a valid mod-97 checksum; it is never exposed in plaintext except through the Admin/Board-only retrieval endpoint.*
- *Whenever an IBAN is stored, `accountHolderName` is mandatory. The account holder may be any person, inside or outside the family unit.*

---

## API Contract

### `PUT /api/family-units/{id}/iban`

Sets or replaces the family's direct-debit IBAN.

**Authorization**: representative of the family unit only (403 otherwise, including Admin/Board).

**Request:**

```json
{
  "iban": "ES9121000418450200051332",
  "accountHolderName": "María López Fernández",
  "accountHolderDocumentNumber": "12345678Z",
  "accountHolderStreetAddress": "Calle Mayor 12, 3ºB",
  "accountHolderPostalCode": "28013",
  "accountHolderLocality": "Madrid",
  "accountHolderProvince": "Madrid",
  "accountHolderCountry": "ES",
  "surchargeTermsAccepted": true
}
```

- `iban` is normalised server-side: whitespace stripped, upper-cased, before validation and encryption.
- `accountHolderName` is required. Everything else in the holder block is optional at API level (see *Validation* for why the address is required by the UI but not the API).
- `accountHolderCountry` defaults to `"ES"` when omitted.

**Response 200 OK** — `ApiResponse<FamilyUnitResponse>` with the new masked value and the holder block.

**Errors:**

- `400 Bad Request` — invalid IBAN format or checksum, missing `accountHolderName`, or `surchargeTermsAccepted` is `false`.
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
  "ibanUpdatedAt": "2026-08-24T10:12:00Z",
  "accountHolderName": "María López Fernández",
  "accountHolderDocumentNumber": "12345678Z",
  "accountHolderStreetAddress": "Calle Mayor 12, 3ºB",
  "accountHolderPostalCode": "28013",
  "accountHolderLocality": "Madrid",
  "accountHolderProvince": "Madrid",
  "accountHolderCountry": "ES"
}
```

`ibanMasked` is built from `IbanLast4` only — the ciphertext is never decrypted to build a list response. The holder block **is** returned in full (it is not encrypted and the family needs it to review and correct their own data); it is visible to the representative and to Admin/Board, and to nobody else.

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

RuleFor(x => x.AccountHolderName)
    .NotEmpty().WithMessage("El nombre del titular de la cuenta es obligatorio.")
    .MaximumLength(140);

RuleFor(x => x.AccountHolderDocumentNumber)
    .MaximumLength(20)
    .When(x => !string.IsNullOrWhiteSpace(x.AccountHolderDocumentNumber));

RuleFor(x => x.AccountHolderPostalCode)
    .Matches(@"^\d{5}$").WithMessage("El código postal debe tener 5 dígitos.")
    .When(x => !string.IsNullOrWhiteSpace(x.AccountHolderPostalCode)
               && (x.AccountHolderCountry ?? "ES") == "ES");

RuleFor(x => x.AccountHolderCountry)
    .Matches(@"^[A-Z]{2}$").WithMessage("El país debe ser un código ISO de 2 letras.")
    .When(x => !string.IsNullOrWhiteSpace(x.AccountHolderCountry));

RuleFor(x => x.SurchargeTermsAccepted)
    .Equal(true)
    .WithMessage("Debes aceptar las condiciones sobre el recargo por devolución de recibos.");
```

Wired through the existing `ValidationFilter<UpdateFamilyIbanRequest>` endpoint filter.

**Deliberate asymmetry — address required in the UI, optional in the API.** The address is needed to fill in the paper mandate, so the form asks for it and marks it required. It is *not* enforced server-side because the Junta must be able to register a family whose holder address is incomplete rather than being blocked, and because a future admin-side correction flow would otherwise be unable to save a partial record. `accountHolderName` is the one holder field enforced at both layers, because an IBAN without a named holder is unusable at the bank.

**No NIF checksum validation.** The document number is checked for length only. The holder may be a foreign resident (NIE), a company (CIF), or a non-resident, and a wrong-but-well-formed NIF is caught by the bank, not by us. Over-validating here would block legitimate families.

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
    public string? PreviousAccountHolderName { get; init; }  // null when Action == Added
    public string? NewAccountHolderName { get; init; }       // null when Action == Removed
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
> | Titular anterior | {PreviousAccountHolderName ?? "—"} |
> | Titular actual | {NewAccountHolderName ?? "— (eliminado)"} |
> | Fecha del cambio | {ChangedAtUtc:dd/MM/yyyy HH:mm} |
>
> Consulta los datos completos del titular (documento y dirección) en la ficha de la familia para actualizar el mandato en papel.
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
    var previousHolderName = familyUnit.AccountHolderName;

    // No-op guard: neither the IBAN nor the holder changed → persist (refreshing
    // the acceptance timestamp) but send no email. A holder-name change alone
    // still notifies: the Junta must issue a new paper mandate for it.
    var isUnchanged = previousLast4 is not null
        && encryptionService.Decrypt(familyUnit.Iban!) == normalized
        && previousHolderName == request.AccountHolderName;

    familyUnit.Iban = encryptionService.Encrypt(normalized);
    familyUnit.IbanLast4 = normalized[^4..];
    familyUnit.IbanUpdatedAt = DateTime.UtcNow;
    familyUnit.IbanUpdatedByUserId = userId;
    familyUnit.IbanSurchargeTermsAcceptedAt = DateTime.UtcNow;

    familyUnit.AccountHolderName = request.AccountHolderName.Trim();
    familyUnit.AccountHolderDocumentNumber = request.AccountHolderDocumentNumber?.Trim().ToUpperInvariant();
    familyUnit.AccountHolderStreetAddress = request.AccountHolderStreetAddress?.Trim();
    familyUnit.AccountHolderPostalCode = request.AccountHolderPostalCode?.Trim();
    familyUnit.AccountHolderLocality = request.AccountHolderLocality?.Trim();
    familyUnit.AccountHolderProvince = request.AccountHolderProvince?.Trim();
    familyUnit.AccountHolderCountry = string.IsNullOrWhiteSpace(request.AccountHolderCountry)
        ? "ES"
        : request.AccountHolderCountry.Trim().ToUpperInvariant();

    await repository.UpdateFamilyUnitAsync(familyUnit, ct);

    logger.LogInformation(
        "Family unit {FamilyUnitId} IBAN updated by {UserId} (last4 {Last4})",
        familyUnitId, userId, familyUnit.IbanLast4);

    if (!isUnchanged)
        await SendIbanNotificationSafeAsync(
            familyUnit, previousLast4, previousHolderName, ct);

    return familyUnit.ToResponse();
}
```

`DeleteIbanAsync` mirrors it: representative check, return early (no email) when `IbanLast4 is null`, null **all** IBAN and holder fields, notify with `Action = Removed`.

`SendIbanNotificationSafeAsync` wraps the email call in `try/catch`, logs the failure and **never propagates** — an email outage must not roll back the IBAN change (same isolation rule as `ChangeStatusAsync` in `RegistrationsService`).

---

## Frontend

### `FamilyIbanForm.vue` (new)

- `InputText` for the IBAN, `maxlength="34"`, auto-normalised on input (strip spaces, upper-case) and rendered in 4-char groups for readability.
- Client-side mirror of `IbanValidator.IsValidSpanishIban` for immediate feedback; the server remains authoritative.
- Persistent `Message severity="warn" :closable="false"` above the field:

  > Los recibos se domiciliarán en esta cuenta. **Si el IBAN no está actualizado y el recibo es devuelto, el coste del recargo generado por la devolución correrá a cargo de la familia.**

- Mandatory `Checkbox` bound to `surchargeTermsAccepted` (same interaction pattern as the existing consent checkbox in `FamilyUnitForm.vue:103-114`). It covers **two** things — surcharge liability and third-party data consent — because the holder may not be a family member:

  > He comprobado que el IBAN y los datos del titular son correctos y acepto asumir el coste del recargo si el recibo resulta devuelto por datos bancarios incorrectos o desactualizados. Si el titular no soy yo, confirmo que cuento con su consentimiento para facilitar sus datos.

- Submit disabled until the IBAN is valid, the holder name is filled **and** the checkbox is ticked.
- The form never receives the current full IBAN — editing always means typing the complete new number. The holder block **is** pre-filled from the current values, since it is not encrypted and re-typing an address is pure friction.

### Account holder sub-section (`FamilyIbanForm.vue`)

This is the part most likely to be filled in wrong, so the UI has to work against the assumption that the holder is the representative.

- A `Select` at the top of the sub-section — **"¿Quién es el titular de la cuenta?"** — listing the family members plus a final `Otra persona` option. Choosing a member pre-fills name and document number from `FamilyMemberResponse`; choosing `Otra persona` clears them and leaves every field editable. The selection is a **UI convenience only** and is not persisted: the stored value is always the resolved name/document text, never a member id. A member can later change their own name without silently rewriting a bank mandate.
- Name field helper text, directly under the input:

  > Escribe el nombre **exactamente como figura en el banco**. Si no coincide, el banco puede devolver el recibo.

- Fields, in mandate-form order: `Nombre y apellidos del titular` (required), `NIF/NIE`, `Dirección`, `Código postal`, `Población`, `Provincia`, `País` (defaults to `España`).
- The address fields are marked required in the UI even though the API accepts them empty (see *Validation*).
- An `info` `Message` at the top of the sub-section, because the paper mandate is the legal instrument and families should not assume the app replaces it:

  > Estos datos son los que la Junta necesita para preparar la **orden de domiciliación en papel**. Si cambias el titular o el IBAN, la Junta te pedirá firmar una orden nueva.

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
2. **Entity + EF config + migration** — add the 5 IBAN fields and the 7 account-holder fields, map columns, generate `AddFamilyUnitIbanFields`, verify it applies against a clean database.
3. **DTOs + mapping** — `UpdateFamilyIbanRequest`, `FamilyIbanResponse`; extend `FamilyUnitResponse` and `ToResponse()` with the masked IBAN and the holder block; extend the admin projection and its repository query.
4. **`UpdateFamilyIbanValidator` (tests first)** — invalid IBAN rejected; empty `accountHolderName` rejected; malformed Spanish postal code rejected; non-Spanish country skips the postal-code rule; `surchargeTermsAccepted: false` rejected.
5. **Service (tests first)**
   - `UpdateIbanAsync` by a non-representative → `ForbiddenException`.
   - `UpdateIbanAsync` on a first-time set → `Iban` encrypted, `IbanLast4` = last 4, timestamps set, `SendFamilyIbanUpdatedAsync` called with `Action = Added` and `PreviousIbanMasked = null`.
   - `UpdateIbanAsync` replacing an existing IBAN → `Action = Updated`, previous masked value and previous holder name passed.
   - `UpdateIbanAsync` with the identical IBAN **and** identical holder name → persisted, **no email sent**.
   - `UpdateIbanAsync` with the identical IBAN but a **changed holder name** → email **is** sent (the Junta needs a new paper mandate).
   - `UpdateIbanAsync` with no `AccountHolderCountry` → stored as `"ES"`.
   - `DeleteIbanAsync` → all IBAN **and** holder fields null, `Action = Removed`.
   - `DeleteIbanAsync` when no IBAN stored → no email, no exception.
   - Email throws → IBAN change still persists, exception swallowed and logged.
   - `GetIbanAsync` on a family with no IBAN → `NotFoundException`.
6. **Email service** — `FamilyIbanUpdatedEmailData` + `SendFamilyIbanUpdatedAsync`; test with the mocked `IResendClient` that `Bcc` contains the board address and that the body contains **no** unmasked IBAN.
7. **Endpoints** — map the three routes with the correct groups, filters and `Produces` metadata; extract `userId` from claims exactly as the sibling endpoints do.
8. **Frontend types + composable** — `updateFamilyIban`, `deleteFamilyIban`, `getFamilyIban`; refresh `familyUnit.value` from the response.
9. **`FamilyIbanForm.vue` (tests first)** — submit disabled until the IBAN is valid, the holder name is filled and the box is ticked; invalid IBAN shows an error; submit emits the normalised value; selecting a family member in the holder selector pre-fills name and document; selecting `Otra persona` clears them; the holder block is pre-filled on edit while the IBAN field starts empty.
10. **`FamilyUnitPage.vue`** — bank-details card, missing-IBAN banner, delete confirmation.
11. **`FamilyUnitsAdminPanel.vue`** — IBAN column.
12. **Docs** — update `data-model.md` (FamilyUnit fields) and `api-endpoints.md` (three endpoints + the additive response fields, in the existing Family Units section around line 759-820).

---

## Non-functional Requirements

- **Security — encryption at rest**: the IBAN is stored AES-256 encrypted via `IEncryptionService`, exactly like `FamilyMember.MedicalNotes` (`FamilyUnitsService.cs:184-190`). Plaintext exists only in memory during a request.
- **Security — exposure**: no list or detail response ever returns the plaintext IBAN. The single plaintext path is the Admin/Board `GET /api/family-units/{id}/iban`.
- **Security — logging**: never log the plaintext IBAN, in any log level, in any service, or in Sentry breadcrumbs. Log `IbanLast4` only. The holder's name, document number and address are PII and must not be logged either.
- **Third-party PII**: the account holder may be someone with no account on the platform. Their data is collected on the family's declared consent (the acknowledgement checkbox), is visible only to the representative and Admin/Board, and is deleted with the rest of the bank block when the IBAN is removed.
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
- [ ] Saving is impossible without a holder name; the holder can be any family member **or** a person outside the family, chosen through the "¿Quién es el titular?" selector.
- [ ] Selecting a family member as holder pre-fills the name and document; the stored value is text, not a member reference, and does not change if that member later renames themselves.
- [ ] The holder's document number and postal address are stored and shown back to the representative and to Admin/Board.
- [ ] Changing only the holder name (same IBAN) still notifies the board.
- [ ] Saving is impossible without ticking the acknowledgement, whose text covers both the surcharge liability and third-party data consent; the timestamp is persisted in `iban_surcharge_terms_accepted_at`.
- [ ] The stored IBAN is encrypted in the database — a direct `SELECT iban_encrypted FROM family_units` shows ciphertext, never `ES…`.
- [ ] `GET /api/family-units/me` returns `hasIban`, `ibanMasked` and `ibanUpdatedAt`, and **never** the full IBAN.
- [ ] Creating, changing or removing the bank details sends an email to the representative with `Resend:BoardBccEmail` in `Bcc`, stating the action, the masked previous/new IBAN and the previous/new holder name.
- [ ] Re-submitting identical IBAN **and** holder name does not send an email.
- [ ] `DELETE` clears the holder block as well as the IBAN — no orphan name or address is left behind.
- [ ] A Resend failure leaves the IBAN change persisted and returns `200`/`204`.
- [ ] A non-representative (including Admin/Board) gets `403` on `PUT`/`DELETE` `/api/family-units/{id}/iban`.
- [ ] `GET /api/family-units/{id}/iban` returns the plaintext IBAN for Admin/Board, `403` for a Member, and `404` when no IBAN is stored; the access is logged.
- [ ] A family with no IBAN sees the orange warning banner naming the surcharge liability on the family page.
- [ ] The admin family units list shows an IBAN `Sí`/`No` tag per family.
- [ ] `data-model.md` and `api-endpoints.md` are updated.
- [ ] All new and existing unit tests pass.

---

## Resolved: SEPA Mandate Scope

**Decision (confirmed with the Junta, 2026-08-25): the platform stores debtor data only. The SEPA mandate stays on paper.**

The mandate (*orden de domiciliación*) is signed on paper and held by the Junta; there is no plan to digitise it. The platform is therefore **not** the legal record of the mandate — it is the place a Junta member looks up the data to fill in the bank's form.

### What this decision removes from scope

Because the mandate is not digital, none of the following are built:

| Not built | Why |
| --- | --- |
| Mandate reference (RUM), sequence type (`FRST`/`RCUR`/`OOFF`), mandate status machine | Only meaningful when the platform generates collections. The bank/Junta owns the sequence. |
| Digital signature evidence (accepted text, timestamp, IP) | The signature is a wet signature on paper. `IbanSurchargeTermsAcceptedAt` covers the association's own surcharge policy and is **not** a mandate signature — the two remain distinct. |
| `SepaMandate` entity with amendment history | With no mandate to amend in-platform, overwriting the IBAN in place is correct. This is what keeps the design to plain columns on `FamilyUnit`. |
| Creditor identifier on `PaymentSettings` | Nothing in-platform consumes it. |
| pain.008 XML export | Debits are keyed in manually. |

### What this decision adds to scope

The account-holder block, because the paper form needs data the platform did not previously hold:

- **The holder is often not the representative**, and families do not reliably supply the holder's exact personal data. Defaulting to the representative would silently produce mandates the bank rejects, so the holder's name is asked for explicitly and is mandatory whenever an IBAN exists.
- **The holder's postal address is printed on the mandate form**, so it is captured as structured fields (street, postal code, locality, province, country) rather than free text.

### Operational consequences the Junta owns, not the platform

These are recorded so nobody later mistakes them for gaps in the implementation:

1. **A changed IBAN or holder requires a newly signed paper mandate.** The platform cannot amend a paper document; it notifies the board so a Junta member can request a new signature. The notification email and the family-facing form both say so explicitly.
2. **A SEPA mandate lapses 36 months after the last collection.** Tracking that is offline bookkeeping. `IbanUpdatedAt` is stored and would support a future reminder, but no expiry logic is built here.

### Revisit if

The Junta ever decides to generate collections from the platform (a pain.008 export, or debiting as a registered SEPA creditor). At that point the design does change shape — mandates gain references, sequence types and amendment history, and `FamilyUnit` columns become a `SepaMandate` collection. That would be a new spec, not an edit to this one. Everything built here — the encrypted IBAN, the holder block, the board notification, the surcharge acknowledgement — carries over as the debtor half of that model.

---

## Open Questions

1. **Non-Spanish IBANs** — any member families with foreign accounts? Current spec accepts `ES` only, so a family banking abroad would be blocked at the form.
2. **Surcharge amount** — should the warning quote a concrete figure (e.g. "aprox. X €") or stay generic as specified here?
3. **Existing paper mandates** — the Junta already holds signed mandates for current members. Should those IBANs be back-loaded into the platform by the board (which would need an admin write path, currently out of scope and assigned to `feat-admin-edit-profiles`), or will families be asked to enter their own? This affects rollout, not the data model.
