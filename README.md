# **ABUVI Web Platform**

A modern, high-performance web platform for the **ABUVI Association**, designed to manage memberships, camp registrations, and preserve the association's 50-year historical archive.

## **🚀 Spec-Driven Development (SDD)**

This project is a showcase of **Spec-Driven Development** assisted by AI Agents (Google Antigravity, Cursor, or Windsurf). Unlike traditional development, here the **Specification is the Source of Truth**.

### **How it works:**

1. **Specs First**: No code is written before defining the feature or standard in `ai-specs/specs/`.
2. **Context-Aware Agents**: We use `.mdc` files (Model Context Rules) to enforce architectural standards (Vertical Slices, Clean Code, Security) directly into the AI's reasoning engine.
3. **Verifiable Progress**: Each feature starts with a "Change Plan" in `ai-specs/changes/`, ensuring the AI follows a logical and documented execution path.
4. **AI Orchestration**: The developer acts as a **Software Architect**, guiding agents through specialized skills defined in the `.agent/` directory.

## **🛠 Tech Stack**

### **Backend**

* **Core**: .NET 9 (Minimal APIs)
* **Database**: PostgreSQL 16 + Entity Framework Core
* **AI/Data Science**: Python 3.12 integration via **CSnakes**
* **Architecture**: Vertical Slice Architecture

### **Frontend**

* **Framework**: Vue 3 (Composition API) + TypeScript
* **UI Toolkit**: PrimeVue + Tailwind CSS
* **Build Tool**: Vite

### **Infrastructure & Integration**

* **Containerization**: Docker & Docker Compose
* **Payments**: Redsys Integration (SHA-256)
* **AI Tooling**: Google Antigravity / Cursor

## **📁 Project Structure**

```text
abuvi-app/                  # Root directory
├── .agent/                 # AI Skills and agent-specific configurations
├── ai-specs/               # SDD Core (The "Brain" of the project)
│   ├── specs/              # Architectural & Feature specifications (.mdc, .md)
│   └── changes/            # Execution plans for specific tasks (Change Plans)
├── src/
│   ├── Abuvi.API/          # .NET 9 Backend (Vertical Slices)
│   ├── Abuvi.Web/          # Vue 3 Frontend
│   └── Abuvi.Analysis/     # Python Data Analysis modules
├── docker-compose.yml      # Local development infrastructure
└── README.md               # You are here
```

## **⚙️ Getting Started**

### **Prerequisites**

* .NET 9 SDK
* Node.js 20+
* Python 3.12
* Docker Desktop

### **Installation**

1. **Clone the repository**:

   ```bash
   git clone https://github.com/your-user/abuvi-app.git
   ```

2. **Setup the infrastructure**:

   ```bash
   docker-compose up -d
   ```

3. **Initialize the Backend**:

   ```bash
   dotnet run --project src/Abuvi.API
   ```

4. **Initialize the Frontend**:

   ```bash
   cd frontend && npm install && npm run dev
   ```

## **🏃 Quick Start (Current Backend Scaffolding)**

### Running the Backend API

1. **Start PostgreSQL**:

   ```bash
   docker compose up -d
   ```

2. **Run database migrations** (first time only):

   ```bash
   dotnet ef database update --project src/Abuvi.API
   ```

3. **Configure JWT secret** (first time only):

   ```bash
   cd src/Abuvi.API
   dotnet user-secrets set "Jwt:Secret" "your-strong-secret-key-at-least-32-characters-long-change-this"
   ```

