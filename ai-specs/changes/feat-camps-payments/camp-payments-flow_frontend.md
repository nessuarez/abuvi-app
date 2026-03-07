V# Frontend Implementation Plan: Camp Payments Flow

## Overview

Implement the frontend for the camp payments feature: bank transfer instructions display, proof-of-transfer upload, installment tracking in the registration wizard and detail page, and admin payment management (review queue, all payments list, payment settings). Built with Vue 3 Composition API, PrimeVue components, Tailwind CSS, and the existing composable-based architecture.

The backend is already implemented with all endpoints ready (see `PaymentsEndpoints.cs`).

---

## Architecture Context

### API Endpoints (Backend Ready)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/registrations/{registrationId}/payments` | User (own) or Admin | List payments for a registration |
| `GET` | `/api/payments/{paymentId}` | User (own) or Admin | Get single payment details |
| `POST` | `/api/payments/{paymentId}/upload-proof` | User (own) | Upload transfer proof (multipart/form-data, field: `file`) |
| `DELETE` | `/api/payments/{paymentId}/proof` | User (own, Pending/PendingReview only) | Remove uploaded proof |
| `POST` | `/api/admin/payments/{paymentId}/confirm` | Admin/Board | Confirm payment (body: `{ notes?: string }`) |
| `POST` | `/api/admin/payments/{paymentId}/reject` | Admin/Board | Reject proof (body: `{ notes: string }`, min 10 chars) |
| `GET` | `/api/admin/payments` | Admin/Board | All payments with filters (query: `status`, `campEditionId`, `fromDate`, `toDate`, `page`, `pageSize`) |
| `GET` | `/api/admin/payments/pending-review` | Admin/Board | Payments awaiting admin review |
| `GET` | `/api/settings/payment` | Public | Get bank transfer settings |
| `PUT` | `/api/settings/payment` | Admin/Board | Update payment settings |

### Key Backend Response Types

```typescript
// PaymentResponse (user-facing)
{ id, registrationId, installmentNumber, amount, dueDate, method, status, transferConcept, proofFileUrl, proofFileName, proofUploadedAt, adminNotes, createdAt }

// AdminPaymentResponse (extends with context)
{ ...PaymentResponse, familyUnitName, campEditionName, confirmedByUserName, confirmedAt }

// PaymentSettingsResponse
{ iban, bankName, accountHolder, secondInstallmentDaysBefore, transferConceptPrefix }

// PaymentStatus enum: 'Pending' | 'PendingReview' | 'Completed' | 'Failed' | 'Refunded'
```

### Files Involved

**New files:**

- `frontend/src/types/payment.ts`
- `frontend/src/composables/usePayments.ts`
- `frontend/src/components/payments/PaymentInstallmentCard.vue`
- `frontend/src/components/payments/BankTransferInstructions.vue`
- `frontend/src/components/payments/ProofUploader.vue`
- `frontend/src/components/payments/PaymentStatusBadge.vue`
- `frontend/src/components/admin/PaymentsAdminPanel.vue`
- `frontend/src/components/admin/PaymentsReviewQueue.vue`
- `frontend/src/components/admin/PaymentsAllList.vue`
- `frontend/src/components/admin/PaymentSettingsForm.vue`

**Modified files:**

- `frontend/src/types/registration.ts` - Add `PendingReview` to `PaymentStatus`
- `frontend/src/types/blob-storage.ts` - Add `'payment-proofs'` to `BlobFolder`
- `frontend/src/views/registrations/RegisterForCampPage.vue` - Add payment step (step 5)
- `frontend/src/views/registrations/RegistrationDetailPage.vue` - Replace payments section with installment cards
- `frontend/src/views/AdminPage.vue` - Add Payments tab

### State Management

- No Pinia store needed. All payment state is local to composable and components.
- `usePayments()` composable handles all API communication with loading/error refs.

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to `feature/feat-camps-payments-frontend` from the current `feat/registrations-payments` branch (which has the backend work).
- **Implementation Steps**:
  1. Ensure you're on `feat/registrations-payments` with latest changes
  2. Create: `git checkout -b feature/feat-camps-payments-frontend`
  3. Verify with `git branch`
- **Notes**: The backend is already implemented on `feat/registrations-payments`. This frontend branch builds on top of it.

### Step 1: Define TypeScript Interfaces

