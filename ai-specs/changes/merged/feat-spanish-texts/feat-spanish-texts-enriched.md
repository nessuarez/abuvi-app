# Spanish Language Implementation - Enriched User Story

## Overview

Translate all user-facing texts in the Abuvi application from English to Spanish, establishing Spanish as the default language for the application. This includes UI components, validation messages, email templates, and API error messages.

## Business Context

The Abuvi application is primarily used by Spanish-speaking families and camp organizers in Spain. Having the entire application in Spanish will:

- Improve user experience and accessibility for Spanish-speaking users
- Reduce confusion with mixed English/Spanish text currently present in the app
- Align with the organization's primary language and target audience
- Meet user expectations for a Spanish-based organization
- Improve trust and professionalism

## Current State Analysis

The application currently has:

**Frontend:**

- **Mixed language state**: Some components have Spanish UI text (LoginForm: "Contraseña", "Recordarme") but English validation messages
- **English-only areas**: Navigation (AppHeader: "Home", "Camp", "Users"), form labels, buttons, error messages
- **Inconsistent**: Different components use different language patterns

**Backend:**

- **English email templates**: Verification, welcome, password reset emails
- **English validation messages**: All FluentValidation rules use English error messages
- **English API errors**: BusinessRuleException, NotFoundException messages

**What Stays in English:**

- Code (variables, function names, class names) - per development standards
- Git commit messages - per development standards
- Log messages - for developer debugging
- Comments in code - per development standards

## Technical Requirements

### 1. Frontend Changes

#### 1.1 Update All Component Texts

**Files to Modify:**

```
frontend/src/components/auth/
  ├── LoginForm.vue           ✅ Already has Spanish UI (needs validation messages)
  ├── RegisterForm.vue        ❌ Needs Spanish text
  └── AuthContainer.vue       ❌ Needs Spanish text

frontend/src/components/layout/
  ├── AppHeader.vue           ❌ All English navigation labels
  ├── AppFooter.vue           ❌ Needs Spanish text
  └── UserMenu.vue            ❌ Needs Spanish text

frontend/src/components/home/
  ├── QuickAccessCards.vue    ❌ Needs Spanish text
  ├── QuickAccessCard.vue     ❌ Needs Spanish text
  └── AnniversarySection.vue  ❌ Needs Spanish text

frontend/src/views/
  ├── LandingPage.vue         ❌ Needs Spanish text
  ├── HomePage.vue            ❌ Needs Spanish text
  ├── CampPage.vue            ❌ Needs Spanish text
  ├── AnniversaryPage.vue     ❌ Needs Spanish text
  ├── ProfilePage.vue         ❌ Needs Spanish text
  └── AdminPage.vue           ❌ Needs Spanish text

frontend/src/components/users/
  ├── UserCard.vue            ❌ Needs Spanish text
  ├── UserForm.vue            ❌ Needs Spanish text
  ├── UserRoleCell.vue        ❌ Needs Spanish text
  └── UserRoleDialog.vue      ❌ Needs Spanish text

frontend/src/pages/ (legacy - to be migrated)
  ├── UsersPage.vue           ❌ Needs Spanish text
  ├── UserDetailPage.vue      ❌ Needs Spanish text
  ├── HomePage.vue            ❌ Needs Spanish text
  ├── LoginPage.vue           ❌ Needs Spanish text
  └── RegisterPage.vue        ❌ Needs Spanish text
```

#### 1.2 Translation Reference Guide

**Common UI Elements:**

