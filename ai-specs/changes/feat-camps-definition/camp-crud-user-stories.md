# Camp CRUD - User Stories

## Overview

This document defines user stories for Camp management in ABUVI. The domain supports:

- **One active camp edition per year** (current running event)
- **Historical camp inventory** (archive of past editions)
- **Separation of concerns**: Static camp location/template info vs. yearly camp editions

## Proposed Data Model Enhancement

### Rationale for Separation

Currently, the `Camp` entity conflates two concerns:

1. **Recurring/Location Information**: Name, description, coordinates (for map), pricing base, historical status
2. **Instance Information**: Specific year, dates, age requirements, capacity, registration status

**Proposed Approach**:

- Create `Camp` to store reusable camp location/concept (name, description, coordinates, pricing template, status for historical tracking)
- Create `CampEdition` for each yearly instance (year, dates, age requirements, capacity, registration status)
- One-to-many relationship: `Camp` → `CampEdition` (yearly editions)

### Updated Data Model

```
Camp (the recurring location/concept)
├── id (PK, UUID)
├── name (e.g., "Mountain Camp", "Beach Camp")
├── description (rich text, reusable info about the camp concept)
├── latitude (for map display)
├── longitude (for map display)
├── basePriceTemplate (default pricing structure)
├── status (Active | Inactive | HistoricalArchive)
├── createdAt
└── updatedAt

CampEdition (the yearly instance)
├── id (PK, UUID)
├── campId (FK → Camp)
├── year (e.g., 2026)
├── startDate
├── endDate
├── location (specific description for this year, e.g. "Swiss Alps region")
├── description (can override camp concept, or null to use camp description)
├── basePrice (can override camp default)
├── minAge (age requirement for this edition)
├── maxAge (age requirement for this edition)
├── maxCapacity (specific for this year/instance)
├── contactEmail
├── contactPhone
├── status (Draft | Open | Closed | Completed)
├── createdAt
└── updatedAt
```

## User Stories

### Phase 1: Camp Inventory Management (Board Role)

#### US-CAMP-001: View Camp Template List

**As a** Board member
**I want to** see all camp templates stored in the system
**So that** I can understand what camp types are available for creating new editions

**Acceptance Criteria**:

- Display paginated list of all CampTemplates
- Show: name, description preview, age range, base price
- Support filtering by name or search
- Sort alphabetically

**UI**: Admin Dashboard → Camp Management → Camp Locations

---

#### US-CAMP-002: Create Camp Template

**As a** Board member
**I want to** create a new camp template with reusable information
**So that** I can quickly set up new camp editions based on this template

**Acceptance Criteria**:

- Form fields: name (required), description, minAge, maxAge, basePriceTemplate
- Validate: maxAge > minAge, basePrice >= 0
- Save template to database
- Confirmation message on success
- Return to template list

**UI**: Camp Management → New Template

---

#### US-CAMP-003: Edit Camp Template

**As a** Board member
**I want to** update an existing camp template
**So that** I can fix details or change pricing structure for future camps

**Acceptance Criteria**:

- Load template details in edit form
- Allow changes to: name, description, age ranges, base price
- Validate inputs same as US-CAMP-002
- Prevent deletion of templates with active camps (show warning)
- Audit timestamp updated

**Constraints**:

- Cannot edit name if multiple active camps using this template (prevent confusion)

---

#### US-CAMP-004: Delete Camp Template

**As a** Board member
**I want to** delete an unused camp template
**So that** I keep the inventory clean

**Acceptance Criteria**:

- Can only delete if no camps reference it
- Confirmation dialog before deletion
- Show count of related camps (if any)
- Success notification

---

### Phase 2: Camp Edition Management (Board Role)

#### US-CAMP-005: View Active Camp

**As a** Board member
**I want to** see the current year's active camp details
**So that** I can manage registrations and camp status

**Acceptance Criteria**:

- Display full camp details: dates, location, pricing, capacity
- Show registration count vs. max capacity
- Show camp status with status indicator
- Quick actions: Edit, View Registrations, Manage Payments
- If no active camp: show "No active camp for [year]"

**UI**: Dashboard → Active Camp (primary card)

---

#### US-CAMP-006: Create New Camp Edition

**As a** Board member
**I want to** create a new camp edition for an upcoming year
**So that** I can open registrations for that year's camp

**Acceptance Criteria**:

- Choose existing template or "Create from scratch"
- If from template: pre-populate name, age ranges, base price
- Form fields: year (must be future or current), startDate, endDate, location, maxCapacity
- Override template defaults if needed (age ranges, pricing)
- Set initial status: Draft
- Prevent multiple camps for same year
- Validate: year not already used, endDate > startDate

**UI**: Camp Management → New Edition → Select Template → Fill Details

---

#### US-CAMP-007: Edit Active Camp Details

