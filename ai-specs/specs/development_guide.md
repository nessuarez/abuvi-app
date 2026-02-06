# Development Guide

This guide provides step-by-step instructions for setting up the development environment, running the application, and executing tests for the ABUVI web platform.

## Prerequisites

Ensure you have the following installed:

- **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Node.js** (v20 or higher) and **npm** (v10 or higher)
- **Docker** and **Docker Compose**
- **Git**
- **Python 3.12** (for CSnakes integration)
- **EF Core Tools** (`dotnet tool install --global dotnet-ef`)

## 1. Clone the Repository

```bash
git clone <repository-url>
cd abuvi-app
```

## 2. Infrastructure Setup (Docker)

Start PostgreSQL and supporting services with Docker Compose:

```bash
# Start all containers (PostgreSQL, pgAdmin, MinIO)
docker-compose up -d

# Verify containers are running
docker-compose ps
```

Services available after startup:

| Service | URL / Connection | Credentials |
|---------|-----------------|-------------|
| **PostgreSQL 16** | `localhost:5432` | See `docker-compose.yml` |
| **pgAdmin 4** | `http://localhost:5050` | See `docker-compose.yml` |
| **MinIO** (S3-compatible storage) | `http://localhost:9000` | See `docker-compose.yml` |

## 3. Backend Setup (.NET 9)

### Environment Configuration

Configure local secrets using .NET User Secrets (never commit credentials):

```bash
cd src/Abuvi.API

# Initialize user secrets
dotnet user-secrets init

# Set connection string
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=AbuviDb;Username=abuvi_user;Password=your_password"

# Set Redsys keys (if testing payments)
dotnet user-secrets set "Redsys:SecretKey" "your_redsys_test_key"
dotnet user-secrets set "Redsys:MerchantCode" "your_merchant_code"
```

### Install Dependencies and Build

```bash
# From project root
dotnet restore
dotnet build
```

### Database Setup

```bash
# Apply EF Core migrations to create/update the database schema
dotnet ef database update --project src/Abuvi.API

# (Optional) Verify migrations status
dotnet ef migrations list --project src/Abuvi.API
```

### Run the Backend

```bash
dotnet run --project src/Abuvi.API
```

The backend API will be available at `http://localhost:5000`. Swagger/OpenAPI documentation is available at `http://localhost:5000/swagger`.

## 4. Frontend Setup (Vue 3)

```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Start the development server with HMR
npm run dev
```

The frontend application will be available at `http://localhost:5173`.

### Environment Configuration

Create a `.env.development` file in the `frontend/` directory:

```env
VITE_API_URL=http://localhost:5000/api
VITE_APP_TITLE=ABUVI - Development
```

## 5. Python Setup (CSnakes)

For data analysis features that use Python via CSnakes:

```bash
# Navigate to analysis directory
cd src/Abuvi.Analysis

# Create a virtual environment
python -m venv .venv

# Activate the virtual environment
# On Windows:
.venv\Scripts\activate
# On macOS/Linux:
source .venv/bin/activate

# Install Python dependencies
pip install -r requirements.txt
```

## Running Tests

### Backend Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests for a specific project
dotnet test src/Abuvi.Tests
```

### Frontend Tests

```bash
cd frontend

# Run unit and component tests with Vitest
npx vitest

# Run tests in watch mode
npx vitest --watch

# Run tests with coverage
npx vitest --coverage

# Open Cypress E2E test runner (interactive)
npx cypress open

# Run Cypress E2E tests headlessly
npx cypress run
```

## Common Tasks

### Creating a New EF Core Migration

```bash
# After modifying entity configurations
dotnet ef migrations add DescriptiveMigrationName --project src/Abuvi.API

# Review the generated migration file before applying
# Then apply it
dotnet ef database update --project src/Abuvi.API
```

### Resetting the Database

```bash
# Drop and recreate the database
dotnet ef database drop --project src/Abuvi.API --force
dotnet ef database update --project src/Abuvi.API
```

### Adding a New Feature (Vertical Slice)

Create the following files in `src/Abuvi.API/Features/[FeatureName]/`:

1. `[Feature]Endpoints.cs` - Minimal API endpoint definitions
2. `[Feature]Models.cs` - Request/Response DTOs and domain entity
3. `[Feature]Service.cs` - Business logic
4. `[Feature]Repository.cs` - Data access interface and implementation
5. `[Feature]Validator.cs` - FluentValidation rules (if needed)

Register the endpoints in `Program.cs`:

```csharp
app.Map[Feature]Endpoints();
```

Register services in `Program.cs`:

```csharp
builder.Services.AddScoped<I[Feature]Repository, [Feature]Repository>();
builder.Services.AddScoped<[Feature]Service>();
```

### Building for Production

```bash
# Backend
dotnet publish src/Abuvi.API -c Release -o publish/

# Frontend
cd frontend
npm run build
```

## Troubleshooting

### PostgreSQL connection refused

Ensure Docker containers are running:

```bash
docker-compose ps
docker-compose logs postgres
```

### EF Core migration errors

Check that the connection string is correctly configured in user secrets and that the database is accessible:

```bash
dotnet ef database update --project src/Abuvi.API --verbose
```

### Frontend proxy not working

Verify that the backend is running on the expected port and that `vite.config.ts` proxy settings match:

```typescript
// vite.config.ts
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true
    }
  }
}
```

### Python/CSnakes integration issues

Ensure Python 3.12 is installed and the virtual environment is activated. Check that the CSnakes NuGet package version matches your Python version.
