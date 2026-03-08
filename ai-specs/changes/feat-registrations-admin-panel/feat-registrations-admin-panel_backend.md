# Backend Implementation Plan: feat-registrations-admin-panel

## Overview

Implement backend support for the Registrations Admin Panel, enabling Board and Admin users to list, filter, and edit registrations scoped to a camp edition. This includes:

1. A new paginated admin list endpoint: `GET /api/camp-editions/{campEditionId}/registrations`
2. A new admin edit endpoint: `PUT /api/registrations/{id}/admin-edit`
3. Adding `Draft` to the `RegistrationStatus` enum
4. EF Core migration for the new enum value
5. Updating the existing detail endpoint to expose `isAdminModified` flag

All changes follow the existing Vertical Slice Architecture within `src/Abuvi.API/Features/Registrations/`.

---

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/Registrations/`
- **Files to modify**:
  - `RegistrationsModels.cs` — Add `Draft` to enum, add admin DTOs, add `AdminModifiedAt` to entity
  - `RegistrationsRepository.cs` — Add `GetAdminPagedAsync` and `AdminUpdateAsync` methods + interface methods
  - `RegistrationsService.cs` — Add `GetAdminListAsync` and `AdminUpdateAsync` methods
  - `RegistrationsEndpoints.cs` — Add admin route group with two new endpoints
- **Files to create**:
  - `AdminEditRegistrationValidator.cs` — FluentValidation for admin edit request
  - New EF Core migration file (auto-generated)
- **Cross-cutting**: No changes to middleware, shared types, or `Program.cs` (endpoints are registered via the existing `MapRegistrationsEndpoints` extension)
- **Entity configuration**: `RegistrationConfiguration.cs` — Add `AdminModifiedAt` column

---

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-registrations-admin-panel-backend`
- **Implementation Steps**:
  1. Ensure on the latest `dev` branch: `git checkout dev && git pull origin dev`
  2. Create new branch: `git checkout -b feature/feat-registrations-admin-panel-backend`
  3. Verify: `git branch`
- **Notes**: This must be the FIRST step before any code changes.

---

### Step 1: Update `RegistrationStatus` Enum and Entity

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `Draft` value to the `RegistrationStatus` enum and `AdminModifiedAt` property to the `Registration` entity

- **Implementation Steps**:
  1. Add `Draft` to the enum:

     ```csharp
     public enum RegistrationStatus { Pending, Confirmed, Cancelled, Draft }
     ```

  2. Add `AdminModifiedAt` nullable DateTime to the `Registration` entity:

     ```csharp
     public DateTime? AdminModifiedAt { get; set; }
     ```

  3. Verify no other code breaks (e.g., switch statements on `RegistrationStatus` — check `CountActiveByEditionAsync` which filters on status)

- **Dependencies**: None
- **Implementation Notes**:
  - `Draft` behaves like `Pending` for capacity counting — it is NOT counted toward confirmed capacity. Verify that `CountActiveByEditionAsync` only counts `Confirmed` registrations (or `Pending` + `Confirmed`). If it counts all non-cancelled, `Draft` will automatically be included, which is fine per the spec.

---

### Step 2: Add Admin DTOs

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add request/response DTOs for the admin list and admin edit endpoints

- **Implementation Steps**:
  1. Add the admin list response DTOs:

     ```csharp
     // ── Admin List DTOs ──
     public record AdminRegistrationListResponse(
         List<AdminRegistrationListItem> Items,
         int TotalCount,
         int Page,
         int PageSize,
         int TotalPages,
         AdminRegistrationTotals Totals
     );

     public record AdminRegistrationListItem(
         Guid Id,
         RegistrationFamilyUnitSummary FamilyUnit,
         RepresentativeSummary Representative,
         RegistrationStatus Status,
         int MemberCount,
         decimal TotalAmount,
         decimal AmountPaid,
         decimal AmountRemaining,
         DateTime CreatedAt
     );

     public record RepresentativeSummary(
         Guid Id,
         string FirstName,
         string LastName,
         string Email
     );

     public record AdminRegistrationTotals(
         int TotalRegistrations,
         int TotalMembers,
         decimal TotalAmount,
         decimal TotalPaid,
         decimal TotalRemaining
     );
     ```

  2. Add admin list projection (internal, used by repository):

     ```csharp
     public record AdminRegistrationProjection(
         Guid Id,
         Guid FamilyUnitId,
         string FamilyUnitName,
         Guid RepresentativeUserId,
         string RepresentativeFirstName,
         string RepresentativeLastName,
         string RepresentativeEmail,
         RegistrationStatus Status,
         int MemberCount,
         decimal TotalAmount,
         decimal AmountPaid,
         DateTime CreatedAt
     );
     ```

  3. Add admin edit request DTO:

     ```csharp
     public record AdminEditRegistrationRequest(
         List<MemberAttendanceRequest>? Members,
         List<ExtraSelectionRequest>? Extras,
         List<AccommodationPreferenceRequest>? Preferences,
         string? Notes,
         string? SpecialNeeds,
         string? CampatesPreference
     );
     ```