**As a** Board member
**I want to** modify the active camp details before registrations are finalized
**So that** I can fix errors or adjust pricing/capacity

**Acceptance Criteria**:

- Allow editing: location, description, dates, basePrice, minAge, maxAge, maxCapacity, contact info
- Only allow edits if status is Draft or Open (not Closed/Completed)
- Show warning: "Changing capacity or pricing will affect pending registrations"
- Update timestamp

**Constraints**:

- Cannot edit year once created
- Cannot reduce maxCapacity below current registration count

---

#### US-CAMP-008: Change Camp Status

**As a** Board member
**I want to** transition camp status through the workflow (Draft → Open → Closed → Completed)
**So that** I control when registrations are accepted and when the camp is finalized

**Acceptance Criteria**:

- Display current status with transition button
- Status = Draft: Show "Open Registrations" button
- Status = Open: Show "Close Registrations" button
- Status = Closed: Show "Mark as Completed" button
- Status = Completed: Read-only
- Confirm before each transition (warning for irreversible changes)
- Log status change with timestamp

**Workflow**:

- Draft → Open: Only if dates are in future and valid
- Open → Closed: Can close anytime, close registrations
- Closed → Completed: Only after camp end date has passed

---

#### US-CAMP-009: View Camp History (Past Editions)

**As a** Board member
**I want to** view all past camp editions with their data
**So that** I can reference previous camps and generate reports

**Acceptance Criteria**:

- List all camps with status = "Completed" or year < current year
- Paginated list: show year, name, location, dates, registration count
- Sort by year (descending), or by name
- Click to view detailed read-only view
- Show summary stats: registrations, total revenue, average family size
- Export to CSV option

**UI**: Camp Management → History

---

#### US-CAMP-010: Archive Camp

**As a** System
**I want to** automatically archive old camps
**So that** they don't clutter the active camp interface

**Technical Detail**:

- After 2 years past the end date: automatically set flag `isArchived = true`
- Archived camps still searchable in history view but not in active list
- Manual archive option in edit screen

---

### Phase 3: Camp Data Validation & Reporting

#### US-CAMP-011: Validate Camp Pricing Against Registrations

**As a** System
**I want to** validate that collected payments match total camp billing
**So that** I detect billing discrepancies

**Acceptance Criteria**:

- Calculate: total registrations × pricing
- Check: sum of completed payments
- Alert if difference > $50 or 5%
- Show detailed breakdown by family

**Trigger**: Manual button on Camp details, or daily automated check

---

#### US-CAMP-012: Export Camp Attendance Report

**As a** Board member
**I want to** export participant details for the active camp
**So that** I can prepare name badges, room assignments, meal planning

**Acceptance Criteria**:

- Export to CSV: family name, members, ages, medical notes (encrypted warning), allergies
- Filter by registration status (Confirmed only, or all)
- Include family contact info
- Option to sort by family name or room assignment
- Only available for Board+ roles

**Format**: CSV with columns: FamilyName, MemberName, Age, Relationship, MedicalNotes, Allergies, EmergencyContact

---

## Data Model - Pseudocode

```typescript
// CampTemplate
interface CampTemplate {
  id: UUID;
  name: string;           // "Mountain Camp"
  description: string;    // Rich text, reusable info
  minAge: number;         // Default: 5
  maxAge: number;         // Default: 17
  basePriceTemplate: decimal;  // Default: €100
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  camps?: Camp[];          // List of editions using this template
}

// Camp (Edition)
interface Camp {
  id: UUID;
  campTemplateId: UUID;   // FK to template
  year: number;           // 2026, 2027, etc.
  name?: string;          // Override template, or null to use template
  startDate: Date;
  endDate: Date;
  location: string;
  description?: string;   // Override template, or null
  basePrice: decimal;     // Can override template
  minAge: number;         // Can override template
  maxAge: number;         // Can override template
  maxCapacity: number;
  contactEmail?: string;
  contactPhone?: string;
  status: 'Draft' | 'Open' | 'Closed' | 'Completed';
  isArchived: boolean;    // Default: false
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  template?: CampTemplate;
  registrations?: Registration[];
}
```

## Implementation Notes

1. **Phase 1** focuses on Board-level CRUD for both templates and camp editions
2. **Phase 2** extends with workflow management and history
3. **Phase 3** adds reporting and validation
4. **Backward Compatibility**: If CampTemplate is new, migrate existing Camps without creating templates initially
5. **Permissions**: Only Admin and Board roles can manage camps
6. **Audit Trail**: Log all camp status changes and edits for compliance

## Next Steps

1. ✅ Define user stories (this document)
2. ⬜ Design API endpoints (GET /camps, POST /camps, etc.)
3. ⬜ Create database migration (add campTemplateId FK)
4. ⬜ Implement backend CRUD endpoints
5. ⬜ Create frontend Camp Management UI
6. ⬜ Add tests (unit + integration)