| English | Spanish |
|---------|---------|
| **Navigation** | |
| Home | Inicio |
| Camp | Campamento |
| Anniversary | Aniversario |
| My Profile | Mi Perfil |
| Users | Usuarios |
| Admin | Administración |
| Logout | Cerrar Sesión |
| **Authentication** | |
| Login | Iniciar Sesión |
| Register | Registrarse |
| Sign In | Iniciar Sesión |
| Sign Up | Crear Cuenta |
| Email | Correo Electrónico |
| Password | Contraseña |
| Remember Me | Recordarme |
| Forgot Password? | ¿Olvidaste tu contraseña? |
| **Forms & Buttons** | |
| Submit | Enviar |
| Save | Guardar |
| Cancel | Cancelar |
| Delete | Eliminar |
| Edit | Editar |
| Create | Crear |
| Update | Actualizar |
| Search | Buscar |
| Filter | Filtrar |
| Clear | Limpiar |
| **User Fields** | |
| First Name | Nombre |
| Last Name | Apellidos |
| Full Name | Nombre Completo |
| Phone | Teléfono |
| Document Number | Número de Documento |
| Role | Rol |
| Status | Estado |
| Created At | Fecha de Creación |
| Updated At | Última Actualización |
| **Validation Messages** | |
| is required | es obligatorio / es obligatoria |
| Invalid email format | Formato de correo inválido |
| Password must be at least 8 characters | La contraseña debe tener al menos 8 caracteres |
| Passwords must match | Las contraseñas deben coincidir |
| Field must not be empty | El campo no puede estar vacío |
| Invalid format | Formato inválido |
| Value must be greater than {0} | El valor debe ser mayor que {0} |
| **User Roles** | |
| Admin | Administrador |
| Board | Junta Directiva |
| Member | Socio |
| Guardian | Tutor |
| **Status Messages** | |
| Loading... | Cargando... |
| Success | Éxito |
| Error | Error |
| Warning | Advertencia |
| No results found | No se encontraron resultados |
| Please wait... | Por favor espera... |
| **Common Actions** | |
| View Details | Ver Detalles |
| Go Back | Volver |
| Next | Siguiente |
| Previous | Anterior |
| Confirm | Confirmar |
| Yes | Sí |
| No | No |
| **Error Messages** | |
| Something went wrong | Algo salió mal |
| Please try again | Por favor intenta de nuevo |
| Operation failed | La operación falló |
| Unauthorized | No autorizado |
| Access denied | Acceso denegado |
| Not found | No encontrado |
| Invalid request | Solicitud inválida |

#### 1.3 Validation Messages Pattern

**Client-side validation (Vue components):**

```typescript
// Before (English)
const validate = (): boolean => {
  errors.value = {}

  if (!formData.email.trim()) {
    errors.value.email = 'Email is required'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Invalid email format'
  }

  if (!formData.password) {
    errors.value.password = 'Password is required'
  }

  return Object.keys(errors.value).length === 0
}

// After (Spanish)
const validate = (): boolean => {
  errors.value = {}

  if (!formData.email.trim()) {
    errors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Formato de correo inválido'
  }

  if (!formData.password) {
    errors.value.password = 'La contraseña es obligatoria'
  }

  return Object.keys(errors.value).length === 0
}
```

**Example Component Updates:**

```vue
<!-- AppHeader.vue - Before -->
<template>
  <div class="flex items-center gap-2">
    <label for="rememberMe">Remember Me</label>
  </div>
  <Button label="Logout" />
</template>

<!-- AppHeader.vue - After -->
<template>
  <div class="flex items-center gap-2">
    <label for="rememberMe">Recordarme</label>
  </div>
  <Button label="Cerrar Sesión" />
</template>
```

#### 1.4 Router Meta Titles

**File:** `frontend/src/router/index.ts`

Update all route meta titles to Spanish:

