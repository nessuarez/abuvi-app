# Feature Spec: feat-registration-accommodations — Accommodation Preferences System

## Status: Pending — Spec needs enrichment

## Origin

Extracted from `feat-registration-extra-fields2` (Google Forms field #9: "Accommodation type preferences").

**Why extracted**: Accommodation preferences are used for **family placement within camp facilities**. A comma-separated string field is insufficient — this needs structured entities for capacity management and assignment logic.

---

## Problem Statement

Each camp edition offers different accommodation options (lodge, caravan zone, tent area, etc.) with limited capacity. Families rank their preferences during registration. Camp organizers then use these preferences to assign families to physical locations.

---

## Proposed Data Model

### `CampEditionAccommodation` — Available options per edition

| Field | Type | Description |
| ----- | ---- | ----------- |
| `Id` | Guid | PK |
| `CampEditionId` | Guid (FK) | Which edition offers this accommodation |
| `Name` | string (200) | e.g. "Refugio A", "Zona Caravanas Norte", "Zona Tiendas" |
| `AccommodationType` | enum | `Lodge`, `Caravan`, `Tent` (or extensible per edition) |
| `Description` | string? (1000) | Optional details |
| `Capacity` | int? | Max families/units (null = unlimited) |
| `IsActive` | bool | Can be disabled without deletion |

### `RegistrationAccommodationPreference` — Family's ranked choices

| Field | Type | Description |
| ----- | ---- | ----------- |
| `Id` | Guid | PK |
| `RegistrationId` | Guid (FK) | Which registration |
| `CampEditionAccommodationId` | Guid (FK) | Which accommodation option |
| `PreferenceOrder` | int | 1 = first choice, 2 = second, etc. |

### Future: `AccommodationAssignment` — Actual placement

| Field | Type | Description |
| ----- | ---- | ----------- |
| `Id` | Guid | PK |
| `RegistrationId` | Guid (FK) | Which registration |
| `CampEditionAccommodationId` | Guid (FK) | Assigned accommodation |
| `AssignedBy` | Guid (FK) | Admin who made the assignment |
| `AssignedAt` | DateTime | When assigned |

---

## Key Business Rules

1. Each family can rank up to 3 accommodation preferences
2. Preferences are ordered (1st, 2nd, 3rd choice)
3. Accommodation options vary per camp edition (configurable by admin)
4. Capacity limits should be visible during assignment (not enforced at registration)
5. Assignment is a separate admin workflow (not part of registration)

## API Endpoints (Proposed)

| Method | Endpoint | Description | Auth |
| ------ | -------- | ----------- | ---- |
| GET | `/api/camps/editions/{id}/accommodations` | List available accommodations | Member+ |
| POST | `/api/camps/editions/{id}/accommodations` | Create accommodation option | Board+ |
| PUT | `/api/camps/editions/accommodations/{id}` | Update accommodation option | Board+ |
| DELETE | `/api/camps/editions/accommodations/{id}` | Delete accommodation option | Board+ |

Registration preferences are submitted/returned as part of the existing registration endpoints.

---

## Frontend Considerations

- Preference selection UI: drag-to-rank or numbered dropdowns
- PrimeVue has no built-in drag-to-rank component — consider `OrderList` or a simple numbered select approach
- Show capacity usage in admin view

---

## Dependencies

- `feat-registration-extra-fields2` (guardian + preference fields — should be merged first)
- `feat-camp-edition-extras` (similar pattern for edition-level configuration)

---

## Document Control

- **Created**: 2026-02-26
- **Status**: Pending — needs enrichment with `/enrich-us` before implementation planning
- **Origin**: Extracted from `feat-registration-extra-fields2`
