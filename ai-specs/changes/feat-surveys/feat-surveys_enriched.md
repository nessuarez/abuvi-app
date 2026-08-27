# Feature: Survey Distribution (Camp Feedback + Association Surveys)

## Summary

The association needs to run two kinds of survey — **camp feedback** after an edition ends, and **association surveys** to all *socios y socias* — and the forms themselves will be built in **Google Forms** (or any equivalent tool).

So the platform does not build forms and does not store answers. It does the two things Google cannot do, because they depend on data only the platform holds:

1. **Resolve who gets surveyed** — the adults who attended a given camp edition, or the adult active members of the association.
2. **Reach them** — send the form link by email, or export the audience as CSV for another tool.

Everything else — question design, response collection, charts — happens in Google Forms and Sheets.

---

## Design Decisions Behind This Shape

Recorded because the obvious reading of "necesitamos hacer encuestas" is a survey builder, and this spec deliberately is not one.

| Decision | Consequence |
| --- | --- |
| Forms live in Google Forms | No question model, no response model, no builder UI, no results aggregation. Roughly two thirds of the original scope removed. |
| Analysis happens in Google Sheets | The platform never receives responses. No CSV import, no cross-referencing with platform data. |
| One shared link, not one token per person | No invitation table, no token lifecycle. Response tracking is a number the board reads in Google Forms. Reminders go to the whole audience. |
| Surveys go to **adults only** | A single filter, and for camp audiences it is free — `RegistrationMember.AgeCategory` is already computed and stored at registration time. |
| Feedback is **anonymous by default**, with an opt-in "contact me" | Guaranteed structurally rather than by policy — see *Anonymity*. |

**What the platform still owns is the part that was always the hard part**: nobody outside the platform knows which adults attended the 2026 edition, or which members' fees are current.

---

## Current State in the Codebase

- There is no survey feature of any kind.
- `IEmailService.SendFeedbackRequestAsync` (`IEmailService.cs:127`, implemented at `ResendEmailService.cs:589`) sends a "How was your experience?" email pointing at `{FrontendUrl}/feedback`. That route **does not exist** in `frontend/src/router/index.ts`, and the method is **never called from any production path** — the only references are its own unit test (`ResendEmailServiceTests.cs:511-525`). It is dead code with a broken link and an English template in a Spanish product. **This feature supersedes it: delete it and its test** rather than repairing it.
- `RegistrationMember` already stores `AgeCategory` and `AgeAtCamp`, computed at registration against the edition's age ranges (`RegistrationsService.cs:91-101`). Camp audiences filter on the stored value; no recomputation, no drift between the price a family paid and who counts as an adult.
- Age ranges for non-camp contexts come from the `age_ranges` association setting, deserialised into `AgeRanges(BabyMaxAge, ChildMinAge, ChildMaxAge, AdultMinAge)` (`RegistrationPricingService.cs:44-50`).
- `Guest` (`GuestsModels.cs:8-27`) has a `DateOfBirth` and an **optional** `Email`. Guests attend camps but have no stored age category.

---

## Scope

### In scope

1. `Survey` entity — one table. Title, description, external form URL, audience, lifecycle, counts.
2. Audience resolution for two audiences, adults only.
3. Audience preview and CSV export.
4. Sending the form link by email to the resolved audience, plus a rate-limited reminder.
5. Board UI: list, create/edit, audience preview, send, reminder, close, record the response count.
6. Deleting the dead `SendFeedbackRequestAsync` and its test.

### Out of scope

- Building forms, storing answers, aggregating results, importing response CSVs.
- Per-person tokens, response tracking per individual, targeted reminders.
- In-app surfacing of open surveys (see *Follow-ups*).
- Formal *asamblea* voting — this is opinion collection, with none of the census, quorum or ballot-integrity guarantees a statutory vote needs. If the Junta wants that, it is a different feature and must not be built by widening this one.

### Follow-ups

