# API Endpoints Documentation

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

| Check name | Failure status | What it verifies                                      |
| ---------- | -------------- | ----------------------------------------------------- |
| `database` | `Unhealthy`    | PostgreSQL connectivity via `SELECT 1` (5s timeout)   |
| `resend`   | `Degraded`     | Resend API key is configured in settings              |

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
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
}
```

> **Note:** `registrationCount` is always `0` and `availableSpots` equals `maxCapacity` until the Registrations feature is implemented.

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
    "photos": [
      {
        "id": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
        "photoReference": "ATplDJa...",
        "photoUrl": null,
        "width": 4032,
        "height": 3024,
        "attributionName": "Google User",
        "attributionUrl": "https://profiles.google.com/1234567890",
        "isPrimary": true,
        "displayOrder": 1
      }
    ],
    "createdAt": "2026-02-17T10:00:00Z",
    "updatedAt": "2026-02-17T10:00:00Z"
  }
}
```

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
  "pricePerBaby": 60.00
}
```

**Success Response (201 Created):** Same as `GET /api/camps/{id}` response (CampDetailResponse with photos)

**Error Responses:**

- **400 Bad Request**: Validation failed (negative prices, invalid coordinates)

---

### PUT /api/camps/{id}

Updates an existing camp.

**Authorization**: Admin or Board

**Request Body:** Same as POST plus `isActive` boolean

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

Manage the status lifecycle of camp editions. Status transitions follow a strict linear workflow: `Proposed → Draft → Open → Closed → Completed`. Rejection (archiving) is handled via `DELETE /api/camps/editions/{id}/reject`.

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

| From | To |
|------|----|
| `Proposed` | `Draft` |
| `Draft` | `Open` |
| `Open` | `Closed` |
| `Closed` | `Completed` |

Additional date constraints:

- `Draft → Open`: Edition's `startDate` must not be in the past
- `Closed → Completed`: Edition's `endDate` must be in the past

**Request Body:**

```json
{
  "status": "Draft"
}
```

**Success Response (200 OK):** Returns the updated `CampEditionResponse`.

**Error Responses:**

- **400 Bad Request**: Invalid transition or date constraint violation (`VALIDATION_ERROR` / `OPERATION_ERROR`)
- **401 Unauthorized**: User not authenticated
- **403 Forbidden**: Insufficient role
- **404 Not Found**: Edition not found (`NOT_FOUND`)

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
  "startDate": "2026-01-01"
}
```

**Field Notes:**

- `startDate`: Required, ISO 8601 date string (`YYYY-MM-DD`), must not be in the future

**Success Response (201 Created):** Same structure as GET response above

**Error Responses:**

- **400 Bad Request**: Validation failed (startDate in future)
- **403 Forbidden**: User is not authorized
- **404 Not Found**: Family unit or member doesn't exist
- **409 Conflict**: Member already has an active membership (`MEMBERSHIP_EXISTS`)

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
