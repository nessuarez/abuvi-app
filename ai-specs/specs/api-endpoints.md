****# API Endpoints Documentation

This document describes the REST API endpoints for the ABUVI web application.

## Base URL

- **Development**: `http://localhost:5079/api`
- **Production**: TBD

---

## System Endpoints

### GET /health

Returns the health status of the API and its external dependencies. Does not require authentication.

**Response — All healthy (HTTP 200):**

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0523416",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "Host=localhost;Database=abuvi",
      "duration": "00:00:00.0412341",
      "data": {}
    },
    "resend": {
      "status": "Healthy",
      "description": "Resend API key is configured",
      "duration": "00:00:00.0000123",
      "data": {}
    }
  }
}
```

**Response — Dependency unavailable (HTTP 503):**

```json
{
  "status": "Unhealthy",
  "totalDuration": "00:00:05.0001234",
  "entries": {
    "database": {
      "status": "Unhealthy",
      "description": "Exception during check: ...",
      "duration": "00:00:05.0001123",
      "data": {}
    },
    "resend": {
      "status": "Healthy",
      "description": "Resend API key is configured",
      "duration": "00:00:00.0000101",
      "data": {}
    }
  }
}
```

**HTTP Status Codes:**

| Overall Status | HTTP Code | Meaning                                            |
| -------------- | --------- | -------------------------------------------------- |
| `Healthy`      | 200       | All checks pass                                    |
| `Degraded`     | 200       | Non-critical issue (e.g. Resend not configured)    |
| `Unhealthy`    | 503       | Critical dependency unavailable (e.g. DB down)     |

**Checks included:**

| Check name      | Failure status | What it verifies                                                 |
| --------------- | -------------- | ---------------------------------------------------------------- |
| `database`      | `Unhealthy`    | PostgreSQL connectivity via `SELECT 1` (5s timeout)              |
| `resend`        | `Degraded`     | Resend API key is configured in settings                         |
| `google-places` | `Degraded`     | Google Places API key is configured                              |
| `seq`           | `Degraded`     | Seq logging server is reachable at configured URL                |
| `blob-storage`  | `Degraded`     | S3-compatible bucket is reachable; quota thresholds are monitored |

## Response Format

All API responses follow a consistent envelope format:

### Success Response

```json
{
  "success": true,
  "data": { /* response payload */ }
}
```

### Error Response

```json
{
  "success": false,
  "error": {
    "message": "Human-readable error message",
    "code": "ERROR_CODE",
    "details": { /* optional validation details */ }
  }
}
```

---

## Authentication Endpoints

### POST /api/auth/register-user

Registers a new user with email verification workflow.

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "Password123!@#",
  "firstName": "John",
  "lastName": "Doe",
  "documentNumber": "12345678A",  // optional
  "phone": "+34612345678",        // optional
  "acceptedTerms": true
}
```

**Validation Rules:**

- `email`: Required, valid email format, max 255 characters, must be unique
- `password`: Required, min 8 characters, must contain:
  - At least one uppercase letter
  - At least one lowercase letter
  - At least one digit
  - At least one special character (@$!%*?&#)
- `firstName`: Required, max 100 characters
- `lastName`: Required, max 100 characters
- `documentNumber`: Optional, max 50 characters, uppercase alphanumeric only, unique when provided
- `phone`: Optional, E.164 format (e.g., +34612345678)
- `acceptedTerms`: Required, must be `true`

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+34612345678",
    "role": "Member",
    "isActive": false,
    "emailVerified": false,
    "createdAt": "2026-02-12T12:00:00Z",
    "updatedAt": "2026-02-12T12:00:00Z"
  }
}
```

**Error Responses:**

- **400 Bad Request** - Validation failed

  ```json
  {
    "success": false,
    "error": {
      "message": "Validation failed",
      "code": "VALIDATION_ERROR",
      "details": {
        "Password": ["Password must be at least 8 characters"]
      }
    }
  }
  ```

- **400 Bad Request** - Duplicate email

  ```json
  {
    "success": false,
    "error": {
      "message": "An account with this email already exists",
      "code": "EMAIL_EXISTS"
    }
  }
  ```

- **400 Bad Request** - Duplicate document number

  ```json
  {
    "success": false,
    "error": {
      "message": "An account with this document number already exists",
      "code": "DOCUMENT_EXISTS"
    }
  }
  ```

**Notes:**

- User account starts with `isActive: false` and `emailVerified: false`
- A verification email is sent with a token (24-hour expiration)
- User must verify email before logging in

---

### POST /api/auth/verify-email

Verifies user's email address using the token sent via email.

**Request Body:**

```json
{
  "token": "GKzE7Z19LDKOQb0oa0nvjXL3yXXhBu9L_qmmF8-R1Q8="
}
```

**Validation Rules:**

- `token`: Required, URL-safe base64 string

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "message": "Email verified successfully"
  }
}
```

**Error Responses:**

- **404 Not Found** - Invalid token

  ```json
  {
    "success": false,
    "error": {
      "message": "User with ID '00000000-0000-0000-0000-000000000000' was not found",
      "code": "NOT_FOUND"
    }
  }
  ```

- **400 Bad Request** - Expired token

  ```json
  {
    "success": false,
    "error": {
      "message": "Verification token has expired",
      "code": "VERIFICATION_FAILED"
    }
  }
  ```

**Notes:**

- Once verified, both `emailVerified` and `isActive` become `true`
- User can then log in normally
- Token can only be used once

---

### POST /api/auth/resend-verification

Resends the email verification link to the user.

**Request Body:**

```json
{
  "email": "user@example.com"
}
```

**Validation Rules:**

- `email`: Required, valid email format

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "message": "Verification email sent"
  }
}
```

**Error Responses:**

- **404 Not Found** - Email not found

  ```json
  {
    "success": false,
    "error": {
      "message": "User with ID '00000000-0000-0000-0000-000000000000' was not found",
      "code": "NOT_FOUND"
    }
  }
  ```

- **400 Bad Request** - Email already verified

  ```json
  {
    "success": false,
    "error": {
      "message": "Email is already verified",
      "code": "RESEND_FAILED"
    }
  }
  ```

**Notes:**

- Generates a new verification token (invalidates previous one)
- New token expires 24 hours from generation
- Can only resend for unverified accounts

---

### POST /api/auth/login

Authenticates a user and returns a JWT token.

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "Password123!@#"
}
```

**Validation Rules:**

- `email`: Required, valid email format
- `password`: Required

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "user": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "Member"
    }
  }
}
```

**Error Responses:**

- **401 Unauthorized** - Invalid credentials

  ```json
  {
    "success": false,
    "error": {
      "message": "Invalid email or password",
      "code": "INVALID_CREDENTIALS"
    }
  }
  ```

- **401 Unauthorized** - Email not verified

  ```json
  {
    "success": false,
    "error": {
      "message": "Email not verified. Please check your email for verification link.",
      "code": "EMAIL_NOT_VERIFIED"
    }
  }
  ```

- **401 Unauthorized** - Account inactive

  ```json
  {
    "success": false,
    "error": {
      "message": "Account is not active",
      "code": "ACCOUNT_INACTIVE"
    }
  }
  ```

**Notes:**

- JWT token expires after 24 hours (configurable)
- Token must be included in `Authorization: Bearer <token>` header for protected endpoints
- User must have verified email and active account to log in

---

### POST /api/auth/forgot-password

Initiates a password reset. Always returns 200 to prevent user enumeration.

**Request Body:**

```json
{ "email": "user@example.com" }
```

**Validation:** email required, valid format, max 255 characters.

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": { "message": "Si tu correo está registrado, recibirás un enlace para restablecer tu contraseña." }
}
```

**Notes:**

- Always returns 200 regardless of whether the email exists — never reveals user existence.
- Reset token expires after 1 hour (shorter than email verification due to higher sensitivity).

---

### POST /api/auth/reset-password

Completes password reset using a one-time token.

**Request Body:**

```json
{ "token": "...", "newPassword": "NewPassword1!" }
```

**Validation:** token required; newPassword min 8 chars, uppercase, lowercase, digit, special char (`@$!%*?&#`).

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": { "message": "Contraseña restablecida exitosamente." }
}
```

**Error Response (400 Bad Request):**

```json
{
  "success": false,
  "error": { "message": "El enlace de recuperación es inválido o ha expirado", "code": "INVALID_OR_EXPIRED_TOKEN" }
}
```

**Notes:**

- Same error message for both invalid and expired tokens — never reveals which case applies.
- Token is single-use: cleared immediately after successful password reset.

---

### POST /api/auth/register (Legacy)

**DEPRECATED**: Use `/api/auth/register-user` instead.

Registers a new user without email verification (legacy endpoint).

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "Password123!@#",
  "firstName": "John",
  "lastName": "Doe",
  "phone": "+34612345678"  // optional
}
```