- **Dependencies**: Reuses existing `MemberAttendanceRequest`, `ExtraSelectionRequest`, `AccommodationPreferenceRequest`, `RegistrationFamilyUnitSummary`
- **Implementation Notes**:
  - Use `RegistrationStatus` enum (not string) for the `Status` field in `AdminRegistrationListItem` — the JSON serializer will output the string name
  - The admin edit request has all fields optional — only non-null fields are updated (partial update)
  - Follow the existing `PagedFamilyUnitsResponse` pattern: include `Page`, `PageSize`, `TotalPages` alongside `TotalCount`

---

### Step 3: Create Admin Edit Validator

- **File**: `src/Abuvi.API/Features/Registrations/AdminEditRegistrationValidator.cs` (new file)
- **Action**: Create FluentValidation validator for `AdminEditRegistrationRequest`

- **Implementation Steps**:
  1. Create `AdminEditRegistrationValidator : AbstractValidator<AdminEditRegistrationRequest>`:

     ```csharp
     public class AdminEditRegistrationValidator : AbstractValidator<AdminEditRegistrationRequest>
     {
         public AdminEditRegistrationValidator()
         {
             When(x => x.Members != null, () =>
             {
                 RuleFor(x => x.Members!).NotEmpty()
                     .WithMessage("La lista de miembros no puede estar vacía si se proporciona");
                 RuleForEach(x => x.Members!).ChildRules(member =>
                 {
                     member.RuleFor(m => m.MemberId).NotEmpty();
                     member.RuleFor(m => m.AttendancePeriod).IsInEnum();
                     // Same rules as UpdateRegistrationMembersValidator for weekend visit dates
                 });
             });

             When(x => x.Extras != null, () =>
             {
                 RuleForEach(x => x.Extras!).ChildRules(extra =>
                 {
                     extra.RuleFor(e => e.CampEditionExtraId).NotEmpty();
                     extra.RuleFor(e => e.Quantity).GreaterThan(0);
                 });
             });

             When(x => x.Preferences != null, () =>
             {
                 RuleForEach(x => x.Preferences!).ChildRules(pref =>
                 {
                     pref.RuleFor(p => p.CampEditionAccommodationId).NotEmpty();
                     pref.RuleFor(p => p.PreferenceOrder).GreaterThan(0);
                 });
             });

             RuleFor(x => x.Notes).MaximumLength(1000);
             RuleFor(x => x.SpecialNeeds).MaximumLength(2000);
             RuleFor(x => x.CampatesPreference).MaximumLength(500);
         }
     }
     ```

- **Dependencies**: `FluentValidation`
- **Implementation Notes**: Reuse the same member validation rules from `UpdateRegistrationMembersValidator` (visit date validation, guardian fields). Consider extracting shared member rules if duplication is significant.

---

