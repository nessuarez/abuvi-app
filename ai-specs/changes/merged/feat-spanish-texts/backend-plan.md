# Backend Implementation Plan: Spanish Language Support

## Overview

Translate all user-facing backend text from English to Spanish, including validation messages, email templates, and API error messages. This plan follows **Test-Driven Development (TDD)** principles.

## TDD Approach

**Critical**: Follow the Red-Green-Refactor cycle:

1. **RED**: Update test expectations to Spanish (tests will fail)
2. **GREEN**: Update code to Spanish (tests will pass)
3. **REFACTOR**: Review and improve (if needed)

**DO NOT** write implementation code before updating tests.

## Implementation Phases

### Phase 1: FluentValidation Messages (TDD)

**Duration**: 2 hours

#### Step 1.1: Update RegisterUserValidator Tests (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/RegisterUserValidatorTests.cs`

**Changes**:
- Update all test assertions to expect Spanish validation messages
- Expected messages:
  - "El correo electrónico es obligatorio"
  - "Formato de correo electrónico inválido"
  - "La contraseña es obligatoria"
  - "La contraseña debe tener al menos 8 caracteres"
  - "La contraseña debe contener al menos una letra mayúscula"
  - "La contraseña debe contener al menos una letra minúscula"
  - "La contraseña debe contener al menos un dígito"
  - "La contraseña debe contener al menos un carácter especial"
  - "El nombre es obligatorio"
  - "Los apellidos son obligatorios"
  - "El número de documento solo debe contener letras mayúsculas y números"
  - "Formato de número de teléfono inválido (E.164)"
  - "Debes aceptar los términos y condiciones"

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~RegisterUserValidatorTests"
```
Expected: Tests should FAIL (RED phase)

#### Step 1.2: Update RegisterUserValidator Implementation (GREEN)

**File**: `src/Abuvi.API/Features/Auth/RegisterUserValidator.cs`

**Changes**:
```csharp
public class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres")
            .Matches(@"[A-Z]").WithMessage("La contraseña debe contener al menos una letra mayúscula")
            .Matches(@"[a-z]").WithMessage("La contraseña debe contener al menos una letra minúscula")
            .Matches(@"\d").WithMessage("La contraseña debe contener al menos un dígito")
            .Matches(@"[@$!%*?&#]").WithMessage("La contraseña debe contener al menos un carácter especial");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Los apellidos son obligatorios")
            .MaximumLength(100);

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(50)
            .Matches(@"^[A-Z0-9]+$").When(x => !string.IsNullOrEmpty(x.DocumentNumber))
            .WithMessage("El número de documento solo debe contener letras mayúsculas y números");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Formato de número de teléfono inválido (E.164)");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("Debes aceptar los términos y condiciones");
    }
}
```

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~RegisterUserValidatorTests"
```
Expected: Tests should PASS (GREEN phase)

#### Step 1.3: Update LoginRequestValidator Tests (RED)

**File**: `tests/Abuvi.Tests/Unit/Features/Auth/LoginRequestValidatorTests.cs`

**Changes**:
- Update test assertions to expect:
  - "El correo electrónico es obligatorio"
  - "Formato de correo electrónico inválido"
  - "La contraseña es obligatoria"

**Verification**: Run tests, expect FAIL

#### Step 1.4: Update LoginRequestValidator Implementation (GREEN)

**File**: `src/Abuvi.API/Features/Auth/LoginRequestValidator.cs`

**Changes**:
```csharp
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio")
            .EmailAddress().WithMessage("Formato de correo electrónico inválido");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria");
    }
}
```

**Verification**: Run tests, expect PASS

#### Step 1.5: Update VerifyEmailValidator Tests (RED) & Implementation (GREEN)

**Files**:
- Test: `tests/Abuvi.Tests/Unit/Features/Auth/VerifyEmailValidatorTests.cs`
- Impl: `src/Abuvi.API/Features/Auth/VerifyEmailValidator.cs`