- **File**: `frontend/src/types/payment.ts`
- **Action**: Create payment type definitions matching backend DTOs
- **Implementation Steps**:
  1. Define `PaymentResponse` interface matching backend `PaymentResponse` record
  2. Define `AdminPaymentResponse` extending `PaymentResponse` with `familyUnitName`, `campEditionName`, `confirmedByUserName`, `confirmedAt`
  3. Define `PaymentSettings` interface: `{ iban, bankName, accountHolder, secondInstallmentDaysBefore, transferConceptPrefix }`
  4. Define `ConfirmPaymentRequest`: `{ notes?: string }`
  5. Define `RejectPaymentRequest`: `{ notes: string }`
  6. Define `PaymentFilterParams` for admin list query: `{ status?, campEditionId?, fromDate?, toDate?, page?, pageSize? }`
  7. Define `AdminPaymentsPagedResponse`: `{ items: AdminPaymentResponse[], totalCount: number, page: number, pageSize: number }`

- **File**: `frontend/src/types/registration.ts`
- **Action**: Update `PaymentStatus` type to include `PendingReview`
- **Implementation Steps**:
  1. Change line 5: `export type PaymentStatus = 'Pending' | 'PendingReview' | 'Completed' | 'Failed' | 'Refunded'`

- **File**: `frontend/src/types/blob-storage.ts`
- **Action**: Add `'payment-proofs'` to `BlobFolder` union type
- **Implementation Steps**:
  1. Change line 1: `export type BlobFolder = 'photos' | 'media-items' | 'camp-locations' | 'camp-photos' | 'payment-proofs'`

### Step 2: Create `usePayments` Composable

- **File**: `frontend/src/composables/usePayments.ts`
- **Action**: Create composable for all payment API communication
- **Function Signature**:

  ```typescript
  export function usePayments() {
    // Reactive state
    const payments = ref<PaymentResponse[]>([])
    const loading = ref(false)
    const error = ref<string | null>(null)

    // User methods
    const getRegistrationPayments = async (registrationId: string): Promise<PaymentResponse[]>
    const getPaymentById = async (paymentId: string): Promise<PaymentResponse | null>
    const uploadProof = async (paymentId: string, file: File): Promise<PaymentResponse | null>
    const removeProof = async (paymentId: string): Promise<PaymentResponse | null>

    // Admin methods
    const getPendingReviewPayments = async (): Promise<AdminPaymentResponse[]>
    const getAllPayments = async (filter: PaymentFilterParams): Promise<AdminPaymentsPagedResponse | null>
    const confirmPayment = async (paymentId: string, notes?: string): Promise<PaymentResponse | null>
    const rejectPayment = async (paymentId: string, notes: string): Promise<PaymentResponse | null>

    // Settings
    const getPaymentSettings = async (): Promise<PaymentSettings | null>
    const updatePaymentSettings = async (settings: PaymentSettings): Promise<PaymentSettings | null>
  }
  ```

- **Implementation Steps**:
  1. Import `ref` from Vue, `api` from `@/utils/api`, `ApiResponse` from `@/types/api`
  2. Import all payment types from `@/types/payment`
  3. Implement each method following the same pattern as `useRegistrations.ts`:
     - Set `loading = true`, `error = null`
     - Make API call using `api` instance
     - Handle success/failure, set error message on failure
     - Return data or null
     - Set `loading = false` in `finally`
  4. For `uploadProof`: Use `FormData` with `Content-Type: multipart/form-data` header. The endpoint expects a single `file` field (IFormFile). **Note**: This uploads directly to the payments endpoint (`POST /api/payments/{paymentId}/upload-proof`), NOT through the blob storage endpoint. The backend service handles blob storage internally.
  5. For `getAllPayments`: Build query string from `PaymentFilterParams`, API returns `{ items, totalCount, page, pageSize }` wrapped in `ApiResponse`
  6. Return all refs and methods
- **Implementation Notes**:
  - Error messages should be in Spanish to match existing composables (e.g., `'Error al cargar los pagos'`)
  - The `uploadProof` endpoint is different from the generic blob upload endpoint. It's a dedicated payment endpoint that handles blob storage internally.

### Step 3: Create `PaymentStatusBadge` Component

