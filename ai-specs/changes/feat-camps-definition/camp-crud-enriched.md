# Camp CRUD - Enriched User Stories (Technical Specification)

## Overview

This document defines the complete technical specification for Camp management in ABUVI, including:

- **Separation of Camp Locations and Camp Editions** for reusability and historical tracking
- **Age-based pricing** (adult, child, baby) instead of single base price
- **Camp extras/add-ons** that vary per edition
- **Candidate/Proposed workflow** for camp selection process
- **Interactive map** showing all camp locations
- **Complete data model**, API endpoints, and implementation details

---

## Key Enhancements from Original

### 1. Pricing Structure

**Original**: Single `basePrice` field per camp

**Enhanced**: Age-based pricing tiers:

- `pricePerAdult` (e.g., €150)
- `pricePerChild` (e.g., €100)
- `pricePerBaby` (e.g., €50)

**Age Definitions** (configurable per edition):

- Baby: 0-2 years
- Child: 3-12 years
- Adult: 13+ years

**Rationale**: Different age groups have different costs (meals, activities, insurance). Family pricing is more accurate when calculated based on actual family composition.

### 2. Camp Extras/Add-ons

**New Feature**: Each camp edition can have multiple optional or required extras:

- Equipment rental (kayak, bike)
- Special workshops or activities
- Transport from meeting point
- Extended stay nights
- Meal upgrades

**Pricing**: Extras can be priced per person or per family

### 3. Candidate/Proposed Status

**New Feature**: Before creating an active camp edition, board can create multiple "proposed" or "candidate" camps:

- Board creates 2-3 candidate locations
- Candidates are evaluated/discussed
- One candidate is selected and promoted to "Draft" status
- Others are rejected or archived

**Workflow**: `Proposed` → (selected) → `Draft` → `Open` → `Closed` → `Completed`

---

## Updated Data Model

### Camp (Location/Concept)

Represents a recurring camp location with reusable template information.

```typescript
interface Camp {
  id: UUID;
  name: string;                           // "Mountain Camp", "Beach Camp"
  description: string;                    // Rich text, reusable info
  latitude: number;                       // -90 to 90
  longitude: number;                      // -180 to 180

  // Pricing template (defaults for new editions)
  basePriceAdult: decimal;                // Default adult price
  basePriceChild: decimal;                // Default child price
  basePriceBaby: decimal;                 // Default baby price

  status: 'Active' | 'Inactive' | 'HistoricalArchive';
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  editions?: CampEdition[];               // List of yearly editions
}
```

**Database Schema (PostgreSQL)**:

```sql
CREATE TABLE camps (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(200) NOT NULL,
    description TEXT,
    latitude DECIMAL(9,6) NOT NULL CHECK (latitude >= -90 AND latitude <= 90),
    longitude DECIMAL(9,6) NOT NULL CHECK (longitude >= -180 AND longitude <= 180),
    base_price_adult DECIMAL(10,2) NOT NULL CHECK (base_price_adult >= 0),
    base_price_child DECIMAL(10,2) NOT NULL CHECK (base_price_child >= 0),
    base_price_baby DECIMAL(10,2) NOT NULL CHECK (base_price_baby >= 0),
    status VARCHAR(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active', 'Inactive', 'HistoricalArchive')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_camps_status ON camps(status);
CREATE INDEX idx_camps_location ON camps(latitude, longitude);
```

### AssociationSettings (New Entity)

Global configuration settings for the association, managed by Board members.

```typescript
interface AssociationSettings {
  id: UUID;
  settingKey: string;                     // Unique key (e.g., 'age_ranges')
  settingValue: JSON;                     // Flexible JSON value
  description?: string;
  updatedBy: UUID;                        // FK to User (last updated by)
  updatedAt: DateTime;

  // Example for age ranges:
  // settingKey: 'age_ranges'
  // settingValue: {
  //   "babyMaxAge": 2,
  //   "childMinAge": 3,
  //   "childMaxAge": 12,
  //   "adultMinAge": 13
  // }
}
```

**Database Schema**:

```sql
CREATE TABLE association_settings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    setting_key VARCHAR(100) NOT NULL UNIQUE,
    setting_value JSONB NOT NULL,
    description TEXT,
    updated_by UUID NOT NULL REFERENCES users(id),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_association_settings_key ON association_settings(setting_key);
```

**Default Age Ranges** (managed via settings):

```json
{
  "settingKey": "age_ranges",
  "settingValue": {
    "babyMaxAge": 2,
    "childMinAge": 3,
    "childMaxAge": 12,
    "adultMinAge": 13
  }
}
```

### CampEdition (Yearly Instance)

Represents a specific camp happening in a given year.

```typescript
interface CampEdition {
  id: UUID;
  campId: UUID;                           // FK to Camp
  year: number;                           // 2026, 2027, etc.
  name?: string;                          // Override camp name, or null to use camp name
  startDate: Date;
  endDate: Date;
  location: string;                       // Specific location for this year
  description?: string;                   // Override camp description, or null

  // Age-based pricing (can override camp defaults)
  pricePerAdult: decimal;
  pricePerChild: decimal;
  pricePerBaby: decimal;

  // Age ranges (optional override, defaults to AssociationSettings.age_ranges)
  useCustomAgeRanges: boolean;            // Default: false (use global settings)
  babyMaxAge?: number;                    // Only if useCustomAgeRanges = true
  childMinAge?: number;                   // Only if useCustomAgeRanges = true
  childMaxAge?: number;                   // Only if useCustomAgeRanges = true
  adultMinAge?: number;                   // Only if useCustomAgeRanges = true

  maxCapacity: number;
  contactEmail?: string;
  contactPhone?: string;

  status: 'Proposed' | 'Draft' | 'Open' | 'Closed' | 'Completed';
  isArchived: boolean;                    // Default: false

  // Selection metadata (for Proposed status)
  proposalReason?: string;                // Why this location was proposed
  proposalNotes?: string;                 // Discussion notes, pros/cons

  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  camp?: Camp;                            // Parent camp location
  registrations?: Registration[];         // Registrations for this edition
  extras?: CampEditionExtra[];            // Available extras for this edition
}
```