**Notes:**

- Creates user with `isActive: true` and `emailVerified: true` immediately
- No email verification required
- Kept for backward compatibility only
- Will be removed in future version

---

## User Registration Flow

```mermaid
sequenceDiagram
    actor User
    participant Frontend
    participant API
    participant Email

    User->>Frontend: Fill registration form
    Frontend->>API: POST /api/auth/register-user
    API->>API: Validate data
    API->>API: Hash password
    API->>API: Generate verification token
    API->>API: Save user (inactive, unverified)
    API->>Email: Send verification email
    API->>Frontend: Return user data
    Frontend->>User: Show "Check your email" message

    User->>Email: Open email
    Email->>User: Click verification link
    User->>Frontend: Navigate to /verify-email?token=...
    Frontend->>API: POST /api/auth/verify-email
    API->>API: Validate token
    API->>API: Mark email verified + activate account
    API->>Frontend: Return success
    Frontend->>User: Show "Email verified" message

    User->>Frontend: Click "Login"
    Frontend->>API: POST /api/auth/login
    API->>API: Validate credentials
    API->>API: Generate JWT
    API->>Frontend: Return token + user data
    Frontend->>User: Redirect to dashboard
```

---

## Authentication

Protected endpoints require a JWT token in the `Authorization` header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Token Claims:**

- `sub`: User ID (UUID)
- `email`: User email
- `role`: User role (Admin, Board, Member)
- `exp`: Expiration timestamp
- `iss`: Issuer (configured in appsettings)
- `aud`: Audience (configured in appsettings)

---

## Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `VALIDATION_ERROR` | 400 | Request validation failed |
| `EMAIL_EXISTS` | 400 | Email already registered |
| `DOCUMENT_EXISTS` | 400 | Document number already registered |
| `VERIFICATION_FAILED` | 400 | Email verification failed (expired/invalid token) |
| `RESEND_FAILED` | 400 | Cannot resend verification (already verified) |
| `INVALID_OR_EXPIRED_TOKEN` | 400 | Password reset token is invalid or has expired |
| `INVALID_CREDENTIALS` | 401 | Invalid email or password |
| `EMAIL_NOT_VERIFIED` | 401 | Email not verified yet |
| `ACCOUNT_INACTIVE` | 401 | Account is inactive |
| `NOT_FOUND` | 404 | Resource not found |
| `INTERNAL_ERROR` | 500 | Server error |

---

## Rate Limiting

**Currently not implemented.** Future consideration:

- Login: 5 attempts per minute per IP
- Registration: 3 attempts per hour per IP
- Resend verification: 3 attempts per hour per email

---

## CORS Configuration

**Allowed Origins (Development):**

- `http://localhost:5173` (Vite dev server)

**Allowed Origins (Production):**

- TBD

---

## Configuration

**appsettings.json:**

```json
{
  "Jwt": {
    "Secret": "your-secret-key-here",
    "Issuer": "https://abuvi.api",
    "Audience": "https://abuvi.app",
    "ExpiryInHours": 24
  },
  "Resend": {
    "ApiKey": "re_...",
    "FromEmail": "noreply@abuvi.org"
  },
  "FrontendUrl": "http://localhost:5173"
}
```

---

## Testing

See [Manual Testing Guide](../../docs/MANUAL_TESTING_REGISTRATION.md) for complete test scenarios.

**Quick Test (Happy Path):**

```bash
# 1. Register
curl -X POST http://localhost:5079/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!@#","firstName":"John","lastName":"Doe","acceptedTerms":true}'

# 2. Check API logs for verification token

# 3. Verify email
curl -X POST http://localhost:5079/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{"token":"TOKEN_FROM_LOGS"}'

# 4. Login
curl -X POST http://localhost:5079/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!@#"}'
```

---

## Implementation Notes

- Email service currently logs to console (Resend integration pending)
- Verification tokens are URL-safe base64 (32 random bytes)
- Tokens are single-use (deleted after verification)
- Password hashing uses BCrypt with automatic salt
- DocumentNumber uses partial unique index (only enforces uniqueness for non-null values)
- All timestamps are UTC
- Database uses PostgreSQL with EF Core

---

## Family Units Endpoints

Family units represent groups of people (families) who attend camp together. Each user can create one family unit and act as its representative. Family members are the individuals within a family unit.

### Authorization

- **Representative**: The user who created the family unit can manage it and its members
- **Admin/Board**: Can view any family unit and its members
- All endpoints require authentication

---

### GET /api/family-units

Returns a paginated, searchable list of all family units. For admin panel use only.

**Authorization**: Admin or Board only

**Query Parameters:**

- `page` (optional, integer, default: 1): Page number (minimum 1)
- `pageSize` (optional, integer, default: 20, max: 100): Items per page
- `search` (optional, string): Filter by family unit name or representative full name (case-insensitive, partial match)
- `sortBy` (optional, string): Sort field — `name` (default) or `createdAt`
- `sortOrder` (optional, string): Sort direction — `asc` (default) or `desc`

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "name": "Familia García",
        "representativeUserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "representativeName": "Juan García",
        "membersCount": 4,
        "createdAt": "2026-01-15T10:00:00Z",
        "updatedAt": "2026-01-15T10:00:00Z"
      }
    ],
    "totalCount": 42,
    "page": 1,
    "pageSize": 20,
    "totalPages": 3
  }
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User role is not Admin or Board

---

### POST /api/family-units

Creates a new family unit for the authenticated user. Automatically creates the representative as the first family member.

**Authorization**: Authenticated users
**Request Body:**

```json
{
  "name": "Garcia Family"
}
```

**Success Response (201 Created):**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Garcia Family",
    "representativeUserId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
    "createdAt": "2026-02-15T09:00:00Z",
    "updatedAt": "2026-02-15T09:00:00Z"
  }
}
```

**Error Responses:**

- **409 Conflict**: User already has a family unit (`FAMILY_UNIT_EXISTS`)

---

### GET /api/family-units/me

Gets the family unit for the current authenticated user.

**Authorization**: Authenticated users
**Success Response (200 OK):** Same as POST response
**Error Responses:**

- **404 Not Found**: User doesn't have a family unit

---

### GET /api/family-units/{id}

Gets a specific family unit by ID.

**Authorization**: Representative OR Admin/Board
**Success Response (200 OK):** Same as POST response
**Error Responses:**

- **403 Forbidden**: User is not the representative and not Admin/Board
- **404 Not Found**: Family unit doesn't exist

---

### PUT /api/family-units/{id}

Updates a family unit.

**Authorization**: Representative only
**Request Body:**

```json
{
  "name": "Garcia-Lopez Family"
}
```

**Success Response (200 OK):** Same as POST response
**Error Responses:**

- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit doesn't exist

---

### DELETE /api/family-units/{id}

Deletes a family unit and all its members (cascade delete).

**Authorization**: Representative only
**Success Response:** 204 No Content
**Error Responses:**

- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit doesn't exist

---

## Family Members Endpoints

### POST /api/family-units/{familyUnitId}/members

Adds a new family member to a family unit.

**Authorization**: Representative only
**Request Body:**

```json
{
  "firstName": "Maria",
  "lastName": "Garcia",
  "dateOfBirth": "2015-06-15",
  "relationship": "Child",
  "documentNumber": "12345678A",
  "email": "maria@example.com",
  "phone": "+34612345678",
  "medicalNotes": "Asthma - requires inhaler",
  "allergies": "Peanuts, dairy"
}
```

**Field Notes:**

- `relationship`: Enum - `Parent`, `Child`, `Sibling`, `Spouse`, `Other`
- `documentNumber`: Optional, uppercase alphanumeric only
- `email`: Optional, valid email format
- `phone`: Optional, E.164 format (e.g., +34612345678)
- `medicalNotes`: Optional, max 2000 characters, encrypted at rest
- `allergies`: Optional, max 1000 characters, encrypted at rest

**Success Response (201 Created):**

```json
{
  "success": true,
  "data": {
    "id": "4fa85f64-5717-4562-b3fc-2c963f66afa6",
    "familyUnitId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": null,
    "firstName": "Maria",
    "lastName": "Garcia",
    "dateOfBirth": "2015-06-15",
    "relationship": "Child",
    "documentNumber": "12345678A",
    "email": "maria@example.com",
    "phone": "+34612345678",
    "hasMedicalNotes": true,
    "hasAllergies": true,
    "createdAt": "2026-02-15T09:00:00Z",
    "updatedAt": "2026-02-15T09:00:00Z"
  }
}
```

**Security Note:** Medical notes and allergies are NEVER exposed in responses. Only boolean flags (`hasMedicalNotes`, `hasAllergies`) indicate their presence.

**Error Responses:**

- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit doesn't exist

---

### GET /api/family-units/{familyUnitId}/members

Gets all family members for a family unit.

**Authorization**: Representative OR Admin/Board
**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    { /* family member object */ },
    { /* family member object */ }
  ]
}
```