- **File**: `frontend/src/components/payments/PaymentStatusBadge.vue`
- **Action**: Reusable badge component for payment status display
- **Props**: `{ status: PaymentStatus }`
- **Implementation Steps**:
  1. Use PrimeVue `Tag` component (similar to `RegistrationStatusBadge`)
  2. Map statuses to colors and Spanish labels:
     - `Pending` -> severity `warn`, label "Pendiente"
     - `PendingReview` -> severity `info`, label "En revision"
     - `Completed` -> severity `success`, label "Completado"
     - `Failed` -> severity `danger`, label "Fallido"
     - `Refunded` -> severity `secondary`, label "Reembolsado"

### Step 4: Create `BankTransferInstructions` Component

- **File**: `frontend/src/components/payments/BankTransferInstructions.vue`
- **Action**: Card displaying bank transfer details with copy-to-clipboard functionality
- **Props**:

  ```typescript
  {
    iban: string
    bankName: string
    accountHolder: string
    amount?: number          // Optional: show amount for specific installment
    transferConcept?: string // Optional: show concept for specific installment
    collapsible?: boolean    // Default false - when true, wraps in an Accordion
  }
  ```

- **Implementation Steps**:
  1. Use PrimeVue `Card` for the container
  2. Display IBAN formatted with spaces (e.g., `ES12 3456 7890 1234 5678 9012`), bank name, account holder
  3. If `amount` is provided, show it formatted as currency (EUR)
  4. If `transferConcept` is provided, show it with a copy button
  5. Add copy-to-clipboard buttons next to IBAN and transferConcept using `navigator.clipboard.writeText()`
  6. Use PrimeVue `Button` with `icon="pi pi-copy"` + `text` + `rounded` style for copy buttons
  7. Show a brief toast or inline feedback on copy success (use `useToast`)
  8. If `collapsible` is true, wrap content in a PrimeVue `Panel` with `toggleable` prop
  9. Responsive layout: stack on mobile, side-by-side on desktop
- **Implementation Notes**:
  - Format IBAN with spaces every 4 characters for readability
  - Copy the IBAN without spaces (raw value)

### Step 5: Create `ProofUploader` Component

- **File**: `frontend/src/components/payments/ProofUploader.vue`
- **Action**: File upload area with preview for payment proofs
- **Props**:

  ```typescript
  {
    paymentId: string
    proofFileUrl: string | null
    proofFileName: string | null
    proofUploadedAt: string | null
    status: PaymentStatus
    adminNotes: string | null
    disabled?: boolean
  }
  ```

- **Emits**: `{ (e: 'uploaded', payment: PaymentResponse): void; (e: 'removed', payment: PaymentResponse): void }`
- **Implementation Steps**:
  1. Show different UI based on state:
     - **No proof uploaded + status Pending**: Show file upload area
     - **Proof uploaded + status PendingReview**: Show proof preview + "En revision" message + remove button
     - **Proof uploaded + status Completed**: Show proof preview + "Confirmado" message (no actions)
     - **Status Pending + adminNotes**: Show rejection message (admin notes in a `Message` with `severity="warn"`) + file upload area for re-upload
  2. File upload area: Use a styled dropzone or PrimeVue `FileUpload` in basic mode
     - Accept: `.jpg, .jpeg, .png, .webp, .pdf`
     - Max file display: show selected file name before upload
     - Upload button triggers `usePayments().uploadProof(paymentId, file)`
  3. Proof preview:
     - For images (jpg/png/webp): Show thumbnail using `<img :src="proofFileUrl">`
     - For PDFs: Show file icon + file name, clickable to open in new tab
  4. Remove button: Only visible when status is `Pending` or `PendingReview`. Calls `usePayments().removeProof(paymentId)`
  5. Loading states: Show spinner during upload/remove operations
  6. Error handling: Show error messages from composable via inline `Message`
- **Implementation Notes**:
  - The upload goes through `/api/payments/{paymentId}/upload-proof`, not the generic blob upload endpoint
  - The backend handles file validation (type, size) server-side

### Step 6: Create `PaymentInstallmentCard` Component

- **File**: `frontend/src/components/payments/PaymentInstallmentCard.vue`
- **Action**: Card showing a single installment's details with status, amount, and proof management
- **Props**:

  ```typescript
  {
    payment: PaymentResponse
    showUpload?: boolean  // Default true - whether to show proof upload area
  }
  ```