**Messages**:
- "El código de verificación es obligatorio"
- "El correo electrónico es obligatorio"
- "Formato de correo electrónico inválido"

Follow RED-GREEN cycle.

#### Step 1.6: Update ResendVerificationValidator Tests (RED) & Implementation (GREEN)

**Files**:
- Test: `tests/Abuvi.Tests/Unit/Features/Auth/ResendVerificationValidatorTests.cs`
- Impl: `src/Abuvi.API/Features/Auth/ResendVerificationValidator.cs`

**Messages**:
- "El correo electrónico es obligatorio"
- "Formato de correo electrónico inválido"

Follow RED-GREEN cycle.

#### Step 1.7: Update UsersValidators Tests (RED) & Implementation (GREEN)

**Files**:
- Test: `tests/Abuvi.Tests/Unit/Features/Users/UsersValidatorsTests.cs`
- Impl: `src/Abuvi.API/Features/Users/UsersValidators.cs`

**Messages** (UpdateUserRoleValidator):
- "El ID de usuario es obligatorio"
- "El rol es obligatorio"

Follow RED-GREEN cycle.

#### Step 1.8: Run All Validator Tests

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~Validator"
```
Expected: All validator tests PASS

---

### Phase 2: API Exception Messages (TDD)

**Duration**: 2 hours

#### Step 2.1: Update NotFoundException Class

**File**: `src/Abuvi.API/Common/Exceptions/NotFoundException.cs`

**Current**:
```csharp
public class NotFoundException(string entityName, Guid id)
    : Exception($"{entityName} with ID '{id}' was not found");
```

**Updated**:
```csharp
public class NotFoundException(string entityName, Guid id)
    : Exception($"No se encontró {entityName} con ID '{id}'");
```

**Note**: This will affect all tests that check NotFoundException messages.

#### Step 2.2: Update AuthService Exception Tests (RED)

**Files**:
- `tests/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests.cs`
- `tests/Abuvi.Tests/Unit/Features/Auth/AuthServiceTests_Registration.cs`

**Update test assertions to expect**:
- "Ya existe un usuario con este correo electrónico"
- "Se requiere verificación de correo electrónico"
- "Correo electrónico o contraseña inválidos"
- "La cuenta de usuario no está activa"
- "Código de verificación inválido o expirado"
- "El correo electrónico ya está verificado"
- "No se encontró el usuario"

**Verification**: Run tests, expect FAIL

#### Step 2.3: Update AuthService Implementation (GREEN)

**File**: `src/Abuvi.API/Features/Auth/AuthService.cs`

**Update all exception messages**:

```csharp
// Registration
throw new BusinessRuleException("Ya existe un usuario con este correo electrónico");

// Login
throw new BusinessRuleException("Se requiere verificación de correo electrónico");
throw new BusinessRuleException("Correo electrónico o contraseña inválidos");
throw new BusinessRuleException("La cuenta de usuario no está activa");

// Email Verification
throw new BusinessRuleException("Código de verificación inválido o expirado");
throw new BusinessRuleException("El correo electrónico ya está verificado");
throw new BusinessRuleException("No se encontró el usuario");
```

**Verification**: Run AuthService tests, expect PASS

#### Step 2.4: Update UsersService Exception Tests (RED)

**Files**:
- `tests/Abuvi.Tests/Unit/Features/Users/UsersServiceTests.cs`
- `tests/Abuvi.Tests/Unit/Features/Users/UsersServiceRoleUpdateTests.cs`

**Update test assertions to expect**:
- "No se encontró User con ID '{id}'" (from NotFoundException)
- "No puedes cambiar tu propio rol"
- "Debe existir al menos un administrador en el sistema"

**Verification**: Run tests, expect FAIL

#### Step 2.5: Update UsersService Implementation (GREEN)

**File**: `src/Abuvi.API/Features/Users/UsersService.cs`

**Update exception messages**:

```csharp
// NotFoundException uses class message (already updated in Step 2.1)
throw new NotFoundException(nameof(User), userId);