**Error Responses:**

- **403 Forbidden**: User is not the representative and not Admin/Board
- **404 Not Found**: Family unit doesn't exist

---

### GET /api/family-units/{familyUnitId}/members/{memberId}

Gets a single family member by ID.

**Authorization**: Representative OR Admin/Board
**Success Response (200 OK):** Same as POST response
**Error Responses:**

- **403 Forbidden**: User is not the representative and not Admin/Board
- **404 Not Found**: Family unit or member doesn't exist

---

### PUT /api/family-units/{familyUnitId}/members/{memberId}

Updates a family member.

**Authorization**: Representative only
**Request Body:** Same as POST request
**Success Response (200 OK):** Same as POST response
**Error Responses:**

- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit or member doesn't exist

---

### DELETE /api/family-units/{familyUnitId}/members/{memberId}

Deletes a family member. Representatives cannot delete their own family member record.

**Authorization**: Representative only
**Success Response:** 204 No Content
**Success Response:** 204 No Content
**Error Responses:**

- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit or member doesn't exist
- **409 Conflict**: Attempting to delete representative's own record (`CANNOT_DELETE_REPRESENTATIVE`)

---

### PUT /api/family-units/{familyUnitId}/members/{memberId}/profile-photo

Uploads a profile photo for a family member. Accepts `multipart/form-data` with an image file. Generates a 400x400 WebP thumbnail and stores its URL as `profilePhotoUrl`. Replaces any existing photo.

**Authorization**: Representative of the family unit or Admin
**Request**: `multipart/form-data` with `file` field (image: .jpg, .jpeg, .png, .webp, .gif; max 50 MB)
**Success Response:** 200 OK — `ApiResponse<FamilyMemberResponse>`
**Error Responses:**

- **400 Bad Request**: Invalid file type or file too large
- **403 Forbidden**: User is not the representative or admin
- **404 Not Found**: Family unit or member doesn't exist

---

### DELETE /api/family-units/{familyUnitId}/members/{memberId}/profile-photo

Removes the profile photo of a family member.

**Authorization**: Representative of the family unit or Admin
**Success Response:** 204 No Content
**Error Responses:**

- **403 Forbidden**: User is not the representative or admin
- **404 Not Found**: Family unit or member doesn't exist

---

### PUT /api/family-units/{id}/profile-photo

Uploads a profile photo for a family unit. Accepts `multipart/form-data` with an image file. Generates a 400x400 WebP thumbnail and stores its URL as `profilePhotoUrl`. Replaces any existing photo.

**Authorization**: Representative of the family unit or Admin
**Request**: `multipart/form-data` with `file` field (image: .jpg, .jpeg, .png, .webp, .gif; max 50 MB)
**Success Response:** 200 OK — `ApiResponse<FamilyUnitResponse>`
**Error Responses:**

- **400 Bad Request**: Invalid file type or file too large
- **403 Forbidden**: User is not the representative or admin
- **404 Not Found**: Family unit doesn't exist

---

### DELETE /api/family-units/{id}/profile-photo

Removes the profile photo of a family unit.

**Authorization**: Representative of the family unit or Admin
**Success Response:** 204 No Content
**Error Responses:**

- **403 Forbidden**: User is not the representative or admin
- **404 Not Found**: Family unit doesn't exist

---

## Google Places API (Backend Proxy)

These endpoints proxy Google Places API calls through the backend to protect the API key from client exposure.

**Base Path:** `/api/places`
**Authentication Required:** Yes (JWT)

---

### POST /api/places/autocomplete

Search for location suggestions based on text input. Results are restricted to Spain (`components=country:es`) and returned in Spanish.

**Authorization**: Any authenticated user

**Request Body:**

```json
{
  "input": "Camping Madrid"
}
```

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
      "description": "Camping El Pinar, Madrid, España",
      "mainText": "Camping El Pinar",
      "secondaryText": "Madrid, España"
    }
  ]
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **503 Service Unavailable**: Google Places API is unavailable (`PLACES_SERVICE_UNAVAILABLE`)

**Notes:**

- Minimum meaningful input length: 3 characters (enforced client-side)
- Frontend applies 300ms debounce before calling this endpoint

---

### POST /api/places/details

Fetch detailed information for a specific place by its Google Place ID, including coordinates.

**Authorization**: Any authenticated user

**Request Body:**

```json
{
  "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4"
}
```

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
    "name": "Camping El Pinar",
    "formattedAddress": "Calle Example, 123, Madrid, España",
    "latitude": 40.416775,
    "longitude": -3.703790,
    "types": ["campground", "lodging"]
  }
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **404 Not Found**: Place ID not found
- **503 Service Unavailable**: Google Places API is unavailable (`PLACES_SERVICE_UNAVAILABLE`)

**Usage Context:**

- Called after user selects a suggestion from the autocomplete endpoint
- Used to auto-fill `name`, `location`, `latitude`, `longitude`, and `googlePlaceId` fields in camp creation/edit forms

---

## Camp Management Endpoints

Manage camp location templates. All endpoints require Admin or Board role.

**Base Path:** `/api/camps`

---

### GET /api/camps/current

Returns the best available camp edition for the current user. Uses status-priority and year-fallback logic:

1. **Priority 1**: Current year + `Open` status
2. **Priority 2**: Current year + `Closed` status
3. **Priority 3**: Previous year + `Completed` status
4. **Priority 4**: Previous year + `Closed` status
5. **404**: No qualifying edition found within the 1-year lookback window

**Authorization**: Admin, Board, or Member

**No query parameters.**

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "campId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "campName": "Camping El Pinar",
    "campLocation": "Sierra de Guadarrama",
    "campFormattedAddress": "Calle del Pinar, 1, 28740 Rascafría, Madrid",
    "campLatitude": 40.8842,
    "campLongitude": -3.8668,
    "year": 2026,
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-10T00:00:00Z",
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "useCustomAgeRanges": false,
    "customBabyMaxAge": null,
    "customChildMinAge": null,
    "customChildMaxAge": null,
    "customAdultMinAge": null,
    "status": "Open",
    "maxCapacity": 100,
    "registrationCount": 0,
    "availableSpots": 100,
    "notes": null,
    "description": null,
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z",
    "campDescription": "A beautiful pine forest camp near the Sierra de Guadarrama",
    "campPhoneNumber": "+34918691311",
    "campNationalPhoneNumber": "918 691 311",
    "campWebsiteUrl": "https://camping-elpinar.es",
    "campGoogleMapsUrl": "https://maps.google.com/?cid=123",
    "campGoogleRating": 4.3,
    "campGoogleRatingCount": 156,
    "campPhotos": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "photoReference": "AUc7tXUr...",
        "photoUrl": null,
        "width": 1024,
        "height": 768,
        "attributionName": "John Doe",
        "attributionUrl": "https://maps.google.com/...",
        "description": null,
        "isPrimary": true,
        "displayOrder": 0
      }
    ],
    "accommodationCapacity": {
      "privateRoomsWithBathroom": 10,
      "privateRoomsSharedBathroom": null,
      "sharedRooms": null,
      "bungalows": 5,
      "campOwnedTents": null,
      "memberTentAreaSquareMeters": null,
      "memberTentCapacityEstimate": null,
      "motorhomeSpots": null,
      "notes": null
    },
    "calculatedTotalBedCapacity": 20,
    "extras": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "campEditionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "name": "Kayak sessions",
        "description": "1-hour guided kayak on the lake",
        "price": 25.00,
        "pricingType": "PerPerson",
        "pricingPeriod": "OneTime",
        "isRequired": false,
        "isActive": true,
        "maxQuantity": 20,
        "currentQuantitySold": 0,
        "createdAt": "2026-02-17T10:00:00Z",
        "updatedAt": "2026-02-17T10:00:00Z"
      }
    ]
  }
}
```

> **Note:** `registrationCount` is always `0` and `availableSpots` equals `maxCapacity` until the Registrations feature is implemented.
>
> - `campPhotos` is ordered: primary photo first, then by `displayOrder` ascending. Empty array when no photos.
> - `accommodationCapacity`: edition-level value takes priority over camp-level value. `null` when neither is set.
> - `calculatedTotalBedCapacity`: computed from private rooms (2 beds/room) and shared rooms. `null` when no accommodation capacity.
> - `extras`: only active extras are included, ordered by creation date. Empty array when none.
> - `currentQuantitySold` is always `0` in this endpoint (placeholder until Registrations feature tracks purchases).

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User role is not Member or above
- **404 Not Found**: No qualifying camp edition exists within the 1-year lookback window

---

### GET /api/camps

Returns all camp locations (lightweight, no photos).

**Authorization**: Admin or Board

**Query Parameters:**

- `isActive` (optional, boolean): Filter by active status
- `skip` (optional, integer, default: 0): Pagination offset
- `take` (optional, integer, default: 100): Pagination limit

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Camping El Pinar",
      "description": "A beautiful pine forest camp",
      "location": "Sierra de Guadarrama",
      "latitude": 40.8167,
      "longitude": -3.9833,
      "googlePlaceId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
      "formattedAddress": "Calle del Pinar, 1, 28740 Rascafría, Madrid",
      "phoneNumber": "+34 918 691 311",
      "websiteUrl": "https://camping-elpinar.es",
      "googleMapsUrl": "https://maps.google.com/?cid=123",
      "googleRating": 4.3,
      "googleRatingCount": 156,
      "businessStatus": "OPERATIONAL",
      "pricePerAdult": 180.00,
      "pricePerChild": 120.00,
      "pricePerBaby": 60.00,
      "isActive": true,
      "createdAt": "2026-02-17T10:00:00Z",
      "updatedAt": "2026-02-17T10:00:00Z"
    }
  ]
}
```

