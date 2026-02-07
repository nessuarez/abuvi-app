# Users API Documentation

## Overview

The Users API provides CRUD operations for managing user accounts in the ABUVI application. This feature is implemented following Vertical Slice Architecture principles with .NET 9 Minimal APIs.

## Architecture

### Feature Slice Structure

```
src/Abuvi.API/Features/Users/
├── UsersModels.cs         # Entity and DTOs
├── UserConfiguration.cs    # EF Core entity configuration (in Data/Configurations/)
├── IUsersRepository.cs    # Repository interface
├── UsersRepository.cs     # Repository implementation
├── UsersService.cs        # Business logic
├── UsersValidators.cs     # FluentValidation validators
└── UsersEndpoints.cs      # Minimal API endpoints
```

## API Endpoints

Base URL: `/api/users`

### 1. Get All Users

**Endpoint:** `GET /api/users`

**Query Parameters:**
- `skip` (optional, default: 0) - Number of records to skip for pagination
- `take` (optional, default: 100) - Number of records to take

**Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "phone": "+1234567890",
      "role": "Member",
      "isActive": true,
      "createdAt": "2024-01-15T10:30:00Z",
      "updatedAt": "2024-01-15T10:30:00Z"
    }
  ],
  "error": null
}
```

### 2. Get User by ID

**Endpoint:** `GET /api/users/{id}`

**Path Parameters:**
- `id` (UUID) - User unique identifier

**Response:** `200 OK` / `404 Not Found`
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+1234567890",
    "role": "Member",
    "isActive": true,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  },
  "error": null
}
```

**Error Response (404):**
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "User with ID {id} not found",
    "code": "NOT_FOUND",
    "details": null
  }
}
```

### 3. Create User

**Endpoint:** `POST /api/users`

**Request Body:**
```json
{
  "email": "newuser@example.com",
  "password": "Password123!",
  "firstName": "John",
  "lastName": "Doe",
  "phone": "+1234567890",
  "role": "Member"
}
```

**Validation Rules:**
- `email`: Required, valid email format, max 255 characters, must be unique
- `password`: Required, min 8 characters, max 100 characters
- `firstName`: Required, max 100 characters
- `lastName`: Required, max 100 characters
- `phone`: Optional, max 20 characters, valid phone format
- `role`: Required, must be one of: `Admin`, `Board`, `Member`

**Response:** `201 Created` / `400 Bad Request` / `409 Conflict`

**Success Response (201):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "newuser@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+1234567890",
    "role": "Member",
    "isActive": true,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  },
  "error": null
}
```

**Validation Error Response (400):**
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Validation failed",
    "code": "VALIDATION_ERROR",
    "details": [
      {
        "field": "Email",
        "message": "Email must be a valid email address"
      }
    ]
  }
}
```

**Duplicate Email Response (409):**
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "A user with this email already exists",
    "code": "DUPLICATE_EMAIL",
    "details": null
  }
}
```

### 4. Update User

**Endpoint:** `PUT /api/users/{id}`

**Path Parameters:**
- `id` (UUID) - User unique identifier

**Request Body:**
```json
{
  "firstName": "Jane",
  "lastName": "Smith",
  "phone": "+9876543210",
  "isActive": false
}
```

**Validation Rules:**
- `firstName`: Required, max 100 characters
- `lastName`: Required, max 100 characters
- `phone`: Optional, max 20 characters, valid phone format
- `isActive`: Required, boolean

**Response:** `200 OK` / `400 Bad Request` / `404 Not Found`

**Success Response (200):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "firstName": "Jane",
    "lastName": "Smith",
    "phone": "+9876543210",
    "role": "Member",
    "isActive": false,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T12:45:00Z"
  },
  "error": null
}
```

### 5. Delete User

**Endpoint:** `DELETE /api/users/{id}`

**Path Parameters:**
- `id` (UUID) - User unique identifier

**Response:** `204 No Content` / `404 Not Found`

**Error Response (404):**
```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "User with ID {id} not found",
    "code": "NOT_FOUND",
    "details": null
  }
}
```

## Data Model

### User Entity

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UUID | PRIMARY KEY | Unique identifier |
| email | VARCHAR(255) | NOT NULL, UNIQUE | User email address |
| password_hash | TEXT | NOT NULL | Hashed password (SHA-256 placeholder) |
| first_name | VARCHAR(100) | NOT NULL | User first name |
| last_name | VARCHAR(100) | NOT NULL | User last name |
| phone | VARCHAR(20) | NULL | User phone number |
| role | VARCHAR(20) | NOT NULL | User role (Admin, Board, Member) |
| family_unit_id | UUID | NULL | Reference to family unit |
| is_active | BOOLEAN | NOT NULL, DEFAULT TRUE | User active status |
| created_at | TIMESTAMP | NOT NULL, DEFAULT NOW() | Creation timestamp |
| updated_at | TIMESTAMP | NOT NULL, DEFAULT NOW() | Last update timestamp |

### UserRole Enum

- `Admin` - Administrator with full access
- `Board` - Board member with management access
- `Member` - Regular member with limited access

## Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| NOT_FOUND | 404 | Resource not found |
| VALIDATION_ERROR | 400 | Request validation failed |
| DUPLICATE_EMAIL | 409 | Email already exists in the system |

## Security Considerations

⚠️ **Important:** The current implementation uses SHA-256 for password hashing as a placeholder. This will be replaced with BCrypt in Phase 2 for proper security.

## Testing

### Unit Tests ✅

Location: `src/Abuvi.Tests/Unit/Features/`

- `UsersServiceTests.cs` - 11 tests for business logic in UsersService
- `UsersValidatorsTests.cs` - 26 tests for FluentValidation validators

**Status:** ✅ All 37 unit tests passing (100%)

### Integration Tests

**Status:** ⚠️ Deferred to Phase 2

Integration tests will be implemented in Phase 2 using Testcontainers with real PostgreSQL. See [Phase 1 Testing Status](../testing/phase1-users-testing-status.md) for details.

### Running Tests

```bash
# Run all unit tests
cd src/Abuvi.Tests
dotnet test --filter "FullyQualifiedName~Unit"

# Run Users unit tests only
dotnet test --filter "FullyQualifiedName~Unit.Features.Users"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReporter=html
```

### Manual Testing

Use Swagger UI at `http://localhost:5000/swagger` for manual endpoint testing.

## Database Migration

### Migration Name
`20260207224928_AddUsersTable`

### Applying Migration

```bash
cd src/Abuvi.API
dotnet ef database update
```

### Rollback Migration

```bash
cd src/Abuvi.API
dotnet ef migrations remove
```

## Service Registration

Services are registered in `Program.cs`:

```csharp
// Feature Services - Users
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<UsersService>();

// Endpoints
app.MapUsersEndpoints();
```

## Dependencies

- **FluentValidation** - Request validation
- **Entity Framework Core** - Data access
- **Npgsql** - PostgreSQL provider
- **Microsoft.AspNetCore.Mvc.Testing** - Integration testing (test project)
- **NSubstitute** - Mocking framework (test project)
- **FluentAssertions** - Test assertions (test project)

## Future Enhancements (Phase 2+)

1. Replace SHA-256 with BCrypt for password hashing
2. Add authentication and authorization
3. Add email verification flow
4. Add password reset functionality
5. Add user profile photo upload
6. Add audit logging for user changes
7. Add soft delete instead of hard delete
8. Add user search and filtering capabilities