- **In-app surfacing**: a card on `HomePage.vue` for open surveys the logged-in user falls into. Cheap once audience resolution exists, but email is the channel that reaches people without accounts, so it is not v1.
- **Response import**: if "read the number in Google Forms" proves too manual, importing the response CSV becomes worthwhile. The `Survey` row is already the natural parent for it.

---

## Data Model

One entity, in a new vertical slice `src/Abuvi.API/Features/Surveys/` following the structure of `Features/Memories/` (`SurveysModels.cs`, `SurveysEndpoints.cs`, `SurveysRepository.cs`, `SurveysService.cs`, `SurveysValidators.cs`, `SurveysExtensions.cs`).

```csharp
public class Survey
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // The external form (Google Forms or equivalent). The platform links to it,
    // never reads from it.
    public string FormUrl { get; set; } = string.Empty;

    public SurveyType Type { get; set; }
    // Required when Type == CampFeedback, null otherwise.
    public Guid? CampEditionId { get; set; }
    public SurveyAudience Audience { get; set; }

    public SurveyStatus Status { get; set; } = SurveyStatus.Draft;
    public DateTime? ClosesAt { get; set; }     // shown in the email as a deadline

    // Snapshot taken when the survey is first sent.
    public int AudienceTotal { get; set; }      // everyone resolved
    public int InvitedCount { get; set; }       // those with an email address

    // Typed in by the board from Google Forms. Optional, purely informational.
    public int? ResponsesCount { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? RemindedAt { get; set; }
    public int RemindersSent { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public enum SurveyType { CampFeedback, AssociationSurvey }

public enum SurveyAudience
{
    CampEditionAdults,   // adults on non-cancelled registrations for the edition
    ActiveAdultMembers   // adults with an active Membership
}

public enum SurveyStatus { Draft, Sent, Closed }
```

**No recipient table.** The audience is resolved on demand — for preview, for export, and again at send time. Nothing per-person is persisted, which is what makes the anonymity guarantee structural rather than procedural.

**Editability.** `Title`, `Description`, `FormUrl` and `ClosesAt` stay editable after sending — a wrong form URL must be fixable, and the follow-up reminder will carry the corrected one. `Type`, `CampEditionId` and `Audience` freeze once `Status != Draft`, because `AudienceTotal` was snapshotted against them and changing them would make the recorded coverage a lie.

### EF configuration

`SurveyConfiguration : IEntityTypeConfiguration<Survey>` → table `surveys`, snake_case columns, `Title` max 200, `FormUrl` max 2048 (matching the `ProfilePhotoUrl` convention in `FamilyUnitConfiguration.cs:53-55`), `Description` max 2000, timestamps defaulting to `NOW()`. Index on `(status, type)` for the list query. Migration: `AddSurveysTable`.

---

## Audience Resolution

The core of the feature. Implemented in `SurveysService.ResolveAudienceAsync(Survey, CancellationToken)` returning `IReadOnlyList<SurveyRecipient>`, a plain projection — not an entity:

```csharp
public record SurveyRecipient(
    string FullName,
    string? Email,
    string Source,        // "Miembro" | "Invitado" — shown in the preview and export
    int? FamilyNumber);   // context for the board, blank for guests
```

### `CampEditionAdults`

All people on registrations for `CampEditionId` whose status is **not** `Cancelled` (so `Pending`, `PartiallyPaid`, `FullyPaid`, `Confirmed` all count):

- **`RegistrationMember`s** where `AgeCategory == AgeCategory.Adult`. The value is already stored — no recomputation.
- **`Guest`s** on those registrations, where the age computed at the edition's `StartDate` reaches `AdultMinAge`. Guests have no stored category, so this uses `RegistrationPricingService.CalculateAge(guest.DateOfBirth, edition.StartDate)` against the edition's effective age ranges — the same helper the pricing path uses, so a guest and a member of identical age are always classified identically.

