# Banc Sabadell Norma43 Import for SEPA CORE Membership Fees — Enriched Technical Specification

## Overview

This feature enables board members to streamline the reconciliation of SEPA CORE direct debit transactions with family unit membership fee payments. Board members download a Norma43 format statement export from Banc Sabadell. The system parses the fixed-width Norma43 format, identifies membership fee debit transactions (by transaction type "05"), fuzzy-matches each debit to a pending membership fee by family name from the transaction concept lines, presents match results for human review and correction, and bulk-confirms all matched payments in a single operation.

## Problem Statement

- Association uses SEPA CORE direct debits for membership fee collection via Banc Sabadell
- Board members receive monthly Norma43 statements from their bank
- Current flow: manually review debit transactions and identify which correspond to which families (error-prone, time-consuming)
- Desired outcome: automated matching of debit transactions to pending fees with user verification and bulk payment confirmation

## Bank-Specific Context

- **Bank**: Banc Sabadell (Spanish bank)
- **Export Format**: Norma43 (Spanish standard fixed-width electronic bank statement format, ISO 20022 predecessor)
- **Payment Method**: SEPA CORE direct debits (recurring, mandate-based)
- **Transaction Type**: Debit entries (type "05" in Norma43 — outgoing direct debits)

### Norma43 Format Overview

Norma43 is a fixed-width format with record types:

- **Record Type 0**: File header
- **Record Type 1**: Account summary
- **Record Type 2**: Transaction detail (debit/credit, date, amount, reference)
- **Record Type 3**: Transaction complement (concept/description lines, 70 chars each, up to 3 per transaction)
- **Record Type 4**: Account totals
- **Record Type 8**: File information
- **Record Type 9**: File footer

For this feature, we parse **Record Type 2 & 3**:

- Record Type 2 contains: date, transaction type (05=SEPA debit), amount, bank reference, payer reference
- Record Type 3 contains: family name and mandate reference (in concept lines)

## Solution Architecture

### Two-Step Wizard Flow

1. **Step 1: Upload Norma43 File** — User uploads `.txt` Norma43 export from Banc Sabadell; system auto-detects format and debit transactions
2. **Step 2: Review & Correct Matches** — Server parses debit transactions (Record Type 2 + 3), fuzzy-matches each by amount + family name in concept lines to pending fees; user reviews confidence badges, corrects mismatches, unchecks rows to skip
3. **Step 3: Bulk Confirm** — Server calls existing `PayFeeAsync` service for each confirmed row; transactions marked as paid with transaction reference + value date

No new database tables. Norma43 data is ephemeral (never persisted).

---

## Data Model

### No New Entities

The feature leverages existing models:

- `MembershipFee` (existing) — state changes from `Pending` → `Paid`
- `Membership` (existing) — parent of fees
- `FamilyMember` / `FamilyUnit` (existing) — used for matching and display

### New DTOs

All DTOs added to `src/Abuvi.API/Features/Memberships/CsvImportModels.cs`:

```csharp
// Norma43 file upload — no column mapping needed (format is fixed-width standard)
// CsvColumnMapping kept for API compatibility but empty for Norma43
public record CsvColumnMapping(
    string SenderNameColumn = "",
    string AmountColumn = "",
    string DateColumn = "",
    string? DescriptionColumn = null
);

// Confidence enum
public enum MatchConfidence { High, Medium, Low, None }

// One parsed Norma43 debit transaction + matched fee
public record CsvMatchResult(
    int RowIndex,
    string RawTransactionReference,      // Norma43 bank reference (19 chars)
    string RawConceptLines,              // Concatenated concept lines (family name usually here)
    decimal Amount,
    DateOnly ValueDate,                  // When debit was posted to account (from Record Type 2)
    string? TransactionType,             // "05" for SEPA CORE debit
    Guid? FeeId,
    Guid? MembershipId,
    string? FamilyUnitName,
    string? MemberName,
    decimal? FeeAmount,
    MatchConfidence Confidence
);

// Confirmed match (after user review)
public record CsvConfirmItem(
    int RowIndex,
    Guid FeeId,
    Guid MembershipId,
    DateOnly PaidDate,                   // Value date from Norma43 debit
    string? PaymentReference              // Norma43 transaction reference code
);

// Bulk confirm request
public record CsvBulkConfirmRequest(
    IReadOnlyList<CsvConfirmItem> Items
);

// Result per item
public record CsvConfirmItemResult(
    int RowIndex,
    bool Success,
    string? Error
);

// Aggregate result
public record CsvBulkConfirmResult(
    int Confirmed,
    int Failed,
    IReadOnlyList<CsvConfirmItemResult> Results
);

// Internal DTO for matching
public record PendingFeeForMatching(
    Guid FeeId,
    Guid MembershipId,
    string FamilyUnitName,
    string MemberName,
    decimal Amount
);

// Internal DTO: parsed Norma43 debit transaction
public class Norma43DebitTransaction
{
    public string TransactionReference { get; set; } = "";
    public string TransactionType { get; set; } = "05";
    public DateOnly ValueDate { get; set; }
    public decimal Amount { get; set; }
    public string ConceptLines { get; set; } = ""; // Concatenated concept lines
}
```

---

## Backend Implementation

### 1. Repository Query

**File:** `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs`

Add to interface `IMembershipsRepository`:

```csharp
Task<IReadOnlyList<PendingFeeForMatching>> GetPendingFeesForMatchingAsync(int year, CancellationToken ct);
```

Implementation returns all active pending membership fees for the given year.

### 2. Norma43 Import Service

**File:** `src/Abuvi.API/Features/Memberships/CsvImportService.cs` (NEW)

Main methods:

**`ParseAndMatchAsync(IFormFile norma43File, CsvColumnMapping mapping, CancellationToken ct)`**:

- Accepts any IFormFile; ignores `mapping` parameter (Norma43 is fixed-width)
- Calls `ParseNorma43DebitTransactionsAsync()` to extract debit transactions
- For each debit: calls `BestMatchByAmountAndConcept()` to find best pending fee
- Returns `List<CsvMatchResult>` with match results and confidence levels

**`BulkConfirmAsync(CsvBulkConfirmRequest request, CancellationToken ct)`**:

- For each confirmed item: calls `MembershipsService.PayFeeAsync()`
- Stores `PaymentReference` as Norma43 transaction reference for audit trail
- Returns `CsvBulkConfirmResult` with success/failure counts per item

**Helper: `ParseNorma43DebitTransactionsAsync()`**:

- Reads file as ISO-8859-1 (Latin-1, standard for Norma43)
- Iterates over lines, filters Record Type 2 (transactions) where transaction type = "05" (SEPA CORE debit)
- Parses fixed-width fields (position 0-1 = record type, 10-11 = txn type, 12-17 = value date YYMMDD, 22-33 = amount, 28-46 = bank reference, 47-66 = payer reference)
- Captures subsequent Record Type 3 lines (concept lines) and concatenates them
- Returns list of `Norma43DebitTransaction` with amount, date, and concatenated concept

**Helper: `BestMatchByAmountAndConcept()`**:

- Three-pass matching strategy:
  1. Exact amount match + fuzzy name match (score ≥ 0.50) → return best fuzzy match
  2. Amount within ±10% tolerance + fuzzy name match (score ≥ 0.50) → return best
  3. Best fuzzy name match (score ≥ 0.70) regardless of amount → return best
  4. If no match found, return null

**Helper: Fuzzy matching**:

- Normalize both strings: remove accents (NFD decomposition, strip diacritics), lowercase, keep only alphanumeric + spaces
- Calculate Levenshtein distance
- Score: substring containment = 0.85, Levenshtein ratio = 1 - (distance / max_len)
- Thresholds: ≥0.90 = High, ≥0.70 = Medium, ≥0.50 = Low, <0.50 = None

### 3. New Endpoints

