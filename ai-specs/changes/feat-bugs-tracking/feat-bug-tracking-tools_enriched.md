# US: Bug Tracking & User Feedback Tooling Setup

## Context

As the sole developer of Abuvi, I need free tooling that:

1. **Automatically captures and reports bugs** (frontend errors, backend exceptions, browser info, stack traces)
2. **Allows users to leave visual feedback** (annotations, suggestions, pin-on-page comments — similar to BugHerd)
3. Works within a **free tier** suitable for a small user base

Previous experience: BugHerd, Hotjar.

## Current Stack

| Layer | Technology |
|-------|-----------|
| Frontend | Vue.js 3 + TypeScript + Vite |
| Backend | .NET 9 Minimal APIs |
| Database | PostgreSQL 16 |
| Logging | Serilog → PostgreSQL + Seq |
| Deployment | Docker Compose |
| Error Tracking | **None** |
| Frontend Analytics | **None** |

---

## Tools Analysis

### Comparison Table

| Tool | Free Tier | Visual Feedback / Annotations | Auto Error Capture | Self-Hostable | Integration Effort |
|---|---|---|---|---|---|
| **Sentry** | 5K errors/mo, 1 user, 30-day retention | Screenshot + crop only (no annotations yet) | Excellent (Vue + .NET SDKs) | Yes (heavy: 12+ services) | Low |
| **GlitchTip** | 1K events/mo hosted; **unlimited self-hosted** | No | Yes (Sentry-compatible SDKs) | Yes (lightweight: 4 services, 256MB RAM) | Low |
| **PostHog** | 1M events, 5K recordings, 100K exceptions/mo | Surveys only (no visual pins) | Yes (exception autocapture) | Yes (Docker, MIT) | Low |
| **Userback** | 2 users, 2 projects, 7-day feedback visibility | **Yes** (annotated screenshots, recordings) | Yes (browser info, console logs) | No | Low (JS widget) |
| **Marker.io** | No free tier ($39/mo+) | Excellent | Yes | No | Low |
| **BugHerd** | No free tier ($39/mo+) | Excellent (gold standard) | Yes | No | Low |
| **Hotjar** | 35 daily recordings, 3 feedback widgets | Feedback widgets only (no element pinning) | No | No | Low |
| **OpenReplay** | Free self-hosted; cloud $200/mo+ | No | Session replay only | Yes | Medium |
| **Highlight.io** | **Shutting down Feb 28, 2026** | N/A | N/A | N/A | N/A |
| **Instabug/Luciq** | Free trial only | Mobile only | Mobile only | No | N/A |
| **Better Stack** | 1-3 GB logs, 3-day retention | No | Logging/uptime only | No | Medium |

### Key Insight

No single free tool covers both automatic error tracking AND visual user feedback. The solution requires **combining two tools**.

---

## Recommended Options

### Option A: GlitchTip (self-hosted) + Userback (free) — RECOMMENDED

| Aspect | Detail |
|--------|--------|
| **Error Tracking** | GlitchTip self-hosted in Docker (unlimited events, zero cost) |
| **Visual Feedback** | Userback free tier (2 users, 7-day feedback window) |
| **Total Cost** | $0 |
| **Infra Overhead** | Low — GlitchTip needs ~256MB RAM, fits in existing Docker Compose |
| **Trade-off** | Must triage Userback feedback within 7 days |

**Why this is best**: GlitchTip uses Sentry-compatible SDKs (well-documented, mature ecosystem) but runs as a lightweight self-hosted service that fits naturally into the existing Docker Compose setup. Unlimited events with zero cost. Userback is the only free tool that provides BugHerd-style visual annotations.

### Option B: PostHog (cloud free) + Userback (free)

| Aspect | Detail |
|--------|--------|
| **Error Tracking** | PostHog cloud free (100K exceptions/mo, 5K session recordings, 1M analytics events) |
| **Visual Feedback** | Userback free tier |
| **Total Cost** | $0 |
| **Infra Overhead** | None — both are cloud-hosted |
| **Trade-off** | Two external dependencies; PostHog error tracking is newer/less mature than Sentry ecosystem |

### Option C: Sentry (cloud free) + Userback (free)

