# Camp Payments Flow - Enriched User Story

## Objective

Enable users to pay for camp registrations via bank transfer. The system shows users the transfer instructions (concept, IBAN, amount) and requires them to upload a proof of transfer. Admins can then match uploaded proofs with actual bank operations and confirm payments.

Payments are split into two installments: 50% at registration time, and 50% before the camp starts.

## Context

### Current State

- **Registration flow exists**: 4-step wizard (`RegisterForCampPage.vue`) that creates a `Registration` with `Status = Pending`.
- **Payment entity exists** in the database (`payments` table) with `Amount`, `PaymentMethod` (Card/Transfer/Cash), `PaymentStatus` (Pending/Completed/Failed/Refunded), and `ExternalReference`.
- **`PaymentsRepository`** exists with only `GetTotalCompletedAsync()`.
- **Pricing is fully calculated** by `RegistrationPricingService` (base + extras + attendance period).
- **Blob storage is fully built**: S3-compatible upload via `BlobUploadButton` component, `useBlobStorage` composable, and `/api/blobs/upload` endpoint with folder-based organization.

### What Needs to Be Built

1. Two-installment payment logic (auto-created on registration)
2. Bank transfer instructions display (concept, IBAN, amount)
3. Transfer proof upload (leveraging existing blob storage)
4. Payment tracking endpoints (list, upload proof, admin confirm/reject)
5. Frontend payment step in the registration wizard
6. Admin payment management (review proofs, confirm/reject payments)

---

## Domain Model Changes

### `Payment` (existing — add fields)

```csharp
public int InstallmentNumber { get; set; }        // 1 or 2
public DateTime? DueDate { get; set; }             // Installment 2: camp start date minus configurable days
public string? TransferConcept { get; set; }       // Auto-generated concept for the bank transfer (e.g., "CAMP-2026-SMITH-1")
public string? ProofFileUrl { get; set; }          // URL to the uploaded transfer proof (blob storage)
public string? ProofFileName { get; set; }         // Original file name of the proof
public DateTime? ProofUploadedAt { get; set; }     // When the proof was uploaded
public string? AdminNotes { get; set; }            // Admin notes when confirming/rejecting
public Guid? ConfirmedByUserId { get; set; }       // Admin who confirmed/rejected
public DateTime? ConfirmedAt { get; set; }         // When admin confirmed/rejected
```

### `PaymentSettings` (new — stored in `AssociationSettings` as JSON)

No new table. Store payment configuration in the existing `AssociationSettings` table with key `"payment_settings"`:

```json
{
  "iban": "ES12 3456 7890 1234 5678 9012",
  "bankName": "Banco Example",
  "accountHolder": "Asociación ABUVI",
  "secondInstallmentDaysBefore": 15,
  "transferConceptPrefix": "CAMP"
}
```

### Database Migration

- Add columns to `payments`: `installment_number` (int, not null, default 1), `due_date` (timestamp nullable), `transfer_concept` (varchar(100) nullable), `proof_file_url` (varchar(500) nullable), `proof_file_name` (varchar(255) nullable), `proof_uploaded_at` (timestamp nullable), `admin_notes` (text nullable), `confirmed_by_user_id` (uuid nullable, FK to users), `confirmed_at` (timestamp nullable).
- Add index on `payments(transfer_concept)`.

---

## Backend Implementation

### 1. Payments Feature Folder

Create `src/Abuvi.API/Features/Payments/` and move existing `PaymentsRepository` from `Features/Registrations/`.

### 2. Payment Endpoints

**File**: `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/registrations/{registrationId}/payments` | User (own) or Admin | List payments for a registration |
| `GET` | `/api/payments/{paymentId}` | User (own) or Admin | Get single payment details |
| `POST` | `/api/payments/{paymentId}/upload-proof` | User (own) | Upload transfer proof file |
| `DELETE` | `/api/payments/{paymentId}/proof` | User (own, only if Pending) | Remove uploaded proof to re-upload |
| `POST` | `/api/payments/{paymentId}/confirm` | Admin only | Confirm payment (mark as Completed) |
| `POST` | `/api/payments/{paymentId}/reject` | Admin only | Reject proof (mark as Failed, user can re-upload) |
| `GET` | `/api/admin/payments` | Admin only | List all payments with filters (status, edition, date range) |
| `GET` | `/api/admin/payments/pending-review` | Admin only | List payments with uploaded proofs awaiting admin review |
| `GET` | `/api/settings/payment` | Public | Get bank transfer instructions (IBAN, bank name, account holder) |
| `PUT` | `/api/settings/payment` | Admin only | Update payment settings |

### 3. Payment Service

