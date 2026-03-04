# Fix: Pantalla de registro no muestra feedback tras registrar usuario

## Contexto

Actualmente, cuando un usuario completa el formulario de registro y lo envía, la pantalla no proporciona feedback adecuado. El usuario se queda viendo el formulario con los datos rellenados, sin mensaje de éxito ni redirección, dando la impresión de que el registro no se completó.

## Análisis de causa raíz

Se han identificado **dos problemas principales** en el flujo actual:

### Problema 1: Desajuste entre frontend y backend (tipo de respuesta)

El **auth store** (`frontend/src/stores/auth.ts:60-85`) llama al endpoint legacy `POST /auth/register`, que devuelve `ApiResponse<UserInfo>` (solo datos del usuario, **sin token JWT**).

Sin embargo, el store espera una respuesta de tipo `AuthResponse` (`{ user, token }`), e intenta desestructurar:

```typescript
const { user: userData, token: authToken } = response.data.data
```

Como `response.data.data` es directamente un `UserInfo` (sin propiedades `user` ni `token`), ambos valores quedan como `undefined`. Consecuencias:

- `user.value = undefined` → `isAuthenticated = false`
- `token.value = undefined`
- `saveToStorage(undefined, undefined)` guarda basura en localStorage
- El método retorna `{ success: true }` porque la API respondió 200

### Problema 2: Redirección silenciosamente fallida

En `RegisterForm.vue:77-78`, al recibir `success: true`, ejecuta `router.push('/home')`. Pero el **route guard** (`router/index.ts`) verifica `requiresAuth` y, como `isAuthenticated` es `false` (por el problema 1), redirige al usuario de vuelta a `/` (landing page), donde el formulario sigue visible con los datos rellenados.

### Problema 3: Falta de flujo de verificación de email

Existe un endpoint nuevo `POST /api/auth/register-user` que implementa el flujo correcto con verificación de email (crea usuario con `isActive: false` y envía email de verificación). El frontend debería usar este endpoint en lugar del legacy.

## User Story enriquecida

**Como** usuario nuevo de la plataforma,
**quiero** que al completar el registro se me muestre un mensaje confirmando que mi cuenta fue creada y que recibiré un email de verificación,
**para** saber que el proceso finalizó correctamente y qué pasos seguir a continuación.

## Criterios de aceptación

### AC1: Migrar el registro al endpoint con verificación de email

- El formulario de registro debe llamar a `POST /api/auth/register-user` en lugar de `POST /auth/register`.
- El payload debe incluir los campos requeridos por `RegisterUserRequest`: `email`, `password`, `firstName`, `lastName`, `acceptedTerms`, y opcionalmente `documentNumber` y `phone`.
- El endpoint crea el usuario con `isActive: false` y envía email de verificación automáticamente.

### AC2: Mostrar pantalla de éxito tras el registro

- Tras un registro exitoso, el formulario debe **ocultarse** y en su lugar mostrar un mensaje de éxito que incluya:
  - Icono o ilustración de confirmación (e.g., icono de email/check de PrimeVue).
  - Título: "¡Registro completado!" (o similar).
  - Mensaje: "Hemos enviado un email de verificación a **{email}**. Revisa tu bandeja de entrada (y la carpeta de spam) para confirmar tu cuenta."
  - Botón primario: "Ir al inicio de sesión" → navega a la pestaña de login dentro del `AuthContainer`.
  - Enlace secundario: "¿No recibiste el email? Reenviar" → llama a `POST /api/auth/resend-verification`.
- **No** se debe redirigir a `/home` ya que el usuario aún no ha verificado su email.
- El formulario debe **limpiarse** (resetear `formData`) para que no queden datos visibles.

### AC3: No almacenar datos de sesión tras registro

- Dado que el usuario registrado vía `register-user` no recibe token JWT ni está activo, el store **no debe** intentar guardar user/token en localStorage tras el registro.
- El método `register()` del store debe adaptarse para devolver `{ success: true; email: string }` (u otra estructura) sin modificar el estado de autenticación.