**File:** `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs`

Add new static method `MapCsvImportEndpoints()`:

```csharp
POST /api/memberships/fees/csv-import/parse
  - multipart/form-data: file (Norma43 .txt)
  - mapping parameter ignored (always empty for Norma43)
  - Auth: Board/Admin only
  - Returns: ApiResponse<List<CsvMatchResult>>

POST /api/memberships/fees/csv-import/confirm
  - Body: CsvBulkConfirmRequest (list of confirmed items)
  - Auth: Board/Admin only
  - Returns: ApiResponse<CsvBulkConfirmResult>
```

### 4. Service Registration

**File:** `src/Abuvi.API/Program.cs`

Register: `services.AddScoped<CsvImportService>();`

Call endpoint mapping: `app.MapCsvImportEndpoints();`

---

## Frontend Implementation

### 1. New Types

**File:** `frontend/src/types/membership.ts`

```typescript
export type MatchConfidence = 'High' | 'Medium' | 'Low' | 'None'

// No CsvColumnMapping needed for Norma43 (fixed format, no user mapping)

export interface CsvMatchResult {
  rowIndex: number
  rawTransactionReference: string // Norma43 bank reference
  rawConceptLines: string // Family name from Norma43 concept
  amount: number
  valueDate: string // YYYY-MM-DD
  transactionType?: string // "05" for SEPA CORE
  feeId: string | null
  membershipId: string | null
  familyUnitName: string | null
  memberName: string | null
  feeAmount: number | null
  confidence: MatchConfidence
}

export interface CsvConfirmItem {
  rowIndex: number
  feeId: string
  membershipId: string
  paidDate: string // YYYY-MM-DD
  paymentReference: string | null
}

export interface CsvBulkConfirmRequest {
  items: CsvConfirmItem[]
}

export interface CsvBulkConfirmResult {
  confirmed: number
  failed: number
  results: Array<{ rowIndex: number; success: boolean; error: string | null }>
}
```

### 2. New Composable

**File:** `frontend/src/composables/useCsvImport.ts` (NEW)

Wraps the two endpoints:

- `parseAndMatch(file: File)` — POSTs to `/api/memberships/fees/csv-import/parse`
- `bulkConfirm(request: CsvBulkConfirmRequest)` — POSTs to `/api/memberships/fees/csv-import/confirm`

No column mapping needed; `mapping` parameter is empty for Norma43.

### 3. Import Wizard Component

**File:** `frontend/src/components/memberships/CsvImportPanel.vue` (NEW)

Two-step wizard:

**Step 1: Upload**

- `FileUpload` component: accept `.txt` files only (Norma43 format)
- Info text: "Descargue su extracto mensual de Banc Sabadell en formato Norma43 (.txt)"
- "Procesar archivo" button calls `parseAndMatch()`

**Step 2: Review**

- `DataTable` showing:
  - Row number
  - Transaction reference (rawTransactionReference)
  - Concept lines (rawConceptLines)
  - Debit amount
  - Value date
  - Matched family unit
  - Matched member
  - Confidence tag (colored: green=High, amber=Medium, red=Low/None)
  - Checkbox to include/exclude
- Manual override column: `AutoComplete` to search and select correct family for Low/None rows
- "Confirmar seleccionados" button calls `bulkConfirm()`

**Step 3: Summary**

- Shows success count, failed count, and error details per row
- "Importar otro extracto" resets wizard

### 4. Admin Panel Shell

**File:** `frontend/src/components/admin/MembershipsAdminPanel.vue` (NEW)

Tabbed interface with `CsvImportPanel` as the main tab.

### 5. Routing & Navigation

**File:** `frontend/src/router/index.ts`

Add route:

```typescript
{ path: '/admin/memberships', component: MembershipsAdminPanel, meta: { requiresAuth: true, requiredRole: 'Board' } }
```

**File:** `frontend/src/components/admin/AdminSidebar.vue`

Add menu item under "Finanzas" group:

```typescript
{ label: 'Cuotas', icon: 'pi pi-wallet', to: '/admin/memberships', visible: auth.isBoard }
```

---

## Out of Scope (v1)

- Saving column presets per bank format (Norma43 is single standard)
- Matching previous-year fees (only current year)
- Amount validation against exact fee amount (shown for visual sanity check, ±10% tolerance applied)
- Camp registration payments (separate existing flow)
- Batch processing / async jobs (Norma43 files are small, <100 debits typically)
- Direct import of mandates or SEPA Direct Debit initiation

---

## Security Considerations

- **Authorization**: Both endpoints require Board or Admin role (enforced via `RequireAuthorization`)
- **CSRF**: Parse endpoint uses `.DisableAntiforgery()` for multipart (follows existing `UploadProof` pattern)
- **Input Validation**: File format validated server-side; malformed Norma43 records skipped with warning
- **Encoding**: Norma43 parsed as ISO-8859-1 (Latin-1); non-debit records ignored
- **SQL Injection**: All queries use parameterized EF Core
- **Decimal Precision**: Amounts stored as `decimal`; no floating-point arithmetic

---

## Performance Considerations

- **File Parsing**: Synchronous line-by-line reading acceptable for typical Norma43 files (50–200 debits per month)
- **Fuzzy Matching**: O(n×m) Levenshtein per debit where n=concept length, m=family name length. For 200 families × 100 debits ≈ 20k small comparisons, <50ms
- **Database**: Single query to load all pending fees; no N+1
- **Bulk Confirm**: Sequential calls to `PayFeeAsync`; partial success acceptable

---

## Testing & Verification

### Unit Tests

- Norma43 fixed-width parsing (Record Type 2 & 3)
- Date format conversion (YYMMDD to DateOnly)
- Name normalization and Levenshtein scoring
- Matching algorithm (exact amount, tolerance, fuzzy)

### Integration Tests

1. Upload real Banc Sabadell Norma43 file with 3–5 debit transactions
2. Verify correct parse: transaction count, amounts, dates, concept lines
3. Create pending fees in DB matching debit amounts/families
4. POST parse request → verify match results with correct confidence levels
5. Manually override 1–2 Low matches
6. POST confirm request → verify fees marked as Paid with Norma43 reference stored

### E2E / Manual Testing

1. Board member navigates to `/admin/memberships`
2. Uploads monthly Norma43 file from Banc Sabadell
3. Reviews match results:
   - High-confidence matches auto-checked
   - Low/None matches require override
4. Unchecks any false positives
5. Clicks "Confirmar seleccionados"
6. Verifies fees move to Paid status in family detail page
7. Checks `PaymentReference` field contains Norma43 transaction reference

---

## Documentation Updates

Update `docs/api/memberships.md`:

- Document `/api/memberships/fees/csv-import/parse` endpoint
- Document `/api/memberships/fees/csv-import/confirm` endpoint
- Add Norma43 format explanation
- Provide example Banc Sabadell Norma43 file snippet
- Add example request/response payloads
- Link to Norma43 standard (BOE/AEB)

---

## Files to Create/Modify

| Action | File |
|--------|------|
| Create | `src/Abuvi.API/Features/Memberships/CsvImportModels.cs` |
| Create | `src/Abuvi.API/Features/Memberships/CsvImportService.cs` |
| Modify | `src/Abuvi.API/Features/Memberships/MembershipsRepository.cs` |
| Modify | `src/Abuvi.API/Features/Memberships/MembershipsEndpoints.cs` |
| Modify | `src/Abuvi.API/Program.cs` |
| Modify | `frontend/src/types/membership.ts` |
| Create | `frontend/src/composables/useCsvImport.ts` |
| Create | `frontend/src/components/memberships/CsvImportPanel.vue` |
| Create | `frontend/src/components/admin/MembershipsAdminPanel.vue` |
| Modify | `frontend/src/router/index.ts` |
| Modify | `frontend/src/components/admin/AdminSidebar.vue` |