---

### GET /api/camps/{id}

Returns full camp details including all Google Places fields and photos.

**Authorization**: Admin or Board
**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Camping El Pinar",
    "description": "A beautiful pine forest camp",
    "location": "Sierra de Guadarrama",
    "latitude": 40.8167,
    "longitude": -3.9833,
    "googlePlaceId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
    "formattedAddress": "Calle del Pinar, 1, 28740 Rascafría, Madrid",
    "streetAddress": "Calle del Pinar, 1",
    "locality": "Rascafría",
    "administrativeArea": "Madrid",
    "postalCode": "28740",
    "country": "Spain",
    "phoneNumber": "+34 918 691 311",
    "nationalPhoneNumber": "918 691 311",
    "websiteUrl": "https://camping-elpinar.es",
    "googleMapsUrl": "https://maps.google.com/?cid=123",
    "googleRating": 4.3,
    "googleRatingCount": 156,
    "businessStatus": "OPERATIONAL",
    "placeTypes": "[\"campground\",\"lodging\"]",
    "lastGoogleSyncAt": "2026-02-17T10:00:00Z",
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "isActive": true,
    "accommodationCapacity": {
      "privateRoomsWithBathroom": 10,
      "privateRoomsSharedBathroom": 5,
      "sharedRooms": [
        { "quantity": 4, "bedsPerRoom": 8, "hasBathroom": false, "hasShower": false, "notes": "Cabin A" }
      ],
      "bungalows": 6,
      "campOwnedTents": 20,
      "memberTentAreaSquareMeters": 500,
      "memberTentCapacityEstimate": 50,
      "motorhomeSpots": 8,
      "notes": "Main building has elevator access"
    },
    "calculatedTotalBedCapacity": 62,
    "photos": [
      {
        "id": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
        "campId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "url": "https://example.com/photo.jpg",
        "description": "Main entrance view",
        "isPrimary": true,
        "isOriginal": false,
        "displayOrder": 0,
        "createdAt": "2026-02-17T10:00:00Z",
        "updatedAt": "2026-02-17T10:00:00Z"
      }
    ],
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
}
```

> **Note:** `accommodationCapacity` and `calculatedTotalBedCapacity` are `null` when no accommodation data has been set. `photos` is an empty array when no photos exist.

**Error Responses:**

- **404 Not Found**: Camp does not exist

---

### POST /api/camps

Creates a new camp. If `googlePlaceId` is provided, the backend automatically enriches the record with Google Places data (address, phone, website, rating, photos).

**Authorization**: Admin or Board
**Request Body:**

```json
{
  "name": "Camping El Pinar",
  "description": "A beautiful pine forest camp",
  "location": "Sierra de Guadarrama",
  "latitude": 40.8167,
  "longitude": -3.9833,
  "googlePlaceId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
  "pricePerAdult": 180.00,
  "pricePerChild": 120.00,
  "pricePerBaby": 60.00,
  "accommodationCapacity": {
    "privateRoomsWithBathroom": 10,
    "bungalows": 6,
    "notes": "Accessible facilities available"
  }
}
```

**Success Response (201 Created):** Same as `GET /api/camps/{id}` response (CampDetailResponse with photos)

**Error Responses:**

- **400 Bad Request**: Validation failed (negative prices, invalid coordinates)

---

### PUT /api/camps/{id}

Updates an existing camp.

**Authorization**: Admin or Board

**Request Body:** Same as POST plus `isActive` boolean and optionally `accommodationCapacity`

**Success Response (200 OK):** Same as `GET /api/camps/{id}` response

**Error Responses:**

- **400 Bad Request**: Validation failed
- **404 Not Found**: Camp does not exist

---

### DELETE /api/camps/{id}

Deletes a camp. Fails if the camp has any editions.

**Authorization**: Admin or Board

**Success Response:** 204 No Content

**Error Responses:**

- **400 Bad Request**: Camp has existing editions (`OPERATION_ERROR`)
- **404 Not Found**: Camp does not exist

---

## Google Places API (Backend Proxy)

These endpoints proxy Google Places API calls through the backend to protect the API key from client exposure.

**Base Path:** `/api/places`
**Authentication Required:** Yes (JWT)

---

### POST /api/places/autocomplete

Search for location suggestions based on text input. Results are restricted to Spain (`components=country:es`) and returned in Spanish.

**Authorization**: Any authenticated user

**Request Body:**

```json
{
  "input": "Camping Madrid"
}
```

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
      "description": "Camping El Pinar, Madrid, España",
      "mainText": "Camping El Pinar",
      "secondaryText": "Madrid, España"
    }
  ]
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **503 Service Unavailable**: Google Places API is unavailable (`PLACES_SERVICE_UNAVAILABLE`)

**Notes:**

- Minimum meaningful input length: 3 characters (enforced client-side)
- Frontend applies 300ms debounce before calling this endpoint

---

### POST /api/places/details

Fetch detailed information for a specific place by its Google Place ID, including coordinates.

**Authorization**: Any authenticated user

**Request Body:**

```json
{
  "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4"
}
```

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "placeId": "ChIJN1t_tDeuEmsRUsoyG83frY4",
    "name": "Camping El Pinar",
    "formattedAddress": "Calle Example, 123, Madrid, España",
    "latitude": 40.416775,
    "longitude": -3.703790,
    "types": ["campground", "lodging"]
  }
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **404 Not Found**: Place ID not found
- **503 Service Unavailable**: Google Places API is unavailable (`PLACES_SERVICE_UNAVAILABLE`)

**Usage Context:**

- Called after user selects a suggestion from the autocomplete endpoint
- Used to auto-fill `name`, `location`, `latitude`, `longitude`, and `googlePlaceId` fields in camp creation/edit forms

---

## Camp Edition Lifecycle Endpoints

Manage the status lifecycle of camp editions. Status transitions follow a strict linear workflow: `Proposed → Draft → Open → Closed → Completed`. Admins can additionally roll back `Open → Draft` to make corrections. Rejection (archiving) is handled via `DELETE /api/camps/editions/{id}/reject`.

**Base Path:** `/api/camps/editions`

---

### GET /api/camps/editions

Returns all non-archived camp editions with optional filtering.

**Authorization**: Admin or Board

**Query Parameters:**

- `year` (optional, integer): Filter by year
- `status` (optional, enum): Filter by status (`Proposed`, `Draft`, `Open`, `Closed`, `Completed`)
- `campId` (optional, GUID): Filter by camp

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "campId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "campName": "Camping El Pinar",
      "year": 2026,
      "startDate": "2026-07-01T00:00:00Z",
      "endDate": "2026-07-10T00:00:00Z",
      "pricePerAdult": 180.00,
      "pricePerChild": 120.00,
      "pricePerBaby": 60.00,
      "useCustomAgeRanges": false,
      "customBabyMaxAge": null,
      "customChildMinAge": null,
      "customChildMaxAge": null,
      "customAdultMinAge": null,
      "status": "Draft",
      "maxCapacity": 100,
      "notes": null,
      "isArchived": false,
      "accommodationCapacity": null,
      "calculatedTotalBedCapacity": null,
      "createdAt": "2026-02-17T10:00:00Z",
      "updatedAt": "2026-02-17T10:00:00Z"
    }
  ]
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board

---

### GET /api/camps/editions/active

Returns the currently active (Open status) edition for the given year. Always returns 200; `data` is `null` if no open edition exists.

**Authorization**: Admin, Board, or Member

**Query Parameters:**

- `year` (optional, integer, default: current year): Target year

**Success Response (200 OK) — Edition exists:**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "campId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "campName": "Camping El Pinar",
    "campLocation": "Sierra de Guadarrama",
    "campFormattedAddress": "Calle del Pinar, 1, 28740 Rascafría, Madrid",
    "year": 2026,
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-10T00:00:00Z",
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "useCustomAgeRanges": false,
    "customBabyMaxAge": null,
    "customChildMinAge": null,
    "customChildMaxAge": null,
    "customAdultMinAge": null,
    "status": "Open",
    "maxCapacity": 100,
    "registrationCount": 0,
    "notes": null,
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
}
```