### AC4: Validación de contraseña alineada con backend

- La validación frontend de contraseña debe alinearse con los requisitos del backend (`RegisterUserValidator`):
  - Mínimo **8 caracteres** (actualmente el frontend exige solo 6).
  - Debe contener: mayúscula, minúscula, dígito y carácter especial (`@$!%*?&#`).
- Mostrar mensajes de error claros si la contraseña no cumple los requisitos.

### AC5: Gestión de errores

- Si el email ya está registrado (`EMAIL_EXISTS`), mostrar mensaje: "Este correo electrónico ya está registrado."
- Si el documento ya existe (`DOCUMENT_EXISTS`), mostrar mensaje: "Este número de documento ya está registrado."
- Errores de red: "Error de conexión. Por favor, inténtalo de nuevo."
- Los mensajes de error deben mostrarse en el componente `Message` de PrimeVue con severity `error`.

### AC6: Añadir logotipo de ABUVI en la pantalla de autenticación

- Mostrar el logotipo de ABUVI **encima** del título "Bienvenidos a ABUVI" en el `AuthContainer`.
- Usar el archivo existente `frontend/src/assets/images/logo.svg` (o `logo-new.png`).
- Tamaño recomendado: `max-w-[120px]` centrado horizontalmente con `mx-auto`.
- **Responsive en móvil**: en pantallas pequeñas (`sm:` y menores), reducir el tamaño del logo a `max-w-[80px]` y reducir márgenes/paddings del contenedor para que el formulario completo de registro quepa en la pantalla sin scroll o con scroll mínimo.
- Reducir el padding del contenedor en móvil: cambiar `p-8` a `p-4 sm:p-8`.
- Considerar reducir el título a `text-2xl sm:text-3xl` en móvil para ganar espacio vertical.

### AC7: Resaltar visualmente el enlace de términos y condiciones

- El enlace "términos y condiciones" en el checkbox de aceptación debe destacar más visualmente respecto al texto circundante.
- Aplicar estilos más prominentes: **negrita** (`font-semibold`), **subrayado** (`underline`) y color primario más intenso para que sea claramente identificable como enlace clicable.
- Estado hover: mantener o intensificar el subrayado.

### AC8: Disclaimer de exclusividad para socios/as

- El texto "Plataforma exclusiva para socios/as" ya existe en `AuthContainer.vue` como subtítulo.
- **Verificar** que este texto es visible y suficientemente prominente. Si es necesario, aumentar ligeramente el tamaño de fuente o el contraste (actualmente `text-sm text-gray-600`).
- No se requiere añadir texto adicional; solo asegurar la visibilidad adecuada del existente.

## Cambios técnicos requeridos

### Frontend

#### 1. `frontend/src/stores/auth.ts`

- **Modificar** `register()`:
  - Cambiar endpoint de `/auth/register` a `/auth/register-user`.
  - Adaptar el payload al tipo `RegisterUserRequest`.
  - **No** almacenar user/token en el state ni en localStorage tras el registro.
  - Retornar `{ success: true; email: string }` en caso de éxito para que el componente pueda mostrar el email en el mensaje de confirmación.
  - Retornar `{ success: false; error: string }` en caso de error.

#### 2. `frontend/src/components/auth/RegisterForm.vue`

- **Añadir** estado `registrationComplete` (`ref<boolean>`) y `registeredEmail` (`ref<string>`).
- **Modificar** `handleSubmit`:
  - Tras éxito, setear `registrationComplete = true` y `registeredEmail = formData.email`.
  - Resetear `formData` a valores iniciales.
  - **No** ejecutar `router.push('/home')`.
- **Añadir** template condicional:
  - Si `registrationComplete === false`: mostrar formulario actual.
  - Si `registrationComplete === true`: mostrar pantalla de éxito con mensaje, botón de ir al login, y opción de reenviar email.
