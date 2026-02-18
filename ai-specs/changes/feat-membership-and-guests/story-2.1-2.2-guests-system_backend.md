# Phase 2: Guests System - Implementation Plan

**Epic:** 2.1 (Guest Entities and Database) + 2.2 (Guest Management API)
**Status:** Ready to implement
**Approach:** Test-Driven Development (TDD)
**Base Branch:** feature/story-1.4-automated-fee-generation

---

## Overview

This plan implements the complete Guests System, allowing families to register external guests who can attend camps.

**Stories covered:**

- Story 2.1.1: Create Guest Entity and Configuration
- Story 2.1.2: Create Guests Repository
- Story 2.2.1: Create Guests Service
- Story 2.2.2: Create Guest Validators
- Story 2.2.3: Create Guest API Endpoints

---

## Implementation Steps

### Step 1: Create Feature Branch

```bash
git checkout feature/story-1.4-automated-fee-generation
git checkout -b feature/story-2.1-guests-entities-database
```

### Step 2: Create Guest Entity and DTOs

**File:** `src/Abuvi.API/Features/Guests/GuestsModels.cs`

- `Guest` entity with personal data, encrypted health fields, and FamilyUnit relationship
- `CreateGuestRequest` / `UpdateGuestRequest` DTOs
- `GuestResponse` DTO (uses `HasMedicalNotes` / `HasAllergies` flags instead of exposing encrypted data)
- `GuestExtensions.ToResponse()` mapping method

### Step 3: Create EF Core Configuration

**File:** `src/Abuvi.API/Data/Configurations/GuestConfiguration.cs`

- Primary key and UUID default
- Field constraints (max lengths, required fields)
- Cascade delete relationship to FamilyUnit
- Indexes on FamilyUnitId and DocumentNumber

### Step 4: Update AbuviDbContext

**File:** `src/Abuvi.API/Data/AbuviDbContext.cs`

- Add `using Abuvi.API.Features.Guests;`
- Add `public DbSet<Guest> Guests => Set<Guest>();`

### Step 5: Add Encryption Key to appsettings.json

**File:** `src/Abuvi.API/appsettings.json`

- Add `"Encryption": { "Key": "abuvi-dev-encryption-key-change-in-production" }` for development/testing

### Step 6: Create and Apply Migration

```bash
dotnet ef migrations add AddGuestEntity --project src/Abuvi.API
dotnet ef database update --project src/Abuvi.API
```

### Step 7: Create Guests Repository

**File:** `src/Abuvi.API/Features/Guests/GuestsRepository.cs`

- `IGuestsRepository` interface: GetByIdAsync, GetByFamilyUnitAsync, AddAsync, UpdateAsync, DeleteAsync
- `GuestsRepository` implementation with encryption/decryption of MedicalNotes and Allergies
- `GetByFamilyUnitAsync` returns only active guests, ordered by LastName then FirstName

### Step 8: Create Guests Service

**File:** `src/Abuvi.API/Features/Guests/GuestsService.cs`

- `CreateAsync`: validates FamilyUnit exists, normalizes DocumentNumber to uppercase, creates guest
- `UpdateAsync`: finds guest, updates fields, normalizes DocumentNumber
- `GetByIdAsync`: returns guest or throws NotFoundException
- `GetByFamilyUnitAsync`: returns list of active guests
- `DeleteAsync`: soft delete (IsActive = false + UpdateAsync)

### Step 9: Create Guest Validators

**Files:**

- `src/Abuvi.API/Features/Guests/CreateGuestValidator.cs`
- `src/Abuvi.API/Features/Guests/UpdateGuestValidator.cs`

Both validators enforce Spanish error messages for:

- FirstName/LastName (required, max 100)
- DateOfBirth (required, must be past date)
- DocumentNumber (optional, uppercase alphanumeric, max 50)
- Email (optional, valid format, max 255)
- Phone (optional, E.164 format)
- MedicalNotes (optional, max 2000)
- Allergies (optional, max 1000)

### Step 10: Create Guest API Endpoints

**File:** `src/Abuvi.API/Features/Guests/GuestsEndpoints.cs`

