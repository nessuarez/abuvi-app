# Camp Registration Flow - Technical Specification

## Overview

This document defines the complete camp registration workflow for ABUVI, including:

- **Family registration** for camp editions
- **Age-based pricing calculation** (adult, child, baby)
- **Extras selection** (equipment, meals, transport)
- **Pricing breakdown display** for transparency
- **Payment tracking** until confirmation
- **Capacity validation** to prevent overbooking

---

## Prerequisites

This feature depends on:

- ✅ **feat-camps-definition**: Camp locations, editions, and extras management
- ✅ **User authentication**: Logged-in members can register families
- ✅ **Family units**: User has created/joined a family unit
- ✅ **Age ranges configured**: Global or edition-specific age ranges

---

## Key Features

### 1. Automatic Pricing Calculation

**Based on Family Composition**:

- System calculates each member's age at camp start date
- Applies appropriate pricing tier (adult/child/baby)
- Sums individual amounts for base total

**Example**:

```
Family: 2 adults (35, 33), 1 child (10), 1 baby (1)
Camp pricing: Adult €180, Child €120, Baby €60
Base total = €180 + €180 + €120 + €60 = €540
```

### 2. Extras Calculation

**Supports Multiple Pricing Models**:

- **OneTime + PerPerson**: Kayak rental €25/person = €50 for 2 people
- **PerDay + PerPerson**: Vegan menu €5/person/day = €50 for 1 person × 10 days
- **OneTime + PerFamily**: Workshop €50/family = €50 total

**Example**:

```
Extras selected:
- Kayak rental (€25, PerPerson, OneTime) × 2 = €50
- Vegan menu (€5, PerPerson, PerDay) × 1 × 10 days = €50
Total extras = €100
```

### 3. Final Total

```
Base Total:    €540 (family members)
Extras:        €100 (selected add-ons)
Discount:      -€20 (member discount)
───────────────────
Grand Total:   €620
```

---

## Data Model (Registration Entities)

### Registration (Updated)

```typescript
interface Registration {
  id: UUID;
  familyUnitId: UUID;                     // FK to FamilyUnit
  campEditionId: UUID;                    // FK to CampEdition
  registeredByUserId: UUID;               // FK to User (who created)

  // Pricing breakdown
  baseTotalAmount: decimal;               // Sum of all member prices
  extrasAmount: decimal;                  // Sum of all selected extras
  discountApplied: decimal;               // Applied discount
  totalAmount: decimal;                   // Calculated: base + extras - discount

  status: 'Pending' | 'Confirmed' | 'Cancelled';
  notes?: string;                         // Optional notes from family
  createdAt: DateTime;
  updatedAt: DateTime;

  // Relationships
  familyUnit?: FamilyUnit;
  campEdition?: CampEdition;
  registeredByUser?: User;
  members?: RegistrationMember[];         // Family members attending
  payments?: Payment[];                   // Payment history
  extras?: RegistrationExtra[];           // Selected extras
}
```

**Database Schema**:

```sql
CREATE TABLE registrations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    family_unit_id UUID NOT NULL REFERENCES family_units(id) ON DELETE RESTRICT,
    camp_edition_id UUID NOT NULL REFERENCES camp_editions(id) ON DELETE RESTRICT,
    registered_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,

    -- Pricing breakdown
    base_total_amount DECIMAL(10,2) NOT NULL CHECK (base_total_amount >= 0),
    extras_amount DECIMAL(10,2) NOT NULL DEFAULT 0 CHECK (extras_amount >= 0),
    discount_applied DECIMAL(10,2) NOT NULL DEFAULT 0 CHECK (discount_applied >= 0),
    total_amount DECIMAL(10,2) NOT NULL CHECK (total_amount >= 0),

    status VARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Confirmed', 'Cancelled')),
    notes TEXT,

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Constraints
    UNIQUE(family_unit_id, camp_edition_id),  -- One registration per family per edition
    CHECK (total_amount = base_total_amount + extras_amount - discount_applied)
);

CREATE INDEX idx_registrations_family ON registrations(family_unit_id);
CREATE INDEX idx_registrations_edition ON registrations(camp_edition_id);
CREATE INDEX idx_registrations_status ON registrations(status);
```

### RegistrationMember (Updated)