```typescript
// Before
const routes = [
  {
    path: '/',
    component: LandingPage,
    meta: { title: 'ABUVI - Welcome' }
  },
  {
    path: '/home',
    component: HomePage,
    meta: { title: 'Home - ABUVI', requiresAuth: true }
  },
  // ...
]

// After
const routes = [
  {
    path: '/',
    component: LandingPage,
    meta: { title: 'ABUVI - Bienvenido' }
  },
  {
    path: '/home',
    component: HomePage,
    meta: { title: 'ABUVI', requiresAuth: true }
  },
  {
    path: '/camp',
    component: CampPage,
    meta: { title: 'Campamento - ABUVI', requiresAuth: true }
  },
  {
    path: '/anniversary',
    component: AnniversaryPage,
    meta: { title: 'Aniversario - ABUVI', requiresAuth: true }
  },
  {
    path: '/profile',
    component: ProfilePage,
    meta: { title: 'Mi Perfil - ABUVI', requiresAuth: true }
  },
  {
    path: '/users',
    component: UsersPage,
    meta: { title: 'Usuarios - ABUVI', requiresAuth: true, requiresBoard: true }
  },
  {
    path: '/admin',
    component: AdminPage,
    meta: { title: 'Administración - ABUVI', requiresAuth: true, requiresAdmin: true }
  }
]
```

#### 1.5 API Error Message Handling

**File:** `frontend/src/stores/auth.ts`

Update error handling to display Spanish messages:

```typescript
// Before
async function login(credentials: LoginRequest): Promise<{ success: boolean; error?: string }> {
  try {
    const response = await api.post<ApiResponse<AuthResponse>>('/auth/login', credentials)

    if (response.data.success && response.data.data) {
      // ...
      return { success: true }
    }

    return { success: false, error: response.data.error?.message || 'Login failed' }
  } catch (error) {
    return { success: false, error: 'An error occurred during login' }
  }
}

// After
async function login(credentials: LoginRequest): Promise<{ success: boolean; error?: string }> {
  try {
    const response = await api.post<ApiResponse<AuthResponse>>('/auth/login', credentials)

    if (response.data.success && response.data.data) {
      // ...
      return { success: true }
    }

    return { success: false, error: response.data.error?.message || 'Error al iniciar sesión' }
  } catch (error) {
    return { success: false, error: 'Ocurrió un error durante el inicio de sesión' }
  }
}
```

### 2. Backend Changes

#### 2.1 Email Templates

**File:** `src/Abuvi.API/Common/Services/ResendEmailService.cs`

Translate all email templates to Spanish:

**Verification Email:**

```csharp
// Before
Subject = "Verify your email - Abuvi Camps",
HtmlBody = $@"
    <html>
    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
            <h2 style='color: #2563eb;'>Welcome to Abuvi, {firstName}!</h2>
            <p>Thank you for registering. Please verify your email address by clicking the link below:</p>
            <p style='margin: 30px 0;'>
                <a href=""{verificationUrl}""
                   style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                    Verify Email
                </a>
            </p>
            <p style='color: #666; font-size: 14px;'>This link will expire in 24 hours.</p>
            <p style='color: #666; font-size: 14px;'>If you didn't create this account, please ignore this email.</p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
            <p style='color: #999; font-size: 12px;'>
                Best regards,<br>
                The Abuvi Team
            </p>
        </div>
    </body>
    </html>
"

// After (Spanish)
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

**Welcome Email:**

```csharp
// After (Spanish)
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

**Password Reset Email (if implemented):**

```csharp
// After (Spanish)
Subject = "Restablece tu contraseña - Abuvi",
HtmlBody = $@"
    <html>
    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
        <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
            <h2 style='color: #2563eb;'>Restablecimiento de Contraseña</h2>
            <p>Hola {firstName},</p>
            <p>Recibimos una solicitud para restablecer tu contraseña. Si no fuiste tú, por favor ignora este correo.</p>
            <p style='margin: 30px 0;'>
                <a href=""{resetUrl}""
                   style='background-color: #2563eb; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                    Restablecer Contraseña
                </a>
            </p>
            <p style='color: #666; font-size: 14px;'>Este enlace expirará en 1 hora por seguridad.</p>
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

#### 2.2 FluentValidation Messages

**File:** `src/Abuvi.API/Features/Auth/RegisterUserValidator.cs`

Update all validation messages to Spanish:

```csharp
// Before
public class RegisterUserValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"\d").WithMessage("Password must contain at least one digit")
            .Matches(@"[@$!%*?&#]").WithMessage("Password must contain at least one special character");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);

        RuleFor(x => x.DocumentNumber)
            .MaximumLength(50)
            .Matches(@"^[A-Z0-9]+$").When(x => !string.IsNullOrEmpty(x.DocumentNumber))
            .WithMessage("Document number must contain only uppercase letters and numbers");

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Invalid phone number format (E.164)");

        RuleFor(x => x.AcceptedTerms)
            .Equal(true).WithMessage("You must accept the terms and conditions");
    }
}