**Success Response (200 OK) — No active edition:**

```json
{
  "success": true,
  "data": null
}
```

> **Note:** `registrationCount` is always `0` until the Registrations feature is integrated.

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User role is not Member or above

---

### GET /api/camps/editions/{id}

Returns a single camp edition by ID.

**Authorization**: Admin, Board, or Member

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "campId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "campName": "Camping El Pinar",
    "year": 2026,
    "startDate": "2026-07-01T00:00:00Z",
    "endDate": "2026-07-10T00:00:00Z",
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "useCustomAgeRanges": false,
    "customBabyMaxAge": null,
    "customChildMinAge": null,
    "customChildMaxAge": null,
    "customAdultMinAge": null,
    "status": "Draft",
    "maxCapacity": 100,
    "notes": null,
    "isArchived": false,
    "accommodationCapacity": null,
    "calculatedTotalBedCapacity": null,
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
}
```

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: Insufficient role
- **404 Not Found**: Edition not found (`NOT_FOUND`)

---

### PUT /api/camps/editions/{id}

Updates a camp edition. Allowed fields depend on current status:

| Status | Allowed fields |
|--------|---------------|
| `Proposed` / `Draft` | All fields |
| `Open` | `notes`, `maxCapacity` only |
| `Closed` / `Completed` | No updates allowed (400) |

**Authorization**: Admin or Board

**Request Body:**

```json
{
  "startDate": "2026-07-01T00:00:00Z",
  "endDate": "2026-07-10T00:00:00Z",
  "pricePerAdult": 180.00,
  "pricePerChild": 120.00,
  "pricePerBaby": 60.00,
  "useCustomAgeRanges": false,
  "customBabyMaxAge": null,
  "customChildMinAge": null,
  "customChildMaxAge": null,
  "customAdultMinAge": null,
  "maxCapacity": 100,
  "notes": "Updated notes"
}
```

**Success Response (200 OK):** Returns the updated `CampEditionResponse` (same shape as GET by ID).

**Error Responses:**

- **400 Bad Request**: Validation error or business rule violation (e.g. changing dates/prices on an Open edition) (`VALIDATION_ERROR` / `OPERATION_ERROR`)
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: Insufficient role
- **404 Not Found**: Edition not found (`NOT_FOUND`)

---

### PATCH /api/camps/editions/{id}/status

Changes the status of a camp edition following the allowed transition chain.

**Authorization**: Admin or Board

**Valid Transitions:**

| From | To | Roles |
|------|----|-------|
| `Proposed` | `Draft` | Admin, Board |
| `Draft` | `Open` | Admin, Board |
| `Open` | `Closed` | Admin, Board |
| `Closed` | `Completed` | Admin, Board |
| `Open` | `Draft` | Admin only |

Additional date constraints:

- `Draft → Open`: Edition's `startDate` must not be in the past (bypassed when `force: true`, Admin only)
- `Closed → Completed`: Edition's `endDate` must be in the past

**Request Body:**

```json
{
  "status": "Draft",
  "force": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `status` | `CampEditionStatus` | Yes | Target status |
| `force` | `boolean` | No (default: `false`) | Admin-only. When `true`, bypasses the `startDate < today` constraint when transitioning `Draft → Open`. Returns 403 if sent by a non-Admin user. |

**Success Response (200 OK):** Returns the updated `CampEditionResponse`.

**Error Responses:**

- **400 Bad Request**: Invalid transition or date constraint violation (`VALIDATION_ERROR` / `OPERATION_ERROR`)
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: Insufficient role (e.g. Board user attempting `Open → Draft` or using `force: true`)
- **404 Not Found**: Edition not found (`NOT_FOUND`)

---

### POST /api/camps/editions/propose

Proposes a new camp edition. The edition is created with `Proposed` status and requires board review before becoming a `Draft`.

**Authorization**: Admin or Board

**Request Body:**

```json
{
  "campId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "year": 2026,
  "startDate": "2026-07-01T00:00:00Z",
  "endDate": "2026-07-10T00:00:00Z",
  "pricePerAdult": 180.00,
  "pricePerChild": 120.00,
  "pricePerBaby": 60.00,
  "maxCapacity": 100,
  "proposalReason": "Annual summer camp",
  "proposalNotes": "Same location as last year",
  "accommodationCapacity": {
    "privateRoomsWithBathroom": 10,
    "bungalows": 6
  }
}
```

**Field Notes:**

- `proposalReason`: **Optional** (`string | null`). Reason for proposing this edition. Previously documented as required; now optional. Maximum 1000 characters.
- `proposalNotes`: **Optional** (`string | null`). Additional context for the board. Accepted by the API but no longer sent by the frontend after the UX improvement. Maximum 2000 characters.
- `accommodationCapacity`: Optional; when provided, is also synced to the parent `Camp.accommodationCapacityJson` in the same transaction

**Success Response (201 Created):** Returns the created `CampEditionResponse` (same shape as `GET /api/camps/editions/{id}`) with `status: "Proposed"`.

**Error Responses:**

- **400 Bad Request**: Validation failed (missing fields, invalid dates)
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Camp not found

---

## Camp Photo Management Endpoints

Manage photos for a camp location. Supports adding manual photos (by URL), editing, deleting, reordering, and setting a primary photo. All endpoints require Admin or Board role.

**Base Path:** `/api/camps/{campId}/photos`

---

### POST /api/camps/{campId}/photos

Adds a new manually managed photo to a camp.

**Authorization**: Admin or Board

**Request Body:**

```json
{
  "url": "https://example.com/photo.jpg",
  "description": "Main entrance in summer",
  "displayOrder": 0,
  "isPrimary": false
}
```

**Field Notes:**

- `url`: Required, valid URL, max 2000 characters
- `description`: Optional, max 500 characters
- `displayOrder`: Required, integer >= 0
- `isPrimary`: Required boolean; if `true`, all other photos for this camp are set to `isPrimary = false`

**Success Response (201 Created):**

```json
{
  "success": true,
  "data": {
    "id": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
    "campId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "url": "https://example.com/photo.jpg",
    "description": "Main entrance in summer",
    "isPrimary": false,
    "isOriginal": false,
    "displayOrder": 0,
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
}
```

**Error Responses:**

- **400 Bad Request**: Validation failed (`VALIDATION_ERROR`)
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Camp not found

---

### PUT /api/camps/{campId}/photos/{photoId}

Updates an existing camp photo.

**Authorization**: Admin or Board

**Request Body:** Same as POST

**Success Response (200 OK):** Updated photo object (same shape as POST response)

**Error Responses:**

- **400 Bad Request**: Validation failed
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Photo not found or does not belong to this camp

---

### DELETE /api/camps/{campId}/photos/{photoId}

Deletes a camp photo permanently.

**Authorization**: Admin or Board

**Success Response:** 204 No Content

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Photo not found or does not belong to this camp

---

### POST /api/camps/{campId}/photos/{photoId}/set-primary

Sets the specified photo as the primary (main display) photo for the camp. Clears `isPrimary` on all other photos for this camp in the same operation.

**Authorization**: Admin or Board

**No request body.**

**Success Response (200 OK):** Updated photo object with `isPrimary: true`

**Error Responses:**

- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Photo not found or does not belong to this camp

---

### PUT /api/camps/{campId}/photos/reorder

Updates the `displayOrder` for a set of photos. Only the specified photos are updated; unspecified photos are left unchanged.

**Authorization**: Admin or Board

**Request Body:**

```json
{
  "photos": [
    { "id": "9fa85f64-5717-4562-b3fc-2c963f66afa6", "displayOrder": 0 },
    { "id": "8fa85f64-5717-4562-b3fc-2c963f66afa6", "displayOrder": 1 }
  ]
}
```

**Success Response:** 204 No Content

**Error Responses:**

- **400 Bad Request**: Any photo ID does not belong to this camp (`VALIDATION_ERROR`)
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Camp not found

---

## Membership Endpoints

A membership represents a family member's status as an active member (socio/a) of the association. Each family member can have at most one membership. Annual fees are generated automatically by a background service.

### Authorization

- **Representative**: Can manage memberships for members of their own family unit
- **Admin/Board**: Can manage memberships for any family unit
- All endpoints require authentication

### GET /api/family-units/{familyUnitId}/members/{memberId}/membership

Gets the membership for a specific family member, including all associated fees.

**Authorization**: Representative OR Admin/Board

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
    "familyMemberId": "4fa85f64-5717-4562-b3fc-2c963f66afa6",
    "startDate": "2026-01-01",
    "endDate": null,
    "isActive": true,
    "fees": [
      {
        "id": "6fa85f64-5717-4562-b3fc-2c963f66afa6",
        "membershipId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
        "year": 2026,
        "amount": 50.00,
        "status": "Pending",
        "paidDate": null,
        "paymentReference": null,
        "createdAt": "2026-01-01T00:00:00Z"
      }
    ],
    "createdAt": "2026-01-01T09:00:00Z",
    "updatedAt": "2026-01-01T09:00:00Z"
  }
}
```

**Error Responses:**

- **404 Not Found**: Member has no membership (treat as "not yet a socio", not an error)
- **403 Forbidden**: User is not authorized
- **404 Not Found**: Family unit or member doesn't exist

---

### POST /api/family-units/{familyUnitId}/members/{memberId}/membership

Activates a membership for a family member.

**Authorization**: Representative OR Admin/Board

**Request Body:**

```json
{
  "year": 2026
}
```

**Field Notes:**

- `year`: Required, calendar year integer. Must be > 2000 and ≤ current calendar year. Backend normalizes to `{year}-01-01T00:00:00Z` for storage.

**Success Response (201 Created):** Same structure as GET response above. The `startDate` field in the response will always be `{year}-01-01T00:00:00Z`.

**Error Responses:**

- **400 Bad Request**: Validation failed (year in future or ≤ 2000)
- **403 Forbidden**: User is not authorized
- **404 Not Found**: Family unit or member doesn't exist
- **409 Conflict**: Member already has an active membership (`MEMBERSHIP_EXISTS`)

---

### POST /api/family-units/{familyUnitId}/membership/bulk

Activates memberships for all family members who do not yet have one, in a single request. Members who already have an active membership are skipped (not an error).

**Authorization**: Admin/Board only

**Request Body:**

```json
{
  "year": 2026
}
```

**Field Notes:**

- `year`: Required, calendar year integer. Must be > 2000 and ≤ current calendar year.

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "activated": 3,
    "skipped": 1,
    "results": [
      { "memberId": "...", "memberName": "Ana García", "status": "Activated" },
      { "memberId": "...", "memberName": "Pedro García", "status": "Activated" },
      { "memberId": "...", "memberName": "Luis García", "status": "Activated" },
      { "memberId": "...", "memberName": "María García", "status": "Skipped", "reason": "Ya tiene membresía activa" }
    ]
  }
}
```

