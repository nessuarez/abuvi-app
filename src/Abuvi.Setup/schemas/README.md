# CSV Importer Validation Schemas

Validation contract schemas for each CSV file consumed by `Abuvi.Setup`. These are designed to be used by an external Python validator to pre-validate CSVs **before** sending them to the .NET importer.

## How schemas were derived

Every rule in these schemas traces back to real code:

- **Column names and required/optional** — from `CsvHelper.Require()` / `CsvHelper.Optional()` calls in each importer
- **`maxLength`** — from EF Core `IEntityTypeConfiguration<T>` classes (e.g., `varchar(255)`)
- **`allowedValues`** — from C# enum definitions (e.g., `UserRole`, `FamilyRelationship`, `CampEditionStatus`)
- **`references`** — from importer FK-resolution logic (e.g., `representativeEmail` resolves to `User.Email`)
- **`uniqueColumns`** — from importer deduplication logic + DB unique constraints/indexes
- **`dependsOn`** — from import order in `SeedRunner.ImportAllAsync()`

## Understanding `confidence: low`

Fields marked with `"confidence": "low"` indicate a validation gap between the importer and the API:

- The **importer** does NOT enforce the rule (it will accept any value)
- The **API FluentValidation** DOES enforce the rule
- The **DB constraints** may or may not catch the issue

The Python validator should decide whether to apply the stricter API-level rules or only what the importer checks.

## Global CSV settings

| Setting | Value |
|---|---|
| Delimiter | `,` (comma) |
| Encoding | UTF-8 |
| Header trimming | Yes |
| Value trimming | Yes |
| Quote handling | None (commas inside values NOT supported) |
| Blank lines | Skipped |
| Decimal format | InvariantCulture (dot separator, e.g., `150.00`) |
| Missing files | Silently skipped by importer |

## Files

| Schema | CSV | Entity |
|---|---|---|
| `users.schema.json` | `users.csv` | User |
| `family-units.schema.json` | `family-units.csv` | FamilyUnit |
| `family-members.schema.json` | `family-members.csv` | FamilyMember |
| `camps.schema.json` | `camps.csv` | Camp |
| `camp-editions.schema.json` | `camp-editions.csv` | CampEdition |
| `import-order.json` | — | Import dependency graph and execution order |