```typescript
interface RegistrationMember {
  id: UUID;
  registrationId: UUID;                   // FK to Registration
  familyMemberId: UUID;                   // FK to FamilyMember
  ageAtCamp: number;                      // Calculated age at camp start
  ageCategory: 'Baby' | 'Child' | 'Adult'; // Determined category
  individualAmount: decimal;              // Price for this member
  createdAt: DateTime;

  // Relationships
  registration?: Registration;
  familyMember?: FamilyMember;
}
```

**Database Schema**:

```sql
CREATE TABLE registration_members (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    registration_id UUID NOT NULL REFERENCES registrations(id) ON DELETE CASCADE,
    family_member_id UUID NOT NULL REFERENCES family_members(id) ON DELETE RESTRICT,
    age_at_camp INTEGER NOT NULL CHECK (age_at_camp >= 0),
    age_category VARCHAR(10) NOT NULL CHECK (age_category IN ('Baby', 'Child', 'Adult')),
    individual_amount DECIMAL(10,2) NOT NULL CHECK (individual_amount >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    UNIQUE(registration_id, family_member_id)  -- Each member once per registration
);

CREATE INDEX idx_registration_members_registration ON registration_members(registration_id);
CREATE INDEX idx_registration_members_family_member ON registration_members(family_member_id);
```

### RegistrationExtra (Already Defined)

See [camp-crud-enriched.md](./camp-crud-enriched.md) for the `RegistrationExtra` entity.

---

## User Stories

### US-REG-001: View Available Camp Editions

**As a** Member
**I want to** see all open camp editions available for registration
**So that** I can choose which camp to attend

**Acceptance Criteria**:

- Display list of camp editions with status "Open"
- Show: camp name, dates, location, pricing summary, capacity status
- Filter by year
- Show "Registration full" badge if capacity reached
- Show "Register" button for available camps
- Show age requirements

**UI**: Dashboard → Available Camps

**API Endpoint**: `GET /api/camps/editions/available`

**Response**:

```json
{
  "success": true,
  "data": [
    {
      "id": "uuid",
      "name": "Summer Mountain Camp 2026",
      "year": 2026,
      "startDate": "2026-07-10",
      "endDate": "2026-07-20",
      "location": "Swiss Alps - Grindelwald",
      "pricePerAdult": 180.00,
      "pricePerChild": 120.00,
      "pricePerBaby": 60.00,
      "maxCapacity": 80,
      "currentRegistrations": 45,
      "spotsRemaining": 35,
      "status": "Open",
      "ageRanges": {
        "baby": "0-2",
        "child": "3-12",
        "adult": "13+"
      }
    }
  ]
}
```

---

### US-REG-002: Create Registration

**As a** Member
**I want to** register my family for a camp edition
**So that** we can attend the camp

**Acceptance Criteria**:

- Select family unit (pre-populated for logged-in user)
- Select camp edition from available list
- Select which family members will attend (at least 1 required)
- Validate each member's age against camp age requirements
- Auto-calculate pricing based on selected members
- Display pricing preview before confirmation
- Add optional notes
- Validate capacity before saving
- Transition to extras selection screen

**UI**: Available Camps → [Select Camp] → Register Family

**API Endpoint**: `POST /api/registrations`

**Request**:

```json
{
  "campEditionId": "uuid-of-edition",
  "familyUnitId": "uuid-of-family",
  "memberIds": [
    "uuid-member-1",
    "uuid-member-2",
    "uuid-member-3"
  ],
  "notes": "Looking forward to the mountain camp!"
}
```

**Response**:

```json
{
  "success": true,
  "data": {
    "id": "uuid-registration",
    "familyUnit": { "id": "uuid", "name": "Garcia Family" },
    "campEdition": { "id": "uuid", "name": "Summer Mountain Camp 2026" },
    "status": "Pending",
    "members": [
      {
        "id": "uuid",
        "name": "John Garcia",
        "ageAtCamp": 35,
        "ageCategory": "Adult",
        "individualAmount": 180.00
      },
      {
        "id": "uuid",
        "name": "Maria Garcia",
        "ageAtCamp": 10,
        "ageCategory": "Child",
        "individualAmount": 120.00
      }
    ],
    "baseTotalAmount": 300.00,
    "extrasAmount": 0.00,
    "discountApplied": 0.00,
    "totalAmount": 300.00,
    "createdAt": "2026-02-13T10:00:00Z"
  }
}
```