// After (Spanish)
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

**Similar updates needed for:**

- `LoginRequestValidator.cs`
- `VerifyEmailValidator.cs`
- `ResendVerificationValidator.cs`
- `UsersValidators.cs` (UpdateUserRoleValidator)
- Any future validators

#### 2.3 API Exception Messages

**Files:**

- `src/Abuvi.API/Common/Exceptions/BusinessRuleException.cs`
- `src/Abuvi.API/Common/Exceptions/NotFoundException.cs`
- Service classes that throw exceptions

**Update all exception messages thrown in services:**

```csharp
// AuthService.cs - Before
throw new BusinessRuleException("User already exists with this email");
throw new BusinessRuleException("Email verification required");
throw new BusinessRuleException("Invalid email or password");
throw new BusinessRuleException("User account is not active");

// AuthService.cs - After (Spanish)
throw new BusinessRuleException("Ya existe un usuario con este correo electrónico");
throw new BusinessRuleException("Se requiere verificación de correo electrónico");
throw new BusinessRuleException("Correo electrónico o contraseña inválidos");
throw new BusinessRuleException("La cuenta de usuario no está activa");

// UsersService.cs - Before
throw new NotFoundException(nameof(User), userId);
throw new BusinessRuleException("Cannot change own role");
throw new BusinessRuleException("At least one admin must exist in the system");

// UsersService.cs - After (Spanish)
throw new NotFoundException(nameof(User), userId); // Keep constructor signature, update message in class
throw new BusinessRuleException("No puedes cambiar tu propio rol");
throw new BusinessRuleException("Debe existir al menos un administrador en el sistema");
```

**Update NotFoundException class:**

```csharp
// Before
public class NotFoundException(string entityName, Guid id)
    : Exception($"{entityName} with ID '{id}' was not found");

// After (Spanish)
public class NotFoundException(string entityName, Guid id)
    : Exception($"{entityName} con ID '{id}' no fue encontrado");

// Or more natural Spanish:
public class NotFoundException(string entityName, Guid id)
    : Exception($"No se encontró {entityName} con ID '{id}'");
```

### 3. Documentation Updates

#### 3.1 Update Frontend Standards

**File:** `ai-specs/specs/frontend-standards.mdc`

Add a new section about language standards:

```markdown
## Language Standards

### User-Facing Text

- **Default Language**: Spanish (Castellano)
- All UI text, labels, buttons, navigation, error messages, and validation messages must be in Spanish
- Use formal "usted" form for professional communication
- Use gender-neutral language where possible

### Code and Documentation

- **Code**: English only (variables, functions, classes, interfaces, types)
- **Comments**: English only
- **Commit Messages**: English only
- **Log Messages**: English only (for developer debugging)
- **Technical Documentation**: English (README, API docs, architecture docs)
- **User Documentation**: Spanish (help guides, FAQs, user manuals)

### Common Translations

| English | Spanish |
|---------|---------|
| Email | Correo Electrónico |
| Password | Contraseña |
| Login | Iniciar Sesión |
| Register | Registrarse |
| Submit | Enviar |
| Cancel | Cancelar |
| Delete | Eliminar |
| Edit | Editar |
| Save | Guardar |
| Loading... | Cargando... |
| Error | Error |
| Success | Éxito |

### Validation Message Patterns

**Gender Agreement:**
- Use masculine form for generic messages: "El campo es obligatorio"
- Use feminine form when the noun is feminine: "La contraseña es obligatoria", "La fecha es obligatoria"
- Common endings:
  - Masculine: obligatorio, inválido, requerido
  - Feminine: obligatoria, inválida, requerida

**Example Messages:**
- "El correo electrónico es obligatorio" (masculine)
- "La contraseña es obligatoria" (feminine)
- "El nombre debe tener al menos 2 caracteres"
- "La fecha debe ser futura"
```