**Result status values:** `Activated` | `Skipped` | `Failed`

**Error Responses:**

- **400 Bad Request**: Validation failed (year in future or ≤ 2000)
- **403 Forbidden**: User is not Board/Admin
- **404 Not Found**: Family unit doesn't exist

---

### DELETE /api/family-units/{familyUnitId}/members/{memberId}/membership

Deactivates the membership for a family member. Sets `isActive = false` and records `endDate`.

**Authorization**: Representative OR Admin/Board

**Success Response:** 204 No Content

**Error Responses:**

- **403 Forbidden**: User is not authorized
- **404 Not Found**: Family unit, member, or membership doesn't exist

---

## Membership Fees Endpoints

Annual fees are auto-generated by a background service when a membership is active. The frontend can list fees and mark them as paid.

### GET /api/memberships/{membershipId}/fees

Lists all fees for a membership, ordered by year descending.

**Authorization**: Representative OR Admin/Board

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "6fa85f64-5717-4562-b3fc-2c963f66afa6",
      "membershipId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
      "year": 2026,
      "amount": 50.00,
      "status": "Pending",
      "paidDate": null,
      "paymentReference": null,
      "createdAt": "2026-01-01T00:00:00Z"
    }
  ]
}
```

**Error Responses:**

- **403 Forbidden**: User is not authorized
- **404 Not Found**: Membership doesn't exist

---

### GET /api/memberships/{membershipId}/fees/current

Gets the fee for the current calendar year.

**Authorization**: Representative OR Admin/Board

**Success Response (200 OK):** Single fee object (same structure as above)

**Error Responses:**

- **403 Forbidden**: User is not authorized
- **404 Not Found**: Membership or current-year fee doesn't exist

---

### POST /api/memberships/{membershipId}/fees/{feeId}/pay

Marks a fee as paid.

**Authorization**: Admin/Board only

**Request Body:**

```json
{
  "paidDate": "2026-02-01",
  "paymentReference": "TRF-2026-001"
}
```

**Field Notes:**

- `paidDate`: Required, ISO 8601 date string (`YYYY-MM-DD`), must not be in the future
- `paymentReference`: Optional, max 100 characters, external reference or transaction ID

**Success Response (200 OK):** Updated fee object

**Error Responses:**

- **400 Bad Request**: Validation failed (paidDate in future)
- **403 Forbidden**: User does not have Admin/Board role
- **404 Not Found**: Membership or fee doesn't exist
- **409 Conflict**: Fee is already marked as paid (`FEE_ALREADY_PAID`)

---

## Guest Endpoints

Guests are external people invited by a family unit to attend camps. They are not platform users. Their sensitive health data (medicalNotes, allergies) is encrypted at rest.

### Authorization

- **Representative**: Can manage guests for their own family unit
- **Admin/Board**: Can manage guests for any family unit
- All endpoints require authentication

### POST /api/family-units/{familyUnitId}/guests

Creates a new guest for a family unit.

**Authorization**: Representative only

**Request Body:**

```json
{
  "firstName": "Ana",
  "lastName": "Pérez",
  "dateOfBirth": "1990-05-20",
  "documentNumber": "12345678B",
  "email": "ana@example.com",
  "phone": "+34612345679",
  "medicalNotes": "Asthma - requires inhaler",
  "allergies": "Peanuts"
}
```

**Field Notes:**

- `firstName`, `lastName`, `dateOfBirth`: Required
- `documentNumber`: Optional, uppercase alphanumeric, max 50 characters
- `email`: Optional, valid email format, max 255 characters
- `phone`: Optional, E.164 format (e.g., +34612345678)
- `medicalNotes`: Optional, max 2000 characters, encrypted at rest
- `allergies`: Optional, max 1000 characters, encrypted at rest

**Success Response (201 Created):**

```json
{
  "success": true,
  "data": {
    "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "familyUnitId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "firstName": "Ana",
    "lastName": "Pérez",
    "dateOfBirth": "1990-05-20",
    "documentNumber": "12345678B",
    "email": "ana@example.com",
    "phone": "+34612345679",
    "hasMedicalNotes": true,
    "hasAllergies": true,
    "isActive": true,
    "createdAt": "2026-02-15T09:00:00Z",
    "updatedAt": "2026-02-15T09:00:00Z"
  }
}
```

**Security Note:** `medicalNotes` and `allergies` are NEVER exposed in responses. Only boolean flags (`hasMedicalNotes`, `hasAllergies`) indicate their presence.

**Error Responses:**

- **400 Bad Request**: Validation failed
- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit doesn't exist

---

### GET /api/family-units/{familyUnitId}/guests

Lists all active guests for a family unit, ordered by last name then first name.

**Authorization**: Representative OR Admin/Board

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    { /* guest object */ },
    { /* guest object */ }
  ]
}
```

**Error Responses:**

- **403 Forbidden**: User is not authorized
- **404 Not Found**: Family unit doesn't exist

---

### GET /api/family-units/{familyUnitId}/guests/{guestId}

Gets a single guest by ID.

**Authorization**: Representative OR Admin/Board

**Success Response (200 OK):** Same as POST response

**Error Responses:**

- **403 Forbidden**: User is not authorized
- **404 Not Found**: Family unit or guest doesn't exist

---

### PUT /api/family-units/{familyUnitId}/guests/{guestId}

Updates a guest's information.

**Authorization**: Representative only

**Request Body:** Same as POST request

**Success Response (200 OK):** Updated guest object

**Error Responses:**

- **400 Bad Request**: Validation failed
- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit or guest doesn't exist

---

### DELETE /api/family-units/{familyUnitId}/guests/{guestId}

Soft-deletes a guest (sets `isActive = false`). The record is retained in the database.

**Authorization**: Representative only

**Success Response:** 204 No Content

**Error Responses:**

- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit or guest doesn't exist

---

## Camp Edition Accommodation Endpoints

Manage accommodation options for a camp edition. Accommodations are preference-only (no price) — families rank up to 3 choices during registration.