| Aspect | Detail |
|--------|--------|
| **Error Tracking** | Sentry cloud free (5K errors/mo, 1 user) |
| **Visual Feedback** | Userback free tier |
| **Total Cost** | $0 |
| **Infra Overhead** | None |
| **Trade-off** | Sentry free limited to 1 user and 5K errors; less headroom than GlitchTip self-hosted |

---

## Implementation Plan (Option A — GlitchTip + Userback)

### Step 1: Add GlitchTip to Docker Compose

Add GlitchTip services (web, worker, PostgreSQL for GlitchTip, Redis) to `docker-compose.yml`.

**Files to modify:**

- `docker-compose.yml` — Add GlitchTip services
- `.env` / `docker-compose.override.yml` — Add GlitchTip environment variables

### Step 2: Integrate Sentry SDK in Vue.js Frontend

Install and configure `@sentry/vue` (GlitchTip is Sentry-compatible).

**Files to modify:**

- `frontend/package.json` — Add `@sentry/vue` dependency
- `frontend/src/main.ts` — Initialize Sentry with GlitchTip DSN
- `frontend/vite.config.ts` — Configure source map upload (optional)

**Configuration:**

```typescript
// main.ts
import * as Sentry from "@sentry/vue";

Sentry.init({
  app,
  dsn: "https://<key>@<glitchtip-host>/1",
  environment: import.meta.env.MODE,
  tracesSampleRate: 1.0,
});
```

### Step 3: Integrate Sentry SDK in .NET Backend

Install and configure `Sentry.AspNetCore` (GlitchTip-compatible).

**Files to modify:**

- `src/Abuvi.API/Abuvi.API.csproj` — Add `Sentry.AspNetCore` NuGet package
- `src/Abuvi.API/Program.cs` — Add Sentry middleware
- `src/Abuvi.API/appsettings.json` — Add Sentry DSN configuration

**Configuration:**

```csharp
// Program.cs
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"];
    o.TracesSampleRate = 1.0;
    o.Environment = builder.Environment.EnvironmentName;
});

app.UseSentryTracing();
```

### Step 4: Embed Userback Widget in Frontend

**Files to modify:**

- `frontend/index.html` — Add Userback script tag
- OR `frontend/src/main.ts` — Programmatic initialization

**Configuration:**

```html
<!-- index.html -->
<script>
  Userback = window.Userback || {};
  Userback.access_token = '<YOUR_USERBACK_TOKEN>';
  (function(d) {
    var s = d.createElement('script');
    s.async = true;
    s.src = 'https://static.userback.io/widget/v1.js';
    (d.head || d.body).appendChild(s);
  })(document);
</script>
```

### Step 5: Verify and Test

1. Trigger a test error in the frontend → verify it appears in GlitchTip
2. Trigger a test exception in the backend → verify it appears in GlitchTip
3. Submit visual feedback via Userback widget → verify it appears in Userback dashboard
4. Verify source maps are resolving stack traces correctly (optional)

---

## Acceptance Criteria

- [ ] GlitchTip is running as part of the Docker Compose stack
- [ ] Frontend errors are automatically captured and visible in GlitchTip dashboard
- [ ] Backend exceptions are automatically captured and visible in GlitchTip dashboard
- [ ] Userback widget is visible on the frontend for authenticated users
- [ ] Users can annotate screenshots and submit visual feedback via Userback
- [ ] Feedback submissions appear in the Userback dashboard
- [ ] Error alerts are configured (email notifications from GlitchTip)
- [ ] Environment variables for DSNs/tokens are properly externalized (not hardcoded)

## Non-Functional Requirements

- **Security**: DSNs and tokens must be stored in environment variables, never committed to source control
- **Performance**: Sentry SDK must not degrade frontend load time by more than 50ms
- **Privacy**: No PII should be sent to external services (Userback). Configure Sentry's `beforeSend` to scrub sensitive data
- **Availability**: GlitchTip failure must not affect the main application (SDK operates in fire-and-forget mode)

## Documentation Updates

- [ ] Update `ai-specs/specs/development_guide.md` with error tracking setup instructions
- [ ] Add GlitchTip access info to team documentation
- [ ] Document how to triage Userback feedback within the 7-day window
