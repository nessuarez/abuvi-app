# Backend Implementation Plan: feat-expand-family-relationships

## Overview

Ampliar el enum `FamilyRelationship` con 6 nuevos valores para cubrir relaciones familiares extendidas (abuelos, nietos, tíos, sobrinos, primos, familia política). Cambio de bajo riesgo sin migración de BD ya que el campo se almacena como `string`.

## Architecture Context

- **Feature slice**: `src/Abuvi.API/Features/FamilyUnits/`
- **Files to modify**: `FamilyUnitsModels.cs` (enum only)
- **Cross-cutting**: `src/Abuvi.Setup/Importers/FamilyMemberImporter.cs` (verify compatibility)
- **Documentation**: `ai-specs/specs/data-model.md`
- **No new endpoints, services, repositories, or validators required**

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Name**: `feature/feat-expand-family-relationships-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/feat-expand-family-relationships-backend`
  4. Verify branch creation: `git branch`

### Step 1: Update `FamilyRelationship` Enum

- **File**: `src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`
- **Action**: Add 6 new values to the `FamilyRelationship` enum
- **Current code** (line 48-55):

```csharp
public enum FamilyRelationship
{
    Parent,
    Child,
    Sibling,
    Spouse,
    Other
}
```

- **New code**:

```csharp
public enum FamilyRelationship
{
    Parent,
    Child,
    Sibling,
    Spouse,
    Grandparent,
    Grandchild,
    UncleAunt,
    NephewNiece,
    Cousin,
    InLaw,
    Other
}
```

- **Implementation Notes**:
  - Keep `Other` as the last value (catch-all)
  - Order logically: direct family first, then extended family, then `Other`
  - All new string values fit within the existing `maxLength: 20` DB constraint (longest: "NephewNiece" = 11 chars)
  - No integer value assignment needed since the enum is stored as string via `HasConversion<string>()`

### Step 2: Verify CSV Importer Compatibility

- **File**: `src/Abuvi.Setup/Importers/FamilyMemberImporter.cs`
- **Action**: Verify (no code change expected)
- **Implementation Steps**:
  1. Confirm that the importer uses `Enum.Parse<FamilyRelationship>(value, ignoreCase: true)` - this will automatically support new enum values without code changes
  2. No modification required unless the importer has hardcoded values

### Step 3: Verify FluentValidation (if applicable)

- **Action**: Check if `FamilyUnitsValidator.cs` or any validator restricts the `Relationship` field to specific enum values
- **Implementation Steps**:
  1. Search for any validator that explicitly checks relationship values
  2. If validators use `.IsInEnum()`, no change needed (it validates against all current enum values automatically)
  3. If validators have hardcoded lists of allowed values, update them

### Step 4: Update Technical Documentation

- **File**: `ai-specs/specs/data-model.md`
- **Action**: Update the enum values list in the FamilyMember section
- **Current** (line 93):

```
- `relationship`: Relationship type within the family unit (required, enum: `Parent` | `Child` | `Sibling` | `Spouse` | `Other`)
```

- **New**:

```
- `relationship`: Relationship type within the family unit (required, enum: `Parent` | `Child` | `Sibling` | `Spouse` | `Grandparent` | `Grandchild` | `UncleAunt` | `NephewNiece` | `Cousin` | `InLaw` | `Other`)
```

- Also update validation rules (line 106):

```
- Relationship enum now includes: Parent, Child, Sibling, Spouse, Grandparent, Grandchild, UncleAunt, NephewNiece, Cousin, InLaw, Other
```

## Implementation Order

1. Step 0: Create feature branch
2. Step 1: Update `FamilyRelationship` enum
3. Step 2: Verify CSV importer compatibility
4. Step 3: Verify FluentValidation compatibility
5. Step 4: Update technical documentation

## Testing Checklist

- [ ] Build succeeds with new enum values (`dotnet build`)
- [ ] Existing unit tests pass (no regressions)
- [ ] API accepts new relationship values in `POST /api/family-units/{id}/members`
- [ ] API accepts new relationship values in `PUT /api/family-units/{id}/members/{memberId}`
- [ ] API returns new relationship values in `GET` responses
- [ ] Existing members with old relationship values continue to work

## Error Response Format

No changes to error responses. The existing `ApiResponse<T>` envelope applies. Invalid enum values will continue to return `400 Bad Request` via model binding.

## Dependencies

- No new NuGet packages required
- No EF Core migration required (relationship is stored as string, not int)

## Notes

- **No database migration needed**: The `relationship` column uses `HasConversion<string>()` with `maxLength: 20`. New enum values are stored as their string names directly.
- **Backward compatible**: Existing data with old relationship values remains valid. No data migration needed.
- **Frontend counterpart required**: The frontend enum and labels in `frontend/src/types/family-unit.ts` must be updated in a separate frontend ticket/branch to display the new options.
- **RGPD**: No impact - no sensitive data changes involved.

## Next Steps After Implementation

1. Create frontend ticket to update the `FamilyRelationship` enum and `FamilyRelationshipLabels` in `frontend/src/types/family-unit.ts`
2. Consider logical ordering of relationship options in the frontend dropdown (not alphabetical)