**Base Path:** `/api/camps/editions`

---

### POST /api/camps/editions/{editionId}/accommodations

Create a new accommodation option for a camp edition.

**Authorization**: Admin or Board

**Request Body:**

```json
{
  "name": "Refugio Norte",
  "accommodationType": "Lodge",
  "description": "Refugio con capacidad para 20 personas",
  "capacity": 20,
  "sortOrder": 0
}
```

**Success Response (201 Created):**

```json
{
  "success": true,
  "data": {
    "id": "...",
    "campEditionId": "...",
    "name": "Refugio Norte",
    "accommodationType": "Lodge",
    "description": "Refugio con capacidad para 20 personas",
    "capacity": 20,
    "isActive": true,
    "sortOrder": 0,
    "currentPreferenceCount": 0,
    "firstChoiceCount": 0,
    "createdAt": "...",
    "updatedAt": "..."
  }
}
```

**Error Responses:**

- **400 Bad Request**: Validation failed
- **404 Not Found**: Camp edition not found

---

### GET /api/camps/editions/{editionId}/accommodations

List accommodation options for a camp edition.

**Authorization**: Any authenticated user (Member+)

**Query Parameters:**

- `activeOnly` (optional, boolean): Filter to active accommodations only

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "id": "...",
      "campEditionId": "...",
      "name": "Refugio Norte",
      "accommodationType": "Lodge",
      "description": "...",
      "capacity": 20,
      "isActive": true,
      "sortOrder": 0,
      "currentPreferenceCount": 5,
      "firstChoiceCount": 3,
      "createdAt": "...",
      "updatedAt": "..."
    }
  ]
}
```

---

### GET /api/camps/editions/accommodations/{id}

Get a single accommodation option by ID.

**Authorization**: Any authenticated user (Member+)

**Success Response (200 OK):** Same structure as single item in list response.

---

### PUT /api/camps/editions/accommodations/{id}

Update an accommodation option.

**Authorization**: Admin or Board

**Request Body:**

```json
{
  "name": "Refugio Norte Ampliado",
  "accommodationType": "Lodge",
  "description": "Refugio ampliado con capacidad para 25 personas",
  "capacity": 25,
  "isActive": true,
  "sortOrder": 0
}
```

**Success Response (200 OK):** Returns updated accommodation.

---

### DELETE /api/camps/editions/accommodations/{id}

Delete an accommodation option. Fails if any registration has preferences referencing this accommodation.

**Authorization**: Admin or Board

**Success Response:** 204 No Content

**Error Responses:**

- **400 Bad Request**: Accommodation has existing preferences (deactivate instead)
- **404 Not Found**: Accommodation not found

---

### PATCH /api/camps/editions/accommodations/{id}/activate

Activate a deactivated accommodation option.

**Authorization**: Admin or Board

**Success Response (200 OK):** Returns updated accommodation.

---

### PATCH /api/camps/editions/accommodations/{id}/deactivate

Deactivate an accommodation option (soft-disable without deleting).

**Authorization**: Admin or Board

**Success Response (200 OK):** Returns updated accommodation.

---

## Registration Accommodation Preference Endpoints

Families rank up to 3 accommodation preferences per registration.

**Base Path:** `/api/registrations`

---

### PUT /api/registrations/{id}/accommodation-preferences

Set or replace accommodation preferences for a registration. Replaces all existing preferences.

**Authorization**: Family representative or Admin/Board

**Request Body:**

```json
{
  "preferences": [
    { "campEditionAccommodationId": "...", "preferenceOrder": 1 },
    { "campEditionAccommodationId": "...", "preferenceOrder": 2 },
    { "campEditionAccommodationId": "...", "preferenceOrder": 3 }
  ]
}
```

**Validation Rules:**

- Maximum 3 preferences
- No duplicate accommodation IDs
- No duplicate preference orders
- Each preference order must be between 1 and 3
- Each accommodation must belong to the same camp edition and be active

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "campEditionAccommodationId": "...",
      "accommodationName": "Refugio Norte",
      "accommodationType": "Lodge",
      "preferenceOrder": 1
    }
  ]
}
```

**Error Responses:**

- **400 Bad Request**: Validation failed
- **403 Forbidden**: User is not the representative
- **404 Not Found**: Registration or accommodation not found
- **422 Unprocessable Entity**: Business rule violation (e.g., registration not in Pending status)

---

### GET /api/registrations/{id}/accommodation-preferences

Get accommodation preferences for a registration, ordered by preference rank.

**Authorization**: Any authenticated user

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": [
    {
      "campEditionAccommodationId": "...",
      "accommodationName": "Refugio Norte",
      "accommodationType": "Lodge",
      "preferenceOrder": 1
    },
    {
      "campEditionAccommodationId": "...",
      "accommodationName": "Parcela Caravanas A",
      "accommodationType": "Caravan",
      "preferenceOrder": 2
    }
  ]
}
```

**Error Responses:**

- **404 Not Found**: Registration not found

---

## Registration Admin Endpoints

Admin/Board-only endpoints for managing registrations across camp editions.

**Authorization**: Requires `Admin` or `Board` role.

---

### GET /api/camp-editions/{campEditionId}/registrations

Get paginated list of registrations for a camp edition with search, filtering, and aggregated totals.

**Query Parameters:**

| Parameter  | Type   | Default | Description                                      |
| ---------- | ------ | ------- | ------------------------------------------------ |
| `page`     | int    | 1       | Page number (clamped to >= 1)                    |
| `pageSize` | int    | 20      | Items per page (clamped to 1-100)                |
| `search`   | string | null    | Search by family name, representative name/email |
| `status`   | string | null    | Filter by registration status                    |

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "...",
        "familyUnit": { "id": "...", "name": "García Family" },
        "representative": {
          "id": "...",
          "firstName": "Juan",
          "lastName": "García",
          "email": "juan@test.com"
        },
        "status": "Pending",
        "memberCount": 3,
        "totalAmount": 900.00,
        "amountPaid": 200.00,
        "amountRemaining": 700.00,
        "createdAt": "2026-03-01T10:00:00Z"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1,
    "totals": {
      "totalRegistrations": 1,
      "totalMembers": 3,
      "totalAmount": 900.00,
      "totalPaid": 200.00,
      "totalRemaining": 700.00
    }
  }
}
```

**Error Responses:**

- **404 Not Found**: Camp edition not found

---

### PUT /api/registrations/{id}/admin-edit

Edit a registration as Admin/Board. Sets the registration status to `Draft` and records `adminModifiedAt`. Supports partial updates — only provided fields are updated.

**Request Body:**

```json
{
  "members": [
    { "familyMemberId": "...", "attendancePeriod": "Complete" }
  ],
  "extras": [
    { "campEditionExtraId": "...", "quantity": 2 }
  ],
  "preferences": [
    { "campEditionAccommodationId": "...", "preferenceOrder": 1 }
  ],
  "notes": "Updated notes",
  "specialNeeds": "Updated special needs",
  "campatesPreference": "Updated preference",
  "hasPet": true
}
```

All fields are optional (null = no change).

**Validation Rules:**

- Members list, if provided, must not be empty
- Extra quantity must be > 0
- Preference order must be > 0
- Notes max 1000 chars, SpecialNeeds max 2000 chars, CampatesPreference max 500 chars

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": { "...RegistrationResponse..." }
}
```

**Error Responses:**

- **400 Bad Request**: Validation failed
- **404 Not Found**: Registration not found
- **422 Unprocessable Entity**: Registration is cancelled

---

## Blob Storage Endpoints

Blob Storage endpoints manage file uploads to and deletions from the configured S3-compatible object store (Hetzner Object Storage). All upload operations are available to any authenticated user; delete and stats operations require Admin role.

### POST /api/blobs/upload

Uploads a file to object storage. Accepts `multipart/form-data`.

**Authorization**: Any authenticated user (`Bearer` token required)

**Request (multipart/form-data):**

| Field               | Type    | Required | Description                                                                        |
| ------------------- | ------- | -------- | ---------------------------------------------------------------------------------- |
| `file`              | File    | Yes      | The file to upload (max 50 MB)                                                     |
| `folder`            | string  | Yes      | Target folder — one of: `photos`, `media-items`, `camp-locations`, `camp-photos`  |
| `contextId`         | GUID    | No       | Optional context entity ID scoped to the file path                                 |
| `generateThumbnail` | boolean | No       | When `true` and the file is an image, a WebP thumbnail is generated (default: `false`) |

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "fileUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc123/photo.jpg",
    "thumbnailUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc123/thumbs/photo.webp",
    "fileName": "photo.jpg",
    "contentType": "image/jpeg",
    "sizeBytes": 204800
  }
}
```