### Step 4: Implement Repository Methods

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs`
- **Action**: Add `GetAdminPagedAsync` method to interface and implementation

- **Implementation Steps**:
  1. Add to `IRegistrationsRepository`:

     ```csharp
     Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
         GetAdminPagedAsync(Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct);
     ```

  2. Implement `GetAdminPagedAsync` in `RegistrationsRepository`:

     ```csharp
     public async Task<(List<AdminRegistrationProjection> Items, int TotalCount, AdminRegistrationTotals Totals)>
         GetAdminPagedAsync(Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct)
     {
         var query = from r in db.Registrations.AsNoTracking()
                     join fu in db.FamilyUnits on r.FamilyUnitId equals fu.Id
                     join u in db.Users on r.RegisteredByUserId equals u.Id
                     where r.CampEditionId == campEditionId
                     select new
                     {
                         r.Id,
                         FamilyUnitId = fu.Id,
                         FamilyUnitName = fu.Name,
                         RepresentativeUserId = u.Id,
                         RepresentativeFirstName = u.FirstName,
                         RepresentativeLastName = u.LastName,
                         RepresentativeEmail = u.Email,
                         r.Status,
                         MemberCount = db.RegistrationMembers.Count(m => m.RegistrationId == r.Id),
                         r.TotalAmount,
                         AmountPaid = db.Payments
                             .Where(p => p.RegistrationId == r.Id && p.Status == PaymentStatus.Completed)
                             .Sum(p => (decimal?)p.Amount) ?? 0m,
                         r.CreatedAt
                     };

         // Status filter
         if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RegistrationStatus>(status, true, out var statusEnum))
         {
             query = query.Where(x => x.Status == statusEnum);
         }

         // Search filter (ILIKE on family name or representative name)
         if (!string.IsNullOrWhiteSpace(search))
         {
             var term = search.Trim().ToLower();
             query = query.Where(x =>
                 x.FamilyUnitName.ToLower().Contains(term) ||
                 (x.RepresentativeFirstName + " " + x.RepresentativeLastName).ToLower().Contains(term));
         }

         // Totals (computed AFTER filters, BEFORE pagination)
         var totalsQuery = query;
         var totalCount = await totalsQuery.CountAsync(ct);

         var aggregateTotals = totalCount == 0
             ? new AdminRegistrationTotals(0, 0, 0, 0, 0)
             : await totalsQuery.GroupBy(_ => 1).Select(g => new AdminRegistrationTotals(
                 TotalRegistrations: g.Count(),
                 TotalMembers: g.Sum(x => x.MemberCount),
                 TotalAmount: g.Sum(x => x.TotalAmount),
                 TotalPaid: g.Sum(x => x.AmountPaid),
                 TotalRemaining: g.Sum(x => x.TotalAmount - x.AmountPaid)
             )).FirstAsync(ct);

         // Pagination
         var items = await query
             .OrderByDescending(x => x.CreatedAt)
             .Skip((page - 1) * pageSize)
             .Take(pageSize)
             .ToListAsync(ct);

         var projections = items.Select(x => new AdminRegistrationProjection(
             x.Id, x.FamilyUnitId, x.FamilyUnitName,
             x.RepresentativeUserId, x.RepresentativeFirstName,
             x.RepresentativeLastName, x.RepresentativeEmail,
             x.Status, x.MemberCount, x.TotalAmount, x.AmountPaid, x.CreatedAt
         )).ToList();

         return (projections, totalCount, aggregateTotals);
     }
     ```

- **Dependencies**: None (uses existing `DbSet`s)
- **Implementation Notes**:
  - `AmountPaid` is calculated via a correlated subquery on `Payments` where `Status == Completed`
  - The `GroupBy(_ => 1)` trick computes aggregates in a single SQL query
  - If the `GroupBy` approach generates problematic SQL for PostgreSQL/EF Core, fallback to separate aggregate queries
  - Totals are computed AFTER filters but BEFORE pagination — this is intentional (totals reflect the filtered dataset)
  - Existing index on `registrations(camp_edition_id)` covers the main filter; existing index on `registrations(status)` helps the status filter

---

### Step 5: Implement Service Methods

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Add `GetAdminListAsync` and `AdminUpdateAsync` methods

- **Implementation Steps**:

  1. **`GetAdminListAsync`**:

     ```csharp
     public async Task<AdminRegistrationListResponse> GetAdminListAsync(
         Guid campEditionId, int page, int pageSize, string? search, string? status, CancellationToken ct)
     {
         // Validate camp edition exists
         var edition = await campEditionsRepo.GetByIdAsync(campEditionId, ct)
             ?? throw new NotFoundException($"Edición de campamento {campEditionId} no encontrada");

         page = Math.Max(1, page);
         pageSize = Math.Clamp(pageSize, 1, 100);

         var (items, totalCount, totals) = await registrationsRepo.GetAdminPagedAsync(
             campEditionId, page, pageSize, search, status, ct);

         var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

         return new AdminRegistrationListResponse(
             Items: items.Select(p => new AdminRegistrationListItem(
                 p.Id,
                 new RegistrationFamilyUnitSummary(p.FamilyUnitId, p.FamilyUnitName),
                 new RepresentativeSummary(p.RepresentativeUserId, p.RepresentativeFirstName, p.RepresentativeLastName, p.RepresentativeEmail),
                 p.Status,
                 p.MemberCount,
                 p.TotalAmount,
                 p.AmountPaid,
                 p.TotalAmount - p.AmountPaid,
                 p.CreatedAt
             )).ToList(),
             TotalCount: totalCount,
             Page: page,
             PageSize: pageSize,
             TotalPages: totalPages,
             Totals: totals
         );
     }
     ```

  2. **`AdminUpdateAsync`**:

     ```csharp
     public async Task<RegistrationResponse> AdminUpdateAsync(
         Guid registrationId, AdminEditRegistrationRequest request, CancellationToken ct)
     {
         var registration = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct)
             ?? throw new NotFoundException($"Inscripción {registrationId} no encontrada");

         // Only allow editing Pending, Confirmed, or Draft registrations
         if (registration.Status == RegistrationStatus.Cancelled)
             throw new BusinessRuleException("No se puede editar una inscripción cancelada");

         var edition = await campEditionsRepo.GetByIdAsync(registration.CampEditionId, ct)
             ?? throw new NotFoundException("Edición de campamento no encontrada");

         // Update members if provided
         if (request.Members != null)
         {
             await registrationsRepo.DeleteMembersByRegistrationIdAsync(registrationId, ct);
             // Recalculate pricing for new members (reuse logic from CreateAsync/UpdateMembersAsync)
             var newMembers = new List<RegistrationMember>();
             decimal baseTotalAmount = 0;
             foreach (var memberReq in request.Members)
             {
                 var familyMember = await familyUnitsRepo.GetMemberByIdAsync(memberReq.MemberId, ct)
                     ?? throw new NotFoundException($"Miembro {memberReq.MemberId} no encontrado");

                 var age = pricingService.CalculateAge(familyMember.DateOfBirth, edition.StartDate);
                 var ageCategory = await pricingService.GetAgeCategoryAsync(age, edition, ct);
                 var price = pricingService.GetPriceForCategory(ageCategory, memberReq.AttendancePeriod, edition);

                 newMembers.Add(new RegistrationMember
                 {
                     Id = Guid.NewGuid(),
                     RegistrationId = registrationId,
                     FamilyMemberId = memberReq.MemberId,
                     AgeAtCamp = age,
                     AgeCategory = ageCategory,
                     IndividualAmount = price,
                     AttendancePeriod = memberReq.AttendancePeriod,
                     VisitStartDate = memberReq.VisitStartDate,
                     VisitEndDate = memberReq.VisitEndDate,
                     GuardianName = memberReq.GuardianName,
                     GuardianDocumentNumber = memberReq.GuardianDocumentNumber,
                     CreatedAt = DateTime.UtcNow
                 });
                 baseTotalAmount += price;
             }
             registration.Members = newMembers;
             registration.BaseTotalAmount = baseTotalAmount;
             registration.TotalAmount = baseTotalAmount + registration.ExtrasAmount;
         }

         // Update extras if provided
         if (request.Extras != null)
         {
             await extrasRepo.DeleteByRegistrationIdAsync(registrationId, ct);
             decimal extrasAmount = 0;
             var newExtras = new List<RegistrationExtra>();
             foreach (var extraReq in request.Extras)
             {
                 var campExtra = await campEditionsRepo.GetExtraByIdAsync(extraReq.CampEditionExtraId, ct)
                     ?? throw new NotFoundException($"Extra {extraReq.CampEditionExtraId} no encontrado");
                 var campDays = RegistrationPricingService.GetPeriodDays(AttendancePeriod.Complete, edition);
                 var amount = pricingService.CalculateExtraAmount(campExtra, extraReq.Quantity, campDays);
                 newExtras.Add(new RegistrationExtra
                 {
                     Id = Guid.NewGuid(),
                     RegistrationId = registrationId,
                     CampEditionExtraId = extraReq.CampEditionExtraId,
                     Quantity = extraReq.Quantity,
                     UnitPrice = campExtra.UnitPrice,
                     CampDurationDays = campDays,
                     TotalAmount = amount,
                     CreatedAt = DateTime.UtcNow
                 });
                 extrasAmount += amount;
             }
             await extrasRepo.AddRangeAsync(newExtras, ct);
             registration.ExtrasAmount = extrasAmount;
             registration.TotalAmount = registration.BaseTotalAmount + extrasAmount;
         }

         // Update accommodation preferences if provided
         if (request.Preferences != null)
         {
             await accommodationPrefsRepo.DeleteByRegistrationIdAsync(registrationId, ct);
             var newPrefs = request.Preferences.Select(p => new RegistrationAccommodationPreference
             {
                 Id = Guid.NewGuid(),
                 RegistrationId = registrationId,
                 CampEditionAccommodationId = p.CampEditionAccommodationId,
                 PreferenceOrder = p.PreferenceOrder,
                 CreatedAt = DateTime.UtcNow
             });
             await accommodationPrefsRepo.AddRangeAsync(newPrefs, ct);
         }

         // Update text fields if provided
         if (request.Notes != null) registration.Notes = request.Notes;
         if (request.SpecialNeeds != null) registration.SpecialNeeds = request.SpecialNeeds;
         if (request.CampatesPreference != null) registration.CampatesPreference = request.CampatesPreference;

         // Set status to Draft and record admin modification
         registration.Status = RegistrationStatus.Draft;
         registration.AdminModifiedAt = DateTime.UtcNow;

         await registrationsRepo.UpdateAsync(registration, ct);

         // Reload with full details for response
         var updated = await registrationsRepo.GetByIdWithDetailsAsync(registrationId, ct);
         var amountPaid = updated!.Payments
             .Where(p => p.Status == PaymentStatus.Completed)
             .Sum(p => p.Amount);

         logger.LogInformation("Registration {RegistrationId} edited by admin, status set to Draft", registrationId);

         return updated.ToResponse(amountPaid);
     }
     ```

- **Dependencies**: Uses existing injected repositories (`registrationsRepo`, `extrasRepo`, `accommodationPrefsRepo`, `familyUnitsRepo`, `campEditionsRepo`, `pricingService`)
- **Implementation Notes**:
  - The `AdminUpdateAsync` method reuses the same pricing calculation logic from `CreateAsync` and `UpdateMembersAsync`. Consider extracting shared member-building logic into a private method to avoid duplication.
  - Only non-null fields in the request trigger updates (partial update pattern)
  - Status is ALWAYS set to `Draft` on any admin edit, regardless of what fields were changed
  - Check if `campEditionsRepo.GetExtraByIdAsync` exists — if not, it needs to be added or the method name adjusted to match the existing repository API

---

### Step 6: Add Admin Endpoints

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsEndpoints.cs`
- **Action**: Add an admin-only route group with two new endpoints