- **Actualizar** validación de contraseña para requerir mínimo 8 caracteres + complejidad.
- **Añadir** `emit('registration-complete')` para que `AuthContainer` pueda cambiar a la pestaña de login si se pulsa el botón correspondiente.
- **Resaltar** el enlace de "términos y condiciones": añadir `font-semibold underline` a las clases del `router-link`.

#### 3. `frontend/src/components/auth/AuthContainer.vue`

- **Añadir** importación del logotipo: `import logo from '@/assets/images/logo.svg'` (o `logo-new.png`).
- **Añadir** `<img>` del logo encima del título `<h1>`, centrado con `mx-auto`, tamaño `max-w-[80px] sm:max-w-[120px]` y `mb-3`.
- **Responsive**: cambiar padding del contenedor de `p-8` a `p-4 sm:p-8`. Reducir título a `text-2xl sm:text-3xl`.
- **Escuchar** evento `registration-complete` de `RegisterForm`.
- **Exponer** método o ref para cambiar la pestaña activa al login programáticamente cuando el usuario pulse "Ir al inicio de sesión".
- **Ajustar** visibilidad del disclaimer "Plataforma exclusiva para socios/as": considerar cambiar `text-gray-600` a `text-gray-700` y/o añadir `font-medium` para mayor contraste.

#### 4. `frontend/src/types/auth.ts`

- **Añadir** interfaz `RegisterUserRequest` alineada con el backend:

  ```typescript
  interface RegisterUserRequest {
    email: string
    password: string
    firstName: string
    lastName: string
    documentNumber?: string | null
    phone?: string | null
    acceptedTerms: boolean
  }
  ```

- **Añadir** interfaz `RegisterResponse`:

  ```typescript
  interface RegisterResponse {
    success: boolean
    email?: string
    error?: string
  }
  ```

#### 5. `frontend/src/composables/useAuth.ts`

- **Actualizar** `register()` para usar el nuevo endpoint y tipo de respuesta.
- **No** llamar a `authStore.setAuth()` tras un registro exitoso.

### Backend

#### 6. Endpoint legacy `POST /auth/register` — NO eliminar por ahora

**Análisis de uso actual:**

- **Frontend** (2 archivos + tests): se migrarán a `register-user` con este ticket → dejarán de usarlo.
- **Backend integration tests** (~12 archivos, ~20+ referencias): lo usan como helper para crear usuarios activos rápidamente en tests de otras features (camps, memberships, guests, blob storage, family units, protected endpoints).

**Decisión**: mantener el endpoint legacy en backend **solo para uso de tests de integración**. Eliminarlo requeriría migrar ~12 archivos de tests backend, lo cual es un cambio de gran alcance que debería hacerse en un ticket separado. Se puede considerar:

- Marcar el endpoint como `[Obsolete]` o añadir un comentario `// Used only by integration tests — see ticket #XXX for removal`.
- Crear un ticket de follow-up para migrar los tests backend al endpoint `register-user` o a un helper de tests dedicado.

### Tests

#### 7. `frontend/src/components/auth/__tests__/RegisterForm.test.ts`

Añadir los siguientes tests:

- **"should show success message after successful registration"**: Verificar que tras submit exitoso, el formulario se oculta y aparece un mensaje con texto sobre email de verificación.
- **"should display registered email in success message"**: Verificar que el email del usuario aparece en el mensaje de confirmación.
- **"should clear form data after successful registration"**: Verificar que `formData` se resetea.
- **"should not redirect to /home after registration"**: Verificar que `router.push` no se llama con `/home`.
- **"should show 'Go to login' button in success state"**: Verificar que aparece botón para ir al login.
- **"should show 'Resend email' link in success state"**: Verificar que aparece enlace para reenviar email.
- **"should validate password minimum 8 characters"**: Verificar que la validación exige 8 caracteres mínimo.
- **"should validate password complexity requirements"**: Verificar que exige mayúscula, minúscula, dígito y carácter especial.
- **"should show error message for duplicate email"**: Verificar que se muestra error si el email ya existe.
- **"should not show form when registration is complete"**: Verificar que el formulario no es visible en estado de éxito.