#### 3.2 Update Backend Standards

**File:** `ai-specs/specs/backend-standards.mdc`

Add language standards section:

```markdown
## Language Standards for User-Facing Content

### Email Templates

- **Language**: Spanish (default)
- **Tone**: Professional and friendly
- **Format**: HTML with inline CSS for email client compatibility
- **Accessibility**: Include alt text for images, high contrast colors

### Validation Messages

- **Language**: Spanish
- **FluentValidation**: All `.WithMessage()` calls must use Spanish text
- **Pattern**: "{Field} es obligatorio/a", "{Field} debe {requirement}"
- **Gender Agreement**: Match grammatical gender with field name

### API Error Messages

- **Language**: Spanish for user-facing errors
- **Log Messages**: English (for developers)
- **Exception Messages**: Spanish (shown to users via API)
- **Pattern**: Clear, actionable messages that explain what went wrong

### Language Consistency

```csharp
// ✅ Good: Spanish user message, English log
_logger.LogError("User registration failed for {Email}", email);
throw new BusinessRuleException("Ya existe un usuario con este correo electrónico");

// ❌ Bad: Mixed languages in user message
throw new BusinessRuleException("User already exists with this email");
```

```

### 4. Testing Updates

#### 4.1 Update Test Expectations

All tests that check for text messages must be updated to expect Spanish:

**Frontend Tests:**

```typescript
// Before
expect(errorMessage).toBe('Email is required')
expect(button.text()).toBe('Login')

// After
expect(errorMessage).toBe('El correo electrónico es obligatorio')
expect(button.text()).toBe('Iniciar Sesión')
```

**Backend Tests:**

```csharp
// Before
var exception = await Assert.ThrowsAsync<BusinessRuleException>(
    () => sut.RegisterAsync(request, CancellationToken.None));
Assert.Equal("User already exists with this email", exception.Message);

// After
var exception = await Assert.ThrowsAsync<BusinessRuleException>(
    () => sut.RegisterAsync(request, CancellationToken.None));
Assert.Equal("Ya existe un usuario con este correo electrónico", exception.Message);
```

**Files to Update:**

```
Frontend Tests:
- frontend/src/components/users/__tests__/UserForm.test.ts
- frontend/src/components/users/__tests__/UserCard.test.ts
- frontend/src/composables/__tests__/useAuth.test.ts
- frontend/src/composables/__tests__/useUsers.test.ts
- frontend/src/stores/__tests__/auth.test.ts

Backend Tests:
- tests/Unit/Features/Auth/RegisterUserValidatorTests.cs
- tests/Unit/Features/Auth/AuthServiceTests.cs
- tests/Unit/Features/Auth/AuthServiceTests_Registration.cs
- tests/Unit/Features/Users/UsersServiceTests.cs
- tests/Unit/Features/Users/UsersServiceRoleUpdateTests.cs
- tests/Unit/Features/UsersValidatorsTests.cs
- tests/Integration/Features/AuthIntegrationTests.cs
```

### 5. Acceptance Criteria

**Definition of Done:**

1. ✅ All frontend components display Spanish text (no English UI text)
2. ✅ All validation messages in frontend are in Spanish
3. ✅ All route meta titles are in Spanish
4. ✅ All email templates are in Spanish with proper formatting
5. ✅ All FluentValidation messages are in Spanish with correct gender agreement
6. ✅ All API exception messages are in Spanish
7. ✅ Frontend standards updated to specify Spanish as default language
8. ✅ Backend standards updated to specify Spanish for user-facing content
9. ✅ All tests updated to expect Spanish messages
10. ✅ All tests passing (unit and integration)
11. ✅ Manual testing completed - application fully usable in Spanish
12. ✅ No mixed English/Spanish text in user-facing areas
13. ✅ Code, comments, and log messages remain in English (per standards)