- POST `/api/family-units/{familyUnitId}/guests` - Create guest (201)
- GET `/api/family-units/{familyUnitId}/guests` - List guests (200)
- GET `/api/family-units/{familyUnitId}/guests/{guestId}` - Get guest (200/404)
- PUT `/api/family-units/{familyUnitId}/guests/{guestId}` - Update guest (200/400/404)
- DELETE `/api/family-units/{familyUnitId}/guests/{guestId}` - Delete guest (204/404)

### Step 11: Register in Program.cs

**File:** `src/Abuvi.API/Program.cs`

- Register: `IGuestsRepository`, `GuestsService`
- Map: `app.MapGuestsEndpoints()`

### Step 12: Create Unit Tests

- `src/Abuvi.Tests/Unit/Features/Guests/GuestEntityTests.cs` - Entity property tests
- `src/Abuvi.Tests/Unit/Features/Guests/GuestsRepositoryTests.cs` - Repository with in-memory DB + mocked IEncryptionService
- `src/Abuvi.Tests/Unit/Features/Guests/GuestsServiceTests.cs` - Service with NSubstitute mocks
- `src/Abuvi.Tests/Unit/Features/Guests/CreateGuestValidatorTests.cs` - Validator tests
- `src/Abuvi.Tests/Unit/Features/Guests/UpdateGuestValidatorTests.cs` - Validator tests

### Step 13: Create Integration Tests

- `src/Abuvi.Tests/Integration/Features/Guests/GuestDatabaseTests.cs` - DB schema and constraints
- `src/Abuvi.Tests/Integration/Features/Guests/GuestsEndpointsTests.cs` - Full HTTP pipeline tests

### Step 14: Run All Tests

```bash
dotnet test src/Abuvi.Tests
```

Expected: All ~90+ tests passing

---

## Acceptance Criteria

**Story 2.1.1:**

- [x] Guest entity with all required fields
- [x] Encrypted MedicalNotes and Allergies fields
- [x] EF Core configuration with cascade delete
- [x] DbSet added to AbuviDbContext
- [x] Migration created and applied

**Story 2.1.2:**

- [x] IGuestsRepository interface defined
- [x] GuestsRepository with encryption/decryption
- [x] Registered in DI
- [x] Unit and integration tests

**Story 2.2.1:**

- [x] GuestsService with Create, Update, Get, List, Delete
- [x] FamilyUnit validation
- [x] DocumentNumber uppercase normalization
- [x] Soft delete

**Story 2.2.2:**

- [x] CreateGuestValidator with Spanish messages
- [x] UpdateGuestValidator with Spanish messages
- [x] All field validations

**Story 2.2.3:**

- [x] 5 REST endpoints
- [x] Validation filters applied
- [x] OpenAPI documentation
- [x] Integration tests

---

## Files Created/Modified

**Created:**

- `src/Abuvi.API/Features/Guests/GuestsModels.cs`
- `src/Abuvi.API/Data/Configurations/GuestConfiguration.cs`
- `src/Abuvi.API/Features/Guests/GuestsRepository.cs`
- `src/Abuvi.API/Features/Guests/GuestsService.cs`
- `src/Abuvi.API/Features/Guests/CreateGuestValidator.cs`
- `src/Abuvi.API/Features/Guests/UpdateGuestValidator.cs`
- `src/Abuvi.API/Features/Guests/GuestsEndpoints.cs`
- `src/Abuvi.Tests/Unit/Features/Guests/GuestEntityTests.cs`
- `src/Abuvi.Tests/Unit/Features/Guests/GuestsRepositoryTests.cs`
- `src/Abuvi.Tests/Unit/Features/Guests/GuestsServiceTests.cs`
- `src/Abuvi.Tests/Unit/Features/Guests/CreateGuestValidatorTests.cs`
- `src/Abuvi.Tests/Unit/Features/Guests/UpdateGuestValidatorTests.cs`
- `src/Abuvi.Tests/Integration/Features/Guests/GuestDatabaseTests.cs`
- `src/Abuvi.Tests/Integration/Features/Guests/GuestsEndpointsTests.cs`

**Modified:**

- `src/Abuvi.API/Data/AbuviDbContext.cs` (add Guest DbSet)
- `src/Abuvi.API/appsettings.json` (add Encryption:Key)
- `src/Abuvi.API/Program.cs` (register services and endpoints)
