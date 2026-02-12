# Camp CRUD - User Stories

## Overview

This document defines user stories for Camp management in ABUVI. The domain supports:

- **One active camp edition per year** (current running event)
- **Historical camp inventory** (archive of past editions)
- **Separation of concerns**: Static camp location/template info vs. yearly camp editions

## Proposed Data Model Enhancement

### Rationale for Separation

The new model separates two distinct concerns:

1. **Camp Location/Concept**: Recurring location (mountain, beach) with coordinates, name, base pricing
2. **CampEdition**: Yearly instance (2026 Mountain Camp, 2027 Mountain Camp) with specific dates, capacity, age requirements

**Naming Convention**:

- `Camp` = The recurring location/concept (with coordinates for mapping)
- `CampEdition` = The yearly instance

**Relationship**: One `Camp` → Many `CampEdition` (editions)

### Updated Data Model

```
Camp (the recurring location/concept - FOR MAPPING)
├── id (PK, UUID)
├── name (e.g., "Mountain Camp", "Beach Camp")
├── description (rich text, reusable info about the camp concept)
├── latitude (for map display)
├── longitude (for map display)
├── basePriceTemplate (default pricing structure)
├── status (Active | Inactive | HistoricalArchive)
├── createdAt
└── updatedAt

CampEdition (the yearly instance - FOR REGISTRATION)
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

### Phase 1: Camp Location Inventory Management (Board Role)

#### US-CAMP-001: View Camp Locations

**As a** Board member
**I want to** see all camp locations stored in the system
**So that** I can understand what camp locations are available for creating new editions

**Acceptance Criteria**:

- Display paginated list of all Camps
- Show: name, description preview, coordinates, base price, status
- Support filtering by name, status (Active/Inactive/Historical), or search
- Sort alphabetically
- Display on interactive map showing all camp location pins
- Show count of editions per camp location

**UI**: Admin Dashboard → Camp Management → Camp Locations

---

#### US-CAMP-002: Create Camp Location

**As a** Board member
**I want to** create a new camp location with reusable information
**So that** I can quickly set up new editions for that location

**Acceptance Criteria**:

- Form fields: name (required), description, latitude (required), longitude (required), basePriceTemplate, status
- Validate: latitude (-90 to 90), longitude (-180 to 180), basePrice >= 0
- Status options: Active (default), Inactive, HistoricalArchive
- Save camp location to database
- Confirmation message on success
- Map preview showing the pin location
- Return to camp locations list

**UI**: Camp Management → New Camp Location

---

#### US-CAMP-003: Edit Camp Location

**As a** Board member
**I want to** update an existing camp location
**So that** I can fix details or change pricing structure for future editions

**Acceptance Criteria**:

- Load camp location details in edit form
- Allow changes to: name, description, latitude, longitude, base price, status
- Validate inputs same as US-CAMP-002
- Map preview shows updated pin location in real-time
- Prevent deletion of camps with active editions (show warning)
- Audit timestamp updated

**Constraints**:

- Cannot edit name if multiple active editions using this camp (prevent confusion)
- Cannot change status to Inactive if active editions exist
- Cannot delete if any editions reference it

---

#### US-CAMP-004: Delete Camp Location

**As a** Board member
**I want to** delete an unused camp location
**So that** I keep the inventory clean

**Acceptance Criteria**:

- Can only delete if no camp editions reference it
- Confirmation dialog before deletion showing related editions count
- Success notification after deletion
- Return to camp locations list

---

#### US-CAMP-005: Map View of All Camp Locations

**As a** Board member or Member
**I want to** see an interactive map showing all active camp locations
**So that** I can visualize where camps are held and their historical locations

**Acceptance Criteria**:

- Display map with all camp pins (coordinates)
- Filter by: Active, Inactive, Historical (toggle)
- Clicking a pin shows: camp name, latest edition year, base price, status
- "View Editions" button to see editions for that location
- Map responsive and zooms to fit all pins
- Search by camp name with auto-zoom to result

**UI**: Public/Members Area → Camp Locations Map

---

### Phase 2: Camp Edition Management (Board Role)

#### US-CAMP-006: View Active Camp Edition

**As a** Board member
**I want to** see the current year's active camp edition details
**So that** I can manage registrations and edition status

**Acceptance Criteria**:

- Display full edition details: dates, location, pricing, capacity, age requirements
- Show registration count vs. max capacity (with progress bar)
- Show edition status with status indicator
- Show parent camp location info (name, coordinates)
- Quick actions: Edit, View Registrations, Manage Payments, View Families
- If no active edition: show "No active camp edition for [year]" with "Create" button

**UI**: Dashboard → Active Camp Edition (primary card)

---

#### US-CAMP-007: Create New Camp Edition

**As a** Board member
**I want to** create a new camp edition for an upcoming year
**So that** I can open registrations for that year

**Acceptance Criteria**:

- Choose existing camp location or create new edition
- Pre-populate: name, base price from camp location
- Form fields: year (must be future or current), startDate, endDate, location (specific description), minAge, maxAge, maxCapacity
- Override camp defaults if needed (age ranges, pricing)
- Set initial status: Draft
- Prevent multiple editions for same year from same camp
- Validate: year not already used for this camp, endDate > startDate
- Confirmation message on success

**UI**: Camp Management → New Edition → Select Camp Location → Fill Details

---

#### US-CAMP-008: Edit Camp Edition Details

**As a** Board member
**I want to** modify a camp edition details before registrations are finalized
**So that** I can fix errors or adjust pricing/capacity

**Acceptance Criteria**:

- Allow editing: location description, description, dates, basePrice, minAge, maxAge, maxCapacity, contact info
- Only allow edits if status is Draft or Open (not Closed/Completed)
- Show warning: "Changing capacity or pricing will affect pending registrations"
- Update timestamp
- Display parent camp info (read-only)

**Constraints**:

- Cannot edit year once created
- Cannot reduce maxCapacity below current registration count
- Cannot edit dates if status is Closed or Completed

---

#### US-CAMP-009: Change Camp Edition Status

**As a** Board member
**I want to** transition camp edition status through the workflow (Draft → Open → Closed → Completed)
**So that** I control when registrations are accepted and when the edition is finalized

**Acceptance Criteria**:

- Display current status with transition button
- Status = Draft: Show "Open Registrations" button
- Status = Open: Show "Close Registrations" button
- Status = Closed: Show "Mark as Completed" button
- Status = Completed: Read-only
- Confirm before each transition with warning message
- Log status change with timestamp and user
- Status = Open: Only if dates are in future and valid

**Workflow**:

- Draft → Open: Only if dates are in future and valid
- Open → Closed: Can close anytime, blocks new registrations
- Closed → Completed: Only after edition end date has passed

---

#### US-CAMP-010: View Camp Edition History (Past Editions)

**As a** Board member
**I want to** view all past camp editions with their data
**So that** I can reference previous editions and generate reports

**Acceptance Criteria**:

- List all editions with status = "Completed" or year < current year
- Paginated list: show year, camp name, location, dates, registration count, edition status
- Sort by year (descending) or by camp name
- Click to view detailed read-only view
- Show summary stats: registrations, total revenue, average family size, total attendees
- Export to CSV option (family list with attendees, contact info)

**UI**: Camp Management → Edition History

---

#### US-CAMP-011: Archive Camp Edition

**As a** System
**I want to** automatically manage old editions
**So that** they don't clutter the active edition interface

**Technical Detail**:

- After 2 years past the end date: automatically set flag `isArchived = true`
- Archived editions still searchable in history view but not in active list
- Manual archive option in edit screen for closed editions
- Board can manually unarchive if needed

---

### Phase 3: Camp Edition Data Validation & Reporting

#### US-CAMP-012: Validate Camp Pricing Against Registrations

**As a** System
**I want to** validate that collected payments match total edition billing
**So that** I detect billing discrepancies

**Acceptance Criteria**:

- Calculate: total registrations × pricing
- Check: sum of completed payments
- Alert if difference > €50 or 5%
- Show detailed breakdown by family
- Display on edition dashboard

**Trigger**: Manual button on Edition details, or daily automated check (if automated)

---

#### US-CAMP-013: Export Camp Edition Attendance Report

**As a** Board member
**I want to** export participant details for a camp edition
**So that** I can prepare name badges, room assignments, meal planning

**Acceptance Criteria**:

- Export to CSV: family name, members, ages, medical notes (encrypted warning), allergies
- Filter by registration status (Confirmed only, or all)
- Include family contact info (phone, email)
- Option to sort by family name or room assignment
- Only available for Board+ roles
- Show data sensitivity warning before export

**Format**: CSV with columns: FamilyName, MemberName, Age, Relationship, MedicalNotes, Allergies, PhoneContact, EmailContact

---

#### US-CAMP-014: Camp Edition Financial Report

**As a** Board member
**I want to** see financial summary for a camp edition
**So that** I can track revenue, expenses, and payment status

**Acceptance Criteria**:

- Show: total registrations, base revenue, discounts applied, total collected, pending payments
- Breakdown by payment status: Pending, Completed, Failed, Refunded
- List unpaid registrations (amount due + family contact)
- Export to CSV or PDF
- Show payment timeline (graph of payments received)

---

## Data Model - TypeScript Interfaces

```typescript
// Camp (the recurring location/concept)
interface Camp {
  id: UUID;
  name: string;                    // "Mountain Camp", "Beach Camp"
  description: string;             // Rich text, reusable info
  latitude: number;                // -90 to 90
  longitude: number;               // -180 to 180
  basePriceTemplate: decimal;      // Base pricing structure
  status: 'Active' | 'Inactive' | 'HistoricalArchive';
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  editions?: CampEdition[];        // List of yearly editions
}