- **Implementation Steps**:
  1. Add a new route group for admin endpoints inside `MapRegistrationsEndpoints`:

     ```csharp
     // Admin endpoints — Board and Admin only
     var adminGroup = app.MapGroup("/api/camp-editions/{campEditionId:guid}/registrations")
         .WithTags("Registrations Admin")
         .WithOpenApi()
         .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

     adminGroup.MapGet("/", GetAdminRegistrations)
         .WithName("GetAdminRegistrations")
         .WithSummary("Get paginated registrations for a camp edition (Admin/Board only)")
         .Produces<ApiResponse<AdminRegistrationListResponse>>()
         .Produces(401).Produces(403).Produces(404);

     // Admin edit endpoint on the main registrations group (with role check in handler)
     var adminEditGroup = app.MapGroup("/api/registrations")
         .WithTags("Registrations Admin")
         .WithOpenApi()
         .RequireAuthorization(policy => policy.RequireRole("Admin", "Board"));

     adminEditGroup.MapPut("/{id:guid}/admin-edit", AdminEditRegistration)
         .WithName("AdminEditRegistration")
         .WithSummary("Edit registration as Admin/Board (sets status to Draft)")
         .AddEndpointFilter<ValidationFilter<AdminEditRegistrationRequest>>()
         .Produces<ApiResponse<RegistrationResponse>>()
         .Produces(400).Produces(401).Produces(403).Produces(404).Produces(422);
     ```

  2. Add handler for `GetAdminRegistrations`:

     ```csharp
     private static async Task<IResult> GetAdminRegistrations(
         Guid campEditionId,
         RegistrationsService service,
         [FromQuery] int page = 1,
         [FromQuery] int pageSize = 20,
         [FromQuery] string? search = null,
         [FromQuery] string? status = null,
         CancellationToken ct = default)
     {
         try
         {
             var result = await service.GetAdminListAsync(campEditionId, page, pageSize, search, status, ct);
             return TypedResults.Ok(ApiResponse<AdminRegistrationListResponse>.Ok(result));
         }
         catch (NotFoundException ex)
         {
             return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
         }
     }
     ```

  3. Add handler for `AdminEditRegistration`:

     ```csharp
     private static async Task<IResult> AdminEditRegistration(
         Guid id,
         AdminEditRegistrationRequest request,
         RegistrationsService service,
         CancellationToken ct)
     {
         try
         {
             var result = await service.AdminUpdateAsync(id, request, ct);
             return TypedResults.Ok(ApiResponse<RegistrationResponse>.Ok(result));
         }
         catch (NotFoundException ex)
         {
             return TypedResults.NotFound(ApiResponse<object>.NotFound(ex.Message));
         }
         catch (BusinessRuleException ex)
         {
             return TypedResults.UnprocessableEntity(
                 ApiResponse<object>.Fail(ex.Message, "BUSINESS_RULE_VIOLATION"));
         }
     }
     ```