- **Emits**: `{ (e: 'updated', payment: PaymentResponse): void }`
- **Implementation Steps**:
  1. Card header: "Plazo {installmentNumber}" + `PaymentStatusBadge`
  2. Card body:
     - Amount: formatted as EUR currency
     - Due date: formatted (or "Inmediato" if null/past)
     - Transfer concept: displayed with copy-to-clipboard button
  3. Embed `ProofUploader` component for proof management (if `showUpload` is true)
  4. On `ProofUploader` events (`uploaded`, `removed`), emit `updated` to parent
  5. Responsive layout with Tailwind: use `border`, `rounded-lg`, `p-4` styling

### Step 7: Modify Registration Wizard - Add Payment Step (Step 5)

- **File**: `frontend/src/views/registrations/RegisterForCampPage.vue`
- **Action**: Add a 5th step "Pago" after the Confirm step, shown after successful registration creation
- **Implementation Steps**:
  1. Import new components: `BankTransferInstructions`, `PaymentInstallmentCard`
  2. Import `usePayments` composable
  3. Add state refs:

     ```typescript
     const createdRegistrationId = ref<string | null>(null)
     const installments = ref<PaymentResponse[]>([])
     const paymentSettings = ref<PaymentSettings | null>(null)
     ```

  4. Compute `paymentStepValue` as `confirmStepValue + 1`
  5. Add a new `<Step>` in `<StepList>`: `<Step :value="paymentStepValue">Pago</Step>`
  6. Modify `handleConfirm`:
     - After successful registration creation (and extras/accommodation), instead of navigating away:
     - Store `createdRegistrationId.value = created.id`
     - Fetch payment settings: `paymentSettings = await getPaymentSettings()`
     - Fetch installments: `installments = await getRegistrationPayments(created.id)`
     - Advance to payment step: `currentStep = paymentStepValue`
     - Remove the `router.push` to registration detail (user stays in wizard)
  7. Add the Payment `<StepPanel>`:

     ```html
     <StepPanel :value="paymentStepValue">
       <div class="flex flex-col gap-6 py-4">
         <div>
           <h2 class="mb-1 text-base font-semibold text-gray-900">
             Instrucciones de pago
           </h2>
           <p class="mb-4 text-sm text-gray-500">
             Realiza una transferencia bancaria con los datos indicados y sube el justificante.
           </p>
         </div>

         <!-- Bank transfer instructions -->
         <BankTransferInstructions
           v-if="paymentSettings"
           :iban="paymentSettings.iban"
           :bank-name="paymentSettings.bankName"
           :account-holder="paymentSettings.accountHolder"
         />

         <!-- Installment cards -->
         <div class="space-y-4">
           <PaymentInstallmentCard
             v-for="payment in installments"
             :key="payment.id"
             :payment="payment"
             @updated="handleInstallmentUpdated"
           />
         </div>

         <!-- Info message for installment 2 -->
         <Message v-if="installments.length > 1" severity="info" :closable="false">
           El segundo plazo vence el {{ formatDate(installments[1].dueDate!) }}.
           Puedes subir el justificante ahora o mas tarde desde el detalle de tu inscripcion.
         </Message>

         <!-- Navigation -->
         <div class="flex justify-end">
           <Button
             label="Ir a mis inscripciones"
             icon="pi pi-arrow-right"
             icon-pos="right"
             @click="router.push({ name: 'registration-detail', params: { id: createdRegistrationId! } })"
           />
         </div>
       </div>
     </StepPanel>
     ```

  8. Add `handleInstallmentUpdated` method to update the installment in the local array when proof is uploaded/removed
  9. The Stepper should NOT allow going back from the payment step (registration already created). Set the `linear` prop behavior.
- **Implementation Notes**:
  - The payment step is only shown AFTER the registration is successfully created. The stepper advances programmatically.
  - The success toast should be moved or changed: instead of "Inscripcion creada" and navigating away, show the toast and advance to the payment step.
  - Keep the existing error handling for extras/accommodation failures — those still navigate to detail page.

### Step 8: Modify Registration Detail Page - Payment Section

