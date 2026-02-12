# Manual Testing - User Registration Workflow

## Prerequisites

1. Start the API: `cd src/Abuvi.API && dotnet run`
2. API should be running on `https://localhost:5001` or `http://localhost:5000`

## Test Scenarios

### Scenario 1: Complete Registration Flow (Happy Path)

#### Step 1: Register New User

```bash
curl -X POST https://localhost:5001/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!@#",
    "firstName": "John",
    "lastName": "Doe",
    "documentNumber": "12345678A",
    "phone": "+34612345678",
    "acceptedTerms": true
  }'
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "id": "guid-here",
    "email": "test@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phone": "+34612345678",
    "role": "Member",
    "isActive": false,
    "emailVerified": false,
    "createdAt": "2026-02-12T...",
    "updatedAt": "2026-02-12T..."
  }
}
```

**Check API Logs:**
Look for: `Verification email would be sent to test@example.com with URL: http://localhost:5173/verify-email?token={TOKEN}`

Copy the token from the logs.

#### Step 2: Verify Email

```bash
curl -X POST https://localhost:5001/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{
    "token": "GKzE7Z19LDKOQb0oa0nvjXL3yXXhBu9L_qmmF8-R1Q8="
  }'
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "message": "Email verified successfully"
  }
}
```

#### Step 3: Login with Verified Account

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!@#"
  }'
```

**Expected Response (200 OK):**

```json
{
  "success": true,
  "data": {
    "token": "jwt-token-here",
    "user": {
      "id": "guid",
      "email": "test@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "Member"
    }
  }
}
```

---

### Scenario 2: Register Without Document Number (Optional Field)

```bash
curl -X POST https://localhost:5001/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{
    "email": "nodoc@example.com",
    "password": "Test123!@#",
    "firstName": "Jane",
    "lastName": "Smith",
    "documentNumber": null,
    "phone": null,
    "acceptedTerms": true
  }'
```

**Expected:** Should succeed (200 OK) with `documentNumber` and `phone` as null.

---

### Scenario 3: Duplicate Email

```bash
curl -X POST https://localhost:5001/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!@#",
    "firstName": "Another",
    "lastName": "User",
    "documentNumber": "DIFFERENT",
    "acceptedTerms": true
  }'
```

**Expected Response (400 Bad Request):**

```json
{
  "success": false,
  "error": {
    "message": "An account with this email already exists",
    "code": "EMAIL_EXISTS"
  }
}
```

---

### Scenario 4: Duplicate Document Number

```bash
curl -X POST https://localhost:5001/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{
    "email": "unique@example.com",
    "password": "Test123!@#",
    "firstName": "Another",
    "lastName": "User",
    "documentNumber": "12345678A",
    "acceptedTerms": true
  }'
```

**Expected Response (400 Bad Request):**

```json
{
  "success": false,
  "error": {
    "message": "An account with this document number already exists",
    "code": "DOCUMENT_EXISTS"
  }
}
```

---

### Scenario 5: Invalid Token for Email Verification

```bash
curl -X POST https://localhost:5001/api/auth/verify-email \
  -H "Content-Type: application/json" \
  -d '{
    "token": "invalid-token-12345"
  }'
```

**Expected Response (404 Not Found):**

```json
{
  "success": false,
  "error": {
    "message": "User with ID 'Guid.Empty' was not found",
    "code": "NOT_FOUND"
  }
}
```

---

### Scenario 6: Resend Verification Email

```bash
curl -X POST https://localhost:5001/api/auth/resend-verification \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com"
  }'
```

**If already verified (400 Bad Request):**

```json
{
  "success": false,
  "error": {
    "message": "Email is already verified",
    "code": "RESEND_FAILED"
  }
}
```

**If not verified (200 OK):**

```json
{
  "success": true,
  "data": {
    "message": "Verification email sent"
  }
}
```

---

### Scenario 7: Validation Errors

#### Weak Password

```bash
curl -X POST https://localhost:5001/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{
    "email": "weak@example.com",
    "password": "weak",
    "firstName": "Test",
    "lastName": "User",
    "acceptedTerms": true
  }'
```

**Expected Response (400 Bad Request):**

```json
{
  "success": false,
  "error": {
    "message": "Validation failed",
    "code": "VALIDATION_ERROR",
    "details": {
      "Password": ["Password must be at least 8 characters", "Password must contain..."]
    }
  }
}
```

#### Invalid Document Number Format

```bash
curl -X POST https://localhost:5001/api/auth/register-user \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test2@example.com",
    "password": "Test123!@#",
    "firstName": "Test",
    "lastName": "User",
    "documentNumber": "abc123",
    "acceptedTerms": true
  }'
```

**Expected:** Should fail validation (document must be uppercase)

---

## Database Verification

Check the database to verify data:

```sql
-- View users
SELECT id, email, first_name, last_name, document_number,
       email_verified, is_active, created_at
FROM users
ORDER BY created_at DESC;

-- Check email verification tokens
SELECT email, email_verification_token, email_verification_token_expiry
FROM users
WHERE email_verified = false;
```

---

## Notes

- Email service is currently logging only (not sending real emails)
- Check API logs for verification tokens
- Tokens expire after 24 hours
- New users start with `isActive=false` and `emailVerified=false`
- After email verification, both become `true`