// Business rules
throw new BusinessRuleException("No puedes cambiar tu propio rol");
throw new BusinessRuleException("Debe existir al menos un administrador en el sistema");
```

**Verification**: Run UsersService tests, expect PASS

#### Step 2.6: Run All Service Tests

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~Service"
```
Expected: All service tests PASS

---

### Phase 3: Email Templates (No Tests)

**Duration**: 1 hour

**Note**: Email templates don't have unit tests. We'll update them directly and verify manually.

#### Step 3.1: Update Verification Email Template

**File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`

**Method**: `SendVerificationEmailAsync`

**Update**:
```csharp
Subject = "Verifica tu correo electrónico - Campamentos Abuvi",
HtmlBody = $@"
    <html>
    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
            <h2 style='color: #2563eb;'>¡Bienvenido a Abuvi, {firstName}!</h2>
            <p>Gracias por registrarte. Por favor verifica tu dirección de correo electrónico haciendo clic en el enlace de abajo:</p>
            <p style='margin: 30px 0;'>
                <a href=""{verificationUrl}""
                   style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                    Verificar Correo
                </a>
            </p>
            <p style='color: #666; font-size: 14px;'>Este enlace expirará en 24 horas.</p>
            <p style='color: #666; font-size: 14px;'>Si no creaste esta cuenta, por favor ignora este correo.</p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
            <p style='color: #999; font-size: 12px;'>
                Saludos cordiales,<br>
                El equipo de Abuvi
            </p>
        </div>
    </body>
    </html>
"
```

#### Step 3.2: Update Welcome Email Template

**File**: `src/Abuvi.API/Common/Services/ResendEmailService.cs`

**Method**: `SendWelcomeEmailAsync`

**Update**:
```csharp
Subject = "¡Bienvenido a Abuvi!",
HtmlBody = $@"
    <html>
    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
            <h2 style='color: #2563eb;'>¡Tu cuenta está activada, {firstName}!</h2>
            <p>Tu correo electrónico ha sido verificado exitosamente. Ahora puedes acceder a todas las funciones de Abuvi.</p>
            <h3 style='color: #1e40af; margin-top: 30px;'>Próximos pasos:</h3>
            <ul style='line-height: 2;'>
                <li>Completa tu perfil</li>
                <li>Explora los próximos campamentos</li>
                <li>Registra a los miembros de tu familia</li>
                <li>Consulta el historial de aniversarios</li>
            </ul>
            <p style='margin: 30px 0;'>
                <a href=""{dashboardUrl}""
                   style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                    Ir al Inicio
                </a>
            </p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
            <p style='color: #999; font-size: 12px;'>
                Saludos cordiales,<br>
                El equipo de Abuvi
            </p>
        </div>
    </body>
    </html>
"
```

#### Step 3.3: Manual Verification

**Test email sending**:
1. Start the application
2. Register a new test user
3. Check email inbox for verification email (Spanish)
4. Verify email and check welcome email (Spanish)
5. Verify all text is in Spanish and properly formatted

---

### Phase 4: Integration Tests Updates (TDD)

**Duration**: 1 hour

**Note**: Only update if integration tests exist and are running.

#### Step 4.1: Update AuthIntegrationTests (RED then GREEN)

**File**: `tests/Abuvi.Tests/Integration/Features/AuthIntegrationTests.cs`

**If exists**: Update all assertions to expect Spanish error messages from API responses.

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

---

### Phase 5: Documentation Updates

**Duration**: 30 minutes

#### Step 5.1: Update Backend Standards

**File**: `ai-specs/specs/backend-standards.mdc`

**Add section**:

```markdown
## Language Standards for User-Facing Content

### Email Templates

- **Language**: Spanish (default)
- **Tone**: Professional and friendly (informal "tú" form)
- **Format**: HTML with inline CSS for email client compatibility
- **Accessibility**: Include alt text for images, high contrast colors

### Validation Messages