**Validation**:

```csharp
public class CreateRegistrationValidator : AbstractValidator<CreateRegistrationRequest>
{
    public CreateRegistrationValidator(
        ICampEditionsRepository editionsRepo,
        IFamilyMembersRepository membersRepo)
    {
        RuleFor(x => x.CampEditionId).NotEmpty()
            .MustAsync(async (id, ct) =>
            {
                var edition = await editionsRepo.GetByIdAsync(id, ct);
                return edition?.Status == CampEditionStatus.Open;
            })
            .WithMessage("Camp edition is not open for registration");

        RuleFor(x => x.FamilyUnitId).NotEmpty();

        RuleFor(x => x.MemberIds).NotEmpty()
            .WithMessage("At least one family member must be selected");

        RuleForEach(x => x.MemberIds).MustAsync(async (memberId, ct) =>
        {
            var member = await membersRepo.GetByIdAsync(memberId, ct);
            return member != null;
        })
        .WithMessage("Invalid family member ID");
    }
}
```

**Business Logic** (RegistrationService):

1. Validate camp edition is Open
2. Validate capacity not exceeded
3. For each selected member:
   - Calculate age at camp start date
   - Determine age category (Baby/Child/Adult)
   - Apply appropriate pricing
   - Validate age is within camp's age requirements
4. Calculate baseTotalAmount
5. Create Registration and RegistrationMembers
6. Return registration with pricing breakdown

---

### US-REG-003: Select Extras for Registration

**As a** Member
**I want to** add optional extras to my registration
**So that** I can rent equipment or purchase additional services

**Acceptance Criteria**:

- Display all available extras for the camp edition
- Show: name, description, price, pricing type (per person/family), pricing period (one-time/per day)
- Show calculated total for each extra based on family size and camp duration
- Mark required extras (auto-selected, disabled)
- Validate max quantity for limited extras
- Show "Sold out" badge for exhausted extras
- Update total amount in real-time as extras are selected
- Save selection and update registration

**UI**: Registration → Select Extras

**API Endpoint**: `POST /api/registrations/{id}/extras`

**Request**:

```json
{
  "extras": [
    {
      "campEditionExtraId": "uuid-kayak",
      "quantity": 2
    },
    {
      "campEditionExtraId": "uuid-vegan-menu",
      "quantity": 1
    }
  ]
}
```

**Response**:

```json
{
  "success": true,
  "data": {
    "registrationId": "uuid",
    "extras": [
      {
        "id": "uuid",
        "name": "Kayak Rental",
        "price": 25.00,
        "pricingType": "PerPerson",
        "pricingPeriod": "OneTime",
        "quantity": 2,
        "totalAmount": 50.00
      },
      {
        "id": "uuid",
        "name": "Vegan Menu",
        "price": 5.00,
        "pricingType": "PerPerson",
        "pricingPeriod": "PerDay",
        "quantity": 1,
        "campDuration": 10,
        "totalAmount": 50.00
      }
    ],
    "extrasAmount": 100.00,
    "updatedTotalAmount": 400.00
  }
}
```

**Business Logic**:

1. Validate extras belong to the camp edition
2. For each extra:
   - If pricingType = PerPerson AND pricingPeriod = OneTime:
     - `totalAmount = price × quantity`
   - If pricingType = PerPerson AND pricingPeriod = PerDay:
     - Calculate camp duration: `days = (endDate - startDate).days`
     - `totalAmount = price × quantity × days`
   - If pricingType = PerFamily AND pricingPeriod = OneTime:
     - `totalAmount = price`
   - If pricingType = PerFamily AND pricingPeriod = PerDay:
     - `totalAmount = price × days`
3. Validate max quantity not exceeded
4. Create RegistrationExtra entries
5. Update Registration.extrasAmount
6. Recalculate Registration.totalAmount

---

### US-REG-004: Display Registration Pricing Breakdown

**As a** Member
**I want to** see a detailed breakdown of my registration cost
**So that** I understand what I'm paying for

**Acceptance Criteria**:

- Show base amount with member breakdown:
  - Member name, age at camp, category (Adult/Child/Baby), individual price
- Show extras with calculation details:
  - Extra name, pricing type, pricing period, quantity, days (if applicable), total