**Note**: Age ranges are now configurable globally by Board members via AssociationSettings. Individual camp editions can override these if needed by setting `useCustomAgeRanges = true`.

**Database Schema**:

```sql
CREATE TABLE camp_editions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    camp_id UUID NOT NULL REFERENCES camps(id) ON DELETE RESTRICT,
    year INTEGER NOT NULL CHECK (year >= 2020 AND year <= 2100),
    name VARCHAR(200),
    start_date DATE NOT NULL,
    end_date DATE NOT NULL CHECK (end_date > start_date),
    location VARCHAR(500) NOT NULL,
    description TEXT,

    -- Age-based pricing
    price_per_adult DECIMAL(10,2) NOT NULL CHECK (price_per_adult >= 0),
    price_per_child DECIMAL(10,2) NOT NULL CHECK (price_per_child >= 0),
    price_per_baby DECIMAL(10,2) NOT NULL CHECK (price_per_baby >= 0),

    -- Age range definitions (optional override of global settings)
    use_custom_age_ranges BOOLEAN NOT NULL DEFAULT FALSE,
    baby_max_age INTEGER CHECK (baby_max_age IS NULL OR baby_max_age >= 0),
    child_min_age INTEGER CHECK (child_min_age IS NULL OR child_min_age > 0),
    child_max_age INTEGER CHECK (child_max_age IS NULL OR child_max_age > 0),
    adult_min_age INTEGER CHECK (adult_min_age IS NULL OR adult_min_age > 0),

    max_capacity INTEGER NOT NULL CHECK (max_capacity > 0),
    contact_email VARCHAR(255),
    contact_phone VARCHAR(20),

    status VARCHAR(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Proposed', 'Draft', 'Open', 'Closed', 'Completed')),
    is_archived BOOLEAN NOT NULL DEFAULT FALSE,

    -- Proposal metadata
    proposal_reason TEXT,
    proposal_notes TEXT,

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    UNIQUE(camp_id, year),  -- One edition per camp per year
    CHECK (
        use_custom_age_ranges = FALSE OR
        (baby_max_age IS NOT NULL AND child_min_age IS NOT NULL AND
         child_max_age IS NOT NULL AND adult_min_age IS NOT NULL)
    )  -- If custom ranges enabled, all must be set
);

CREATE INDEX idx_camp_editions_camp ON camp_editions(camp_id);
CREATE INDEX idx_camp_editions_year ON camp_editions(year);
CREATE INDEX idx_camp_editions_status ON camp_editions(status);
CREATE INDEX idx_camp_editions_dates ON camp_editions(start_date, end_date);
```

### CampEditionExtra (New Entity)

Represents an optional or required extra/add-on for a specific camp edition.

```typescript
interface CampEditionExtra {
  id: UUID;
  campEditionId: UUID;                    // FK to CampEdition
  name: string;                           // "Kayak rental", "Vegan menu", "Transport"
  description?: string;
  price: decimal;                         // Base price in euros
  pricingType: 'PerPerson' | 'PerFamily'; // How price is applied
  pricingPeriod: 'OneTime' | 'PerDay';    // NEW: Pricing period
  isRequired: boolean;                    // Required for all registrations (e.g., transport)
  maxQuantity?: number;                   // Optional limit (null = unlimited)
  currentQuantity: number;                // Track how many sold
  sortOrder: number;                      // Display order
  isActive: boolean;                      // Can be disabled without deleting
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  campEdition?: CampEdition;
  registrationExtras?: RegistrationExtra[]; // Registrations that selected this extra
}
```

**Pricing Examples**:
- **Kayak rental** (€25, PerPerson, OneTime): €25 per person for entire camp
- **Vegan menu** (€5, PerPerson, PerDay): €5 per person per day (calculated based on camp duration)
- **Transport** (€30, PerPerson, OneTime): €30 per person one-time fee
- **Workshop** (€50, PerFamily, OneTime): €50 per family regardless of size

**Database Schema**:

```sql
CREATE TABLE camp_edition_extras (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    camp_edition_id UUID NOT NULL REFERENCES camp_editions(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    price DECIMAL(10,2) NOT NULL CHECK (price >= 0),
    pricing_type VARCHAR(20) NOT NULL CHECK (pricing_type IN ('PerPerson', 'PerFamily')),
    pricing_period VARCHAR(20) NOT NULL DEFAULT 'OneTime' CHECK (pricing_period IN ('OneTime', 'PerDay')),
    is_required BOOLEAN NOT NULL DEFAULT FALSE,
    max_quantity INTEGER CHECK (max_quantity IS NULL OR max_quantity > 0),
    current_quantity INTEGER NOT NULL DEFAULT 0 CHECK (current_quantity >= 0),
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_camp_edition_extras_edition ON camp_edition_extras(camp_edition_id);
CREATE INDEX idx_camp_edition_extras_active ON camp_edition_extras(is_active);
```