### 6. Implementation Steps (Incremental Approach)

#### Phase 1: Frontend Core Components (2-3 hours)

**Priority: High - Most user-visible**

1. **Authentication Components**
   - Update `LoginForm.vue` validation messages
   - Update `RegisterForm.vue` to Spanish
   - Update `AuthContainer.vue` to Spanish
   - Test authentication flow

2. **Navigation & Layout**
   - Update `AppHeader.vue` navigation labels
   - Update `AppFooter.vue` to Spanish
   - Update `UserMenu.vue` to Spanish
   - Test navigation between pages

3. **Router & Page Titles**
   - Update `router/index.ts` meta titles
   - Update `App.vue` default title
   - Test page title updates on navigation

#### Phase 2: Backend Validation & Errors (2 hours)

**Priority: High - Core functionality**

1. **Validation Messages**
   - Update `RegisterUserValidator.cs`
   - Update `LoginRequestValidator.cs`
   - Update `VerifyEmailValidator.cs`
   - Update `ResendVerificationValidator.cs`
   - Update `UsersValidators.cs`

2. **Exception Messages**
   - Update `BusinessRuleException` usages in `AuthService.cs`
   - Update `BusinessRuleException` usages in `UsersService.cs`
   - Update `NotFoundException` class message
   - Update other service exception messages

3. **Test Updates**
   - Update validator tests to expect Spanish messages
   - Update service tests to expect Spanish messages
   - Run all backend tests

#### Phase 3: Email Templates (1-2 hours)

**Priority: High - User communication**

1. **Transactional Emails**
   - Update `SendVerificationEmailAsync` template
   - Update `SendWelcomeEmailAsync` template
   - Update `SendPasswordResetEmailAsync` template (if exists)
   - Test email sending with real Resend account

2. **Future Email Templates**
   - Update camp registration confirmation template (when implemented)
   - Update payment receipt template (when implemented)
   - Update event reminder template (when implemented)

#### Phase 4: Frontend Views & Components (3-4 hours)

**Priority: Medium - Complete coverage**

1. **Home & Landing**
   - Update `views/LandingPage.vue`
   - Update `views/HomePage.vue`
   - Update `components/home/QuickAccessCards.vue`
   - Update `components/home/QuickAccessCard.vue`
   - Update `components/home/AnniversarySection.vue`

2. **User Management**
   - Update `views/ProfilePage.vue`
   - Update `pages/UsersPage.vue`
   - Update `pages/UserDetailPage.vue`
   - Update `components/users/UserCard.vue`
   - Update `components/users/UserForm.vue`
   - Update `components/users/UserRoleCell.vue`
   - Update `components/users/UserRoleDialog.vue`

3. **Camp & Anniversary**
   - Update `views/CampPage.vue`
   - Update `views/AnniversaryPage.vue`
   - Update `views/AdminPage.vue`

4. **API Error Handling**
   - Update `stores/auth.ts` error messages
   - Update `composables/useAuth.ts` error messages
   - Update `composables/useUsers.ts` error messages

#### Phase 5: Testing & Documentation (2 hours)

**Priority: High - Quality assurance**

1. **Update Tests**
   - Update all frontend tests to expect Spanish
   - Update all backend tests to expect Spanish
   - Run full test suite
   - Fix any failing tests

2. **Update Documentation**
   - Update `frontend-standards.mdc` with language section
   - Update `backend-standards.mdc` with language section
   - Update README.md to mention Spanish as default language
   - Create translation reference guide (this document)

3. **Manual Testing**
   - Complete user registration flow
   - Test all email templates
   - Test all validation errors
   - Test all pages and components
   - Test on mobile and desktop
   - Test with screen reader (accessibility)