#### 8. `frontend/src/composables/__tests__/useAuth.test.ts`

Actualizar tests de `register`:

- **"should call register-user endpoint"**: Verificar que usa `/auth/register-user` en lugar de `/auth/register`.
- **"should not call setAuth after registration"**: Verificar que no se establece sesión tras el registro.
- **"should return email on successful registration"**: Verificar que retorna el email registrado.
- **"should handle DOCUMENT_EXISTS error"**: Verificar manejo de error por documento duplicado.

#### 9. `frontend/src/stores/__tests__/auth.test.ts`

Actualizar tests de `register`:

- **"should not modify user/token state after registration"**: Verificar que `user` y `token` permanecen null.
- **"should not save to localStorage after registration"**: Verificar que no se persiste nada.
- **"should return success with email"**: Verificar estructura de respuesta correcta.

#### 10. `frontend/src/components/auth/__tests__/AuthContainer.test.ts` (nuevo si no existe)

- **"should render ABUVI logo"**: Verificar que el logo se muestra en el contenedor.
- **"should display disclaimer text"**: Verificar que "Plataforma exclusiva para socios/as" es visible.
- **"should switch to login tab on registration-complete event"**: Verificar que al emitir el evento, se cambia a la pestaña de login.

## Archivos afectados

| Archivo | Acción |
|---------|--------|
| `frontend/src/stores/auth.ts` | Modificar `register()` |
| `frontend/src/components/auth/RegisterForm.vue` | Modificar: pantalla de éxito, limpiar form, validación, enlace términos |
| `frontend/src/components/auth/AuthContainer.vue` | Modificar: logo, responsive, escuchar evento, disclaimer |
| `frontend/src/types/auth.ts` | Añadir `RegisterUserRequest`, `RegisterResponse` |
| `frontend/src/composables/useAuth.ts` | Actualizar `register()` |
| `frontend/src/components/auth/__tests__/RegisterForm.test.ts` | Añadir 10 tests nuevos |
| `frontend/src/composables/__tests__/useAuth.test.ts` | Actualizar 4 tests |
| `frontend/src/stores/__tests__/auth.test.ts` | Actualizar 3 tests |
| `frontend/src/components/auth/__tests__/AuthContainer.test.ts` | Nuevo: 3 tests |

## Requisitos no funcionales

- **Seguridad**: No almacenar token/sesión hasta que el email esté verificado y el usuario haga login explícito.
- **UX**: El usuario debe tener certeza visual de que su registro fue exitoso. Nunca debe quedarse en un estado ambiguo.
- **Accesibilidad**: El mensaje de éxito debe ser accesible (role="alert" o similar) para lectores de pantalla.
- **i18n**: Todos los textos nuevos deben estar en español, consistentes con el idioma actual de la aplicación.
- **Responsive**: El formulario de registro completo (incluido logo y disclaimer) debe ser visible sin scroll excesivo en pantallas móviles. Priorizar que el contenido quepa en viewport en dispositivos comunes (>= 375px de ancho).

## Notas de implementación

- El endpoint legacy `POST /auth/register` se mantiene temporalmente para los tests de integración backend (~12 archivos). Se recomienda crear un ticket de follow-up para su eliminación y migración de tests.
- El composable `useAuth.ts` y el store `auth.ts` tienen funciones `register()` independientes. El `RegisterForm.vue` usa el del store directamente. Ambos deben actualizarse para consistencia.
- La contraseña mínima del frontend pasa de 6 a 8 caracteres, alineándose con `RegisterUserValidator.cs`.
- Los logos existentes son `logo.svg` (558 KB) y `logo-new.png` (862 KB) en `frontend/src/assets/images/`. Preferir SVG por escalabilidad y menor peso renderizado.