- **Dependencies**: `Microsoft.AspNetCore.Mvc.FromQuery`, existing `ValidationFilter<T>`, `ApiResponse<T>`
- **Implementation Notes**:
  - The admin list endpoint uses a SEPARATE route group (`/api/camp-editions/{campEditionId}/registrations`) since the camp edition is a required path parameter — this is a different URL namespace from the existing `/api/registrations` group
  - The admin edit endpoint uses a new group on `/api/registrations` with `RequireRole("Admin", "Board")` policy

---

### Step 7: Update Entity Configuration

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationConfiguration.cs` (or the relevant EF config file)
- **Action**: Add `AdminModifiedAt` column configuration

- **Implementation Steps**:
  1. Find the existing `RegistrationConfiguration` class (implements `IEntityTypeConfiguration<Registration>`)
  2. Add column mapping:

     ```csharp
     builder.Property(r => r.AdminModifiedAt)
         .HasColumnName("admin_modified_at")
         .IsRequired(false);
     ```

- **Dependencies**: None
- **Implementation Notes**: The column is nullable — only populated when an admin edits the registration

---

### Step 8: Update Existing Detail Response

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsModels.cs`
- **Action**: Add `IsAdminModified` flag to `RegistrationResponse` so the frontend can show the re-confirmation banner