`thumbnailUrl` is `null` when `generateThumbnail` is `false` or the file type does not support thumbnails (audio, video, documents).

**Error Responses:**

- **400 Bad Request**: Validation failed — invalid folder, disallowed extension for the folder, or file exceeds maximum size.
- **401 Unauthorized**: No valid Bearer token.
- **413 Payload Too Large**: File exceeds the server-side body limit (55 MB including multipart headers).

---

### DELETE /api/blobs

Deletes one or more objects from storage by their storage keys.

**Authorization**: Admin role required

**Request Body:**

```json
{
  "blobKeys": [
    "photos/abc123/photo.jpg",
    "photos/abc123/thumbs/photo.webp"
  ]
}
```

**Success Response:** 204 No Content

**Error Responses:**

- **400 Bad Request**: `blobKeys` array is empty or null.
- **401 Unauthorized**: No valid Bearer token.
- **403 Forbidden**: Authenticated user does not have Admin role.

---

### GET /api/blobs/stats

Returns storage usage statistics. Results are cached server-side for 5 minutes to avoid expensive bucket enumeration on every call.

**Authorization**: Admin role required

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "totalObjects": 27,
    "totalSizeBytes": 15728640,
    "totalSizeHumanReadable": "15 MB",
    "quotaBytes": 53687091200,
    "usedPct": 0.029,
    "freeBytes": 53671362560,
    "byFolder": {
      "photos": { "objects": 15, "sizeBytes": 8388608 },
      "media-items": { "objects": 8, "sizeBytes": 5242880 },
      "camp-locations": { "objects": 3, "sizeBytes": 1572864 },
      "camp-photos": { "objects": 1, "sizeBytes": 524288 }
    }
  }
}
```

`quotaBytes`, `usedPct`, and `freeBytes` are `null` when `StorageQuotaBytes` is not configured (set to 0).

**Error Responses:**

- **401 Unauthorized**: No valid Bearer token.
- **403 Forbidden**: Authenticated user does not have Admin role.

---

## Blob Storage Endpoints

Blob Storage endpoints manage file uploads to and deletions from the configured S3-compatible object store (Hetzner Object Storage). All upload operations are available to any authenticated user; delete and stats operations require Admin role.

### POST /api/blobs/upload

Uploads a file to object storage. Accepts `multipart/form-data`.

**Authorization**: Any authenticated user (`Bearer` token required)

**Request (multipart/form-data):**

| Field               | Type    | Required | Description                                                                        |
| ------------------- | ------- | -------- | ---------------------------------------------------------------------------------- |
| `file`              | File    | Yes      | The file to upload (max 50 MB)                                                     |
| `folder`            | string  | Yes      | Target folder — one of: `photos`, `media-items`, `camp-locations`, `camp-photos`  |
| `contextId`         | GUID    | No       | Optional context entity ID scoped to the file path                                 |
| `generateThumbnail` | boolean | No       | When `true` and the file is an image, a WebP thumbnail is generated (default: `false`) |

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "fileUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc123/photo.jpg",
    "thumbnailUrl": "https://abuvi-media.fsn1.your-objectstorage.com/photos/abc123/thumbs/photo.webp",
    "fileName": "photo.jpg",
    "contentType": "image/jpeg",
    "sizeBytes": 204800
  }
}
```

`thumbnailUrl` is `null` when `generateThumbnail` is `false` or the file type does not support thumbnails (audio, video, documents).

**Error Responses:**

- **400 Bad Request**: Validation failed — invalid folder, disallowed extension for the folder, or file exceeds maximum size.
- **401 Unauthorized**: No valid Bearer token.
- **413 Payload Too Large**: File exceeds the server-side body limit (55 MB including multipart headers).

---

### DELETE /api/blobs

Deletes one or more objects from storage by their storage keys.

**Authorization**: Admin role required

**Request Body:**

```json
{
  "blobKeys": [
    "photos/abc123/photo.jpg",
    "photos/abc123/thumbs/photo.webp"
  ]
}
```

**Success Response:** 204 No Content

**Error Responses:**

- **400 Bad Request**: `blobKeys` array is empty or null.
- **401 Unauthorized**: No valid Bearer token.
- **403 Forbidden**: Authenticated user does not have Admin role.

---

### GET /api/blobs/stats

Returns storage usage statistics. Results are cached server-side for 5 minutes to avoid expensive bucket enumeration on every call.

**Authorization**: Admin role required

**Success Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "totalObjects": 27,
    "totalSizeBytes": 15728640,
    "totalSizeHumanReadable": "15 MB",
    "quotaBytes": 53687091200,
    "usedPct": 0.029,
    "freeBytes": 53671362560,
    "byFolder": {
      "photos": { "objects": 15, "sizeBytes": 8388608 },
      "media-items": { "objects": 8, "sizeBytes": 5242880 },
      "camp-locations": { "objects": 3, "sizeBytes": 1572864 },
      "camp-photos": { "objects": 1, "sizeBytes": 524288 }
    }
  }
}
```

`quotaBytes`, `usedPct`, and `freeBytes` are `null` when `StorageQuotaBytes` is not configured (set to 0).

**Error Responses:**

- **401 Unauthorized**: No valid Bearer token.
- **403 Forbidden**: Authenticated user does not have Admin role.

---

## Payment Listing (Admin)

### GET /api/admin/payments

List all payments with optional filters. Admin or Board role required. Returns paginated results.

**Query Parameters:**

- `Status` (optional): Filter by payment status (`Pending`, `PendingReview`, `Completed`, `Failed`, `Refunded`)
- `CampEditionId` (optional): Filter by camp edition UUID
- `InstallmentNumber` (optional): Filter by installment period. Values 1 and 2 are exact matches; value 3 or higher returns all payments with installment >= 3 (extras and manual payments)
- `FromDate` (optional): Filter payments created on or after this date (ISO 8601)
- `ToDate` (optional): Filter payments created on or before this date (ISO 8601)
- `Page` (optional, default 1): Page number for pagination
- `PageSize` (optional, default 20): Number of items per page

**Response (HTTP 200):** `ApiResponse<{ Items: AdminPaymentResponse[], TotalCount: number, Page: number, PageSize: number }>`

**Error Responses:**

- **403 Forbidden**: User is not Admin or Board

---

## Manual Payment Management (Admin)

### POST /api/admin/registrations/{registrationId}/payments/manual

Create a manual/custom payment for a registration. Admin or Board role required.

**Request Body:**

```json
{
  "amount": 50.00,
  "description": "Custom payment for extra activity",
  "dueDate": "2026-07-15T00:00:00Z",
  "adminNotes": "Requested by family"
}
```

**Validation:**
- `amount`: Required, must be > 0
- `description`: Required, max 500 characters
- `dueDate`: Optional
- `adminNotes`: Optional, max 2000 characters

**Response (HTTP 201):** `ApiResponse<AdminPaymentResponse>` with the created payment. The payment is assigned the next available `installmentNumber` and `isManual = true`. Registration `totalAmount` is updated.

**Error Responses:**
- **400 Bad Request**: Validation failure
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Registration not found

### PUT /api/admin/payments/{paymentId}/manual

Update a manual payment. Only Pending manual payments can be edited. Admin or Board role required.

**Request Body (all fields optional — partial update):**

```json
{
  "amount": 75.00,
  "description": "Updated description",
  "dueDate": "2026-08-01T00:00:00Z",
  "adminNotes": "Updated notes"
}
```

**Response (HTTP 200):** `ApiResponse<AdminPaymentResponse>` with the updated payment. If `amount` changes, Registration `totalAmount` is adjusted.

**Error Responses:**
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Payment not found
- **422 Unprocessable Entity**: Payment is not manual, or not in Pending status

### DELETE /api/admin/payments/{paymentId}/manual

Delete a manual payment. Only Pending manual payments can be deleted. Admin or Board role required.

**Response (HTTP 200):** `ApiResponse<object>` with success message. Registration `totalAmount` is decreased by the deleted payment amount.

**Error Responses:**
- **403 Forbidden**: User is not Admin or Board
- **404 Not Found**: Payment not found
- **422 Unprocessable Entity**: Payment is not manual, or not in Pending status

---

## Sequential Payment Enforcement

Payments must be completed in order: P1 before P2, P2 before P3. Manual payments are always actionable regardless of other payment statuses.

**Computed `isActionable` flag** is included in all `PaymentResponse` and `AdminPaymentResponse` objects:
- P1: Always actionable when Pending
- P2: Actionable when Pending AND P1 is Completed
- P3: Actionable when Pending AND P2 is Completed
- Manual payments: Always actionable when Pending

**Server-side enforcement:** Uploading proof for a non-actionable payment returns **422 Unprocessable Entity** with message "Debes completar el pago anterior antes de subir un comprobante."