**File**: `src/Abuvi.API/Features/Payments/PaymentsService.cs`

#### Key Methods

```csharp
// Called after registration is created — creates two Payment records with transfer concepts
Task<List<PaymentResponse>> CreateInstallmentsAsync(Guid registrationId);

// Upload transfer proof for a pending payment (uses blob storage)
Task<PaymentResponse> UploadProofAsync(Guid paymentId, Guid userId, IFormFile file);

// Remove uploaded proof (only if payment is still Pending)
Task<PaymentResponse> RemoveProofAsync(Guid paymentId, Guid userId);

// Admin confirms payment after reviewing proof
Task<PaymentResponse> ConfirmPaymentAsync(Guid paymentId, Guid adminUserId, string? notes);

// Admin rejects proof — payment goes back to Pending, user must re-upload
Task<PaymentResponse> RejectPaymentAsync(Guid paymentId, Guid adminUserId, string notes);

// List payments for a registration
Task<List<PaymentResponse>> GetByRegistrationAsync(Guid registrationId);

// Admin: list all payments with filters
Task<PagedResult<AdminPaymentResponse>> GetAllPaymentsAsync(PaymentFilterRequest filter);

// Admin: list payments awaiting review (proof uploaded, status still Pending)
Task<List<AdminPaymentResponse>> GetPendingReviewAsync();

// Get payment settings (IBAN, bank info)
Task<PaymentSettingsResponse> GetPaymentSettingsAsync();

// Update payment settings
Task<PaymentSettingsResponse> UpdatePaymentSettingsAsync(PaymentSettingsRequest request);
```

#### Business Rules

1. **Installment creation**: On registration creation, auto-create two `Payment` records:
   - Installment 1: `Amount = Math.Ceiling(TotalAmount / 2)`, `Method = Transfer`, `Status = Pending`, `DueDate = now`.
   - Installment 2: `Amount = TotalAmount - Installment1.Amount`, `Method = Transfer`, `Status = Pending`, `DueDate = CampEdition.StartDate - N days` (from settings).
   - `TransferConcept` auto-generated: `"{Prefix}-{EditionYear}-{FamilyLastName}-{InstallmentNumber}"` (e.g., `"CAMP-2026-GARCIA-1"`).

2. **Proof upload**:
   - Only allowed for `Pending` payments.
   - Uses existing blob storage with folder `"payment-proofs"` and `contextId = paymentId`.
   - Accepted file types: images (jpg, png, webp) and documents (pdf).
   - Updates `ProofFileUrl`, `ProofFileName`, `ProofUploadedAt`.
   - A new `PaymentStatus` value `PendingReview` is introduced: once proof is uploaded, status transitions from `Pending` → `PendingReview`.

3. **Admin confirmation**:
   - Only for payments with status `PendingReview`.
   - Sets `Status = Completed`, `ConfirmedByUserId`, `ConfirmedAt`, optional `AdminNotes`.
   - After both installments are `Completed` → `Registration.Status = Confirmed`.

4. **Admin rejection**:
   - Only for payments with status `PendingReview`.
   - Sets `Status = Pending` (back to waiting), saves `AdminNotes` with rejection reason.
   - Does NOT delete the proof file — keeps it for reference. User can upload a new one.

5. **Registration status transitions**:
   - On registration creation → `Registration.Status = Pending`, two `Payment` records created.
   - After both installments `Completed` → `Registration.Status = Confirmed`.
   - If registration is cancelled → all `Pending`/`PendingReview` payments are voided.

6. **Audit logging**: Log all payment state changes with timestamp, old status, new status, and actor (user or admin).

### 4. Payment Repository

**File**: `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`

```csharp
Task<Payment?> GetByIdAsync(Guid paymentId);
Task<Payment?> GetByIdWithRegistrationAsync(Guid paymentId);  // Include Registration + CampEdition
Task<List<Payment>> GetByRegistrationIdAsync(Guid registrationId);
Task<Payment> CreateAsync(Payment payment);
Task<Payment> UpdateAsync(Payment payment);
Task<decimal> GetTotalCompletedAsync(Guid registrationId);  // existing
Task<List<Payment>> GetPendingReviewAsync();  // Status = PendingReview
Task<PagedResult<Payment>> GetFilteredAsync(PaymentFilterRequest filter);  // Admin listing
```

### 5. DTOs

**File**: `src/Abuvi.API/Features/Payments/PaymentsModels.cs`