4. **Configure Resend email service** (first time only):

   The application uses [Resend](https://resend.com) for sending transactional emails (verification emails, password resets, camp confirmations, payment receipts, etc.).

   ```bash
   cd src/Abuvi.API
   # Get your API key from https://resend.com/api-keys
   dotnet user-secrets set "Resend:ApiKey" "re_your_api_key_here"

   # Optional: Customize sender email (default: noreply@abuvi.org)
   dotnet user-secrets set "Resend:FromEmail" "noreply@yourdomain.com"
   dotnet user-secrets set "Resend:FromName" "Your Organization Name"
   ```

   **Email Types Sent:**
   - **Verification Email**: On user registration (blocks account activation until verified)
   - **Welcome Email**: After successful email verification
   - **Password Reset**: On password reset request
   - **Camp Registration Confirmation**: After successful camp booking
   - **Payment Receipt**: After successful payment
   - **Event Reminders**: Before upcoming events
   - **Feedback Requests**: After camp completion
   - **Camp Update Notifications**: When camp details change

   **Troubleshooting:**
   - **Emails not sending**: Verify API key is configured correctly
   - **401 Unauthorized**: API key is invalid, regenerate in Resend dashboard
   - **403 Forbidden**: Domain not verified (production only)
   - **429 Rate Limit**: Free tier limit reached (100/day), upgrade plan or wait 24h

5. **Start the Backend API**:

   ```bash
   dotnet run --project src/Abuvi.API
   ```

5. **Verify the application**:
   - API: http://localhost:5079/health
   - Swagger UI: http://localhost:5079/swagger

## **💻 Development Commands**

### Backend (.NET)

- `dotnet restore` - Restore NuGet packages
- `dotnet build` - Build the solution
- `dotnet run --project src/Abuvi.API` - Run the API
- `dotnet test` - Run all tests
- `dotnet ef migrations add <MigrationName> --project src/Abuvi.API` - Create a new migration
- `dotnet ef database update --project src/Abuvi.API` - Apply migrations to the database
- `dotnet ef migrations list --project src/Abuvi.API` - List all migrations

### Frontend (Vue 3)

- `cd frontend && npm install` - Install dependencies
- `npm run dev` - Start development server (http://localhost:5173)
- `npm run build` - Build for production
- `npm run test` - Run Vitest unit tests in watch mode
- `npm run test:run` - Run tests once
- `npm run test:ui` - Run tests with Vitest UI
- `npm run cypress` - Open Cypress E2E test runner
- `npm run cypress:run` - Run Cypress tests headlessly
- `npm run lint` - Lint code
- `npm run format` - Format code with Prettier

### Database (Docker)

- `docker compose up -d` - Start PostgreSQL in detached mode
- `docker compose down` - Stop PostgreSQL
- `docker compose ps` - Check container status
- `docker compose logs postgres` - View PostgreSQL logs

### Testing

- `dotnet test` - Run all tests
- `dotnet test --logger "console;verbosity=detailed"` - Run tests with detailed output
- `dotnet test --collect:"XPlat Code Coverage"` - Run tests with code coverage

## **📂 Actual Project Structure**

```text
abuvi-app/
├── src/
│   ├── Abuvi.API/              # Backend .NET 9 API
│   │   ├── Features/           # Vertical slice features (empty, ready for features)
│   │   ├── Common/             # Cross-cutting concerns
│   │   │   ├── Middleware/     # Global middleware (exception handling)
│   │   │   ├── Models/         # Shared models (ApiResponse, ApiError)
│   │   │   ├── Extensions/     # Service extensions (empty)
│   │   │   └── Filters/        # Action filters (empty)
│   │   ├── Data/               # EF Core DbContext & migrations
│   │   │   ├── Configurations/ # Entity configurations (empty)
│   │   │   └── Migrations/     # EF Core migrations
│   │   ├── Program.cs          # Application entry point
│   │   └── appsettings.json    # Application configuration
│   ├── Abuvi.Analysis/         # Python integration (CSnakes)
│   │   ├── requirements.txt    # Python dependencies
│   │   └── __init__.py         # Python module initialization
│   └── Abuvi.Tests/            # xUnit test project
│       ├── Unit/               # Unit tests
│       ├── Integration/        # Integration tests (HealthCheckTests)
│       └── Helpers/            # Test utilities
│           ├── Builders/       # Test data builders (empty)
│           └── Fixtures/       # Test fixtures (empty)
├── frontend/                   # Vue 3 Frontend
│   ├── src/
│   │   ├── assets/             # Images, fonts, styles
│   │   ├── components/         # Vue components
│   │   │   └── common/         # Shared components
│   │   ├── composables/        # Reusable logic (useXxx)
│   │   ├── layouts/            # Page layouts
│   │   ├── pages/              # Route-level pages
│   │   │   └── HomePage.vue    # Home page component
│   │   ├── router/             # Vue Router config
│   │   │   └── index.ts        # Router configuration
│   │   ├── stores/             # Pinia state stores
│   │   ├── types/              # TypeScript types
│   │   │   └── api.ts          # API response types
│   │   ├── utils/              # Utilities (API client)
│   │   │   └── api.ts          # Axios API client
│   │   ├── App.vue             # Root component
│   │   └── main.ts             # Entry point
│   ├── cypress/                # E2E tests
│   │   ├── e2e/                # E2E test specs
│   │   ├── fixtures/           # Test data
│   │   └── support/            # Cypress commands
│   ├── index.html              # HTML entry
│   ├── vite.config.ts          # Vite configuration
│   ├── tsconfig.json           # TypeScript config
│   ├── tailwind.config.js      # Tailwind configuration
│   └── package.json            # NPM dependencies
├── ai-specs/                   # Specifications (SDD approach)
│   ├── .agents/                # AI agent configurations
│   ├── .commands/              # Custom AI commands
│   ├── specs/                  # Core specifications
│   └── changes/                # Change plans
├── docker-compose.yml          # PostgreSQL container
├── Abuvi.sln                   # .NET solution file
└── README.md                   # This file
```

## **✨ Implemented Features**

### Phase 1: User Management (CRUD)

* **Backend** (`feature/phase1-user-crud-backend`):
  * User entity with roles (Admin, Board, Member)
  * REST API endpoints: GET, POST, PUT for users
  * Vertical Slice Architecture implementation
  * Comprehensive unit and integration tests

* **Frontend** (`feature/phase1-user-crud-frontend`):
  * User list page with DataTable (pagination, sorting)
  * User detail page with view/edit modes
  * Create user dialog with validation
  * Composable-based API communication
  * PrimeVue + Tailwind CSS styling
  * Comprehensive Vitest + Cypress tests

### Phase 2: Authentication Layer ✅ **COMPLETED**

* **Backend** (`feature/phase2-authentication-backend`):
  * BCrypt password hashing (work factor 12)
  * JWT token generation with configurable expiry (24h)
  * Authentication middleware with Bearer token validation
  * Login endpoint: POST /api/auth/login
  * Register endpoint: POST /api/auth/register
  * Role-based authorization (Admin, Board, Member)
  * Protected user endpoints with JWT authentication
  * Comprehensive unit tests (67 tests) and integration tests (47 tests)
  * **Total: 114/114 tests passing (100% success rate)**

* **Security Features**:
  * JWT secret stored in user-secrets (not in source code)
  * Password requirements: min 8 chars, uppercase, lowercase, number
  * Token validation: Issuer, Audience, Lifetime, SigningKey
  * Role-based access control on all endpoints
  * Active user status verification on login

* **API Endpoints**:
  * Public: POST /api/auth/register, POST /api/auth/login
  * Authenticated: GET /api/users/{id}, PUT /api/users/{id}
  * Admin Only: GET /api/users, POST /api/users, DELETE /api/users/{id}

**Next**: Phase 3 will implement frontend authentication integration.

## **🔍 Health Checks & Endpoints**

* **Frontend**: <http://localhost:5173>
  * Home: Displays "Welcome to ABUVI" message
  * Users: <http://localhost:5173/users> (User management interface)
* **Backend API Health Check**: <http://localhost:5079/health>
  * Returns: `{"status":"healthy","timestamp":"2026-02-06T..."}`
* **Backend API Endpoints**:
  * **Authentication (Public)**:
    * `POST /api/auth/register` - Register new user
    * `POST /api/auth/login` - Login and receive JWT token
  * **Users (Protected)**:
    * `GET /api/users` - Get all users (Admin only)
    * `GET /api/users/:id` - Get user by ID (Authenticated)
    * `POST /api/users` - Create new user (Admin only)
    * `PUT /api/users/:id` - Update user (Authenticated)
    * `DELETE /api/users/:id` - Delete user (Admin only)
* **Swagger UI (API Documentation)**: <http://localhost:5079/swagger>
* **PostgreSQL Database**: `localhost:5432`
  * Database: `abuvi`
  * Username: `abuvi_user`
  * Password: `dev_password` (local dev only)

## **🛠️ Troubleshooting**

### Port 5432 already in use

If PostgreSQL port is already in use, change the port mapping in `docker-compose.yml`:

```yaml
ports:
  - "5433:5432"  # Changed host port to 5433
```

Then update the connection string in `src/Abuvi.API/appsettings.json`:

```json
"DefaultConnection": "Host=localhost;Port=5433;Database=abuvi;Username=abuvi_user;Password=dev_password"
```

### Docker not running

Ensure Docker Desktop is running:

```bash
docker ps  # Should not error
```

### Database connection failed

1. Check PostgreSQL is running: `docker compose ps`
2. Verify health status shows "healthy"
3. Check connection string in `appsettings.json` matches `docker-compose.yml` environment variables

### Build errors related to .NET version

This project targets .NET 9, but can be built with .NET 10 SDK (backward compatible). If you encounter framework errors:

1. Check installed SDKs: `dotnet --list-sdks`
2. Verify you have .NET 9.0.x SDK installed
3. If needed, create `global.json` to pin SDK version

### EF Core migrations fail

Ensure PostgreSQL is running before creating or applying migrations:

```bash
docker compose ps  # Should show "healthy" status
```

## **📄 License**

This project is licensed under the **Apache License 2.0** - see the [LICENSE](LICENSE) file for details. This license allows for community use and transparency while protecting the core architectural patterns and the non-profit organization's brand.

*Developed with ❤️ for ABUVI using Spec-Driven Development methodology.*
