# API Endpoints Documentation

This document describes the REST API endpoints for the ABUVI web application.

## Base URL

- **Development**: `http://localhost:5079/api`
- **Production**: TBD

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
**Error Responses:**
- **403 Forbidden**: User is not the representative
- **404 Not Found**: Family unit or member doesn't exist
- **409 Conflict**: Attempting to delete representative's own record (`CANNOT_DELETE_REPRESENTATIVE`)

---

## Camp Management Endpoints

Manage camp location templates. All endpoints require Admin or Board role.

**Base Path:** `/api/camps`

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