Email comes from `FamilyMember.Email` / `Guest.Email`, both optional. **Falls back to the family representative's email** when a member has none, because the representative is the household's contact of record — with a per-recipient note in the preview so the board can see that two invitations are landing in one inbox.

### `ActiveAdultMembers`

`FamilyMember`s with a `Membership` where `IsActive == true` (`MembershipsModels.cs:8-24`), whose age **today** reaches `AdultMinAge` from the `age_ranges` setting. Same email fallback to the representative.

### Deduplication

A person can appear twice — two adults in one family both falling back to the same representative email, or a member who is also listed as a guest. **Dedupe by normalised email**, keeping the first occurrence. Sending the same person two links to one anonymous form invites duplicate responses.

---

## Anonymity

The user requirement is: **camp feedback is anonymous by default, unless the respondent chooses to be contacted**.

With this architecture that is not a promise the platform makes — it is a fact about what the platform can physically know. One shared link, no tokens, no response storage: the platform cannot associate a response with a person, because it never sees a response at all. Nothing in the schema needs to enforce it.

**But the guarantee can be broken outside the platform, and that is the real risk.** Two traps in Google Forms, both on by default in some configurations, both silently identifying:

1. **"Collect email addresses"** — records the respondent's Google account on every submission. This must be **off**. It is the single most common way a survey advertised as anonymous is not.
2. **"Limit to 1 response"** — requires sign-in, which implies collecting identity. Must be **off**, which is the cost of anonymity: nothing stops a determined person from responding twice, and nothing stops a link forwarded outside the association from being answered. For an association of this size the trade-off favours anonymity, but the Junta should know it is a trade-off and not assume the response count is tamper-proof.

The board UI states both requirements next to the form URL field, because whoever creates the survey in the platform is whoever created the form in Google, and that is the only moment they will read it.

### Opt-in contact

Anonymity with an escape hatch is a **form-design** matter, not a platform one. Guidance for whoever builds the form, to be reproduced in the board UI as help text:

> Last question of the form, optional and clearly marked:
>
> *"Esta encuesta es anónima. Si quieres que la Junta pueda responderte sobre algo concreto que hayas comentado, déjanos tu email aquí. Es totalmente voluntario."*

A respondent who fills it in has chosen to be identifiable for that response; one who leaves it blank stays anonymous. The platform stores neither.

---

## API Contract

