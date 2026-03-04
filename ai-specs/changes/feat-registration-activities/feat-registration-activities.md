# Feature Spec: feat-registration-activities — Activity Sign-up System

## Status: Pending — Spec needs enrichment

## Origin

Extracted from `feat-registration-extra-fields2` (Google Forms field #14: "Activity participation").

**Why extracted**: Activities are mostly the same each year but have **conditions/requirements per edition** that affect member participation. This needs structured entities to track sign-ups and display conditions.

---

## Problem Statement

Each camp edition offers activities that members/families can volunteer for (cooking, hikes, sports, crafts, etc.). Activities usually repeat across years but may have different conditions, schedules, or requirements per edition. The registration flow needs to show available activities with their conditions and collect sign-ups.

---

## Proposed Data Model

### `CampEditionActivity` — Available activities per edition

| Field | Type | Description |
| ----- | ---- | ----------- |
| `Id` | Guid | PK |
| `CampEditionId` | Guid (FK) | Which edition offers this activity |
| `Name` | string (200) | e.g. "Cocina", "Excursiones", "Deportes" |
| `Description` | string? (1000) | Activity description and conditions |
| `Requirements` | string? (500) | Participation requirements/conditions |
| `MinAge` | int? | Minimum age for participation (null = no limit) |
| `MaxParticipants` | int? | Capacity limit (null = unlimited) |
| `IsActive` | bool | Can be disabled without deletion |

### `RegistrationActivitySelection` — Member/family sign-ups

| Field | Type | Description |
| ----- | ---- | ----------- |
| `Id` | Guid | PK |
| `RegistrationId` | Guid (FK) | Which registration |
| `CampEditionActivityId` | Guid (FK) | Which activity |
| `Notes` | string? (500) | Optional notes from the family |

---

## Key Business Rules

1. Activities are configured per edition by admin/board
2. Activities usually repeat across years (consider a "template" approach for seeding)
3. Each activity can have conditions displayed to users during registration
4. Activity sign-ups are informational (volunteering interest) — not enforced
5. No pricing effect — activities are included in the camp fee

## Common Activities (Reference)

These are the typical activities from previous years:

- Coordinacion del campamento
- Cocina
- Excursiones
- Cultura / Teatro
- Deportes
- Manualidades
- Fiestas
- Actividades infantiles

---

## API Endpoints (Proposed)

| Method | Endpoint | Description | Auth |
| ------ | -------- | ----------- | ---- |
| GET | `/api/camps/editions/{id}/activities` | List available activities | Member+ |
| POST | `/api/camps/editions/{id}/activities` | Create activity | Board+ |
| PUT | `/api/camps/editions/activities/{id}` | Update activity | Board+ |
| DELETE | `/api/camps/editions/activities/{id}` | Delete activity | Board+ |

Activity selections are submitted/returned as part of the existing registration endpoints.

---

## Frontend Considerations

- Multi-select checkbox group with activity descriptions
- Show conditions/requirements next to each activity
- Show current participant count if capacity is limited
- Consider grouping or categorizing activities

---

## Dependencies

- `feat-registration-extra-fields2` (guardian + preference fields — should be merged first)
- `feat-camp-edition-extras` (similar pattern for edition-level configuration)

---

## Document Control

- **Created**: 2026-02-26
- **Status**: Pending — needs enrichment with `/enrich-us` before implementation planning
- **Origin**: Extracted from `feat-registration-extra-fields2`