### RegistrationExtra (New Entity)

Links selected extras to a specific registration.

```typescript
interface RegistrationExtra {
  id: UUID;
  registrationId: UUID;                   // FK to Registration
  campEditionExtraId: UUID;               // FK to CampEditionExtra
  quantity: number;                       // How many (if per-person pricing)
  totalAmount: decimal;                   // Calculated: extra.price * quantity
  createdAt: DateTime;

  // Relationships
  registration?: Registration;
  extra?: CampEditionExtra;
}
```

**Database Schema**:

```sql
CREATE TABLE registration_extras (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    registration_id UUID NOT NULL REFERENCES registrations(id) ON DELETE CASCADE,
    camp_edition_extra_id UUID NOT NULL REFERENCES camp_edition_extras(id) ON DELETE RESTRICT,
    quantity INTEGER NOT NULL DEFAULT 1 CHECK (quantity > 0),
    total_amount DECIMAL(10,2) NOT NULL CHECK (total_amount >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE(registration_id, camp_edition_extra_id)  -- Each extra selected once per registration
);

CREATE INDEX idx_registration_extras_registration ON registration_extras(registration_id);
CREATE INDEX idx_registration_extras_extra ON registration_extras(camp_edition_extra_id);
```

### Updated Registration Model

**Changes**: Registration total now includes extras.

```typescript
interface Registration {
  id: UUID;
  familyUnitId: UUID;
  campEditionId: UUID;                    // Changed from campId
  registeredByUserId: UUID;

  // Pricing breakdown
  baseTotalAmount: decimal;               // Sum of all member prices (without extras)
  extrasAmount: decimal;                  // Sum of all extras
  discountApplied: decimal;
  totalAmount: decimal;                   // baseTotalAmount + extrasAmount - discountApplied

  status: 'Pending' | 'Confirmed' | 'Cancelled';
  notes?: string;
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  familyUnit?: FamilyUnit;
  campEdition?: CampEdition;              // Changed from camp
  registeredByUser?: User;
  members?: RegistrationMember[];
  payments?: Payment[];
  extras?: RegistrationExtra[];           // New relationship
}
```

---

## User Stories

### Phase 0: Camp Candidate/Proposal Management (NEW)

#### US-CAMP-000: Propose Camp Candidates

**As a** Board member
**I want to** create multiple proposed camp candidates for the next year
**So that** we can evaluate different options before committing to a location

**Acceptance Criteria**:

- Create new camp edition with status "Proposed"
- Select existing camp location or create new one
- Fill required fields: year, dates, location description, pricing
- Add proposal reason and notes for discussion
- Save as Proposed (not visible to members)
- Can create multiple Proposed editions for same year (different camps)

**UI**: Camp Management → Propose New Camp

**API Endpoint**: `POST /api/camps/editions/propose`

**Request**:

```json
{
  "campId": "uuid-of-camp-location",
  "year": 2026,
  "startDate": "2026-07-10",
  "endDate": "2026-07-20",
  "location": "Swiss Alps - Grindelwald region",
  "description": "Mountain camp with hiking and climbing",
  "pricePerAdult": 180.00,
  "pricePerChild": 120.00,
  "pricePerBaby": 60.00,
  "maxCapacity": 80,
  "proposalReason": "Excellent facilities, close to activities",
  "proposalNotes": "Pros: Great location, good price. Cons: Limited capacity"
}
```

**Response**: CampEdition with status "Proposed"

**Validation**:

- User must have Board or Admin role
- Year must be current or future year
- Can have multiple Proposed editions for same year (different camps)
- EndDate > StartDate
- All pricing fields >= 0

---

#### US-CAMP-001: View Proposed Candidates

**As a** Board member
**I want to** see all proposed camp candidates for a given year
**So that** I can compare options and make a decision

**Acceptance Criteria**:

- Display all Proposed editions for selected year
- Show: location, dates, capacity, pricing summary, proposal notes
- Support side-by-side comparison (table view)
- Show pros/cons from proposal notes
- Quick actions: View Details, Approve, Reject

**UI**: Camp Management → Proposed Camps → [Select Year]

**API Endpoint**: `GET /api/camps/editions/proposed?year=2026`

**Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "camp": { "id": "uuid", "name": "Mountain Camp" },
      "year": 2026,
      "location": "Swiss Alps - Grindelwald",
      "startDate": "2026-07-10",
      "endDate": "2026-07-20",
      "pricePerAdult": 180.00,
      "pricePerChild": 120.00,
      "pricePerBaby": 60.00,
      "maxCapacity": 80,
      "proposalReason": "Excellent facilities",
      "proposalNotes": "Pros: Great location...",
      "status": "Proposed"
    }
  ]
}
```

---

#### US-CAMP-002: Select Winning Candidate

**As a** Board member
**I want to** promote a proposed candidate to Draft status
**So that** we can begin planning the camp edition

**Acceptance Criteria**:

- Select one Proposed edition
- Confirm selection with dialog
- Change status from Proposed → Draft
- Optionally: Automatically reject other Proposed editions for same year
- Success notification
- Redirect to edition details

**UI**: Proposed Camps → [Select Candidate] → "Approve & Promote to Draft"

**API Endpoint**: `POST /api/camps/editions/{id}/promote`

**Request**: None (just POST to the endpoint)

**Response**: Updated CampEdition with status "Draft"

**Business Logic**:

- Only Proposed editions can be promoted
- User must have Board or Admin role
- Log status change in audit trail
- Optionally: Update other Proposed editions for same year to status "Rejected" (soft delete)

---

#### US-CAMP-003: Reject Proposed Candidate

**As a** Board member
**I want to** reject a proposed candidate
**So that** it's removed from active consideration

**Acceptance Criteria**:

- Select Proposed edition
- Confirm rejection with reason
- Mark as rejected (soft delete or status change)
- No longer appears in Proposed list
- Can still be viewed in history

**UI**: Proposed Camps → [Select Candidate] → "Reject"

**API Endpoint**: `DELETE /api/camps/editions/{id}/reject`

**Request**:

```json
{
  "reason": "Too expensive, limited capacity"
}
```

**Response**: Success message

**Business Logic**:

- Only Proposed editions can be rejected
- Soft delete: Update isArchived = true or add rejectedReason field
- Preserve proposal data for historical reference

---

### Phase 1: Camp Location Inventory Management (Board Role)

#### US-CAMP-101: View Camp Locations

**As a** Board member
**I want to** see all camp locations stored in the system
**So that** I can understand what camp locations are available for creating new editions

**Acceptance Criteria**:

- Display paginated list of all Camps
- Show: name, description preview, coordinates, pricing template (adult/child/baby), status
- Support filtering by name, status (Active/Inactive/Historical), or search
- Sort alphabetically
- Display on interactive map showing all camp location pins
- Show count of editions per camp location

**UI**: Admin Dashboard → Camp Management → Camp Locations

**API Endpoint**: `GET /api/camps?page=1&pageSize=20&status=Active`

**Response**:

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "uuid",
        "name": "Mountain Camp",
        "description": "Beautiful mountain location...",
        "latitude": 46.5833,
        "longitude": 7.9833,
        "basePriceAdult": 180.00,
        "basePriceChild": 120.00,
        "basePriceBaby": 60.00,
        "status": "Active",
        "editionCount": 5
      }
    ],
    "totalCount": 15,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

---

#### US-CAMP-102: Create Camp Location

**As a** Board member
**I want to** create a new camp location with reusable information
**So that** I can quickly set up new editions for that location

**Acceptance Criteria**:

- Form fields: name, description, latitude, longitude, basePriceAdult, basePriceChild, basePriceBaby, status
- Validate: latitude (-90 to 90), longitude (-180 to 180), all prices >= 0
- Status options: Active (default), Inactive, HistoricalArchive
- Save camp location to database
- Confirmation message on success
- Map preview showing the pin location
- Return to camp locations list

**UI**: Camp Management → New Camp Location

**API Endpoint**: `POST /api/camps`

**Request**:

```json
{
  "name": "Mountain Camp",
  "description": "Beautiful alpine location with hiking trails",
  "latitude": 46.5833,
  "longitude": 7.9833,
  "basePriceAdult": 180.00,
  "basePriceChild": 120.00,
  "basePriceBaby": 60.00,
  "status": "Active"
}
```

**Validation** (FluentValidation):

```csharp
public class CreateCampRequestValidator : AbstractValidator<CreateCampRequest>
{
    public CreateCampRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(5000);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.BasePriceAdult).GreaterThanOrEqualTo(0).PrecisionScale(10, 2, true);
        RuleFor(x => x.BasePriceChild).GreaterThanOrEqualTo(0).PrecisionScale(10, 2, true);
        RuleFor(x => x.BasePriceBaby).GreaterThanOrEqualTo(0).PrecisionScale(10, 2, true);
        RuleFor(x => x.Status).IsInEnum();
    }
}
```

---

#### US-CAMP-103: Edit Camp Location

**As a** Board member
**I want to** update an existing camp location
**So that** I can fix details or change pricing structure for future editions

**Acceptance Criteria**:

- Load camp location details in edit form
- Allow changes to: name, description, latitude, longitude, base prices, status
- Validate inputs same as US-CAMP-102
- Map preview shows updated pin location in real-time
- Prevent deletion of camps with active editions (show warning)
- Audit timestamp updated

**Constraints**:

- Cannot delete if any editions reference it
- Cannot change status to Inactive if active editions exist

**API Endpoint**: `PUT /api/camps/{id}`

---

#### US-CAMP-104: Configure Global Age Ranges

**As a** Board member
**I want to** configure the global age range definitions for the association
**So that** all camp editions use consistent age categories unless overridden

**Acceptance Criteria**:

- View current age range settings (Baby max age, Child min/max age, Adult min age)
- Update age ranges with validation:
  - Baby max age ≥ 0
  - Child min age > Baby max age
  - Child max age ≥ Child min age
  - Adult min age > Child max age
- Show warning: "Changing age ranges will affect pricing calculation for pending registrations"
- Save changes to AssociationSettings table
- Log who made the change and when
- Show success confirmation

**UI**: Admin Dashboard → Settings → Age Range Configuration

**API Endpoints**:

- `GET /api/settings/age-ranges` - Get current age ranges
- `PUT /api/settings/age-ranges` - Update age ranges

**Request** (Update):

```json
{
  "babyMaxAge": 2,
  "childMinAge": 3,
  "childMaxAge": 12,
  "adultMinAge": 13
}
```

**Validation**:

```csharp
public class UpdateAgeRangesValidator : AbstractValidator<UpdateAgeRangesRequest>
{
    public UpdateAgeRangesValidator()
    {
        RuleFor(x => x.BabyMaxAge).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChildMinAge).GreaterThan(x => x.BabyMaxAge)
            .WithMessage("Child minimum age must be greater than baby maximum age");
        RuleFor(x => x.ChildMaxAge).GreaterThanOrEqualTo(x => x.ChildMinAge)
            .WithMessage("Child maximum age must be greater than or equal to child minimum age");
        RuleFor(x => x.AdultMinAge).GreaterThan(x => x.ChildMaxAge)
            .WithMessage("Adult minimum age must be greater than child maximum age");
    }
}
```

**Business Logic**:

- Only Board and Admin users can update age ranges
- Changes are global and affect all new camp editions
- Existing editions with `useCustomAgeRanges = false` will use new ranges
- Existing editions with `useCustomAgeRanges = true` remain unchanged
- Consider recalculating pending registrations when ranges change

---

### Phase 2: Camp Edition Management (Board Role)

#### US-CAMP-201: View Active Camp Edition

**As a** Board member
**I want to** see the current year's active camp edition details
**So that** I can manage registrations and edition status

**Acceptance Criteria**:

- Display full edition details: dates, location, pricing (adult/child/baby), capacity, age requirements
- Show registration count vs. max capacity (with progress bar)
- Show edition status with status indicator
- Show parent camp location info (name, coordinates)
- Show list of extras with availability
- Quick actions: Edit, Manage Extras, View Registrations, Manage Payments
- If no active edition: show "No active camp edition for [year]" with "Create" button

**UI**: Dashboard → Active Camp Edition (primary card)

**API Endpoint**: `GET /api/camps/editions/active`

---

#### US-CAMP-202: Create New Camp Edition

**As a** Board member
**I want to** create a new camp edition for an upcoming year
**So that** I can open registrations for that year

**Acceptance Criteria**:

- Choose existing camp location (from dropdown)
- Pre-populate: name, pricing from camp location defaults
- Form fields: year, startDate, endDate, location (specific description), maxCapacity
- Override camp defaults if needed: pricing, age ranges
- Set initial status: Draft
- Prevent multiple editions for same year from same camp
- Validate: year not already used for this camp, endDate > startDate
- Confirmation message on success

**UI**: Camp Management → New Edition → Select Camp Location → Fill Details

**API Endpoint**: `POST /api/camps/editions`

**Request**:

```json
{
  "campId": "uuid-of-camp-location",
  "year": 2026,
  "startDate": "2026-07-10",
  "endDate": "2026-07-20",
  "location": "Grindelwald region, Swiss Alps",
  "description": "Summer mountain camp with hiking and climbing activities",
  "pricePerAdult": 180.00,
  "pricePerChild": 120.00,
  "pricePerBaby": 60.00,
  "babyMaxAge": 2,
  "childMinAge": 3,
  "childMaxAge": 12,
  "adultMinAge": 13,
  "maxCapacity": 80,
  "contactEmail": "info@abuvi.org",
  "contactPhone": "+34612345678"
}
```

**Validation**:

```csharp
public class CreateCampEditionRequestValidator : AbstractValidator<CreateCampEditionRequest>
{
    public CreateCampEditionRequestValidator(ICampsRepository campsRepo)
    {
        RuleFor(x => x.CampId).NotEmpty().MustAsync(async (id, ct) =>
            await campsRepo.GetByIdAsync(id, ct) != null)
            .WithMessage("Camp location not found");

        RuleFor(x => x.Year).GreaterThanOrEqualTo(DateTime.UtcNow.Year);

        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date");

        RuleFor(x => x.PricePerAdult).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PricePerChild).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PricePerBaby).GreaterThanOrEqualTo(0);

        RuleFor(x => x.BabyMaxAge).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChildMinAge).GreaterThan(x => x.BabyMaxAge);
        RuleFor(x => x.ChildMaxAge).GreaterThanOrEqualTo(x => x.ChildMinAge);
        RuleFor(x => x.AdultMinAge).GreaterThan(x => x.ChildMaxAge);

        RuleFor(x => x.MaxCapacity).GreaterThan(0);
    }
}
```

---

#### US-CAMP-203: Manage Camp Edition Extras

**As a** Board member
**I want to** add, edit, or remove extras for a camp edition
**So that** families can select additional services or activities

**Acceptance Criteria**:

- View list of extras for the edition
- Add new extra: name, description, price, pricing type (per person/family), required, max quantity
- Edit existing extra (only if no registrations have selected it, or allow with warning)
- Activate/deactivate extras
- Set display order
- Show current quantity selected across all registrations
- Prevent deletion if registrations reference the extra

**UI**: Camp Edition Details → Manage Extras

**API Endpoints**:

- `GET /api/camps/editions/{editionId}/extras`
- `POST /api/camps/editions/{editionId}/extras`
- `PUT /api/camps/editions/{editionId}/extras/{extraId}`
- `DELETE /api/camps/editions/{editionId}/extras/{extraId}`

**Create Extra Request**:

```json
{
  "name": "Kayak Rental",
  "description": "Full-day kayak rental including safety equipment",
  "price": 25.00,
  "pricingType": "PerPerson",
  "pricingPeriod": "OneTime",
  "isRequired": false,
  "maxQuantity": 20,
  "sortOrder": 1
}
```

**Examples by Pricing Configuration**:

```json
// One-time per-person charge (e.g., kayak rental)
{
  "name": "Kayak Rental",
  "price": 25.00,
  "pricingType": "PerPerson",
  "pricingPeriod": "OneTime"
  // Total for 2 people = 25 × 2 = €50
}