Mounted at `/api/surveys`, **entirely Admin/Board** — there is no respondent-facing endpoint, because respondents go to Google. One route group with `.RequireAuthorization(policy => policy.RequireRole("Admin", "Board"))`, as in `FamilyUnitsEndpoints.cs:25-28`.

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/surveys` | Create a draft. |
| `GET` | `/api/surveys` | Paged list, filters `status`, `type`, `campEditionId`. |
| `GET` | `/api/surveys/{id}` | Detail with the stored counts. |
| `PUT` | `/api/surveys/{id}` | Update. Rejects frozen fields once sent. |
| `DELETE` | `/api/surveys/{id}` | Draft only. |
| `GET` | `/api/surveys/{id}/audience` | Resolved recipients, paged. Preview before sending. |
| `GET` | `/api/surveys/{id}/audience/export` | Same list as CSV. |
| `POST` | `/api/surveys/{id}/send` | Draft → Sent. Emails everyone contactable, snapshots the counts. |
| `POST` | `/api/surveys/{id}/remind` | Re-sends to the whole audience. Rate-limited. |
| `POST` | `/api/surveys/{id}/close` | Sent → Closed. |
| `PATCH` | `/api/surveys/{id}/responses-count` | Record the figure read from Google Forms. |

### `GET /api/surveys/{id}/audience`

```json
{
  "success": true,
  "data": {
    "items": [
      { "fullName": "Ana García", "email": "ana@…", "source": "Miembro", "familyNumber": 42, "usesRepresentativeEmail": false },
      { "fullName": "Luis Pérez", "email": "rep@…", "source": "Invitado", "familyNumber": 42, "usesRepresentativeEmail": true },
      { "fullName": "Marta Ruiz", "email": null, "source": "Miembro", "familyNumber": 57, "usesRepresentativeEmail": false }
    ],
    "total": 142,
    "contactable": 118,
    "uncontactable": 24
  }
}
```

### `POST /api/surveys/{id}/send`

Resolves the audience, emails every contactable recipient, sets `Status = Sent`, `SentAt`, and snapshots `AudienceTotal` / `InvitedCount`.

**Response `200 OK`:**

```json
{ "surveyId": "…", "status": "Sent", "audienceTotal": 142, "invitedCount": 118, "failedCount": 0 }
```

**Errors:** `422` when `FormUrl` is empty, when `Type == CampFeedback` and `CampEditionId` is null, when the resolved audience is empty, or when the survey is not `Draft`.

Email failures **do not** abort the send: each is caught, logged and counted into `failedCount`, matching the error-isolation rule used elsewhere in the codebase. A survey half-sent is recoverable via the reminder; a survey that rolls back because one address bounced is not.

### `POST /api/surveys/{id}/remind`

Re-resolves the audience and re-sends, with reminder wording. Rate-limited to **one per 7 days**, enforced server-side against `RemindedAt` — a board member clicking twice must not mail the membership twice. Returns `422` when the survey is not `Sent` or the window has not elapsed, naming the date the next reminder becomes possible.

Because there are no per-person tokens, the reminder reaches people who already responded. The email says so plainly: *"Si ya la has respondido, gracias — puedes ignorar este mensaje."*

---

## Validation

`SurveysValidators.cs`, FluentValidation, through the existing `ValidationFilter<T>`:

- `Title` required, max 200. `Description` max 2000.
- `FormUrl` required, max 2048, must parse as an absolute `http`/`https` URI. **No domain allowlist** — the Junta may use Microsoft Forms, Typeform or anything else, and hardcoding `docs.google.com` would be a support ticket the first time they switch.
- `CampEditionId` required when `Type == CampFeedback`; must be null when `Type == AssociationSurvey`.
- `Audience == CampEditionAdults` requires `CampEditionId`; `ActiveAdultMembers` requires it to be null.
- `ClosesAt`, when set, must be in the future at creation.
- `ResponsesCount` ≥ 0.

---

## Email

One new method on `IEmailService`, covering both the invitation and the reminder — the templates differ by two sentences, and two near-identical templates drift apart within a release or two.

```csharp
public record SurveyInvitationEmailData
{
    public required string ToEmail { get; init; }
    public required string RecipientFirstName { get; init; }
    public required string SurveyTitle { get; init; }
    public string? SurveyDescription { get; init; }
    public required string FormUrl { get; init; }
    public DateTime? ClosesAt { get; init; }
    public bool IsReminder { get; init; }
}

