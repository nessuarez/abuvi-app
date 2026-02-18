# Import CSV file with families data

## Objective

As an admin user, I want to be able to import a CSV file with the families data so that I can easily add multiple families to the system without having to enter each one manually.

## Description

The user can import a CSV file with the families data. The file must have the following format:

```csv
;e-mail;Fam.;;ord.;Nombre;Apellidos;Año Nc;Edad;Domicilio;C.P.;Ciudad;Telefono;Cuotas 2015;Cuotas 2016;Ajustes Deudas;Cuotas 2017;Cuotas 2018;e-mail;Nif;Alta;;;
271;;128;;1;María;Garrido López;1982;44;AVENIDA OCHO DE MARZO,14, PORTAL 1, 5-G;28523;Madrid;678643826;;;;;;
272;;128;;2;Néstor;Suárez Alonso;1983;43;AVENIDA OCHO DE MARZO,14, PORTAL 1, 5-G;28523;Madrid;699462834;;;;;;
273;;128;;3;Lucas;Suárez Garrido;2020;6;AVENIDA OCHO DE MARZO,14, PORTAL 1, 5-G;28523;Madrid;;;;;;;;55442150Y;2023;;;
274;;128;;4;Adrián;Suárez Garrido;2023;3;AVENIDA OCHO DE MARZO,14, PORTAL 1, 5-G;28523;Madrid;;;;;;;;;2023;;;
275;;128;;5;Naria;Suárez Garrido;2023;3;AVENIDA OCHO DE MARZO,14, PORTAL 1, 5-G;28523;Madrid;;;;;;;;;2023;;;
```

## User story

As a user, I want to import all the families data. Before importing the data, we should check data quality and format.
This is a side project, that can be done in Python script using pandas library to read the CSV file and perform the necessary validations and transformations before importing the data into the database.

User can run a Jupyter notebook that reads the CSV file, validates the data, and then imports it into the database. The notebook should also provide feedback on the import process, including any errors encountered and a summary of the imported data.

## Database schema

The database schema for the families data includes the following tables and fields:

### FamilyUnit

Groups people who attend camp together as a family. A User acts as the representative of their family unit.

**Fields:**

- `id`: Unique identifier for the FamilyUnit entity (Primary Key, UUID)
- `name`: Family display name, e.g. "Garcia Family" (required, max 200 characters)
- `representativeUserId`: User who manages this family unit (required, FK -> User)
- `createdAt`: Record creation timestamp (required, auto-generated)
- `updatedAt`: Last update timestamp (required, auto-updated)

**Validation rules:**

- Each FamilyUnit must have exactly one representative User
- The representative User must have an active account

**Relationships:**

- One FamilyUnit has exactly one representative User (via `representativeUserId`)
- One FamilyUnit contains many FamilyMembers
- One FamilyUnit can have many Registrations (one per camp)

---

### FamilyMember

A person (child or adult) within a family unit. In the future, a FamilyMember may gain their own User account for self-access to the platform.

**Fields:**

- `id`: Unique identifier for the FamilyMember entity (Primary Key, UUID)
- `familyUnitId`: The family unit this person belongs to (required, FK -> FamilyUnit)
- `userId`: Optional linked User account for future self-access (optional, FK -> User)
- `firstName`: Person first name (required, max 100 characters)
- `lastName`: Person last name (required, max 100 characters)
- `dateOfBirth`: Date of birth, used for camp age validation (required)
- `relationship`: Relationship type within the family unit (required, enum: `Parent` | `Child` | `Sibling` | `Spouse` | `Other`)
- `documentNumber`: National ID/passport number (optional, max 50 characters, uppercase alphanumeric, e.g., "12345678A", "ABC123")
- `email`: Email address (optional, max 255 characters, valid email format)
- `phone`: Contact phone number (optional, max 20 characters, E.164 format, e.g., "+34612345678")
- `medicalNotes`: Medical information (optional, max 2000 characters, sensitive data, must be stored encrypted at rest)
- `allergies`: Allergy information (optional, max 1000 characters, sensitive data, must be stored encrypted at rest)
- `createdAt`: Record creation timestamp (required, auto-generated)
- `updatedAt`: Last update timestamp (required, auto-updated)

**Validation rules:**

- DateOfBirth must be a valid past date
- Relationship enum now includes: Parent, Child, Sibling, Spouse, Other
- DocumentNumber format: uppercase letters and numbers only (e.g., "12345678A", "ABC123")
- Email must be a valid email format when provided
- Phone must be in E.164 format when provided (e.g., "+34612345678")
- MedicalNotes (max 2000 chars) and Allergies (max 1000 chars) must be encrypted at rest (AES-256) due to sensitive health data
- Sensitive fields (medical notes, allergies) are NEVER exposed in API responses - only boolean flags indicating presence
- A FamilyMember can only belong to one FamilyUnit

**Relationships:**

- Each FamilyMember belongs to exactly one FamilyUnit (via `familyUnitId`)
- Each FamilyMember may optionally link to one User account (via `userId`)

## Acceptance criteria

- User can import a CSV file only once, this is a setup action that should be done only once, so the system should prevent importing the same file multiple times.
- The system must validate the format of the CSV file. If the format is incorrect, the system must show an error message.
- The system must import the data from the CSV file and create the corresponding families in the database.
- The system must show a success message after the import is completed successfully and indicate the number of families imported.
- The system must handle any errors that may occur during the import process and show an appropriate error message to the user.
- The system must ensure that the imported data does not create duplicate entries in the database. If a duplicate entry is detected, the system should skip it and continue importing the rest of the data, while also logging the skipped entries for review.
