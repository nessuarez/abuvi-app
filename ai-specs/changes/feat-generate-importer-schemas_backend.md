# Backend Implementation Plan: feat-generate-importer-schemas — Generate JSON Schema Validation for CSV Importer

## Overview

Generate JSON schema files that describe the validation contract for each CSV file consumed by the `Abuvi.Setup` importer. These schemas will be used by an external Python tool to pre-validate CSVs before they reach the .NET importer.

This is **not a typical API feature** — it's a static file generation task. No new endpoints, services, or DB changes are needed. The output is a set of `.schema.json` files and an `import-order.json` file placed under `schemas/` at the project root.

## Architecture Context

- **Source of truth**: `src/Abuvi.Setup/` — importers, `CsvHelper.cs`, entity models
- **DB constraints**: `src/Abuvi.API/Data/Configurations/` — EF Core entity type configurations
- **API validators** (secondary reference): `src/Abuvi.API/Features/*/` — FluentValidation validators
- **Output directory**: `schemas/` at the repository root
- **No code changes** to `Abuvi.Setup` or `Abuvi.API` are required

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Create and switch to a new feature branch
- **Branch Naming**: `feature/feat-generate-importer-schemas-backend`
- **Implementation Steps**:
  1. Ensure you're on the latest `dev` branch
  2. Pull latest changes: `git pull origin dev`
  3. Create new branch: `git checkout -b feature/feat-generate-importer-schemas-backend`
  4. Verify branch creation: `git branch`

### Step 1: Create `schemas/users.schema.json`

- **File**: `schemas/users.schema.json`
- **Action**: Create the JSON schema for `users.csv`
- **Implementation Details**:

```json
{
  "fileName": "users.csv",
  "separator": ",",
  "encoding": "UTF-8",
  "columns": [
    {
      "name": "email",
      "type": "string",
      "required": true,
      "maxLength": 255,
      "format": "^[\\w.+-]+@[\\w.-]+\\.[a-zA-Z]{2,}$",
      "references": null,
      "notes": "Stored lowercase in DB. Used as unique identifier for deduplication."
    },
    {
      "name": "password",
      "type": "string",
      "required": true,
      "maxLength": null,
      "notes": "BCrypt-hashed before storage (work factor 12). API validator requires min 8 chars; importer has NO min-length check.",
      "confidence": "low"
    },
    {
      "name": "firstName",
      "type": "string",
      "required": true,
      "maxLength": 100
    },
    {
      "name": "lastName",
      "type": "string",
      "required": true,
      "maxLength": 100
    },
    {
      "name": "phone",
      "type": "string",
      "required": false,
      "maxLength": 20,
      "format": "^\\+?[0-9\\s\\-\\(\\)]+$",
      "notes": "Format regex comes from API FluentValidation (CreateUserRequestValidator), not enforced during CSV import.",
      "confidence": "low"
    },
    {
      "name": "role",
      "type": "string",
      "required": true,
      "allowedValues": ["Admin", "Board", "Member"],
      "notes": "Enum.Parse with ignoreCase:true, so 'admin', 'ADMIN', 'Admin' all valid. Stored as varchar(20)."
    },
    {
      "name": "documentNumber",
      "type": "string",
      "required": false,
      "maxLength": 50,
      "notes": "DB has partial unique index (unique when NOT NULL)."
    }
  ],
  "constraints": {
    "uniqueColumns": ["email"],
    "dependsOn": []
  }
}
```

- **Key Notes**:
  - `email` has a UNIQUE DB constraint — duplicates are skipped silently by the importer
  - `password` has no min-length check in the importer itself (only in API validators)
  - `role` is case-insensitive via `Enum.Parse(ignoreCase: true)`
  - `documentNumber` has a conditional UNIQUE index (unique when NOT NULL)

### Step 2: Create `schemas/family-units.schema.json`

- **File**: `schemas/family-units.schema.json`
- **Action**: Create the JSON schema for `family-units.csv`
- **Implementation Details**:

```json
{
  "fileName": "family-units.csv",
  "separator": ",",
  "encoding": "UTF-8",
  "columns": [
    {
      "name": "name",
      "type": "string",
      "required": true,
      "maxLength": 200
    },
    {
      "name": "representativeEmail",
      "type": "string",
      "required": true,
      "references": "users.csv:email",
      "notes": "Must match an existing User.Email in the DB. Case-insensitive lookup."
    }
  ],
  "constraints": {
    "uniqueColumns": ["name"],
    "dependsOn": ["users.csv"]
  }
}
```

