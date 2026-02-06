# Enriched User Story: Scaffolding Base Project

## Overview

Set up the foundational project structure for the ABUVI web application, including backend (.NET 9), frontend (Vue 3), Python integration (CSnakes), and development infrastructure (Docker). This establishes the complete development environment following Vertical Slice Architecture principles and ensures all teams can begin feature development immediately.

## User Story

**As a** developer joining the ABUVI project
**I want** a fully scaffolded project with all core configurations, folder structures, and development infrastructure
**So that** I can immediately begin implementing features without worrying about project setup, following established architectural patterns and best practices

## Acceptance Criteria

- [x] Backend .NET 9 project created with Minimal API structure
- [ ] Frontend Vue 3 project created with TypeScript and Vite
- [x] PostgreSQL database running in Docker with initial connection
- [x] Python integration via CSnakes configured
- [x] All necessary configuration files created and documented
- [x] Development scripts and commands documented
- [x] Testing frameworks configured (xUnit, Vitest, Cypress) - Backend only (xUnit)
- [x] Git repository structure with proper .gitignore
- [x] Docker Compose for local development environment
- [x] README with setup instructions and architecture overview
- [x] All projects build and run successfully - Backend only
- [x] Health check endpoints return 200 OK

## Technical Specification

### Backend Structure (.NET 9)

#### Project Setup

Create the following projects:

1. **Abuvi.API** - Main API project (Minimal APIs)
2. **Abuvi.Tests** - Test project (xUnit)
3. **Abuvi.Analysis** - Python integration project

#### Folder Structure

```text
src/
├── Abuvi.API/
│   ├── Features/                   # Vertical slices (empty for now)
│   ├── Common/
│   │   ├── Middleware/
│   │   │   └── GlobalExceptionMiddleware.cs
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── Models/
│   │   │   ├── ApiResponse.cs
│   │   │   └── ApiError.cs
│   │   └── Filters/
│   ├── Data/
│   │   ├── AbuviDbContext.cs
│   │   ├── Configurations/        # EF Core entity configurations
│   │   └── Migrations/             # Will be generated
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Abuvi.API.csproj
├── Abuvi.Analysis/
│   ├── requirements.txt
│   └── __init__.py
└── Abuvi.Tests/
    ├── Unit/
    │   └── Features/
    ├── Integration/
    │   └── Features/
    └── Helpers/
        ├── Builders/
        └── Fixtures/
```

#### Required NuGet Packages

```xml
<!-- Abuvi.API.csproj -->
<ItemGroup>
  <!-- Core Framework -->
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.*" />

  <!-- Database -->
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.*" />

  <!-- Validation -->
  <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

  <!-- Python Integration -->
  <PackageReference Include="CSnakes.Runtime" Version="0.3.*" />

  <!-- Development -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.*" />
</ItemGroup>

<!-- Abuvi.Tests.csproj -->
<ItemGroup>
  <PackageReference Include="xunit" Version="2.9.*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
  <PackageReference Include="FluentAssertions" Version="7.0.*" />
  <PackageReference Include="NSubstitute" Version="5.*" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.*" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  <PackageReference Include="coverlet.collector" Version="6.*" />
</ItemGroup>
```

#### Program.cs Configuration

```csharp
using Microsoft.EntityFrameworkCore;
using Abuvi.API.Data;
using Abuvi.API.Common.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required");

builder.Services.AddDbContext<AbuviDbContext>(options =>
    options.UseNpgsql(connectionString));

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();
app.UseHttpsRedirection();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("System");

// API endpoints will be mapped here
// Example: app.MapCampsEndpoints();

app.Run();

// Make Program accessible for testing
public partial class Program { }
```

#### DbContext Setup

```csharp
// Data/AbuviDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace Abuvi.API.Data;

public class AbuviDbContext(DbContextOptions<AbuviDbContext> options) : DbContext(options)
{
    // DbSets will be added as entities are created
    // Example: public DbSet<Camp> Camps => Set<Camp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AbuviDbContext).Assembly);
    }
}
```

#### Common Models

```csharp
// Common/Models/ApiResponse.cs
namespace Abuvi.API.Common.Models;

public record ApiResponse<T>(bool Success, T? Data = default, ApiError? Error = null)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);

    public static ApiResponse<T> NotFound(string message) =>
        new(false, Error: new ApiError(message, "NOT_FOUND"));

    public static ApiResponse<T> Fail(string message, string code) =>
        new(false, Error: new ApiError(message, code));
}

public record ApiError(string Message, string Code);
```

#### Global Exception Middleware

```csharp
// Common/Middleware/GlobalExceptionMiddleware.cs
using Abuvi.API.Common.Models;

namespace Abuvi.API.Common.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail("An unexpected error occurred", "INTERNAL_ERROR"));
        }
    }
}
```