// Daily per-person charge (e.g., vegan menu upgrade)
{
  "name": "Vegan Menu",
  "price": 5.00,
  "pricingType": "PerPerson",
  "pricingPeriod": "PerDay"
  // Total for 1 person, 10-day camp = 5 × 1 × 10 = €50
}

// One-time per-family charge (e.g., workshop)
{
  "name": "Mountain Guide Workshop",
  "price": 50.00,
  "pricingType": "PerFamily",
  "pricingPeriod": "OneTime"
  // Total for entire family = €50 (regardless of size)
}
```

**Business Logic**:

- When extra is required, automatically add to all new registrations
- When extra has maxQuantity, prevent selection when sold out
- When extra is deleted, check if any registrations reference it (cascade or prevent)
- Calculate camp duration (days) from edition startDate and endDate for PerDay pricing

---

#### US-CAMP-204: Change Camp Edition Status

**As a** Board member
**I want to** transition camp edition status through the workflow
**So that** I control when registrations are accepted and when the edition is finalized

**Acceptance Criteria**:

- Display current status with transition button
- Status = Proposed: Show "Promote to Draft" button (covered in US-CAMP-002)
- Status = Draft: Show "Open Registrations" button
- Status = Open: Show "Close Registrations" button
- Status = Closed: Show "Mark as Completed" button
- Status = Completed: Read-only
- Confirm before each transition with warning message
- Log status change with timestamp and user
- Status = Open: Only if dates are in future and valid

**Workflow**:

```
Proposed → (promote) → Draft → Open → Closed → Completed
         (reject)