- **Key Notes**:
  - `name` uniqueness is checked case-insensitively in the importer
  - `representativeEmail` is an FK-like reference to `users.csv` — the referenced user must exist in DB
  - After creating the FamilyUnit, the importer back-patches `User.FamilyUnitId`

### Step 3: Create `schemas/family-members.schema.json`

- **File**: `schemas/family-members.schema.json`
- **Action**: Create the JSON schema for `family-members.csv`
- **Implementation Details**:

```json
{
  "fileName": "family-members.csv",
  "separator": ",",
  "encoding": "UTF-8",
  "columns": [
    {
      "name": "familyUnitName",
      "type": "string",
      "required": true,
      "references": "family-units.csv:name",
      "notes": "Must match an existing FamilyUnit.Name in the DB. Case-insensitive lookup."
    },
    {
      "name": "firstName",
      "type": "string",
      "required": true,
      "maxLength": 100
    },
    {
      "name": "lastName",
      "type": "string",
      "required": true,
      "maxLength": 100
    },
    {
      "name": "dateOfBirth",
      "type": "date",
      "required": true,
      "format": "yyyy-MM-dd",
      "notes": "Parsed with DateOnly.Parse(). Exact format depends on system locale, but ISO 8601 (yyyy-MM-dd) is safest.",
      "confidence": "low"
    },
    {
      "name": "relationship",
      "type": "string",
      "required": true,
      "allowedValues": ["Parent", "Child", "Sibling", "Spouse", "Other"],
      "notes": "Enum.Parse with ignoreCase:true. Stored as varchar(20)."
    },
    {
      "name": "documentNumber",
      "type": "string",
      "required": false,
      "maxLength": 50,
      "notes": "API validator enforces ^[A-Z0-9]+$ but importer does NOT validate format."
    },
    {
      "name": "email",
      "type": "string",
      "required": false,
      "maxLength": 255
    },
    {
      "name": "phone",
      "type": "string",
      "required": false,
      "maxLength": 20,
      "notes": "API validator enforces E.164 format (^\\+[1-9]\\d{1,14}$) but importer does NOT validate format.",
      "confidence": "low"
    }
  ],
  "constraints": {
    "uniqueColumns": [["familyUnitName", "firstName", "lastName", "dateOfBirth"]],
    "dependsOn": ["family-units.csv"],
    "notes": "Composite uniqueness: (familyUnitId + firstName + lastName + dateOfBirth). The importer resolves familyUnitName to familyUnitId for this check."
  }
}
```

- **Key Notes**:
  - Composite duplicate check on `(familyUnitId, firstName, lastName, dateOfBirth)`
  - `dateOfBirth` uses `DateOnly.Parse()` which accepts multiple formats — `yyyy-MM-dd` is recommended but not guaranteed by the importer
  - API validators enforce stricter rules (E.164 phone, uppercase doc numbers) that the importer does NOT apply

### Step 4: Create `schemas/camps.schema.json`

- **File**: `schemas/camps.schema.json`
- **Action**: Create the JSON schema for `camps.csv`
- **Implementation Details**:

```json
{
  "fileName": "camps.csv",
  "separator": ",",
  "encoding": "UTF-8",
  "columns": [
    {
      "name": "name",
      "type": "string",
      "required": true,
      "maxLength": 200
    },
    {
      "name": "description",
      "type": "string",
      "required": false,
      "maxLength": 2000
    },
    {
      "name": "location",
      "type": "string",
      "required": false,
      "maxLength": 500
    },
    {
      "name": "pricePerAdult",
      "type": "decimal",
      "required": true,
      "format": "^\\d+(\\.\\d{1,2})?$",
      "notes": "Parsed with decimal.Parse(InvariantCulture). Use dot (.) as decimal separator. DB constraint: >= 0, precision(10,2)."
    },
    {
      "name": "pricePerChild",
      "type": "decimal",
      "required": true,
      "format": "^\\d+(\\.\\d{1,2})?$",
      "notes": "Same rules as pricePerAdult."
    },
    {
      "name": "pricePerBaby",
      "type": "decimal",
      "required": true,
      "format": "^\\d+(\\.\\d{1,2})?$",
      "notes": "Same rules as pricePerAdult."
    }
  ],
  "constraints": {
    "uniqueColumns": ["name"],
    "dependsOn": [],
    "notes": "Name uniqueness checked case-insensitively. Prices must be >= 0 (DB CHECK constraint)."
  }
}
```