- **Implementation Steps**:
  1. Add `bool IsAdminModified` to `RegistrationResponse`:

     ```csharp
     public record RegistrationResponse(
         // ... existing fields ...
         bool IsAdminModified  // NEW: true when AdminModifiedAt is not null and status is Draft
     );
     ```

  2. Update the `ToResponse` extension method to populate it:

     ```csharp
     IsAdminModified: r.AdminModifiedAt != null && r.Status == RegistrationStatus.Draft
     ```

- **Dependencies**: None
- **Implementation Notes**: `IsAdminModified` is `true` only when the registration is in Draft status AND was modified by an admin. Once the representative re-confirms (Draft → Confirmed), the flag becomes `false`.

---

### Step 9: Update Confirm Logic for Draft Status

- **File**: `src/Abuvi.API/Features/Registrations/RegistrationsService.cs`
- **Action**: Ensure existing confirm/re-confirm logic handles Draft → Confirmed transition

- **Implementation Steps**:
  1. Check if there's an existing confirm endpoint or if confirmation is handled differently (e.g., payment-based auto-confirmation)
  2. If confirmation is payment-based: verify that when `amountPaid >= totalAmount`, the status transition handles `Draft` → `Confirmed` the same way as `Pending` → `Confirmed`
  3. If there's a manual confirm endpoint: ensure it accepts `Draft` as a valid source status
  4. Verify `canCancel` logic: Draft registrations should be cancellable (same as Pending)
  5. Verify `CountActiveByEditionAsync`: Draft should NOT count toward confirmed capacity — check the filter condition