- Show discount if applied (with reason)
- Show final total amount prominently
- Show payment status:
  - Amount paid so far
  - Amount remaining
  - Payment history (date, amount, method, status)
- Export breakdown as PDF for records

**UI**: My Registrations → [Select Registration] → View Details

**API Endpoint**: `GET /api/registrations/{id}`

**Response**:

```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "familyUnit": { "id": "uuid", "name": "Garcia Family" },
    "campEdition": {
      "id": "uuid",
      "name": "Summer Mountain Camp 2026",
      "startDate": "2026-07-10",
      "endDate": "2026-07-20",
      "duration": 10
    },
    "status": "Pending",
    "pricing": {
      "members": [
        {
          "name": "John Garcia",
          "dateOfBirth": "1991-03-15",
          "ageAtCamp": 35,
          "ageCategory": "Adult",
          "individualAmount": 180.00
        },
        {
          "name": "Maria Garcia",
          "dateOfBirth": "2016-05-20",
          "ageAtCamp": 10,
          "ageCategory": "Child",
          "individualAmount": 120.00
        },
        {
          "name": "Baby Garcia",
          "dateOfBirth": "2025-01-10",
          "ageAtCamp": 1,
          "ageCategory": "Baby",
          "individualAmount": 60.00
        }
      ],
      "baseTotalAmount": 360.00,
      "extras": [
        {
          "name": "Kayak Rental",
          "price": 25.00,
          "pricingType": "PerPerson",
          "pricingPeriod": "OneTime",
          "quantity": 2,
          "calculation": "€25 × 2 people",
          "totalAmount": 50.00
        },
        {
          "name": "Vegan Menu",
          "price": 5.00,
          "pricingType": "PerPerson",
          "pricingPeriod": "PerDay",
          "quantity": 1,
          "campDuration": 10,
          "calculation": "€5 × 1 person × 10 days",
          "totalAmount": 50.00
        }
      ],
      "extrasAmount": 100.00,
      "discountApplied": 20.00,
      "discountReason": "Member discount (5%)",
      "totalAmount": 440.00
    },
    "payments": [
      {
        "id": "uuid",
        "amount": 100.00,
        "paymentDate": "2026-02-13T12:00:00Z",
        "status": "Completed",
        "method": "Card"
      }
    ],
    "amountPaid": 100.00,
    "amountRemaining": 340.00,
    "createdAt": "2026-02-13T10:00:00Z",
    "updatedAt": "2026-02-13T12:30:00Z"
  }
}
```

---

### US-REG-005: Update Registration Members or Extras

**As a** Member
**I want to** modify my registration before payment is complete
**So that** I can adjust who is attending or which extras I've selected

**Acceptance Criteria**:

- Only allow updates for registrations with status = Pending
- Allow adding/removing family members (recalculates pricing)
- Allow adding/removing extras (recalculates pricing)
- Validate capacity when adding members
- Show warning: "This will recalculate your total amount"
- Recalculate all amounts automatically
- Preserve payment history
- Update registration timestamp

**UI**: My Registrations → [Select Registration] → Edit

**API Endpoints**:

- `PUT /api/registrations/{id}/members` - Update family members
- `PUT /api/registrations/{id}/extras` - Update extras

**Update Members Request**:

```json
{
  "memberIds": [
    "uuid-member-1",
    "uuid-member-2"
  ]
}
```

**Business Logic**:

- Delete existing RegistrationMembers
- Create new RegistrationMembers for selected members
- Recalculate baseTotalAmount
- Recalculate totalAmount
- Update updatedAt timestamp

---

### US-REG-006: Cancel Registration

**As a** Member
**I want to** cancel my registration
**So that** I can withdraw from the camp

**Acceptance Criteria**:

- Only allow cancellation for Pending or Confirmed registrations
- Show warning: "Are you sure? This action cannot be undone."
- If payments exist, show refund policy message
- Change status to Cancelled
- Release capacity for other families
- Log cancellation with timestamp and user
- Send cancellation confirmation email

**UI**: My Registrations → [Select Registration] → Cancel

**API Endpoint**: `DELETE /api/registrations/{id}`

**Response**:

```json
{
  "success": true,
  "data": {
    "message": "Registration cancelled successfully",
    "refundInfo": "Please contact admin@abuvi.org for refund processing"
  }
}
```