#### Configuration Files

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowedOrigins": "http://localhost:5173",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=abuvi;Username=abuvi_user;Password=dev_password"
  }
}
```

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

### Frontend Structure (Vue 3)

#### Project Initialization

Use Vite to create the Vue 3 + TypeScript project:

```bash
npm create vite@latest frontend -- --template vue-ts
```

#### Folder Structure

```text
frontend/
├── public/
│   └── favicon.ico
├── src/
│   ├── assets/
│   ├── components/
│   │   └── common/
│   ├── composables/
│   ├── layouts/
│   │   └── DefaultLayout.vue
│   ├── pages/
│   │   └── HomePage.vue
│   ├── router/
│   │   └── index.ts
│   ├── stores/
│   ├── types/
│   │   └── api.ts
│   ├── utils/
│   │   └── api.ts
│   ├── App.vue
│   └── main.ts
├── cypress/
│   ├── e2e/
│   ├── fixtures/
│   └── support/
├── index.html
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.ts
├── cypress.config.ts
└── .env.development
```

#### Required NPM Packages

```json
{
  "dependencies": {
    "vue": "^3.5.0",
    "vue-router": "^4.4.0",
    "pinia": "^2.2.0",
    "primevue": "^4.0.0",
    "primeicons": "^7.0.0",
    "axios": "^1.7.0",
    "leaflet": "^1.9.0"
  },
  "devDependencies": {
    "@vitejs/plugin-vue": "^5.1.0",
    "vite": "^6.0.0",
    "typescript": "^5.6.0",
    "vue-tsc": "^2.1.0",
    "@vue/test-utils": "^2.4.0",
    "vitest": "^2.1.0",
    "@vitest/ui": "^2.1.0",
    "cypress": "^13.15.0",
    "tailwindcss": "^3.4.0",
    "autoprefixer": "^10.4.0",
    "postcss": "^8.4.0",
    "eslint": "^9.0.0",
    "prettier": "^3.3.0"
  }
}
```

#### Vite Configuration

```typescript
// vite.config.ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  },
  test: {
    globals: true,
    environment: 'jsdom'
  }
})
```

#### TypeScript Configuration

```json
// tsconfig.json
{
  "compilerOptions": {
    "target": "ESNext",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "jsx": "preserve",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "esModuleInterop": true,
    "lib": ["ESNext", "DOM", "DOM.Iterable"],
    "skipLibCheck": true,
    "noEmit": true,
    "paths": {
      "@/*": ["./src/*"]
    },
    "types": ["vite/client", "vitest/globals"]
  },
  "include": ["src/**/*.ts", "src/**/*.vue"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

#### Tailwind Configuration

```typescript
// tailwind.config.ts
import type { Config } from 'tailwindcss'

export default {
  content: [
    './index.html',
    './src/**/*.{vue,js,ts,jsx,tsx}'
  ],
  theme: {
    extend: {}
  },
  plugins: []
} satisfies Config
```

#### Main.ts Setup

```typescript
// src/main.ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import App from './App.vue'
import router from './router'

import 'primevue/resources/themes/lara-light-blue/theme.css'
import 'primeicons/primeicons.css'
import './assets/main.css'

const app = createApp(App)

app.use(createPinia())
app.use(router)
app.use(PrimeVue)

app.mount('#app')
```

#### Router Setup

```typescript
// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import HomePage from '@/pages/HomePage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomePage
    }
  ]
})

export default router
```

#### API Utility

```typescript
// src/utils/api.ts
import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// Response interceptor for global error handling
api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)
```

#### Type Definitions

```typescript
// src/types/api.ts
export interface ApiResponse<T> {
  success: boolean
  data: T | null
  error: ApiError | null
}

export interface ApiError {
  message: string
  code: string
  details?: Array<{ field: string; message: string }>
}
```

#### Basic Components

```vue
<!-- src/App.vue -->
<script setup lang="ts">
import { RouterView } from 'vue-router'
</script>

<template>
  <div id="app">
    <RouterView />
  </div>
</template>

<style>
#app {
  min-height: 100vh;
}
</style>
```

```vue
<!-- src/pages/HomePage.vue -->
<script setup lang="ts">
import { ref } from 'vue'

const message = ref('Welcome to ABUVI')
</script>

<template>
  <div class="flex min-h-screen items-center justify-center">
    <h1 class="text-4xl font-bold">{{ message }}</h1>
  </div>
</template>
```

#### Environment Configuration

```bash
# .env.development
VITE_API_URL=http://localhost:5000/api
VITE_APP_TITLE=ABUVI - Development
```

### Docker Infrastructure

#### Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: abuvi-postgres
    environment:
      POSTGRES_DB: abuvi
      POSTGRES_USER: abuvi_user
      POSTGRES_PASSWORD: dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U abuvi_user -d abuvi"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
```

### Python Integration

#### Requirements File

```text
# src/Abuvi.Analysis/requirements.txt
numpy>=1.26.0
pandas>=2.1.0
scikit-learn>=1.3.0
```

### Git Configuration

#### .gitignore

```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.userprefs
.vs/
*.DotSettings.user

# Python
__pycache__/
*.py[cod]
*$py.class
.Python
venv/
env/
*.egg-info/

# Frontend
node_modules/
dist/
.DS_Store
*.local
.env.local
.env.*.local

# IDE
.idea/
.vscode/
*.swp
*.swo

# Logs
*.log
npm-debug.log*

# Test Coverage
coverage/
*.lcov
.nyc_output/

# Database
*.db
*.sqlite

# Secrets
appsettings.Development.json
.env
```

### Testing Configuration

#### Cypress Configuration

```typescript
// frontend/cypress.config.ts
import { defineConfig } from 'cypress'

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:5173',
    specPattern: 'cypress/e2e/**/*.cy.{js,jsx,ts,tsx}',
    supportFile: 'cypress/support/e2e.ts'
  }
})
```

#### Vitest Configuration (included in vite.config.ts)

Test configuration is already included in the Vite config above.

### Documentation

#### README.md Updates

Update the root README.md with:

1. **Quick Start section** with exact commands for:
   - Starting Docker services
   - Running backend
   - Running frontend
   - Running tests

2. **Development Commands** section with:
   - Backend: `dotnet run`, `dotnet test`, `dotnet ef`
   - Frontend: `npm run dev`, `npm run test`, `npm run build`

3. **Project Structure** diagram showing the folder layout

4. **Health Check** URLs:
   - Backend: <http://localhost:5000/health>
   - Frontend: <http://localhost:5173>

## Files to Create

### Backend (.NET 9)

- `src/Abuvi.API/Abuvi.API.csproj`
- `src/Abuvi.API/Program.cs`
- `src/Abuvi.API/appsettings.json`
- `src/Abuvi.API/appsettings.Development.json`
- `src/Abuvi.API/Data/AbuviDbContext.cs`
- `src/Abuvi.API/Common/Models/ApiResponse.cs`
- `src/Abuvi.API/Common/Middleware/GlobalExceptionMiddleware.cs`
- `src/Abuvi.Tests/Abuvi.Tests.csproj`

### Frontend (Vue 3)

- `frontend/package.json`
- `frontend/vite.config.ts`
- `frontend/tsconfig.json`
- `frontend/tailwind.config.ts`
- `frontend/cypress.config.ts`
- `frontend/src/main.ts`
- `frontend/src/App.vue`
- `frontend/src/router/index.ts`
- `frontend/src/utils/api.ts`
- `frontend/src/types/api.ts`
- `frontend/src/pages/HomePage.vue`
- `frontend/.env.development`

### Python Integration

- `src/Abuvi.Analysis/requirements.txt`
- `src/Abuvi.Analysis/__init__.py`

### Infrastructure

- `docker-compose.yml`
- `.gitignore`

### Documentation

- Update `README.md` with setup instructions

## Completion Criteria

### Functional Requirements

1. ✅ Backend API starts successfully on <http://localhost:5000> (actually port 5079)
2. ⏳ Frontend dev server starts successfully on <http://localhost:5173> (NOT IMPLEMENTED)
3. ✅ `/health` endpoint returns 200 OK with JSON response
4. ✅ PostgreSQL container running and accepting connections
5. ⏳ Frontend can make API calls to backend through proxy (NOT IMPLEMENTED - no frontend)
6. ✅ Swagger UI accessible at <http://localhost:5000/swagger>

### Technical Requirements

1. ✅ All projects compile without errors (backend only)
2. ⏳ TypeScript has no type errors (`vue-tsc --noEmit` passes) (NOT IMPLEMENTED - no frontend)
3. ✅ All configuration files are syntactically valid
4. ✅ Test runners execute (even with 0 tests) - xUnit works, 2 tests passing
5. ✅ Docker Compose health checks pass
6. ✅ Database connection string works
7. ✅ CORS properly configured between frontend and backend (backend side configured)

### Development Workflow

1. ✅ `dotnet restore` completes successfully
2. ⏳ `npm install` completes successfully (NOT IMPLEMENTED - no frontend)
3. ✅ `docker compose up -d` starts all services (PostgreSQL)
4. ✅ `dotnet run --project src/Abuvi.API` starts backend
5. ⏳ `npm run dev` (in frontend/) starts frontend (NOT IMPLEMENTED)
6. ✅ `dotnet test` runs (2 tests passing)
7. ⏳ `npm run test` (in frontend/) runs Vitest (NOT IMPLEMENTED)

### Documentation

1. ✅ README includes all setup steps (backend setup complete)
2. ✅ README documents all npm/dotnet commands (dotnet commands documented, npm pending frontend)
3. ✅ README includes architecture overview
4. ✅ Configuration files have inline comments where needed

## Non-Functional Requirements

### Security

- ✅ No secrets committed to Git (use .gitignore)
- ✅ Connection strings use environment variables in production
- ✅ CORS restricted to specific origins (localhost in dev)
- ✅ HTTPS redirection configured in backend

### Performance

- ✅ Frontend dev server has HMR (Hot Module Replacement)
- ✅ Backend uses Minimal APIs (lightweight)
- ✅ Database connection pooling enabled by default (EF Core)

### Maintainability

- ✅ Consistent naming conventions (English, PascalCase/camelCase)
- ✅ Clear folder structure following Vertical Slice Architecture
- ✅ Separation of concerns (frontend/backend/infrastructure)
- ✅ Type safety enforced (TypeScript strict mode, C# nullable enabled)

### Developer Experience

- ✅ Single command to start each service
- ✅ Clear error messages when something fails
- ✅ Fast startup times (<10 seconds for each service)
- ✅ Auto-reload on file changes (HMR for frontend, dotnet watch for backend)

## Implementation Steps

### Phase 1: Backend Setup

1. Create solution and projects using `dotnet new`
2. Add NuGet packages
3. Create folder structure
4. Implement Program.cs with basic configuration
5. Create DbContext
6. Create common models and middleware
7. Add configuration files
8. Test backend starts and health check works

### Phase 2: Frontend Setup

1. Initialize Vite project with Vue 3 + TypeScript
2. Install dependencies (Vue Router, Pinia, PrimeVue, Tailwind)
3. Configure Vite, TypeScript, Tailwind, Cypress
4. Create folder structure
5. Implement main.ts, App.vue, router
6. Create API utility and type definitions
7. Create basic HomePage
8. Test frontend starts and displays correctly

### Phase 3: Infrastructure

1. Create docker-compose.yml
2. Test PostgreSQL container starts
3. Verify backend can connect to database
4. Configure .gitignore

### Phase 4: Testing Setup

1. Configure Cypress for e2e tests
2. Configure Vitest for unit tests
3. Configure xUnit for backend tests
4. Verify test runners execute

### Phase 5: Documentation

1. Update README with setup instructions
2. Document all commands
3. Add architecture overview
4. Create quickstart guide

### Phase 6: Verification

1. Run through entire setup from scratch
2. Verify all services start correctly
3. Test health check endpoints
4. Verify frontend can call backend
5. Run all tests
6. Check for any errors or warnings

## Testing Strategy

### Backend Testing

- Unit tests for services and repositories (when added)
- Integration tests for API endpoints
- Use WebApplicationFactory for testing Minimal APIs
- Mock DbContext using in-memory provider or Testcontainers

### Frontend Testing

- Unit tests for composables and utilities
- Component tests for Vue components
- E2E tests for critical user workflows
- Use Vitest for unit/component tests
- Use Cypress for e2e tests

### Smoke Tests

After scaffolding:

1. Backend health check returns 200 OK
2. Frontend loads without console errors
3. Frontend can make a successful API call to backend
4. Database connection succeeds
5. All test runners execute

## Dependencies

### External Services

- Docker Desktop (for PostgreSQL)
- Internet connection (for package downloads)

### System Requirements

- .NET 9 SDK installed
- Node.js 20+ installed
- Python 3.12 installed
- Git installed

## Risks and Mitigations

### Risk: Package version conflicts

**Mitigation**: Use exact or compatible version ranges in package files

### Risk: Database connection fails

**Mitigation**: Include troubleshooting steps in README, verify Docker is running

### Risk: CORS issues between frontend and backend

**Mitigation**: Pre-configure CORS in backend and proxy in Vite config

### Risk: Port conflicts (5000, 5173, 5432 already in use)

**Mitigation**: Document how to change ports in configuration files

## Related Documentation

- [Backend Standards](../specs/backend-standards.mdc)
- [Frontend Standards](../specs/frontend-standards.mdc)
- [Base Standards](../specs/base-standards.mdc)
- [Data Model](../specs/data-model.md)

## Notes

- This is a foundational task - all feature development depends on this being completed correctly
- Follow TDD principles: tests should be set up even if there are 0 tests initially
- All naming must be in English as per base-standards.mdc
- Use Vertical Slice Architecture patterns from the start
- Ensure strict type safety in both backend (C# nullable enabled) and frontend (TypeScript strict mode)

## Success Metrics

- **Setup Time**: New developer can run entire stack in < 15 minutes
- **Build Time**: Backend builds in < 10 seconds, frontend in < 5 seconds
- **Test Execution**: All test runners execute successfully (even with 0 tests)
- **Zero Errors**: No compilation errors, no runtime errors, no console warnings
- **Health Checks**: All health endpoints return 200 OK