- **Dependencies**: None
- **Implementation Notes**: This step may require no code changes if the existing logic already handles non-Cancelled statuses generically. Review carefully.

---

### Step 10: Create EF Core Migration

- **Action**: Generate EF Core migration for the `Draft` enum value and `AdminModifiedAt` column

- **Implementation Steps**:
  1. Run migration generation:

     ```bash
     cd src/Abuvi.API
     dotnet ef migrations add AddDraftStatusAndAdminModifiedAt
     ```

  2. Review the generated migration:
     - Should add `admin_modified_at` column to `registrations` table
     - Should add `Draft` to the PostgreSQL enum type for `RegistrationStatus`
     - **Important**: If `RegistrationStatus` is stored as a string (check `HasColumnType` and max length in config), no enum migration is needed — just the new column
  3. Verify the migration applies cleanly:

     ```bash
     dotnet ef database update
     ```

- **Dependencies**: EF Core CLI tools
- **Implementation Notes**:
  - Based on the entity config, `status` is stored as a string with `MaxLength(20)` — so `"Draft"` (5 chars) fits and NO PostgreSQL enum migration is needed
  - Only the `admin_modified_at` column needs to be added

---

### Step 11: Write Unit Tests

- **File**: `tests/Abuvi.API.Tests/Features/Registrations/` (new test files)
- **Action**: Write xUnit tests with FluentAssertions and NSubstitute

#### Test Categories

**A. `GetAdminListAsync` Service Tests:**

1. ✅ Returns paginated list for valid camp edition
2. ✅ Throws `NotFoundException` for non-existent camp edition
3. ✅ Clamps page/pageSize to valid ranges
4. ✅ Filters by status correctly
5. ✅ Filters by search term (family name, representative name)
6. ✅ Returns correct totals (filtered, not global)
7. ✅ Returns empty list for edition with no registrations

**B. `AdminUpdateAsync` Service Tests:**

1. ✅ Updates members and recalculates pricing
2. ✅ Updates extras and recalculates totals
3. ✅ Updates text fields (notes, specialNeeds, campatesPreference)
4. ✅ Sets status to Draft on any edit
5. ✅ Sets AdminModifiedAt timestamp
6. ✅ Throws `NotFoundException` for non-existent registration
7. ✅ Throws `BusinessRuleException` for cancelled registration
8. ✅ Partial update: only updates provided fields
9. ✅ Works for Pending, Confirmed, and Draft source statuses

**C. Repository Tests (Integration):**

1. ✅ `GetAdminPagedAsync` returns correct data with joins
2. ✅ Pagination works correctly (skip/take)
3. ✅ Search filter matches family name and representative name (case-insensitive)
4. ✅ Status filter returns only matching registrations
5. ✅ Totals reflect filtered data

**D. Validator Tests:**

1. ✅ Valid request passes validation
2. ✅ Members with missing MemberId fails
3. ✅ Notes exceeding 1000 chars fails
4. ✅ Extras with quantity <= 0 fails

- **Dependencies**: `xUnit`, `FluentAssertions`, `NSubstitute`

---

### Step 12: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made

- **Implementation Steps**:
  1. **Data model changes** → Update `ai-specs/specs/data-model.md`:
     - Add `Draft` to `RegistrationStatus` enum documentation
     - Add `AdminModifiedAt` column to `registrations` table schema
  2. **API endpoint changes** → Update `ai-specs/specs/api-spec.yml` (if it exists) or `api-endpoints.md`:
     - Document `GET /api/camp-editions/{campEditionId}/registrations` with query params, response shape
     - Document `PUT /api/registrations/{id}/admin-edit` with request/response shapes
     - Update `GET /api/registrations/{id}` response to include `isAdminModified`
  3. **Verify documentation** follows established structure and is in English

- **References**: Follow `ai-specs/specs/documentation-standards.mdc`
- **Notes**: This step is MANDATORY before considering the implementation complete.

---

## Implementation Order