- **File**: `frontend/src/views/registrations/RegistrationDetailPage.vue`
- **Action**: Replace the current simple payments list with installment cards and bank transfer instructions
- **Implementation Steps**:
  1. Import: `PaymentInstallmentCard`, `BankTransferInstructions`, `PaymentStatusBadge`, `usePayments`
  2. Add state:

     ```typescript
     const { getRegistrationPayments, getPaymentSettings } = usePayments()
     const installments = ref<PaymentResponse[]>([])
     const paymentSettings = ref<PaymentSettings | null>(null)
     ```

  3. In `onMounted`, after fetching the registration, also fetch:

     ```typescript
     const [paymentsResult, settingsResult] = await Promise.all([
       getRegistrationPayments(registrationId.value),
       getPaymentSettings()
     ])
     installments.value = paymentsResult
     paymentSettings.value = settingsResult
     ```

  4. Replace the existing "Pagos" section (lines 188-222) with:
     - `BankTransferInstructions` (collapsible, with settings data)
     - `PaymentInstallmentCard` for each installment
     - Keep the "Pagado" / "Pendiente de pago" summary at the bottom
  5. Add `handleInstallmentUpdated` to refresh the payment in the list
  6. Add `PendingReview` to the `PAYMENT_STATUS_LABELS` record: `PendingReview: 'En revision'`
- **Implementation Notes**:
  - The existing `registration.payments` (PaymentSummary[]) is a simplified view from the registration response. Use the dedicated payments endpoint for full installment data including proof fields.
  - The bank transfer instructions should be in a collapsible panel (`collapsible` prop) to avoid cluttering the page.

### Step 9: Create Admin Payment Components

#### 9a: `PaymentsReviewQueue` Component

- **File**: `frontend/src/components/admin/PaymentsReviewQueue.vue`
- **Action**: Queue of payments with uploaded proofs awaiting admin review
- **Implementation Steps**:
  1. Use `usePayments().getPendingReviewPayments()` to fetch data on mount
  2. Display a list of cards, each showing:
     - Family name + camp edition name (from `AdminPaymentResponse`)
     - Installment number + amount + transfer concept
     - Proof preview (image thumbnail or PDF link, clickable to open full size)
     - "Confirmar" button -> opens a small Dialog with optional notes, calls `confirmPayment()`
     - "Rechazar" button -> opens a Dialog with required notes textarea (min 10 chars), calls `rejectPayment()`
  3. After confirm/reject, remove the item from the list
  4. Loading state with `ProgressSpinner`
  5. Empty state: "No hay pagos pendientes de revision"
  6. Use PrimeVue `Dialog` for confirm/reject modals
  7. On reject, validate that notes are >= 10 characters before enabling submit

#### 9b: `PaymentsAllList` Component

- **File**: `frontend/src/components/admin/PaymentsAllList.vue`
- **Action**: Filterable DataTable of all payments
- **Implementation Steps**:
  1. Use `usePayments().getAllPayments(filter)` with pagination
  2. Filters (above table):
     - Status dropdown: All / Pending / PendingReview / Completed / Failed / Refunded
     - Camp edition dropdown: populated from `useCampEditions()` (optional)
     - Date range: PrimeVue `DatePicker` for fromDate/toDate
  3. PrimeVue `DataTable` with columns:
     - Familia (familyUnitName)
     - Edicion (campEditionName)
     - Plazo (installmentNumber)
     - Importe (amount, formatted EUR)
     - Estado (PaymentStatusBadge)
     - Concepto (transferConcept)
     - Justificante (proof link/icon, or "-")
     - Confirmado por (confirmedByUserName or "-")
     - Fecha (createdAt, formatted)
  4. Pagination: Use `DataTable` with `lazy`, `paginator`, `rows`, `totalRecords` props
  5. Clicking a proof opens it in a new tab
  6. Filters trigger refetch with updated query params

#### 9c: `PaymentSettingsForm` Component

- **File**: `frontend/src/components/admin/PaymentSettingsForm.vue`
- **Action**: Form to view/update bank transfer settings
- **Implementation Steps**:
  1. Load current settings with `usePayments().getPaymentSettings()` on mount
  2. Form fields (PrimeVue components):
     - IBAN: `InputText` (validate ES + 22 digits pattern)
     - Bank name: `InputText` (required, max 200)
     - Account holder: `InputText` (required, max 200)
     - Days before deadline: `InputNumber` (min 1, max 90)
     - Transfer concept prefix: `InputText` (max 20, uppercase letters/numbers/hyphens)
  3. Save button calls `updatePaymentSettings()`
  4. Show success toast on save
  5. Client-side validation matching backend validators