```csharp
public record PaymentResponse(
    Guid Id,
    Guid RegistrationId,
    int InstallmentNumber,
    decimal Amount,
    DateTime? DueDate,
    PaymentMethod Method,
    PaymentStatus Status,
    string? TransferConcept,
    string? ProofFileUrl,
    string? ProofFileName,
    DateTime? ProofUploadedAt,
    string? AdminNotes,
    DateTime CreatedAt
);

public record AdminPaymentResponse(
    Guid Id,
    Guid RegistrationId,
    string FamilyUnitName,       // For admin context
    string CampEditionName,      // For admin context
    int InstallmentNumber,
    decimal Amount,
    DateTime? DueDate,
    PaymentStatus Status,
    string? TransferConcept,
    string? ProofFileUrl,
    string? ProofFileName,
    DateTime? ProofUploadedAt,
    string? AdminNotes,
    string? ConfirmedByUserName,
    DateTime? ConfirmedAt,
    DateTime CreatedAt
);

public record ConfirmPaymentRequest(string? Notes);

public record RejectPaymentRequest(string Notes);  // Notes required for rejection

public record PaymentFilterRequest(
    PaymentStatus? Status,
    Guid? CampEditionId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20
);

public record PaymentSettingsResponse(
    string Iban,
    string BankName,
    string AccountHolder,
    int SecondInstallmentDaysBefore
);

public record PaymentSettingsRequest(
    string Iban,
    string BankName,
    string AccountHolder,
    int SecondInstallmentDaysBefore
);
```

### 6. PaymentStatus Enum Update

Add `PendingReview` to the existing enum:

```csharp
public enum PaymentStatus
{
    Pending,         // Awaiting user proof upload
    PendingReview,   // Proof uploaded, awaiting admin confirmation
    Completed,       // Admin confirmed
    Failed,          // Legacy/future use
    Refunded         // Refunded by admin
}
```

### 7. Validators

**File**: `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`

- `RejectPaymentRequestValidator`: `Notes` is required, min length 10 characters.
- `PaymentSettingsRequestValidator`: IBAN format validation (ES + 22 digits), BankName and AccountHolder required, SecondInstallmentDaysBefore between 1 and 90.
- `PaymentFilterRequestValidator`: Page >= 1, PageSize between 1 and 100.

### 8. Modify Registration Flow

**File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`

After `CreateAsync` successfully creates a registration:

- Call `PaymentsService.CreateInstallmentsAsync(registration.Id)` to auto-generate the two payment records.
- Return payment information in the `RegistrationResponse`.

Update `RegistrationResponse` to include `List<PaymentResponse> Payments`.

### 9. Blob Storage Folder Registration

Add `"payment-proofs"` to the allowed folders in `BlobStorageValidator`:

- Accepted file types for `payment-proofs`: images (jpg, jpeg, png, webp) and documents (pdf).
- No thumbnail generation needed for payment proofs.

---

## Frontend Implementation

### 1. Types

**File**: `src/types/payment.ts`

```typescript
export interface PaymentResponse {
  id: string;
  registrationId: string;
  installmentNumber: number;
  amount: number;
  dueDate: string | null;
  method: 'Card' | 'Transfer' | 'Cash';
  status: 'Pending' | 'PendingReview' | 'Completed' | 'Failed' | 'Refunded';
  transferConcept: string | null;
  proofFileUrl: string | null;
  proofFileName: string | null;
  proofUploadedAt: string | null;
  adminNotes: string | null;
  createdAt: string;
}

export interface AdminPaymentResponse extends PaymentResponse {
  familyUnitName: string;
  campEditionName: string;
  confirmedByUserName: string | null;
  confirmedAt: string | null;
}

export interface PaymentSettings {
  iban: string;
  bankName: string;
  accountHolder: string;
  secondInstallmentDaysBefore: number;
}
```

### 2. Composable

**File**: `src/composables/usePayments.ts`

```typescript
// Key functions:
getRegistrationPayments(registrationId: string): Promise<PaymentResponse[]>
getPaymentSettings(): Promise<PaymentSettings>
uploadProof(paymentId: string, file: File): Promise<PaymentResponse>
removeProof(paymentId: string): Promise<PaymentResponse>