1. **Step 0**: Create feature branch
2. **Step 1**: Update `RegistrationStatus` enum + add `AdminModifiedAt` to entity
3. **Step 2**: Add admin DTOs (projections, request, response records)
4. **Step 3**: Create admin edit validator
5. **Step 7**: Update entity configuration (add `AdminModifiedAt` column)
6. **Step 4**: Implement repository methods (`GetAdminPagedAsync`)
7. **Step 5**: Implement service methods (`GetAdminListAsync`, `AdminUpdateAsync`)
8. **Step 8**: Update `RegistrationResponse` with `IsAdminModified` flag
9. **Step 9**: Review/update confirm logic for Draft status
10. **Step 6**: Add admin endpoints
11. **Step 10**: Create EF Core migration
12. **Step 11**: Write unit tests
13. **Step 12**: Update technical documentation

---

## Testing Checklist

- [ ] All unit tests pass with `dotnet test`
- [ ] `GET /api/camp-editions/{id}/registrations` returns 200 with paginated data
- [ ] `GET /api/camp-editions/{id}/registrations?status=Confirmed` filters correctly
- [ ] `GET /api/camp-editions/{id}/registrations?search=García` searches correctly
- [ ] `GET /api/camp-editions/{id}/registrations` returns 404 for non-existent edition
- [ ] `GET /api/camp-editions/{id}/registrations` returns 401/403 for non-admin users
- [ ] `PUT /api/registrations/{id}/admin-edit` sets status to Draft
- [ ] `PUT /api/registrations/{id}/admin-edit` returns 404 for non-existent registration
- [ ] `PUT /api/registrations/{id}/admin-edit` returns 422 for cancelled registration
- [ ] `PUT /api/registrations/{id}/admin-edit` returns 400 for invalid request
- [ ] `GET /api/registrations/{id}` includes `isAdminModified: true` for Draft registrations
- [ ] EF Core migration applies cleanly
- [ ] Test coverage ≥ 90%

---

## Error Response Format

| Scenario | HTTP Status | Error Code |
|---|---|---|
| Camp edition not found | 404 | (NotFound message) |
| Registration not found | 404 | (NotFound message) |
| Edit cancelled registration | 422 | `BUSINESS_RULE_VIOLATION` |
| Invalid request body | 400 | Validation errors |
| Unauthorized (no JWT) | 401 | — |
| Forbidden (wrong role) | 403 | — |

All errors use the `ApiResponse<T>` envelope:

```json
{
  "success": false,
  "data": null,
  "error": { "message": "...", "code": "..." }
}
```

---

## Partial Update Support

The `AdminEditRegistrationRequest` supports partial updates:

- All fields are nullable/optional
- Only non-null fields trigger updates
- `Members`, `Extras`, `Preferences` — when provided, replace the entire collection (delete + re-create)
- `Notes`, `SpecialNeeds`, `CampatesPreference` — when provided, update the field value
- Status is ALWAYS set to `Draft` regardless of which fields are updated

---

## Dependencies

- **NuGet packages**: No new packages required. Uses existing `FluentValidation`, `Microsoft.EntityFrameworkCore`
- **EF Core migration**: `dotnet ef migrations add AddDraftStatusAndAdminModifiedAt`

---

## Notes

- **Business rule**: Admin edits ALWAYS set status to `Draft`, even if the registration was already in Draft. This updates `AdminModifiedAt` to the latest edit timestamp.
- **Capacity counting**: `Draft` registrations are NOT counted toward confirmed capacity (same as `Pending`). Verify `CountActiveByEditionAsync` only counts `Confirmed` or adjust if needed.
- **Security**: The admin list endpoint does NOT expose sensitive health fields (`medicalNotes`, `allergies`). The projection only includes: family name, representative info, status, member count, financial amounts, and creation date.
- **RGPD/GDPR**: Representative email is exposed in the admin list — this is acceptable for Admin/Board roles who need it for camp management.
- **Language**: All user-facing error messages are in Spanish (es-ES), following existing convention.
- **No service interface**: Following the project convention, the concrete `RegistrationsService` is injected directly — no `IRegistrationsService` interface.

---

## Next Steps After Implementation

1. Frontend integration — the frontend components are already implemented (see `feat-registrations-admin-panel_enriched.md` frontend section)
2. End-to-end testing with the frontend
3. Consider adding email notification to the family representative when their registration is set to Draft by an admin