- **Language**: Spanish
- **FluentValidation**: All `.WithMessage()` calls must use Spanish text
- **Pattern**: "{Field} es obligatorio/a", "{Field} debe {requirement}"
- **Gender Agreement**: Match grammatical gender with field name
  - Masculine: "El campo es obligatorio", "El correo es obligatorio"
  - Feminine: "La contraseña es obligatoria", "La fecha es obligatoria"

### API Error Messages

- **Language**: Spanish for user-facing errors
- **Log Messages**: English (for developers)
- **Exception Messages**: Spanish (shown to users via API)
- **Pattern**: Clear, actionable messages that explain what went wrong

### Examples

```csharp
// ✅ Good: Spanish user message, English log
_logger.LogError("User registration failed for {Email}", email);
throw new BusinessRuleException("Ya existe un usuario con este correo electrónico");

// ❌ Bad: English user message
throw new BusinessRuleException("User already exists with this email");

// ❌ Bad: Mixed languages in log
_logger.LogError("Error en registro para {Email}", email);
```

### Common Translations

| English | Spanish |
|---------|---------|
| Email is required | El correo electrónico es obligatorio |
| Password is required | La contraseña es obligatoria |
| Invalid email format | Formato de correo electrónico inválido |
| User already exists | Ya existe un usuario con este correo electrónico |
| Invalid credentials | Correo electrónico o contraseña inválidos |
| Verification required | Se requiere verificación de correo electrónico |
| Account not active | La cuenta de usuario no está activa |
| Not found | No se encontró |
```

---

## Verification Checklist

After completing all phases:

### Unit Tests
- [ ] All validator tests pass
- [ ] All service tests pass
- [ ] All auth tests pass
- [ ] All user tests pass
- [ ] Run full test suite: `dotnet test`

### Code Review
- [ ] All FluentValidation messages in Spanish
- [ ] All exception messages in Spanish
- [ ] All email templates in Spanish
- [ ] No English text in user-facing areas
- [ ] Log messages remain in English (developer-facing)

### Manual Testing
- [ ] Register new user → Spanish validation errors
- [ ] Submit invalid login → Spanish error message
- [ ] Receive verification email → Spanish content
- [ ] Verify email → Spanish welcome email
- [ ] Update user role with validation errors → Spanish messages
- [ ] Test all API error scenarios → Spanish responses

### Documentation
- [ ] Backend standards updated with language section
- [ ] Examples provided for developers
- [ ] Translation reference included

---

## Success Criteria

1. ✅ All backend tests passing (100%)
2. ✅ All validation messages in Spanish with correct gender agreement
3. ✅ All API exception messages in Spanish
4. ✅ All email templates in Spanish with professional formatting
5. ✅ Backend standards documentation updated
6. ✅ Log messages remain in English for debugging
7. ✅ Manual testing confirms all user-facing text is Spanish
8. ✅ No mixed English/Spanish in any user-facing message

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing tests | Follow TDD: Update tests first, then code |
| Grammatical errors in Spanish | Use provided translation reference |
| Missing translations | Use comprehensive checklist |
| Log messages accidentally translated | Keep clear separation: Spanish for users, English for logs |

---

## Estimated Time

- Phase 1: FluentValidation Messages - 2 hours
- Phase 2: API Exception Messages - 2 hours
- Phase 3: Email Templates - 1 hour
- Phase 4: Integration Tests - 1 hour (if applicable)
- Phase 5: Documentation - 30 minutes

**Total**: ~6.5 hours

---

## Dependencies

- Existing test infrastructure (xUnit, Moq, FluentAssertions)
- ResendEmailService already implemented
- No external dependencies required

---

## Next Steps

After backend completion:
1. Frontend implementation (separate ticket)
2. End-to-end testing with both frontend and backend in Spanish
3. User acceptance testing

---

**Plan Status**: Ready for development
**TDD Approach**: ✅ Enforced (RED-GREEN-REFACTOR)
**Estimated Effort**: 6.5 hours
**Priority**: High