```

**API Endpoint**: `POST /api/camps/editions/{id}/status`

**Request**:

```json
{
  "newStatus": "Open"
}
```

**Validation**:

- Proposed → Draft: Requires promotion action
- Draft → Open: startDate must be in future
- Open → Closed: Always allowed
- Closed → Completed: endDate must be in past
- Cannot go backwards in workflow

---

> **Note**: Registration pricing calculation and breakdown functionality has been moved to a separate feature: `feat-camp-registration-flow`. See [camp-registration-flow.md](./camp-registration-flow.md) for the complete registration workflow specification.

---

## API Endpoints Summary

### Camp Locations

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/camps` | List all camp locations (paginated) | Board+ |
| GET | `/api/camps/{id}` | Get camp location by ID | Board+ |
| POST | `/api/camps` | Create new camp location | Board+ |
| PUT | `/api/camps/{id}` | Update camp location | Board+ |
| DELETE | `/api/camps/{id}` | Delete camp location (if no editions) | Board+ |

### Camp Editions

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/camps/editions/proposed?year={year}` | List proposed candidates for year | Board+ |
| POST | `/api/camps/editions/propose` | Create proposed camp candidate | Board+ |
| POST | `/api/camps/editions/{id}/promote` | Promote proposed to draft | Board+ |
| DELETE | `/api/camps/editions/{id}/reject` | Reject proposed candidate | Board+ |
| GET | `/api/camps/editions/active` | Get active camp edition | Board+ |
| GET | `/api/camps/editions/{id}` | Get camp edition by ID | Board+ |
| POST | `/api/camps/editions` | Create new camp edition | Board+ |
| PUT | `/api/camps/editions/{id}` | Update camp edition | Board+ |
| POST | `/api/camps/editions/{id}/status` | Change edition status | Board+ |
| DELETE | `/api/camps/editions/{id}` | Delete camp edition (if no registrations) | Board+ |

### Camp Edition Extras

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/camps/editions/{editionId}/extras` | List extras for edition | Board+ |
| GET | `/api/camps/editions/{editionId}/extras/{extraId}` | Get extra by ID | Board+ |
| POST | `/api/camps/editions/{editionId}/extras` | Create new extra | Board+ |
| PUT | `/api/camps/editions/{editionId}/extras/{extraId}` | Update extra | Board+ |
| DELETE | `/api/camps/editions/{editionId}/extras/{extraId}` | Delete extra | Board+ |