### Step 5: Create `schemas/camp-editions.schema.json`

- **File**: `schemas/camp-editions.schema.json`
- **Action**: Create the JSON schema for `camp-editions.csv`
- **Implementation Details**:

```json
{
  "fileName": "camp-editions.csv",
  "separator": ",",
  "encoding": "UTF-8",
  "columns": [
    {
      "name": "campName",
      "type": "string",
      "required": true,
      "references": "camps.csv:name",
      "notes": "Must match an existing Camp.Name in the DB. Case-insensitive lookup."
    },
    {
      "name": "year",
      "type": "integer",
      "required": true,
      "notes": "Parsed with int.Parse(). API validator restricts 2000-2100 but importer has no range check.",
      "confidence": "low"
    },
    {
      "name": "startDate",
      "type": "date",
      "required": true,
      "notes": "Parsed with DateTime.Parse(). ISO 8601 format recommended (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss).",
      "confidence": "low"
    },
    {
      "name": "endDate",
      "type": "date",
      "required": true,
      "notes": "Parsed with DateTime.Parse(). API validator enforces endDate > startDate but importer does NOT.",
      "confidence": "low"
    },
    {
      "name": "pricePerAdult",
      "type": "decimal",
      "required": true,
      "format": "^\\d+(\\.\\d{1,2})?$",
      "notes": "decimal.Parse(InvariantCulture). DB constraint: >= 0, precision(10,2)."
    },
    {
      "name": "pricePerChild",
      "type": "decimal",
      "required": true,
      "format": "^\\d+(\\.\\d{1,2})?$"
    },
    {
      "name": "pricePerBaby",
      "type": "decimal",
      "required": true,
      "format": "^\\d+(\\.\\d{1,2})?$"
    },
    {
      "name": "maxCapacity",
      "type": "integer",
      "required": false,
      "notes": "Parsed with int.Parse() if present."
    },
    {
      "name": "status",
      "type": "string",
      "required": true,
      "allowedValues": ["Proposed", "Draft", "Open", "Closed", "Completed"],
      "notes": "Enum.Parse with ignoreCase:true. Stored as varchar(20)."
    },
    {
      "name": "notes",
      "type": "string",
      "required": false,
      "maxLength": 2000
    }
  ],
  "constraints": {
    "uniqueColumns": [["campName", "year"]],
    "dependsOn": ["camps.csv"],
    "notes": "Composite uniqueness: (campId, year). campName is resolved to campId. DB has composite index IX_CampEditions_CampId_Year."
  }
}
```

### Step 6: Create `schemas/import-order.json`

- **File**: `schemas/import-order.json`
- **Action**: Create the import dependency graph and execution order
- **Implementation Details**:

```json
{
  "description": "Import order for Abuvi.Setup CSV importer. Files must be imported in this sequence to satisfy foreign key dependencies.",
  "importOrder": [
    {
      "order": 1,
      "fileName": "users.csv",
      "entity": "User",
      "dependsOn": [],
      "notes": "No dependencies. Creates User entities. Duplicates detected by email (case-insensitive)."
    },
    {
      "order": 2,
      "fileName": "family-units.csv",
      "entity": "FamilyUnit",
      "dependsOn": ["users.csv"],
      "notes": "Requires Users to exist (resolves representativeEmail -> User). Also back-patches User.FamilyUnitId."
    },
    {
      "order": 3,
      "fileName": "family-members.csv",
      "entity": "FamilyMember",
      "dependsOn": ["family-units.csv"],
      "notes": "Requires FamilyUnits to exist (resolves familyUnitName -> FamilyUnit)."
    },
    {
      "order": 4,
      "fileName": "camps.csv",
      "entity": "Camp",
      "dependsOn": [],
      "notes": "No dependencies. Independent of Users/FamilyUnits track. Could run in parallel with steps 1-3."
    },
    {
      "order": 5,
      "fileName": "camp-editions.csv",
      "entity": "CampEdition",
      "dependsOn": ["camps.csv"],
      "notes": "Requires Camps to exist (resolves campName -> Camp)."
    }
  ],
  "parallelGroups": [
    {
      "group": "A",
      "files": ["users.csv"],
      "notes": "Must complete before group B"
    },
    {
      "group": "B",
      "files": ["family-units.csv", "camps.csv"],
      "notes": "family-units.csv depends on group A. camps.csv is independent."
    },
    {
      "group": "C",
      "files": ["family-members.csv", "camp-editions.csv"],
      "notes": "family-members.csv depends on family-units.csv. camp-editions.csv depends on camps.csv."
    }
  ],
  "globalSettings": {
    "delimiter": ",",
    "encoding": "UTF-8",
    "headerTrimming": true,
    "valueTrimming": true,
    "quoteHandling": "none",
    "blankLinesSkipped": true,
    "decimalFormat": "InvariantCulture (dot separator)",
    "missingFileBehavior": "silently skipped"
  }
}
```