#### 9d: `PaymentsAdminPanel` Component

- **File**: `frontend/src/components/admin/PaymentsAdminPanel.vue`
- **Action**: Container with sub-tabs for payment admin features
- **Implementation Steps**:
  1. Use PrimeVue `Tabs` / `TabList` / `Tab` / `TabPanels` / `TabPanel`
  2. Three tabs:
     - "Pendientes de revision" -> `PaymentsReviewQueue`
     - "Todos los pagos" -> `PaymentsAllList`
     - "Configuracion" -> `PaymentSettingsForm`
  3. Default to the review queue tab

### Step 10: Add Payments Tab to Admin Page

- **File**: `frontend/src/views/AdminPage.vue`
- **Action**: Add a "Pagos" tab to the admin panel
- **Implementation Steps**:
  1. Import `PaymentsAdminPanel`
  2. Add a new `<Tab>` after the existing tabs (with `v-if="auth.isBoard"`):

     ```html
     <Tab v-if="auth.isBoard" value="5" data-testid="tab-payments">
       <i class="pi pi-credit-card mr-2" />
       Pagos
     </Tab>
     ```

  3. Add corresponding `<TabPanel>`:

     ```html
     <TabPanel v-if="auth.isBoard" value="5" data-testid="panel-payments">
       <div class="py-4">
         <PaymentsAdminPanel />
       </div>
     </TabPanel>
     ```

- **Notes**: No new route needed. Admin payments are embedded within the existing admin page tab structure, consistent with the current pattern.

### Step 11: Write Vitest Unit Tests

- **Files**:
  - `frontend/src/composables/__tests__/usePayments.spec.ts`
  - `frontend/src/components/payments/__tests__/PaymentStatusBadge.spec.ts`
  - `frontend/src/components/payments/__tests__/PaymentInstallmentCard.spec.ts`
  - `frontend/src/components/payments/__tests__/BankTransferInstructions.spec.ts`

- **`usePayments.spec.ts`** Tests:
  1. `getRegistrationPayments` - returns payments array on success
  2. `getRegistrationPayments` - sets error on failure
  3. `uploadProof` - sends FormData and returns updated payment
  4. `removeProof` - calls DELETE and returns updated payment
  5. `confirmPayment` - sends POST with notes
  6. `rejectPayment` - sends POST with required notes
  7. `getPaymentSettings` - returns settings
  8. `updatePaymentSettings` - sends PUT with settings
  9. Loading state transitions correctly for each method

- **`PaymentStatusBadge.spec.ts`** Tests:
  1. Renders correct label and severity for each status value

- **`PaymentInstallmentCard.spec.ts`** Tests:
  1. Displays installment number, amount, due date, transfer concept
  2. Shows ProofUploader when `showUpload` is true
  3. Emits `updated` when proof is uploaded

- **`BankTransferInstructions.spec.ts`** Tests:
  1. Displays IBAN formatted with spaces
  2. Displays bank name and account holder
  3. Copy buttons trigger clipboard write
  4. Optional amount and concept display

### Step 12: Update Technical Documentation

- **Action**: Review and update documentation after implementation
- **Implementation Steps**:
  1. Update `ai-specs/specs/api-spec.yml` (if it exists) with new payment endpoints
  2. Update `ai-specs/specs/frontend-standards.mdc` if new patterns are introduced
  3. Update `ai-specs/specs/data-model.md` to document new PaymentStatus value `PendingReview`
  4. Verify all documentation is in English

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Define TypeScript interfaces (types/payment.ts + update registration.ts + blob-storage.ts)
3. **Step 2**: Create `usePayments` composable
4. **Step 3**: Create `PaymentStatusBadge` component
5. **Step 4**: Create `BankTransferInstructions` component
6. **Step 5**: Create `ProofUploader` component
7. **Step 6**: Create `PaymentInstallmentCard` component
8. **Step 7**: Modify registration wizard - add payment step
9. **Step 8**: Modify registration detail page - payment section
10. **Step 9**: Create admin payment components (9a-9d)
11. **Step 10**: Add payments tab to admin page
12. **Step 11**: Write Vitest unit tests
13. **Step 12**: Update technical documentation

---

## Testing Checklist