### Association Settings

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/settings/age-ranges` | Get current global age ranges | Board+ |
| PUT | `/api/settings/age-ranges` | Update global age ranges | Board+ |

> **Note**: Registration endpoints have been moved to `feat-camp-registration-flow`. See [camp-registration-flow.md](./camp-registration-flow.md).

---

## Implementation Guide

### File Structure (Vertical Slice Architecture)

```
src/Abuvi.API/
├── Features/
│   ├── Camps/
│   │   ├── CampsEndpoints.cs           # HTTP endpoints for camp locations
│   │   ├── CampsModels.cs              # Camp entity, DTOs, requests/responses
│   │   ├── CampsService.cs             # Business logic for camp locations
│   │   ├── CampsRepository.cs          # Data access for camps
│   │   ├── CreateCampValidator.cs      # FluentValidation for CreateCampRequest
│   │   └── UpdateCampValidator.cs
│   │
│   ├── CampEditions/
│   │   ├── CampEditionsEndpoints.cs    # HTTP endpoints for editions
│   │   ├── CampEditionsModels.cs       # CampEdition entity, DTOs
│   │   ├── CampEditionsService.cs      # Business logic for editions
│   │   ├── CampEditionsRepository.cs   # Data access for editions
│   │   ├── CreateCampEditionValidator.cs
│   │   ├── ProposeCampEditionValidator.cs
│   │   └── CampEditionStatusValidator.cs
│   │
│   ├── CampEditionExtras/
│   │   ├── CampEditionExtrasEndpoints.cs
│   │   ├── CampEditionExtrasModels.cs  # CampEditionExtra entity, DTOs
│   │   ├── CampEditionExtrasService.cs
│   │   ├── CampEditionExtrasRepository.cs
│   │   └── CreateExtraValidator.cs
│   │
│   └── Registrations/
│       ├── RegistrationsEndpoints.cs
│       ├── RegistrationsModels.cs      # Updated Registration with extras
│       ├── RegistrationsService.cs     # Includes pricing calculation logic
│       ├── RegistrationsRepository.cs
│       ├── RegistrationPricingService.cs  # NEW: Dedicated pricing calculator
│       └── CreateRegistrationValidator.cs
│
├── Data/
│   ├── AbuviDbContext.cs               # Add new DbSets
│   ├── Configurations/
│   │   ├── CampConfiguration.cs        # Updated with pricing fields
│   │   ├── CampEditionConfiguration.cs # Updated with new fields
│   │   ├── CampEditionExtraConfiguration.cs  # NEW
│   │   └── RegistrationExtraConfiguration.cs # NEW
│   └── Migrations/
│       └── 20260213_AddCampPricingAndExtras.cs  # NEW migration
│
└── Program.cs                          # Register new endpoints
```

### Implementation Steps (TDD Approach)

#### Step 1: Update Data Model (Database First)

1. Create migration for new tables and updated columns:

   ```bash
   dotnet ef migrations add AddCampPricingAndExtras --project src/Abuvi.API
   ```

2. Verify generated migration SQL
3. Apply migration to dev database:

   ```bash
   dotnet ef database update --project src/Abuvi.API
   ```

#### Step 2: Camp Locations with Pricing Template (TDD)

**Write Tests First**:

`tests/Abuvi.Tests/Unit/Features/Camps/CampsServiceTests.cs`:

```csharp
public class CampsServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidPricingTemplate_CreatesCamp()
    {
        // Arrange
        var repository = Substitute.For<ICampsRepository>();
        var sut = new CampsService(repository);
        var request = new CreateCampRequest(
            "Mountain Camp",
            "Alpine location",
            46.5833,
            7.9833,
            180.00m,  // adult
            120.00m,  // child
            60.00m,   // baby
            CampStatus.Active
        );

        // Act
        var result = await sut.CreateAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BasePriceAdult.Should().Be(180.00m);
        result.BasePriceChild.Should().Be(120.00m);
        result.BasePriceBaby.Should().Be(60.00m);

        await repository.Received(1).AddAsync(
            Arg.Is<Camp>(c =>
                c.BasePriceAdult == 180.00m &&
                c.BasePriceChild == 120.00m &&
                c.BasePriceBaby == 60.00m
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CreateAsync_WithNegativePrices_ThrowsValidationException()
    {
        // Test validation logic...
    }
}
```

**Then Implement**:

`src/Abuvi.API/Features/Camps/CampsService.cs`:

```csharp
public class CampsService(ICampsRepository repository)
{
    public async Task<CampResponse> CreateAsync(CreateCampRequest request, CancellationToken ct)
    {
        var camp = new Camp
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            BasePriceAdult = request.BasePriceAdult,
            BasePriceChild = request.BasePriceChild,
            BasePriceBaby = request.BasePriceBaby,
            Status = request.Status
        };

        await repository.AddAsync(camp, ct);
        return camp.ToResponse();
    }
}
```

#### Step 3: Camp Editions with Age-Based Pricing (TDD)

**Write Tests First** for:

- Creating edition with pricing
- Validating age range consistency
- Preventing duplicate year for same camp

**Then Implement** service, repository, validators

#### Step 4: Camp Edition Extras (TDD)

**Write Tests First** for:

- Creating extras with per-person/per-family pricing
- Max quantity validation
- Required extras auto-added to registrations

**Then Implement** extras feature

#### Step 5: Registration Pricing Calculator (TDD)

**Write Tests First**:

```csharp
public class RegistrationPricingServiceTests
{
    [Fact]
    public async Task CalculateTotalAsync_WithMixedAges_CalculatesCorrectly()
    {
        // Arrange: Family with 2 adults, 1 child, 1 baby
        // Edition: Adult=180, Child=120, Baby=60
        // Expected base total: 180 + 180 + 120 + 60 = 540

        // Act
        var result = await sut.CalculateTotalAsync(registrationId, ct);

        // Assert
        result.BaseTotalAmount.Should().Be(540.00m);
    }

    [Fact]
    public async Task CalculateTotalAsync_WithPerPersonExtra_AddsCorrectAmount()
    {
        // Extra: Kayak rental, 25€ per person, quantity = 2
        // Expected extras amount: 50€

        // Assert
        result.ExtrasAmount.Should().Be(50.00m);
    }
}
```

**Then Implement** pricing service

---

## Database Migration Strategy

### Migration Plan

1. **Backup existing data**:
   - Export current `camps` table
   - Export current `registrations` table

2. **Create new tables**:
   - `camp_edition_extras`
   - `registration_extras`

3. **Update existing tables**:
   - `camps`: Add `base_price_adult`, `base_price_child`, `base_price_baby`
   - `camps`: Drop `base_price` (or rename to `base_price_adult` for backward compat)
   - `camp_editions`: Add new pricing columns, age range columns, status "Proposed"
   - `registrations`: Add `base_total_amount`, `extras_amount`, change `camp_id` to `camp_edition_id`

4. **Data migration**:
   - Migrate existing camp `basePrice` → `basePriceAdult`
   - Set `basePriceChild = basePrice * 0.7` (example ratio)
   - Set `basePriceBaby = basePrice * 0.4`
   - Create default age ranges for all editions

5. **Add constraints and indexes**

### Migration SQL (Sample)

```sql
-- Step 1: Add new pricing columns to camps
ALTER TABLE camps
ADD COLUMN base_price_adult DECIMAL(10,2),
ADD COLUMN base_price_child DECIMAL(10,2),
ADD COLUMN base_price_baby DECIMAL(10,2);

-- Step 2: Migrate existing data
UPDATE camps
SET base_price_adult = base_price,
    base_price_child = base_price * 0.7,  -- 70% of adult price
    base_price_baby = base_price * 0.4;   -- 40% of adult price

-- Step 3: Make new columns NOT NULL
ALTER TABLE camps
ALTER COLUMN base_price_adult SET NOT NULL,
ALTER COLUMN base_price_child SET NOT NULL,
ALTER COLUMN base_price_baby SET NOT NULL;

-- Step 4: Drop old column (optional, keep for backward compat)
-- ALTER TABLE camps DROP COLUMN base_price;

-- Step 5: Add constraints
ALTER TABLE camps
ADD CONSTRAINT chk_base_price_adult_positive CHECK (base_price_adult >= 0),
ADD CONSTRAINT chk_base_price_child_positive CHECK (base_price_child >= 0),
ADD CONSTRAINT chk_base_price_baby_positive CHECK (base_price_baby >= 0);

-- Similar steps for camp_editions...
```

---

## Testing Strategy

### Unit Tests (90% coverage target)

**Test Categories**:

1. **Service Tests**:
   - CampsService: CRUD operations, validation
   - CampEditionsService: Status transitions, proposal workflow
   - CampEditionExtrasService: Extra management, quantity validation
   - RegistrationPricingService: Pricing calculations for all scenarios

2. **Validator Tests**:
   - CreateCampRequestValidator
   - CreateCampEditionRequestValidator
   - CreateExtraRequestValidator
   - Age range validation
   - Pricing validation

3. **Repository Tests** (using in-memory or test DB):
   - Complex queries (proposed editions, active editions)
   - Cascade deletes
   - Unique constraints

### Integration Tests

**Test Scenarios**:

- Full camp creation workflow (location → proposed → draft → open)
- Registration with extras calculation
- Payment workflow with extras
- Concurrent registration handling (capacity limits)

### Manual Testing Checklist

- [ ] Create camp location with pricing template
- [ ] Propose 3 camp candidates for 2026
- [ ] Compare candidates side-by-side
- [ ] Promote one candidate to draft
- [ ] Add extras to edition
- [ ] Open registrations
- [ ] Create registration with mixed-age family
- [ ] Add optional extras to registration
- [ ] Verify pricing breakdown is correct
- [ ] Complete payment
- [ ] Close registrations
- [ ] Mark edition as completed

---

## Security Considerations

- **Authorization**: Only Board and Admin can manage camps, editions, and extras
- **Validation**: All monetary values validated as non-negative decimals
- **Audit Trail**: Log all status changes, pricing changes, and deletions
- **RGPD Compliance**: No PII in camp/edition entities

---

## Performance Considerations

- **Indexes**:
  - `camp_editions(status)` for filtering proposed/active editions
  - `camp_edition_extras(camp_edition_id, is_active)` for fast extra lookups
  - `registration_extras(registration_id)` for pricing calculations

- **Caching**: Cache active camp edition (rarely changes)

- **Query Optimization**: Use `.Include()` for related data to avoid N+1

---

## Next Steps

1. ✅ Define enhanced data model
2. ⬜ Create database migration
3. ⬜ Implement Camp Location CRUD (TDD)
4. ⬜ Implement Camp Edition Proposal workflow (TDD)
5. ⬜ Implement Camp Edition Extras (TDD)
6. ⬜ Implement Registration Pricing Calculator (TDD)
7. ⬜ Create frontend UI for camp management
8. ⬜ Create frontend UI for registration with extras
9. ⬜ End-to-end testing

---

## Appendix: Example Scenarios

### Scenario 1: Proposing and Selecting a Camp

1. Board creates 3 proposed camps for 2026:
   - Mountain Camp (Grindelwald, CH) - €180/adult
   - Beach Camp (Costa Brava, ES) - €150/adult
   - Adventure Camp (Pyrenees, FR) - €200/adult

2. Board compares proposals in table view

3. Board selects Mountain Camp and promotes to Draft

4. Board adds extras:
   - Kayak rental: €25/person, max 20
   - Transport from Barcelona: €30/person
   - Mountain guide workshop: €50/family

5. Board opens registrations

### Scenario 2: Family Registration with Extras

1. Garcia family (2 adults, 1 child age 10, 1 baby age 1) registers

2. System calculates base amount:
   - Adult 1: €180
   - Adult 2: €180
   - Child: €120
   - Baby: €60
   - **Base total: €540**

3. Family selects extras:
   - Kayak rental × 2: €50
   - Mountain guide workshop: €50
   - **Extras total: €100**

4. No discount applied

5. **Final total: €640**

6. Family makes deposit payment of €200

7. Registration status: Pending (€440 remaining)

8. Family completes remaining payment

9. Registration status: Confirmed

---

## Document Control

- **Version**: 1.0
- **Date**: 2026-02-13
- **Author**: Product Team + AI Assistant
- **Status**: Draft for Review
- **Next Review**: After board approval