Task SendSurveyInvitationAsync(SurveyInvitationEmailData data, CancellationToken ct);
```

Follows the conventions already in `ResendEmailService`: `IsTestAddress` early-return, `From = $"{_fromName} <{_fromEmail}>"`, inline styles, 600 px wrapper, Spanish copy, a single prominent CTA button.

**No board `Bcc`**, deliberately unlike the registration emails: bcc'ing the Junta on 118 invitations would bury their inbox, and it serves no purpose when the response data lives in Google anyway.

**Subject:** `{Title}` — or `Recordatorio: {Title}` when `IsReminder`.

**Body:**

> Hola, {RecipientFirstName}:
>
> {IsReminder: "Te recordamos que sigue abierta la encuesta" | "Nos gustaría conocer tu opinión"}: **{SurveyTitle}**
>
> {SurveyDescription}
>
> [ Responder la encuesta ] ← `FormUrl`
>
> {ClosesAt: "Puedes responder hasta el {ClosesAt:dd/MM/yyyy}."}
>
> La encuesta es anónima: no sabemos quién responde qué.
>
> {IsReminder: "Si ya la has respondido, gracias — puedes ignorar este mensaje."}
>
> Saludos cordiales, El equipo de Abuvi

The anonymity line is stated in the email because a claim made only in the form is a claim made after the person has already decided whether to click.

### Removal

Delete `IEmailService.SendFeedbackRequestAsync`, its implementation in `ResendEmailService`, and the `SendFeedbackRequestAsync` test region in `ResendEmailServiceTests.cs:511-525`. It is unreachable, points at a non-existent route, and is superseded here.

---

## Frontend

Small, entirely inside the admin area.

| File | Change |
| --- | --- |
| `frontend/src/types/survey.ts` | **New.** Enums and DTOs mirroring the backend. |
| `frontend/src/composables/useSurveys.ts` | **New.** CRUD, audience preview, export, send, remind, close, responses count. |
| `frontend/src/views/admin/SurveysAdminPage.vue` | **New.** Route `/admin/surveys`. |
| `frontend/src/components/admin/SurveysAdminPanel.vue` | **New.** `DataTable` of surveys with status tag, audience, coverage (`38 / 118`), actions. |
| `frontend/src/components/admin/SurveyFormDialog.vue` | **New.** Create/edit: title, description, form URL, type, edition, audience, closing date. |
| `frontend/src/components/admin/SurveyAudienceDialog.vue` | **New.** Paged recipient preview + "Exportar CSV" + "Enviar ahora". |
| `frontend/src/components/admin/AdminSidebar.vue` | Add `{ label: 'Encuestas', icon: 'pi pi-chart-bar', to: '/admin/surveys', testId: 'sidebar-surveys', visible: auth.isBoard }` to the **`Contenido`** group, next to "Revisión de medios". |
| `frontend/src/router/index.ts` | Route `/admin/surveys`, `requiresAuth: true`, title `ABUVI \| Encuestas`. |

### Interaction details that matter

- **The audience preview is a required stop before sending.** "Enviar" lives inside `SurveyAudienceDialog`, not on the list row, so nobody mails 118 people without having seen the list first. The dialog leads with the counts: *"142 personas, 118 con email. 24 no recibirán el correo."*
- **The form URL field carries the Google Forms warning inline**, as an `info` `Message`: *"Antes de enviar, comprueba en Google Forms que 'Recopilar direcciones de correo' está **desactivado**. Si no, la encuesta no será anónima."* Plus the suggested opt-in contact question, copyable.
- **The reminder button shows when it will next be available** rather than failing on click: *"Disponible a partir del 12/09"*.
- **`ResponsesCount` is an inline editable number** on the list row, not a separate dialog. It is a figure copied from another tab; anything more ceremonious than an input is friction.
- Send and remind both sit behind a `ConfirmDialog` naming the recipient count, since both are irreversible outbound actions.

---

## Implementation Steps (TDD)

1. **Entity + EF configuration + migration** — `Survey`, `SurveyConfiguration`, `AddSurveysTable`.
2. **Validators (tests first)** — camp survey without an edition rejected; association survey *with* an edition rejected; non-absolute `FormUrl` rejected; audience/type mismatch rejected.
3. **Audience resolution (tests first)** — this is the feature, so it gets the most tests:
   - `CampEditionAdults` includes members with `AgeCategory.Adult` and excludes children and babies.
   - Cancelled registrations are excluded; `Pending` ones are included.
   - Adult guests are included; a guest under `AdultMinAge` at the edition's start date is excluded.
   - A member with no email falls back to the representative's, flagged `usesRepresentativeEmail`.
   - A member with no email and no reachable representative is counted `uncontactable`, not dropped silently.
   - Duplicate emails collapse to one recipient.
   - `ActiveAdultMembers` excludes lapsed memberships and members below the adult age today.
4. **Service — CRUD (tests first)** — deleting a sent survey throws; editing `Audience` after sending throws; editing `FormUrl` after sending succeeds.
5. **Service — send (tests first)** — empty audience throws `422`; counts are snapshotted; a failing email is counted into `failedCount` without aborting the rest; sending a non-draft throws.
6. **Service — remind (tests first)** — within 7 days throws with the next available date; after 7 days succeeds and increments `RemindersSent`; reminding a `Draft` or `Closed` survey throws.
7. **Email** — `SurveyInvitationEmailData` + `SendSurveyInvitationAsync`; test that reminder wording appears only when `IsReminder`, and that the anonymity line is always present.
8. **Delete** `SendFeedbackRequestAsync`, its implementation and its test region.
9. **Endpoints** — the eleven routes, `Produces` metadata, CSV export with `text/csv` and a `Content-Disposition` filename.
10. **Frontend** — types, composable, admin page, panel, form dialog, audience dialog, sidebar, route.
11. **Docs** — `api-endpoints.md` gains a Surveys section; `data-model.md` gains the `Survey` entity.

---

## Non-functional Requirements

- **Anonymity**: the platform stores nothing per respondent. No table, no token, no log line naming who received which survey — send logging records counts and failures, not a recipient roll.
- **Outbound safety**: send and remind are irreversible and reach the whole membership. Both are Board-only, both require confirmation naming the recipient count, and remind is rate-limited server-side. The rate limit is enforced in the service, not the UI, because the UI is not the only caller.
- **Error isolation**: a failing email never aborts a send in progress; failures are counted and logged with the recipient's email and the Resend exception.
- **Performance**: audience resolution for `ActiveAdultMembers` touches a few hundred rows. It runs as a projection in SQL — no loading of full `FamilyMember` graphs — and the preview endpoint is paged.
- **Authorization**: every endpoint requires `Admin`/`Board`. There is no member-facing endpoint at all.
- **Data protection**: the audience export is a list of names and emails of members and guests. It is Board-only, and the CSV is generated on demand rather than stored.
- **i18n**: all copy in Spanish. The English `SendFeedbackRequestAsync` template leaves the codebase as part of this work.

---

## Acceptance Criteria

- [ ] A board member can create a survey with a title, an external form URL, a type and an audience.
- [ ] A camp survey requires an edition; an association survey rejects one.
- [ ] The audience preview lists the resolved recipients with their source and family number, and shows total / contactable / uncontactable.
- [ ] **Only adults appear.** Children and babies on a registration are absent; guests below the adult age at the edition's start date are absent.
- [ ] Cancelled registrations contribute nobody.
- [ ] A member without an email is shown against the family representative's address, marked as such, and appears once — not twice for two adults in the same household.
- [ ] The audience exports as CSV with the same rows as the preview.
- [ ] Sending emails every contactable recipient in Spanish with a working link to the external form, and records the coverage snapshot.
- [ ] A single failing address does not prevent the rest of the send; it is reported in `failedCount`.
- [ ] A second reminder within 7 days is refused, naming the date it becomes available.
- [ ] The reminder email tells people who already responded to ignore it.
- [ ] Every invitation email states that the survey is anonymous.
- [ ] The form URL field warns, before sending, that "Recopilar direcciones de correo" must be off in Google Forms.
- [ ] The board can record the response count read from Google Forms and see coverage on the list.
- [ ] `SendFeedbackRequestAsync` and its test no longer exist.
- [ ] `api-endpoints.md` and `data-model.md` are updated.

---

## Open Questions

1. **Guests.** Included as specified, since they attended and their opinion of the camp is as valid as a member's. Excluding them is a one-line filter if the Junta prefers.
2. **Adult age for association surveys.** Uses `AdultMinAge` from `age_ranges`, which exists for camp pricing and may not be the age the association considers a full member. If the statutes say 18 and the pricing setting says something else, this needs its own constant.
3. **Representative email fallback.** Sends two household adults one link at one address. The alternative — treating them as uncontactable — undercounts reach. Current choice favours reach; worth a sanity check with the Junta.
