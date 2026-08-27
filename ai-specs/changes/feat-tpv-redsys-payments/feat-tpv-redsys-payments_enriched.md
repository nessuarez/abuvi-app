# Feature: Online Payments via Banc Sabadell TPV Virtual (Redsys) — Readiness Assessment & Phased Specification

## Summary

The association wants families to pay **inside the platform** instead of making a manual bank transfer and uploading a proof image. The proposed rail is the **TPV Virtual of Banc Sabadell**, which runs on the **Redsys/Sermepa** gateway. The long-term goal is that payments are recorded automatically and reconciled against the bank without a board member keying anything in.

This document answers the original question — *what prerequisites must the application have?* — and is therefore structured as:

1. A **readiness assessment**: what already exists, and the concrete gaps that block a gateway integration.
2. The **prerequisites**, split into contractual/organisational (outside the code) and technical (inside the code).
3. A **phased delivery plan**, where Phase 0 is "make the payment model gateway-ready" and can be built and shipped **before** the bank contract exists.
4. The **decisions the Junta must take** before Phase 1 is planned, with a recommendation for each.

It is a parent spec. Each phase becomes its own `_backend.md` / `_frontend.md` ticket. Nothing here should be implemented as one branch.

---

## Business Context

- ABUVI is a small association. Money flows in through three channels today: **annual membership fees** (SEPA direct debit, executed by the Junta at the bank), **camp registration installments** (manual bank transfer, 3 installments), and **occasional manual adjustments** (admin-created payments).
- Today's platform is a **record-keeping system, not a payment rail**. A family transfers money, uploads a `justificante`, and an admin confirms the payment by hand (`POST /api/admin/payments/{id}/confirm`).
- The pain is the manual confirmation loop and the reconciliation work at the end of each campaign.
- Banc Sabadell is the association's only bank, so its TPV Virtual is the natural candidate. **Sabadell's TPV Virtual is a Redsys product** — the integration is against Redsys endpoints and Redsys documentation, with Sabadell supplying the merchant credentials and the settlement account. Any "Sabadell-specific" work is credential provisioning and the Canal de Comercios portal, not protocol work.

### The cost reality that shapes the design

A camp installment is typically **€150–€400**. Card acquiring on a Spanish TPV costs roughly **0.5–1.5% + a fixed cents amount** per transaction; **Bizum** through the same TPV is usually cheaper; **SEPA direct debit** is a **flat ~€0.20–€0.50** per receipt. For a €300 installment, card costs the association ~€2–4 and direct debit ~€0.30.

This is not a footnote — it determines whether card should be the default rail or the convenience fallback. See *Decision D1*.

### The reconciliation reality that shapes the design

**A TPV settlement does not appear in the bank statement as one line per payment.** Redsys settles in daily batches: the association's account receives **one aggregated credit** ("LIQUIDACION TPV" or similar), usually **net of commissions**. Consequently:

- Per-payment reconciliation **cannot** come from the Norma43 statement for card payments. It must come from the **Redsys server-to-server notification** (the authoritative event) plus the **Redsys settlement report**.
- The Norma43 statement reconciles at the **batch level**: "this €4,238.16 credit corresponds to these 17 payments minus €31.84 of fees".
- This is the opposite of the SEPA direct debit case (`feat-csv-bank-import`), where Norma43 *does* carry one line per receipt.

Any automation plan that assumes "we'll match TPV payments from the bank statement" is wrong and will produce a permanently unbalanced ledger. The data model must be able to represent *settlement batches* and *fees* from day one, or the ledger will never balance.

---

## Current Behavior (what exists today)

### Data model