- [ ] `usePayments` composable: All API methods mocked and tested
- [ ] `PaymentStatusBadge`: Correct label/color for all 5 statuses
- [ ] `BankTransferInstructions`: IBAN display, copy-to-clipboard, optional fields
- [ ] `PaymentInstallmentCard`: Renders payment data, proof upload integration
- [ ] `ProofUploader`: Upload, preview, remove flows; disabled states
- [ ] Registration wizard: Payment step appears after registration, installments displayed
- [ ] Registration detail: Installments with proof upload, bank instructions collapsible
- [ ] Admin review queue: List pending, confirm, reject with notes validation
- [ ] Admin all payments: Filters, pagination, DataTable columns
- [ ] Admin settings: Load, edit, save payment settings
- [ ] Error states: API failures show appropriate messages
- [ ] Mobile responsiveness: All components usable on mobile viewport

---

## Error Handling Patterns

- Each composable method sets `error.value` with a Spanish error message on failure
- Components show errors via PrimeVue `Message` component inline
- Toast notifications (`useToast`) for success actions (proof uploaded, payment confirmed)
- Upload errors show specific messages (file too large, invalid type)
- Admin reject validation: notes must be >= 10 characters (client-side + server-side)

---

## UI/UX Considerations

- **Status colors**: Pending = orange/warn, PendingReview = blue/info, Completed = green/success, Failed = red/danger, Refunded = gray/secondary
- **Copy-to-clipboard**: IBAN and transfer concept have copy buttons with brief feedback
- **Proof preview**: Images show thumbnail, PDFs show file icon + name
- **Responsive**: Cards stack on mobile, side-by-side on desktop. Use Tailwind `sm:`, `md:` breakpoints
- **Loading states**: ProgressSpinner during data fetch, Button `:loading` during actions
- **Collapsible bank instructions**: On detail page to reduce visual noise
- **Rejection feedback**: When admin rejects, the user sees the rejection reason prominently above the re-upload area

---

## Dependencies

### PrimeVue Components Used

- `Tag` (status badges)
- `Button` (actions, copy)
- `Card` / `Panel` (containers)
- `Dialog` (confirm/reject modals)
- `DataTable` + `Column` (admin list)
- `Paginator` (admin list)
- `FileUpload` (proof upload, basic mode)
- `InputText`, `InputNumber`, `Textarea` (settings form, reject notes)
- `DatePicker` (admin filter)
- `Select` (admin filter - status dropdown)
- `Message` (inline messages, rejection notes)
- `ProgressSpinner` (loading)
- `Tabs`, `TabList`, `Tab`, `TabPanels`, `TabPanel` (admin sub-tabs)
- `Accordion`, `AccordionTab` or `Panel` with `toggleable` (collapsible instructions)

### No New npm Packages Required

All functionality is achievable with existing dependencies (Vue 3, PrimeVue, Tailwind CSS, Axios).

---

## Notes

- **Language**: All code (variables, functions, comments) in English. UI labels in Spanish (matching existing app).
- **TypeScript**: Strict typing, no `any`. All props, emits, and composable returns fully typed.
- **No `<style>` blocks**: Use only Tailwind CSS utility classes.
- **Upload path**: Proof upload uses the dedicated payment endpoint (`/api/payments/{id}/upload-proof`), NOT the generic blob upload. The backend handles blob storage internally.
- **PaymentSummary vs PaymentResponse**: The `RegistrationResponse.payments` field uses the simplified `PaymentSummary` type. For full installment data (with proof fields), use the dedicated `/api/registrations/{id}/payments` endpoint.
- **Admin access**: Payment admin features require `isBoard` (Admin or Board role), consistent with other admin panels.

---

## Next Steps After Implementation

1. Integration testing with the running backend
2. Verify proof upload/download works with actual blob storage (S3/MinIO)
3. Test admin confirm/reject flow end-to-end
4. Consider future: email notifications when proof is uploaded (for admin) or payment is confirmed/rejected (for user)

---

## Implementation Verification

- [ ] All components use `<script setup lang="ts">`
- [ ] No `<style>` blocks - Tailwind CSS only
- [ ] No `any` types
- [ ] Composables handle loading/error states
- [ ] API calls only through `usePayments` composable
- [ ] PrimeVue components used consistently
- [ ] Mobile-responsive layout tested
- [ ] All tests pass
- [ ] Documentation updated
