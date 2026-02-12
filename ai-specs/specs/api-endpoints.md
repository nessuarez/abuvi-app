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