`Payment` ([RegistrationsModels.cs:118-143](src/Abuvi.API/Features/Registrations/RegistrationsModels.cs#L118-L143)) already carries a surprising amount of the needed shape:

| Field | Status for gateway use |
| --- | --- |
| `Amount` (decimal 10,2) | Usable; needs a cents conversion helper |
| `Method` (`Card` \| `Transfer` \| `Cash`) | `Card` **exists but is never produced** — dead enum member today |
| `Status` (`Pending` \| `PendingReview` \| `Completed` \| `Failed` \| `Refunded`) | Missing the in-flight and user-abandoned states |
| `ExternalReference` (string, 255) | Documented as "e.g. Redsys reference" but never written |
| `TransferConcept` (e.g. `CAMP-GAR-1`) | Human-readable transfer memo — **not** a valid `Ds_Order` |
| `InstallmentNumber`, `DueDate` | Reusable as-is |
| `ProofFileUrl` / `ProofFileName` / `ProofUploadedAt` | Transfer-only; irrelevant for card |
| `ConfirmedByUserId` / `ConfirmedAt` | Human confirmation; a gateway confirmation has no user |
| `ConceptLinesSerialized` | Reusable for the gateway's product description |
| `IsManual`, `ConceptOverridden`, `OriginalAmount` | Reusable as-is |

`PaymentSettings` (JSON inside `AssociationSettings`, [PaymentsModels.cs:153-162](src/Abuvi.API/Features/Payments/PaymentsModels.cs#L153-L162)) holds the association's **inbound IBAN**, bank name, holder, the three installment offsets and `TransferConceptPrefix`. It has **no gateway section**.

### Endpoints

All payment endpoints are behind `.RequireAuthorization()` ([PaymentsEndpoints.cs:15-138](src/Abuvi.API/Features/Payments/PaymentsEndpoints.cs#L15-L138)). There is **no anonymous, signature-verified endpoint** anywhere in the application — the pattern a gateway callback requires does not exist yet.

### Related specs already written (do not duplicate)

| Spec | Relationship to this one |
| --- | --- |
| [`feat-csv-bank-import`](../feat-csv-bank-import) | Norma43 parsing + fuzzy matching for **SEPA direct debit membership fees**. Its parser is a direct dependency of Phase 3. Its "no persistence, ephemeral parse" decision must be **revisited** — see *Gap G7*. |
| [`feat-family-iban-direct-debit`](../feat-family-iban-direct-debit) | Stores the family IBAN encrypted. It is the **prerequisite for the direct-debit rail** (Phase 4b) and explicitly rules out SEPA XML generation. |
| [`feat-3-payments`](../feat-3-payments) | Sequential installments, concept lines, manual payments. Phase 0 must not break its ordering rules. |
| [`feat-payment-adjustments`](../feat-payment-adjustments) | Admin edits of amounts and the refund-generation path. Card refunds must route through the gateway rather than only writing a negative `Payment`. |

---

## Readiness Assessment — the gaps that block a gateway

These are the "requisitos previos" in the original question. Each is a genuine blocker, ordered by how early it must be solved.

### G1 — There is no gateway order identifier

Redsys requires `DS_MERCHANT_ORDER`: **4–12 characters, the first 4 numeric**, and **unique forever** for the merchant code + terminal. Reusing an order number is rejected (`SIS0051`).

`Payment.Id` is a UUID (too long, not numeric-prefixed). `TransferConcept` (`CAMP-GAR-1`) is neither unique nor correctly shaped.

**Required:** a `GatewayOrderNumber` column with a unique index, populated from a **database sequence** (not a `Guid` slice, not a timestamp — both collide under concurrency). A payment that is retried after a failure needs a **new** order number, so the relationship is one payment → many order numbers over time, which is another reason the attempt log (G2) is mandatory rather than nice-to-have.

### G2 — There is no payment event / attempt log

`Payment` stores only the *current* state. A gateway integration produces a stream of events: request built, user redirected, notification received, signature validated, response code `0000`, settlement batch assigned, refund issued. Reconciliation, dispute handling and "why did this family's payment not go through" are all unanswerable without them.

**Required:** an append-only `PaymentTransaction` (or `PaymentEvent`) entity storing the order number, event type, gateway response code, authorisation code, the **raw payload**, whether the signature validated, and the timestamp. This is the single most important prerequisite for later automation — everything in Phase 3 reads from it.

**Never store:** PAN, CVV, or expiry. Redirect/InSite integration keeps card data entirely off ABUVI servers (PCI-DSS **SAQ-A** scope). Storing card data would move the association to SAQ-D and is out of the question.

### G3 — The status machine has no in-flight or abandoned state

A user who is redirected to the bank and closes the tab leaves a payment that is neither `Pending` nor `Failed`. A notification that arrives while the user is still on the bank page must not race the return redirect.

**Required:** extend `PaymentStatus` with `Initiated` (redirect issued, awaiting outcome) and `Cancelled` (user abandoned or the bank returned a user-cancel code). Define the full transition table explicitly, including the illegal transitions, and enforce it in `PaymentsService` — not in the endpoint.

### G4 — Nothing in the codebase is idempotent against duplicate callbacks

Redsys **retries** the `Ds_MerchantURL` notification when it does not receive a `200`. The same authorisation can therefore arrive two or three times, and it can arrive **before** the user's browser returns. `PaymentsService.ConfirmPaymentAsync` today is a read-modify-write with no locking.

**Required:** confirmation keyed on `(GatewayOrderNumber, Ds_AuthorisationCode)` with a unique constraint, the state transition inside an explicit transaction with `SELECT … FOR UPDATE` semantics on the payment row, and a second identical notification returning `200 OK` while changing nothing. The backend standards already call for this ("Idempotency: Payment processing must be idempotent to handle duplicate callbacks") — no code implements it yet.

### G5 — There is no anonymous, signature-verified endpoint pattern

The callback comes from **Redsys servers**, not a logged-in browser. It carries no JWT and no cookie. It must:

- be `AllowAnonymous` while every sibling route stays authorised,
- validate `Ds_Signature` (HMAC-SHA256 over `Ds_MerchantParameters`, keyed by a 3DES-derived per-order key) **before** parsing anything as trusted,
- be excluded from the CORS policy (it is a server-to-server POST, not a browser call),
- be rate limited on an **anonymous partition** — note that `UseRateLimiter()` sits after `UseAuthentication()` and the current partition key falls back to the literal `"anonymous"` bucket, so a naive addition would put the callback in the same bucket as every unauthenticated request,
- be reachable over public HTTPS with a certificate Redsys accepts (this constrains the deployment, see G9).

### G6 — Membership fees and camp payments are two disconnected ledgers

`MembershipFee` ([data-model.md](../../specs/data-model.md)) has its own `status`, `paidDate` and `paymentReference`, and is marked paid through `PayFeeAsync` in the Memberships slice. It **never creates a `Payment` row**. A TPV can charge either, but the platform has no common notion of "a thing that can be paid".

**Required (decision D3):** either introduce a `Payable` abstraction that both `Payment` and `MembershipFee` satisfy, or make `MembershipFee` produce a `Payment` row when charged online. Choosing this late means writing the gateway slice twice.

### G7 — Bank statement imports are ephemeral by design

`feat-csv-bank-import` explicitly states: *"No new database tables. Norma43 data is ephemeral (never persisted)."* That is a fine choice for a human-in-the-loop wizard. It makes **automated** reconciliation impossible: there is no record of which statement lines were already consumed, so a re-uploaded file would double-match, and there is no way to leave an unmatched line open as an exception.

**Required for Phase 3:** persist `BankStatementImport` + `BankStatementLine` with a natural key (account, value date, amount, bank reference) so imports are idempotent and unmatched lines become a work queue.

### G8 — Money is decimal everywhere; Redsys wants integer cents

`DS_MERCHANT_AMOUNT` is the amount **in cents with no separator** (`€150.00` → `15000`). Mixing this up is the classic Redsys bug that charges 100× or 1/100×.

**Required:** a single `ToGatewayAmount(decimal) / FromGatewayAmount(int)` pair in the gateway slice, with unit tests for rounding, trailing zeros and the €0.01 / €9,999.99 boundaries. No inline `* 100` anywhere.

### G9 — Secrets and environments

`appsettings.json` currently ships a **real-looking dev encryption key** (`"Encryption:Key": "abuvi-dev-encryption-key-change-in-production"`). The Redsys merchant key must never follow that pattern.

**Required:** `Redsys:SecretKey` from environment variable / user-secrets only, validated at startup with a hard fail (the pattern is already documented in the backend standards), never logged, never returned by `GET /api/settings/payment`. Additionally, the **test environment** (`sis-t.redsys.es`) needs its own credentials and a `Redsys:Environment` switch, and `appsettings.E2E.json` needs a **fake gateway** so E2E tests never touch Redsys.

### G10 — Commissions are not representable

If Sabadell deducts commission from the settlement, `sum(Payment.Amount) ≠ money received`. Today there is nowhere to record the difference, so the ledger would drift by a few euros per batch forever and reconciliation would always show a mismatch.

**Required:** either a `FeeAmount` on the settlement line, or explicit settlement entities (Phase 3). This must be decided before Phase 1 writes its first row, because retrofitting fee attribution onto historical payments is painful.

### G11 — No receipt / justificante generation

Today the *family* uploads the proof. With a TPV the platform becomes the issuer and must be able to produce a payment receipt (and the Junta will want one for its own bookkeeping). No PDF generation exists in the codebase.

**Required (Phase 2):** a receipt document with a stable number, or an explicit decision that the confirmation email is the receipt.

### G12 — No dunning / deadline automation

There is a `AnnualFeeGenerationService` background service to copy the pattern from, but nothing sends "your installment is due in 5 days" or marks overdue installments. Automation of *payments* without automation of *reminders* moves the manual work rather than removing it.

---

## Prerequisites — Contractual and Organisational (outside the code)

These are the Junta's tasks. Phase 0 does not depend on any of them; Phase 1 depends on all of them.

1. **Contract the TPV Virtual with Banc Sabadell.** Deliverables from the bank: `FUC` / código de comercio, terminal number, **SHA-256 secret key**, access to the *Canal de Comercios* Redsys portal, and the settlement account (the association's existing Sabadell account).
2. **Request the test environment credentials** (`sis-t.redsys.es`) with test cards. Do not develop against production.
3. **Negotiate and record the fee schedule** — per-transaction commission for card and for Bizum, plus any fixed monthly fee. Needed for D1 and G10.
4. **Enable Bizum on the same terminal** if D1 selects it — it is an add-on to the same TPV contract, not a separate integration.
5. **Confirm 3DS2 / PSD2 SCA is active on the terminal** (it is mandatory; the redirect integration handles the challenge, but the terminal must be configured).
6. **If recurring/reference payments are wanted** (Phase 4a): request **COF / pago por referencia** activation explicitly. It is not enabled by default and requires the bank's approval.
7. **RGPD:** update the association's *registro de actividades de tratamiento* and privacy notice for payment data processing, and record Redsys/Sabadell as processors. Card data never reaches ABUVI, which keeps this light — but the processing record must still exist.
8. **Decide who bears the commission** (absorbed by the association vs. surcharged to the family). Note that surcharging card payments to consumers is restricted in the EU for most consumer cards — take this to the association's advisor before assuming it is an option.

---

## Prerequisites — Technical (Phase 0 scope)

Phase 0 is **buildable today, with no bank contract**, and is the actual answer to "what should the application have first". It touches no external service.

| # | Prerequisite | Deliverable |
| --- | --- | --- |
| P1 | Gateway order numbers | `Payment.GatewayOrderNumber` (string, 12, unique index, nullable) + PostgreSQL sequence + generator service + tests for concurrency and format |
| P2 | Payment event log | `PaymentTransaction` entity + configuration + migration + repository. Append-only, never updated |
| P3 | Status machine | `PaymentStatus.Initiated`, `PaymentStatus.Cancelled` + an explicit transition table enforced in `PaymentsService` + tests for every illegal transition |
| P4 | Idempotent confirmation | Confirmation keyed on order + authorisation code, wrapped in a transaction with row locking; duplicate confirmation is a no-op returning success |
| P5 | Money conversion | `GatewayAmount` helper with boundary tests |
| P6 | Gateway settings | `PaymentSettingsJson` gains a nested gateway block (**merchant code, terminal, environment, enabled methods — never the secret key**), exposed read-only to Admin, secret sourced from configuration only |
| P7 | Payable abstraction | Resolve D3 and implement whichever shape is chosen |
| P8 | Anonymous verified-callback pattern | An endpoint group that is `AllowAnonymous`, CORS-excluded, rate-limited on a dedicated partition, with a signature-verification endpoint filter. Ship it with a **fake provider** so the pattern is testable before Redsys exists |
| P9 | Fake gateway for tests | An `IPaymentGateway` implementation registered in `appsettings.E2E.json` that produces deterministic success/failure/timeout outcomes |

**Explicitly not in Phase 0:** any HTTP call to Redsys, any signature algorithm, any UI. Phase 0 makes the model correct; Phase 1 plugs a gateway into it.

### Suggested slice layout

Following Vertical Slice Architecture, the gateway is its **own feature**, not a bulge in `Features/Payments`:

```text
src/Abuvi.API/Features/PaymentGateway/
    IPaymentGateway.cs              # provider-agnostic port
    PaymentGatewayModels.cs         # PaymentTransaction entity, DTOs, enums
    PaymentGatewayEndpoints.cs      # checkout initiation + anonymous callback
    PaymentGatewayService.cs        # orchestration, idempotency, state transitions
    PaymentGatewayRepository.cs
    PaymentGatewayValidators.cs
    Redsys/
        RedsysGateway.cs            # IPaymentGateway implementation
        RedsysSignature.cs          # 3DES key derivation + HMAC-SHA256
        RedsysModels.cs             # Ds_* parameter records, response codes
        RedsysOptions.cs            # bound configuration + startup validation
    Fake/
        FakePaymentGateway.cs       # test/E2E provider
```

`IPaymentGateway` keeps Redsys replaceable. This is not speculative abstraction — the fake provider (P9) is a second implementation from day one, and the association may later add a second rail. Follow the `IPaymentHandler` example already in the backend standards.

---

## Phased Delivery Plan

### Phase 0 — Gateway-ready payment model *(no bank dependency — start here)*

Everything in the table above. Ships behind no feature flag because it changes nothing user-visible. Estimated as one backend ticket with no frontend work.

**Done when:** a fake gateway can drive a `Payment` from `Pending → Initiated → Completed`, a duplicate callback is a no-op, and an illegal transition throws `BusinessRuleException`.

### Phase 1 — Redsys redirect checkout *(requires the bank contract)*

- `POST /api/payments/{paymentId}/checkout` (authenticated, ownership-checked) → returns the Redsys form parameters (`Ds_SignatureVersion=HMAC_SHA256_V1`, `Ds_MerchantParameters`, `Ds_Signature`) plus the target URL. The frontend auto-submits the form.
- `POST /api/payment-gateway/redsys/notification` — **anonymous, signature-verified, idempotent**. This is the authoritative confirmation. Returns `200` even for duplicates so Redsys stops retrying.
- `GET /payments/{id}/ok` and `/ko` frontend routes — **UX only**. They must poll or re-fetch the payment; they must never be trusted to confirm anything, because a user can navigate to them directly.
- Response codes `0000`–`0099` = authorised; everything else is a decline with a mapped Spanish message.
- Frontend: a "Pagar con tarjeta" action on `PaymentInstallmentCard.vue`, a pending/in-flight state, and result pages. `BankTransferInstructions.vue` stays — transfer remains available.
- Confirmation email to the family and the board on successful capture.

**Scope note:** card only, one payment at a time, no refunds, no stored cards.

### Phase 2 — Administration and money-out

- Refunds through the gateway (`DS_MERCHANT_TRANSACTIONTYPE=3`, referencing the original order) instead of only writing a negative `Payment`. Integrate with `feat-payment-adjustments` rather than duplicating it.
- An admin gateway console: transactions, response codes, retry, manual reconcile.
- Receipts (G11).
- Deadline reminders and overdue marking (G12), following `AnnualFeeGenerationService`.

### Phase 3 — Reconciliation automation

- `PaymentSettlement` + `PaymentSettlementLine` entities; import of the Redsys settlement report from Canal de Comercios; each line links to a `PaymentTransaction` and carries the **fee**.
- Persist bank statement imports (G7), reusing the `feat-csv-bank-import` Norma43 parser.
- Match at **batch level**: settlement total + fees ↔ the aggregated Norma43 credit line.
- An **exception queue**: unmatched settlement lines, unmatched statement lines, amount mismatches. The goal is not zero manual work — it is that manual work only happens on exceptions.
- A reconciliation dashboard: per campaign, expected vs. authorised vs. settled vs. credited.

**This phase, not Phase 1, is where the original "automatizar la conciliación" goal is actually delivered.** Phases 0–2 exist to make it possible.

### Phase 4 — Recurring collection *(choose one; see D1)*

**4a — Card on file / pago por referencia.** First payment carries `DS_MERCHANT_IDENTIFIER=REQUIRED` and completes SCA; Redsys returns a token stored against the family. Subsequent installments are merchant-initiated (`DS_MERCHANT_DIRECTPAYMENT`, COF parameters, `Ds_Merchant_Cof_Ini=N`, original transaction id) and are SCA-exempt. Requires bank activation. Keeps the card cost per installment.

**4b — SEPA direct debit generation.** Build on `feat-family-iban-direct-debit` (which stores the IBAN) and generate **pain.008** files for upload to Sabadell. Far cheaper per transaction, and Norma43 already reconciles it line by line via `feat-csv-bank-import`. But it requires digitising the SEPA mandate — mandate reference (RUM), sequence type (FRST/RCUR), signature date, and evidence — which `feat-family-iban-direct-debit` **explicitly declined to do**. Reopening that decision is the real cost of 4b.

---

## Decisions Required Before Phase 1

| # | Decision | Recommendation |
| --- | --- | --- |
| **D1** | Which rail is the default? Card-first, direct-debit-first, or card as a convenience option alongside transfer? | **Card + Bizum as an opt-in convenience, transfer stays the default**, and revisit direct debit for recurring collection in Phase 4b. The fee difference on €150–400 installments is real money for a small association, and the transfer flow already works |
| **D2** | Who pays the commission? | **The association absorbs it**, budgeted into the camp price. Surcharging consumer cards is legally constrained in the EU and operationally messy |
| **D3** | Is the gateway wired to `Payment` only, or to a shared `Payable` covering `MembershipFee` too? | **Introduce `Payable` in Phase 0.** Membership fees are the highest-volume, lowest-value charge — the case where automation pays off most — and retrofitting is expensive |
| **D4** | Bizum on the same terminal? | **Yes.** It is the dominant Spanish P2P/C2B method, cheaper than card, and free on the integration side (same protocol, different `DS_MERCHANT_PAYMETHODS`) |
| **D5** | Redirect, InSite (iframe) or REST integration? | **Redirect.** It keeps PCI scope at SAQ-A, is the least code, and handles 3DS2 challenges without us building a challenge flow. InSite is a Phase 2+ UX improvement, not a starting point |
| **D6** | Are partial payments of an installment allowed by card? | **No.** Keep one payment = one charge. Partial amounts break the sequential-installment rules from `feat-3-payments` |
| **D7** | Does Phase 3 pull the bank statement automatically (PSD2 AIS against Sabadell's API) or keep the manual Norma43 upload? | **Keep the manual upload.** Sabadell's PSD2 interface requires an eIDAS QWAC certificate and a registered TPP — disproportionate for an association. Revisit only if the manual step proves to be the bottleneck |

---

## Non-Functional Requirements

**Security**

- Card data never touches ABUVI infrastructure (PCI-DSS SAQ-A). No PAN, CVV or expiry stored, logged or transmitted.
- `Redsys:SecretKey` from environment/user-secrets only; startup fails hard if absent in Production; never logged; never returned by any endpoint including `GET /api/settings/payment`.
- Every callback signature verified **before** the payload is treated as data. A signature failure is logged at `Warning` with the raw body and returns `200` (never reveal validation detail to an unauthenticated caller) while changing no state.
- All payment endpoints HTTPS-only.
- The callback endpoint is rate-limited on its own partition and excluded from the browser CORS policy.
- Amount is re-derived **server-side** from the `Payment` row at checkout time — never taken from the client request.

**Reliability**

- Duplicate notifications are no-ops. A notification arriving before the user's return redirect wins.
- Gateway calls have explicit timeouts; a timeout leaves the payment `Initiated`, never `Failed` — only the gateway may declare failure.
- An `Initiated` payment older than a configurable window (default 30 min) is swept to `Cancelled` by a background service, and a query against the gateway confirms the outcome before the sweep in Phase 2.

**Auditability**

- Every gateway interaction produces a `PaymentTransaction` row with the raw payload. Append-only.
- Log all payment events with structured logging in English (`logger.LogInformation("Payment {PaymentId} authorised with order {OrderNumber}", …)`); user-facing messages stay in Spanish.

**Testing**

- 90% coverage threshold applies (backend standards). Signature generation and verification need vectors from the Redsys documentation as fixtures.
- Idempotency, concurrency and every illegal state transition are explicit tests, not incidental coverage.
- E2E runs against `FakePaymentGateway` via `appsettings.E2E.json`. No test ever calls Redsys, not even the test environment.

**Documentation**

- `ai-specs/specs/data-model.md` — new `PaymentTransaction`, extended `Payment` and `PaymentStatus`, and (per D3) `Payable`.
- `ai-specs/specs/api-endpoints.md` — checkout and callback endpoints, with the callback marked anonymous and signature-verified.
- `ai-specs/changes/INDEX.md` — this feature.
- A runbook for the Junta: what to do when a payment is authorised but not settled, and how to read the exception queue.

---

## Definition of Done — Phase 0

- [ ] `Payment.GatewayOrderNumber` exists with a unique index and is generated from a sequence; concurrency test proves no collisions under parallel creation.
- [ ] `PaymentTransaction` exists, is append-only, and stores raw payloads.
- [ ] `PaymentStatus` includes `Initiated` and `Cancelled`; the transition table is enforced in the service and every illegal transition has a failing-first test.
- [ ] Confirmation is idempotent: the same `(order, authorisation code)` applied twice changes state once.
- [ ] `GatewayAmount` conversion is tested at the boundaries.
- [ ] `PaymentSettings` carries a gateway block; the secret key is **not** in it and is not exposed by any endpoint.
- [ ] D3 is resolved and the chosen shape is implemented.
- [ ] The anonymous signature-verified endpoint pattern exists and is exercised by `FakePaymentGateway` end to end.
- [ ] `dotnet test` green, coverage ≥ 90%, `dotnet format` clean.
- [ ] `data-model.md` and `api-endpoints.md` updated.

---

## Out of Scope (this document)

- Any Redsys HTTP call, signature implementation or UI — those are Phase 1 tickets.
- SEPA XML (pain.008) generation — Phase 4b, and gated on reopening the mandate-digitisation decision in `feat-family-iban-direct-debit`.
- PSD2 AIS / open-banking statement retrieval — see D7.
- Multi-acquirer support. `IPaymentGateway` keeps the door open; no second acquirer is built.
- Accounting-software export (the Junta's own bookkeeping tool). Worth a separate spec once Phase 3 produces a clean settlement ledger.