---

### US-REG-007: Auto-Confirm Registration When Fully Paid

**As a** System
**I want to** automatically confirm registration when total amount is paid
**So that** families know their registration is secured

**Acceptance Criteria**:

- When a payment is marked as Completed
- Calculate sum of all Completed payments for the registration
- If sum ≥ totalAmount:
  - Change registration status from Pending → Confirmed
  - Send confirmation email
  - Log status change
- If sum < totalAmount:
  - Keep status as Pending
  - Show remaining amount

**Implementation**: Event handler or service method triggered on payment completion

**Business Logic** (PaymentService):

```csharp
public async Task CompletePaymentAsync(Guid paymentId, CancellationToken ct)
{
    var payment = await _paymentsRepo.GetByIdAsync(paymentId, ct);
    payment.Status = PaymentStatus.Completed;
    await _paymentsRepo.UpdateAsync(payment, ct);

    // Auto-confirm registration if fully paid
    var registration = await _registrationsRepo.GetByIdAsync(payment.RegistrationId, ct);
    var totalPaid = await _paymentsRepo.GetTotalCompletedAsync(payment.RegistrationId, ct);

    if (totalPaid >= registration.TotalAmount && registration.Status == RegistrationStatus.Pending)
    {
        registration.Status = RegistrationStatus.Confirmed;
        await _registrationsRepo.UpdateAsync(registration, ct);
        await _emailService.SendConfirmationEmailAsync(registration, ct);
    }
}
```

---

## API Endpoints Summary