// CampEdition (the yearly instance)
interface CampEdition {
  id: UUID;
  campId: UUID;                    // FK to Camp
  year: number;                    // 2026, 2027, etc.
  name?: string;                   // Override camp name, or null to use camp name
  startDate: Date;
  endDate: Date;
  location: string;                // Specific location description for this year
  description?: string;            // Override camp description, or null
  basePrice: decimal;              // Can override camp default
  minAge: number;                  // Age requirement for this edition
  maxAge: number;                  // Age requirement for this edition
  maxCapacity: number;
  contactEmail?: string;
  contactPhone?: string;
  status: 'Draft' | 'Open' | 'Closed' | 'Completed';
  isArchived: boolean;             // Default: false
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  camp?: Camp;                     // Parent camp location
  registrations?: Registration[];  // Registrations for this edition
}
```

## Implementation Notes

1. **Phase 1** focuses on Board-level CRUD for both camp locations and camp editions
2. **Phase 2** extends with workflow management and history
3. **Phase 3** adds reporting and validation
4. **Backward Compatibility**: Migrate existing Camp entities to CampEdition; optionally extract reusable Camp locations
5. **Permissions**: Only Admin and Board roles can manage camps and editions
6. **Audit Trail**: Log all edition status changes and edits for compliance
7. **Map Integration**: Use coordinates from Camp to display interactive map of all locations (public feature)

## Next Steps

1. ✅ Define user stories (this document)
2. ⬜ Design API endpoints (GET /camps, POST /camps/editions, etc.)
3. ⬜ Create database migration (rename Camp → CampEdition, create new Camp table, add campId FK)
4. ⬜ Implement backend CRUD endpoints
5. ⬜ Create frontend Camp Management UI with map view
6. ⬜ Add tests (unit + integration)