### Step 7: Update Technical Documentation

- **Action**: Review and update technical documentation according to changes made
- **Implementation Steps**:
  1. No `data-model.md` changes needed (no schema changes)
  2. No `api-spec.yml` changes needed (no new endpoints)
  3. Add a brief entry in the project README or a new `schemas/README.md` explaining:
     - What the schemas are for
     - How they were derived (from the real importer code + DB constraints)
     - The `confidence: low` markers and what they mean
     - How to use them with the Python validator
  4. Consider adding a note in `ai-specs/specs/backend-standards.mdc` about keeping schemas in sync when importer changes

## Implementation Order

1. Step 0: Create feature branch
2. Step 1: `schemas/users.schema.json`
3. Step 2: `schemas/family-units.schema.json`
4. Step 3: `schemas/family-members.schema.json`
5. Step 4: `schemas/camps.schema.json`
6. Step 5: `schemas/camp-editions.schema.json`
7. Step 6: `schemas/import-order.json`
8. Step 7: Update documentation

## Testing Checklist

Since this task produces static JSON files (no runtime code), testing focuses on correctness verification:

- [ ] All 5 schema files are valid JSON (parse with `jq` or similar)
- [ ] All column names match the actual CSV headers in `src/Abuvi.Setup/seed/*.csv`
- [ ] All `required` fields match the `Require()` vs `Optional()` calls in each importer
- [ ] All `maxLength` values match EF Core entity configurations (e.g., `varchar(255)` → `maxLength: 255`)
- [ ] All `allowedValues` match the C# enum definitions exactly
- [ ] All `references` point to correct entity/column combinations
- [ ] All `uniqueColumns` match the deduplication logic in importers + DB unique constraints
- [ ] All `dependsOn` chains match the order in `SeedRunner.ImportAllAsync()`
- [ ] `import-order.json` order matches `SeedRunner` execution order
- [ ] Decimal format uses InvariantCulture (dot separator)
- [ ] No invented validations — every rule traces back to real code or DB constraints

## Error Response Format

Not applicable — no API endpoints are created in this task.

## Dependencies

- No new NuGet packages needed
- No EF Core migrations needed
- No runtime code changes

## Notes

### Critical: Validation Gap Between Importer and API

The schemas document **what the importer actually validates**, NOT what the API validators enforce. Several discrepancies exist:

| Rule | API Validator | CSV Importer |
|---|---|---|
| Password min length | 8 chars | No check |
| Phone format | Regex validated | No check |
| Document number format | `^[A-Z0-9]+$` | No check |
| Date of birth in past | Enforced | No check |
| End date > start date | Enforced | No check |
| Year range 2000-2100 | Enforced | No check |
| Prices >= 0 | Enforced | No check (but DB CHECK will reject) |

Fields where the importer is more lenient are marked with `"confidence": "low"` in the schemas. The Python validator should decide whether to apply the stricter API-level rules or only the importer-level rules.

### CSV Parser Limitations

- The custom `CsvHelper.cs` uses raw `string.Split(',')` — **commas inside values are NOT supported**
- No quote handling — values must not contain the delimiter
- Header matching is case-insensitive (`OrdinalIgnoreCase`)
- Enum parsing is case-insensitive

### Language

- All schema files, keys, and documentation must be in **English** per project standards
- The original spec was in Spanish but output must follow `base-standards.mdc` English-only rule

## Next Steps After Implementation

1. Share schemas with the Python validation tool team
2. Consider adding a CI step that regenerates schemas from code (to prevent drift)
3. Decide whether the Python validator should apply API-level validation rules (stricter) or importer-level only
4. Consider adding schemas for any future CSV entity types added to the importer