### Registrations

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/api/camps/editions/available` | List open camp editions for registration | Member+ |
| GET | `/api/registrations` | List my family's registrations | Member+ |
| GET | `/api/registrations/{id}` | Get registration with full pricing breakdown | Member+ |
| POST | `/api/registrations` | Create new registration | Member+ |
| PUT | `/api/registrations/{id}/members` | Update family members in registration | Member+ |
| POST | `/api/registrations/{id}/extras` | Add/update extras for registration | Member+ |
| DELETE | `/api/registrations/{id}` | Cancel registration | Member+ |
| POST | `/api/registrations/{id}/calculate` | Recalculate registration totals (manual trigger) | Member+ |

---

## Pricing Calculation Service

### RegistrationPricingService

**Responsibility**: Calculate all amounts for a registration based on family composition, extras, and discounts.

```csharp
public class RegistrationPricingService(
    ICampEditionsRepository editionsRepo,
    IFamilyMembersRepository membersRepo,
    ICampEditionExtrasRepository extrasRepo,
    IAssociationSettingsRepository settingsRepo)
{
    public async Task<PricingBreakdown> CalculateAsync(
        Guid campEditionId,
        List<Guid> memberIds,
        List<ExtraSelection> extras,
        CancellationToken ct)
    {
        var edition = await editionsRepo.GetByIdAsync(campEditionId, ct);
        var ageRanges = await GetAgeRangesAsync(edition, ct);

        // Calculate member pricing
        var memberPricing = new List<MemberPricing>();
        foreach (var memberId in memberIds)
        {
            var member = await membersRepo.GetByIdAsync(memberId, ct);
            var ageAtCamp = CalculateAge(member.DateOfBirth, edition.StartDate);
            var (category, price) = DeterminePrice(ageAtCamp, ageRanges, edition);

            memberPricing.Add(new MemberPricing
            {
                MemberId = memberId,
                AgeAtCamp = ageAtCamp,
                AgeCategory = category,
                IndividualAmount = price
            });
        }

        var baseTotalAmount = memberPricing.Sum(m => m.IndividualAmount);

        // Calculate extras pricing
        var campDuration = (edition.EndDate - edition.StartDate).Days;
        var extraPricing = new List<ExtraPricing>();

        foreach (var extraSelection in extras)
        {
            var extra = await extrasRepo.GetByIdAsync(extraSelection.ExtraId, ct);
            var totalAmount = CalculateExtraAmount(extra, extraSelection.Quantity, campDuration);

            extraPricing.Add(new ExtraPricing
            {
                ExtraId = extra.Id,
                Quantity = extraSelection.Quantity,
                TotalAmount = totalAmount
            });
        }

        var extrasAmount = extraPricing.Sum(e => e.TotalAmount);

        // Apply discount (if any)
        var discountApplied = CalculateDiscount(baseTotalAmount, extrasAmount);

        return new PricingBreakdown
        {
            Members = memberPricing,
            BaseTotalAmount = baseTotalAmount,
            Extras = extraPricing,
            ExtrasAmount = extrasAmount,
            DiscountApplied = discountApplied,
            TotalAmount = baseTotalAmount + extrasAmount - discountApplied
        };
    }

    private (AgeCategory, decimal) DeterminePrice(
        int age,
        AgeRanges ranges,
        CampEdition edition)
    {
        if (age <= ranges.BabyMaxAge)
            return (AgeCategory.Baby, edition.PricePerBaby);
        if (age >= ranges.ChildMinAge && age <= ranges.ChildMaxAge)
            return (AgeCategory.Child, edition.PricePerChild);
        if (age >= ranges.AdultMinAge)
            return (AgeCategory.Adult, edition.PricePerAdult);

        throw new BusinessRuleException($"Age {age} does not fit any category");
    }

    private decimal CalculateExtraAmount(
        CampEditionExtra extra,
        int quantity,
        int campDuration)
    {
        var baseAmount = extra.PricingType == ExtraPricingType.PerPerson
            ? extra.Price * quantity
            : extra.Price;

        return extra.PricingPeriod == PricingPeriod.PerDay
            ? baseAmount * campDuration
            : baseAmount;
    }

    private async Task<AgeRanges> GetAgeRangesAsync(CampEdition edition, CancellationToken ct)
    {
        if (edition.UseCustomAgeRanges)
        {
            return new AgeRanges
            {
                BabyMaxAge = edition.BabyMaxAge!.Value,
                ChildMinAge = edition.ChildMinAge!.Value,
                ChildMaxAge = edition.ChildMaxAge!.Value,
                AdultMinAge = edition.AdultMinAge!.Value
            };
        }

        // Get global settings
        var settings = await settingsRepo.GetByKeyAsync("age_ranges", ct);
        return settings.Value.ToObject<AgeRanges>()!;
    }

    private int CalculateAge(DateOnly dateOfBirth, DateOnly campStartDate)
    {
        var age = campStartDate.Year - dateOfBirth.Year;
        if (campStartDate.Month < dateOfBirth.Month ||
            (campStartDate.Month == dateOfBirth.Month && campStartDate.Day < dateOfBirth.Day))
        {
            age--;
        }
        return age;
    }

    private decimal CalculateDiscount(decimal baseTotalAmount, decimal extrasAmount)
    {
        // Example: 5% member discount on base amount only
        var discountPercentage = 0.05m;
        return Math.Round(baseTotalAmount * discountPercentage, 2);
    }
}
```

---

## Implementation Steps (TDD)

### Step 1: Pricing Calculator Tests (TDD)

**Write Tests First**:

`tests/Abuvi.Tests/Unit/Features/Registrations/RegistrationPricingServiceTests.cs`:

```csharp
public class RegistrationPricingServiceTests
{
    [Fact]
    public async Task CalculateAsync_WithMixedAges_AppliesCorrectPricing()
    {
        // Arrange: Family with 2 adults, 1 child, 1 baby
        // Edition: Adult=€180, Child=€120, Baby=€60
        var memberIds = new List<Guid> { adult1Id, adult2Id, childId, babyId };

        // Act
        var result = await sut.CalculateAsync(editionId, memberIds, [], ct);

        // Assert
        result.Members.Should().HaveCount(4);
        result.Members[0].AgeCategory.Should().Be(AgeCategory.Adult);
        result.Members[0].IndividualAmount.Should().Be(180m);
        result.BaseTotalAmount.Should().Be(540m);
    }

    [Fact]
    public async Task CalculateAsync_WithPerDayExtra_MultipliesByDuration()
    {
        // Arrange: Vegan menu €5/person/day, 1 person, 10-day camp
        var extras = new List<ExtraSelection>
        {
            new(veganMenuId, quantity: 1)
        };

        // Act
        var result = await sut.CalculateAsync(editionId, memberIds, extras, ct);

        // Assert
        result.Extras[0].TotalAmount.Should().Be(50m); // 5 × 1 × 10
    }