// Admin functions:
getPendingReviewPayments(): Promise<AdminPaymentResponse[]>
getAllPayments(filter: PaymentFilter): Promise<PagedResult<AdminPaymentResponse>>
confirmPayment(paymentId: string, notes?: string): Promise<AdminPaymentResponse>
rejectPayment(paymentId: string, notes: string): Promise<AdminPaymentResponse>
updatePaymentSettings(settings: PaymentSettings): Promise<PaymentSettings>
```

### 3. Registration Wizard — Add Payment Step (Step 5)

**File**: `src/views/registrations/RegisterForCampPage.vue`

Add a 5th step to the existing wizard after confirmation:

1. Participants
2. Extras
3. Accommodation (conditional)
4. Confirm
5. **Payment Instructions** (new)

The payment step shows:

- **Bank transfer details card**:
  - IBAN (formatted, with copy-to-clipboard button)
  - Account holder name
  - Bank name
  - Amount for installment 1
  - Transfer concept (with copy-to-clipboard button) — e.g., `"CAMP-2026-GARCIA-1"`. To be generated by the backend when the registration is created, so it can be unique and include family name and `-1` to indicate which payment is this.
- **Informational message**: "Please make a bank transfer with the details above. Once completed, upload your transfer proof below."
- **Two installment cards** showing:
  - Installment number, amount, due date
  - Status badge (Pending = orange, PendingReview = blue, Completed = green)
  - Upload proof button (for Pending installments) — uses existing `BlobUploadButton` pattern with accepted types: `.jpg, .jpeg, .png, .webp, .pdf`
  - Uploaded proof preview (thumbnail for images, file name + icon for PDFs)
  - Remove proof button (if proof uploaded and still Pending/PendingReview)
- **Note for installment 2**: "This payment is due by {date}. You can upload the proof now or later from your registrations page."
- **"Go to My Registrations"** button to navigate to the registration detail.

### 4. Registration Detail — Payment Section

**File**: Modify existing registration detail page/component.

Add a "Payments" section showing:

- Table/cards of installments with:
  - Status badges: Pending (orange), PendingReview (blue "Under review"), Completed (green), Refunded (gray).
  - Amount and due date.
  - Transfer concept (copyable).
  - Proof upload/preview area.
  - If rejected: show admin notes with rejection reason, allow re-upload.
- Bank transfer instructions (collapsible, fetched from settings).

### 5. Admin Payment Management

**File**: `src/views/admin/AdminPaymentsPage.vue` (or tab within existing admin camp management)

- **Pending Review Queue** (primary view):
  - List of payments with status `PendingReview`.
  - Each item shows: family name, camp edition, installment, amount, transfer concept, uploaded proof (clickable to view/download).
  - "Confirm" button (with optional notes dialog).
  - "Reject" button (with required notes explaining rejection reason).
- **All Payments** (secondary view/tab):
  - Filterable DataTable with all payments.
  - Filters: status, camp edition, date range.
  - Columns: family, edition, installment, amount, status, concept, proof, confirmed by, date.
  - Export to CSV option.
- **Payment Settings** (admin section):
  - Form to update IBAN, bank name, account holder, days before deadline.

### 6. Components

- `src/components/payments/PaymentInstallmentCard.vue` — Reusable card showing installment details, status, proof upload/preview.
- `src/components/payments/BankTransferInstructions.vue` — Card displaying IBAN, bank name, account holder, amount, concept with copy buttons.
- `src/components/payments/ProofUploader.vue` — File upload area (wraps blob upload) with preview for uploaded proofs.
- `src/components/payments/AdminPaymentReviewCard.vue` — Card for admin review queue with confirm/reject actions.

### 7. Routes

**File**: `src/router/index.ts`

```typescript
// Admin routes under existing admin section:
{ path: '/admin/payments', component: AdminPaymentsPage }
```

No new public routes needed — payment management is embedded in the registration wizard and detail page.

---

## Security Requirements

1. **Authorization**: Users can only view/upload proofs for their own registrations. Admin role required for confirm/reject/list-all.
2. **File validation**: Only accept images and PDFs for proof uploads. Validate MIME type server-side.
3. **IBAN protection**: IBAN is public (needed for transfers), but update requires admin role.
4. **Audit trail**: Log all payment state transitions with actor, timestamp, and notes.

---

## Non-Functional Requirements

- **Performance**: Payment listing and proof upload should respond in < 1s.
- **Concurrency**: Use optimistic concurrency on `Payment.UpdatedAt` to prevent race conditions on admin confirm/reject.
- **UX**: Copy-to-clipboard for IBAN and transfer concept. Clear status indicators. Mobile-friendly proof upload.

---

## Testing Strategy (TDD)

### Backend Unit Tests

**File**: `tests/Abuvi.Tests/Features/Payments/`

1. **PaymentsService Tests**:
   - `CreateInstallments_ValidRegistration_CreatesTwoPayments`
   - `CreateInstallments_ValidRegistration_SplitsAmountCorrectly`
   - `CreateInstallments_OddAmount_RoundsFirstInstallmentUp`
   - `CreateInstallments_GeneratesUniqueTransferConcepts`
   - `UploadProof_PendingPayment_UpdatesProofFieldsAndStatus`
   - `UploadProof_CompletedPayment_ThrowsBusinessRuleException`
   - `UploadProof_WrongUser_ThrowsUnauthorized`
   - `RemoveProof_PendingPayment_ClearsProofFields`
   - `RemoveProof_CompletedPayment_ThrowsBusinessRuleException`
   - `ConfirmPayment_PendingReview_MarksCompleted`
   - `ConfirmPayment_PendingWithoutProof_ThrowsBusinessRuleException`
   - `ConfirmPayment_BothInstallmentsCompleted_ConfirmsRegistration`
   - `RejectPayment_PendingReview_ResetsToPending`
   - `RejectPayment_RequiresNotes_ThrowsIfEmpty`
   - `GetPaymentSettings_ReturnsFromAssociationSettings`
   - `UpdatePaymentSettings_SavesJsonToAssociationSettings`

2. **PaymentsValidator Tests**:
   - `RejectRequest_EmptyNotes_Fails`
   - `RejectRequest_ShortNotes_Fails`
   - `PaymentSettings_InvalidIban_Fails`
   - `PaymentSettings_ValidData_Passes`

### Frontend Unit Tests

- `usePayments` composable: API call mocking for all functions.
- `PaymentInstallmentCard`: Renders correct status, shows/hides upload area.
- `BankTransferInstructions`: Displays IBAN and concept, copy buttons work.
- `ProofUploader`: File selection, upload trigger, preview display.

---

## Files to Create/Modify Summary

### New Files (Backend)

- `src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs`
- `src/Abuvi.API/Features/Payments/PaymentsModels.cs`
- `src/Abuvi.API/Features/Payments/PaymentsService.cs`
- `src/Abuvi.API/Features/Payments/IPaymentsService.cs`
- `src/Abuvi.API/Features/Payments/PaymentsRepository.cs`
- `src/Abuvi.API/Features/Payments/IPaymentsRepository.cs`
- `src/Abuvi.API/Features/Payments/PaymentsValidators.cs`
- `tests/Abuvi.Tests/Features/Payments/PaymentsServiceTests.cs`
- `tests/Abuvi.Tests/Features/Payments/PaymentsValidatorTests.cs`

### Modified Files (Backend)

- `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — Add `Payments` to `RegistrationResponse`.
- `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` — Call `CreateInstallmentsAsync` after registration creation.
- `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs` — Add `PendingReview` to `PaymentStatus` enum.
- `src/Abuvi.API/Data/Configurations/PaymentConfiguration.cs` — Add new column mappings.
- `src/Abuvi.API/Features/BlobStorage/BlobStorageValidator.cs` — Add `"payment-proofs"` folder.
- `src/Abuvi.API/Program.cs` — Register `PaymentsService`, `PaymentsRepository`.
- New EF Core migration for schema changes.