### 7. Translation Quality Guidelines

#### Grammar Rules

**Gender Agreement:**

- Adjectives must agree with noun gender
  - "El campo es obligatorio" (masculine)
  - "La contraseña es obligatoria" (feminine)
  - "Los datos son obligatorios" (plural masculine)
  - "Las fechas son obligatorias" (plural feminine)

**Common Nouns Gender:**

- Masculine: el campo, el nombre, el correo, el teléfono, el documento, el rol, el estado
- Feminine: la contraseña, la fecha, la dirección, la cuenta

**Formal vs Informal:**

- Use formal "usted" form for all user-facing text
  - "Por favor verifica tu correo" (informal) ❌
  - "Por favor verifique su correo" (formal) ❌
  - "Por favor verifica tu correo" (informal but acceptable for friendly tone) ✅

**Decision**: Use informal "tú" form for a friendlier, more approachable tone suitable for a family camp organization.

#### Translation Best Practices

1. **Clarity over Literal Translation**
   - "Submit" → "Enviar" (not "Someter")
   - "Loading..." → "Cargando..." (not "Carga...")

2. **Natural Spanish Phrasing**
   - "Email is required" → "El correo electrónico es obligatorio" (natural)
   - Not: "Email es requerido" (anglicism)

3. **Consistent Terminology**
   - Always use "correo electrónico" for "email" (not "e-mail", "mail", or "correo")
   - Always use "contraseña" for "password" (not "clave" or "password")

4. **Button Labels**
   - Use infinitive verbs: "Iniciar Sesión", "Guardar", "Enviar"
   - Not imperative: "Inicie Sesión", "Guarde", "Envíe"

5. **Error Messages**
   - Be specific and helpful
   - "Invalid email format" → "Formato de correo electrónico inválido"
   - "Something went wrong" → "Algo salió mal. Por favor intenta de nuevo."

### 8. Future Enhancements (Out of Scope)

**Multi-language Support (Future):**

- Implement i18n library (vue-i18n for frontend)
- Extract all text to language files
- Support English as secondary language
- Allow users to choose language preference
- Store language preference in user profile

**Regional Variations:**

- Support for Latin American Spanish vs Spain Spanish
- Different date/time formats
- Different currency symbols

**Accessibility:**

- Add aria-label translations
- Support for screen readers in Spanish
- High contrast mode with Spanish labels

### 9. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Missing translations | Inconsistent UX | Create comprehensive translation reference table |
| Grammatical errors | Unprofessional appearance | Review by native Spanish speaker |
| Test failures | Delays deployment | Update tests incrementally with code changes |
| Breaking existing functionality | User impact | Follow TDD, test after each change |
| Mixed English/Spanish | Confusing UX | Thorough manual testing, checklist review |
| Email templates not rendering | Email delivery issues | Test with real Resend account, preview emails |

### 10. Success Metrics

- 100% of UI text in Spanish (0 English text in user interface)
- 100% of email templates in Spanish
- 100% of validation messages in Spanish
- 100% test coverage maintained (90%+ for new code)
- 0 English text found in manual testing
- All tests passing after updates
- Positive user feedback on language consistency

### 11. Related Documentation

- [Frontend Standards](../../specs/frontend-standards.mdc) - To be updated with language section
- [Backend Standards](../../specs/backend-standards.mdc) - To be updated with language section
- [Resend Integration](../feat-resend-integration/resend-integration_enriched.md) - Email template context
- [Base Standards](../../specs/base-standards.mdc) - Core principles (English for code)

### 12. Dependencies

**External:**

- None (self-contained language update)

**Internal:**

- Resend email service (already implemented)
- All existing components and services
- All existing validators

---

**Story Status:** Ready for development
**Priority:** High
**Estimated Effort:** 8-12 hours (over 2-3 days)
**Dependencies:** None (can start immediately)
**Assigned Feature Directory:** `ai-specs/changes/feat-spanish-texts/`