    [Fact]
    public async Task CalculateAsync_WithPerFamilyExtra_IgnoresQuantity()
    {
        // Arrange: Workshop €50/family
        var extras = new List<ExtraSelection>
        {
            new(workshopId, quantity: 1) // Quantity ignored for PerFamily
        };

        // Act
        var result = await sut.CalculateAsync(editionId, memberIds, extras, ct);

        // Assert
        result.Extras[0].TotalAmount.Should().Be(50m);
    }

    [Fact]
    public async Task CalculateAsync_AppliesMemberDiscount()
    {
        // Arrange
        var baseTotalAmount = 540m;

        // Act
        var result = await sut.CalculateAsync(editionId, memberIds, [], ct);

        // Assert (5% discount on base)
        result.DiscountApplied.Should().Be(27m); // 540 × 0.05
        result.TotalAmount.Should().Be(513m); // 540 - 27
    }
}
```

**Then Implement**: RegistrationPricingService

---

### Step 2: Registration Service Tests (TDD)

**Write Tests First** for:

- Creating registration with members
- Validating camp capacity
- Preventing duplicate registrations
- Updating members/extras
- Cancelling registration

**Then Implement**: RegistrationsService

---

### Step 3: Integration Tests

**Test Scenarios**:

- Complete registration flow (create → add extras → pay → confirm)
- Capacity validation (reject registration when full)
- Pricing recalculation on member changes
- Auto-confirmation on full payment

---

## Example Scenario

### Family Registration Journey

**1. Garcia Family Registration**:

```json
// Step 1: Create registration
POST /api/registrations
{
  "campEditionId": "uuid-summer-2026",
  "familyUnitId": "uuid-garcia-family",
  "memberIds": ["adult1", "adult2", "child1", "baby1"]
}

// Response: Base total = €540 (2×€180 + 1×€120 + 1×€60)

// Step 2: Add extras
POST /api/registrations/{id}/extras
{
  "extras": [
    { "campEditionExtraId": "kayak-rental", "quantity": 2 },
    { "campEditionExtraId": "vegan-menu", "quantity": 1 }
  ]
}

// Response: Extras = €100 (kayak €50 + vegan menu €50)

// Step 3: View breakdown
GET /api/registrations/{id}

// Response: Total = €620 (€540 base + €100 extras - €20 discount)

// Step 4: Make deposit payment
POST /api/payments
{
  "registrationId": "uuid",
  "amount": 200.00,
  "method": "Card"
}

// Step 5: Make final payment
POST /api/payments
{
  "registrationId": "uuid",
  "amount": 420.00,
  "method": "Card"
}

// System auto-confirms registration (status → Confirmed)
```

---

## Security & Validation

- **Authorization**: Only family representative can create/modify their registration
- **Capacity Check**: Atomic check-and-increment to prevent race conditions
- **Age Validation**: Reject members outside camp's age requirements
- **Pricing Integrity**: Server-side calculation only (never trust client)
- **Payment Verification**: Verify payment status with Redsys before confirming

---

## Performance Considerations

- **Cache age ranges**: Global settings are read-only, cache for 24h
- **Batch member queries**: Load all family members in one query
- **Index registrations**: By status, edition, and family for fast lookups
- **Denormalize totals**: Store calculated amounts to avoid recalculation

---

## Next Steps

1. ✅ Review pricing calculation logic
2. ⬜ Implement RegistrationPricingService (TDD)
3. ⬜ Implement RegistrationsService (TDD)
4. ⬜ Create registration API endpoints
5. ⬜ Build frontend registration flow (Vue 3 + PrimeVue)
6. ⬜ Integrate with payment processing
7. ⬜ Add email notifications
8. ⬜ End-to-end testing

---

For frontend design and user experience, refer to Stich project screens for the camp registration flow:

## Stitch Instructions

Get the images and code for the following Stitch project's screens:

## Project

Title: ABUVI app
ID: 12733491114602106159

## Screens

1. Formulario de Inscripción al Campamento
    ID: 0fe0c4cdb59e4216a7f63fc85c586bf2

Use a utility like `curl -L` to download the hosted URLs

---

## Document Control

- **Feature**: `feat-camp-registration-flow`
- **Depends On**: `feat-camps-definition`
- **Version**: 1.0
- **Date**: 2026-02-13
- **Status**: Ready for Implementation