### New Files (Frontend)

- `src/types/payment.ts`
- `src/composables/usePayments.ts`
- `src/components/payments/PaymentInstallmentCard.vue`
- `src/components/payments/BankTransferInstructions.vue`
- `src/components/payments/ProofUploader.vue`
- `src/components/payments/AdminPaymentReviewCard.vue`
- `src/views/admin/AdminPaymentsPage.vue`

### Modified Files (Frontend)

- `src/views/registrations/RegisterForCampPage.vue` — Add payment step (step 5).
- `src/router/index.ts` — Add admin payments route.
- `src/types/registration.ts` — Add `payments` field to `RegistrationResponse`.
- `src/composables/useRegistrations.ts` — Handle payment data in responses.
- `src/types/blob-storage.ts` — Add `'payment-proofs'` to `BlobFolder` type.

### Documentation Updates

- `ai-specs/specs/data-model.md` — Document new payment fields and PaymentStatus values.
- `ai-specs/specs/api-endpoints.md` — Document new payment endpoints.

---

## Implementation Order (Suggested)

1. **Database migration** — Add new columns to `payments`, update `PaymentStatus` enum.
2. **Blob storage update** — Add `payment-proofs` folder to allowed folders.
3. **Payments repository** — Data access methods (TDD).
4. **Payments service** — Installment creation, proof upload, confirm/reject logic (TDD).
5. **Payments endpoints** — Wire up API routes.
6. **Modify registration flow** — Auto-create installments on registration.
7. **Payment settings** — CRUD via AssociationSettings (TDD).
8. **Frontend types + composable** — Payment API integration.
9. **Frontend payment components** — InstallmentCard, BankTransferInstructions, ProofUploader.
10. **Registration wizard step 5** — Payment instructions + proof upload.
11. **Registration detail** — Payment section with status and proof management.
12. **Admin payment management** — Pending review queue + all payments list + settings.
