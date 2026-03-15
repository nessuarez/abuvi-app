# Frontend Implementation Plan: fix-duplicate-family-member-email — Prevenir emails duplicados en miembros de familia

## Overview

Añadir validación de email duplicado en el formulario de miembros familiares para impedir que el representante introduzca su propio email o repita el email de otro miembro de la misma familia. Incluye mejora del texto de ayuda del campo email para aclarar que es opcional. Implementado con Vue 3 Composition API, PrimeVue + Tailwind CSS.

## Architecture Context

- **Components afectados**:
  - `frontend/src/components/family-units/FamilyMemberForm.vue` — Formulario de miembro (validación + hint text)
  - `frontend/src/views/FamilyUnitPage.vue` — Componente padre que pasa las nuevas props
- **Composables**: `frontend/src/composables/useFamilyUnits.ts` — Sin cambios necesarios (la API no cambia)
- **Stores**: `frontend/src/stores/auth.ts` — Lectura del email del usuario logueado (`useAuthStore().user?.email`)
- **Types**: `frontend/src/types/family-unit.ts` — Sin cambios necesarios
- **Routing**: Sin cambios
- **State management**: Props locales (no se necesita Pinia para esto)

## Implementation Steps

### Step 0: Create Feature Branch

- **Action**: Crear rama frontend desde `dev`
- **Branch Naming**: `feature/fix-duplicate-family-member-email-frontend`
- **Implementation Steps**:
  1. `git checkout dev && git pull origin dev`
  2. `git checkout -b feature/fix-duplicate-family-member-email-frontend`
  3. `git branch` para verificar

### Step 1: Add Props to FamilyMemberForm.vue

- **File**: `frontend/src/components/family-units/FamilyMemberForm.vue`
- **Action**: Añadir dos nuevas props para recibir contexto de emails desde el padre
- **Implementation Steps**:
  1. Añadir las props al `defineProps`:
     ```typescript
     const props = defineProps<{
       member?: FamilyMemberResponse | null
       loading?: boolean
       representativeEmail?: string   // NEW: email del usuario logueado
       siblingEmails?: string[]       // NEW: emails de otros miembros de la familia
     }>()
     ```
  2. Ambas props son opcionales con defaults razonables (`''` y `[]` respectivamente) para mantener retrocompatibilidad
- **Implementation Notes**:
  - `representativeEmail` es el email del usuario logueado (representante de la familia)
  - `siblingEmails` es la lista de emails de los otros miembros de la misma familia (excluyendo el miembro actualmente en edición). Lo calcula el padre.

### Step 2: Extend Email Validation in FamilyMemberForm.vue

- **File**: `frontend/src/components/family-units/FamilyMemberForm.vue`
- **Action**: Extender la función `validateEmail()` existente para incluir chequeos de duplicados
- **Implementation Steps**:
  1. Localizar la función `validateEmail()` existente que ya valida formato de email
  2. Después de la validación de formato (y solo si el formato es válido), añadir:
     - **Comparación con representativeEmail**: Si `email.value.trim().toLowerCase() === props.representativeEmail?.toLowerCase()`, setear `emailError.value = 'No puedes usar tu propio correo para un familiar'`
     - **Comparación con siblingEmails**: Si `props.siblingEmails?.some(e => e.toLowerCase() === email.value.trim().toLowerCase())`, setear `emailError.value = 'Este correo ya está asignado a otro miembro de tu familia'`
  3. Retornar `false` si se detecta duplicado (misma lógica que los otros errores de validación)
- **Implementation Notes**:
  - La comparación es **case-insensitive** usando `.toLowerCase()` en ambos lados
  - Solo se valida si el email no está vacío y tiene formato válido (no sobreescribir errores de formato)
  - El orden de validación es: (1) formato, (2) duplicado con representante, (3) duplicado con siblings
  - La validación se dispara en `@blur` del campo email, consistente con el patrón existente

### Step 3: Update Email Hint Text in FamilyMemberForm.vue

- **File**: `frontend/src/components/family-units/FamilyMemberForm.vue`
- **Action**: Cambiar el texto de ayuda del campo email para aclarar que es opcional y su propósito
- **Implementation Steps**:
  1. Localizar el hint/helper text actual del campo email. Actualmente dice algo sobre que si el miembro quiere registrarse en la plataforma
  2. Reemplazar por: `"Opcional. Indica el correo personal de este familiar solo si deseas que pueda registrarse en la plataforma con su propia cuenta. Si no tiene correo propio, déjalo en blanco."`
- **Implementation Notes**:
  - El texto debe ir en un `<small>` con clase de color neutro (ej: `text-surface-500`) para diferenciarlo del error (que usa `text-red-500`)
  - El hint se muestra siempre, el error se muestra condicionalmente y reemplaza/complementa al hint

### Step 4: Pass New Props from FamilyUnitPage.vue

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action**: Calcular y pasar `representativeEmail` y `siblingEmails` al FamilyMemberForm
- **Implementation Steps**:
  1. Importar `useAuthStore` si no está importado ya:
     ```typescript
     import { useAuthStore } from '@/stores/auth'
     const auth = useAuthStore()
     ```
  2. Crear computed para `representativeEmail`:
     ```typescript
     const representativeEmail = computed(() => auth.user?.email ?? '')
     ```
  3. Crear computed para `siblingEmails` que excluya al miembro en edición:
     ```typescript
     const siblingEmails = computed(() => {
       if (!familyMembers.value) return []
       return familyMembers.value
         .filter(m => m.id !== editingMember.value?.id)
         .map(m => m.email)
         .filter((e): e is string => !!e)
     })
     ```
  4. Pasar las props en el template:
     ```html
     <FamilyMemberForm
       :member="editingMember"
       :loading="loading"
       :representative-email="representativeEmail"
       :sibling-emails="siblingEmails"
       @submit="handleMemberSubmit"
       @cancel="showMemberDialog = false"
     />
     ```
- **Implementation Notes**:
  - `siblingEmails` filtra por `editingMember.value?.id` para excluir al miembro que se está editando (no debería alertar de duplicado consigo mismo)
  - Cuando `editingMember` es `null` (modo creación), no se excluye ningún miembro, lo cual es correcto
  - `filter((e): e is string => !!e)` elimina emails null/undefined/vacíos de la lista
  - `familyMembers` viene del composable `useFamilyUnits()` que ya se usa en FamilyUnitPage

### Step 5: Handle Backend Error Responses

- **File**: `frontend/src/views/FamilyUnitPage.vue`
- **Action**: Asegurar que los errores 409 del backend (BusinessRuleException por email duplicado) se muestran correctamente al usuario
- **Implementation Steps**:
  1. Verificar que `handleMemberSubmit` ya maneja errores del composable y muestra Toast
  2. Los mensajes del backend ("El correo electrónico no puede ser el mismo que el del representante de la familia", "Ya existe otro miembro en esta familia con el mismo correo electrónico") ya deberían mostrarse vía el error handling existente del composable
  3. Si el composable atrapa el error y lo guarda en `error.value`, asegurarse de que el componente padre muestra ese error via Toast
- **Implementation Notes**:
  - Esto es una red de seguridad: la validación principal es en frontend (Step 2), el backend es backup
  - No debería requerir cambios si el error handling genérico ya funciona, pero verificar

### Step 6: Update Technical Documentation

- **Action**: Actualizar documentación si hay patrones nuevos introducidos
- **Implementation Steps**:
  1. **Review Changes**: Los cambios son menores (2 props nuevas, extensión de validación existente)
  2. No se introducen patrones nuevos, composables nuevos, ni dependencias nuevas
  3. No se requiere actualización de documentación técnica — los cambios siguen patrones existentes
- **Notes**: Si durante la implementación se descubre que algún patrón es nuevo, documentar en ese momento

## Implementation Order

1. **Step 0**: Create Feature Branch
2. **Step 1**: Add Props to FamilyMemberForm.vue
3. **Step 2**: Extend Email Validation in FamilyMemberForm.vue
4. **Step 3**: Update Email Hint Text
5. **Step 4**: Pass New Props from FamilyUnitPage.vue
6. **Step 5**: Handle Backend Error Responses (verify)
7. **Step 6**: Update Technical Documentation (if needed)

## Testing Checklist

### Manual Testing
- [ ] Crear miembro: escribir email del representante → error inline aparece on blur
- [ ] Crear miembro: escribir email de otro miembro existente → error inline aparece on blur
- [ ] Crear miembro: escribir email único → sin error, submit funciona
- [ ] Crear miembro: dejar email vacío → sin error de duplicado
- [ ] Editar miembro: cambiar email al del representante → error inline
- [ ] Editar miembro: cambiar email al de otro miembro → error inline
- [ ] Editar miembro: mantener su propio email → sin error (se excluye a sí mismo)
- [ ] Case-insensitive: "Test@Mail.com" = "test@mail.com" → detecta como duplicado
- [ ] Hint text actualizado visible bajo el campo email
- [ ] Botón guardar no se activa si hay error de email duplicado

### Vitest Unit Tests (si el proyecto los tiene para FamilyMemberForm)
- [ ] `validateEmail` retorna error cuando email === representativeEmail
- [ ] `validateEmail` retorna error cuando email está en siblingEmails
- [ ] `validateEmail` no retorna error cuando email es único
- [ ] `validateEmail` no valida duplicados si email está vacío
- [ ] Comparación case-insensitive funciona

### Verificación de Error Handling
- [ ] Si el backend responde 409 (email duplicado), el Toast muestra el mensaje del backend
- [ ] Si la red falla, el error genérico se muestra correctamente

## Error Handling Patterns

- **Validación frontend (UX)**: Error inline bajo el campo email, usando `emailError` ref existente con clases `text-red-500`
- **Validación backend (seguridad)**: El composable `useFamilyUnits` ya atrapa errores de API y los expone vía `error.value`. El padre muestra Toast con el mensaje
- **Prioridad de mensajes de error**: formato inválido > duplicado con representante > duplicado con sibling

## UI/UX Considerations

- **Campo email**: Mantiene mismo estilo visual (PrimeVue InputText)
- **Error inline**: Mismo patrón visual que errores existentes (`<small class="text-red-500">`)
- **Hint text**: `<small class="text-surface-500">` — tono neutro, siempre visible (no compite con error)
- **Mensajes en español**: Consistente con el resto del formulario
- **Accesibilidad**: Los errores de validación se asocian al campo vía proximity (mismo patrón existente)
- **Responsive**: Sin cambios — el formulario ya es responsive
- **Loading states**: Sin cambios — el botón submit ya se deshabilita durante loading

## Dependencies

- **npm packages**: Ninguno nuevo
- **PrimeVue components**: Los mismos que ya usa el formulario (InputText, Button, etc.)
- **Stores**: `useAuthStore` — ya existe, solo se importa en FamilyUnitPage si no estaba

## Notes

- **Separación de responsabilidades**: La validación frontend es solo UX (feedback rápido). El backend DEBE validar independientemente.
- **Retrocompatibilidad**: Las nuevas props son opcionales. Si no se pasan, la validación de duplicados simplemente no se ejecuta. El formulario funciona igual que antes.
- **Datos reactivos**: `siblingEmails` es computed, así que si se añade un miembro nuevo y se abre otro diálogo de creación, la lista se actualiza automáticamente.
- **Idioma**: Todos los mensajes al usuario en español, nombres de variables/funciones en inglés.

## Next Steps After Implementation

1. Integrar con rama backend (`feature/fix-duplicate-family-member-email-backend`) para testing E2E completo
2. Verificar que los errores 409 del backend se muestran correctamente cuando la validación frontend se bypasea (ej: DevTools)
3. Considerar si se necesita un script de notificación a usuarios con datos duplicados existentes (decisión de producto)

## Implementation Verification

- [ ] **Code Quality**: TypeScript estricto, sin `any`, `<script setup lang="ts">`
- [ ] **Functionality**: Validación de email duplicado funciona en creación y edición
- [ ] **Props typing**: Nuevas props correctamente tipadas en `defineProps<T>()`
- [ ] **Reactive computed**: `siblingEmails` se recalcula cuando cambia `editingMember` o `familyMembers`
- [ ] **Error UX**: Mensajes claros y consistentes con el resto del formulario
- [ ] **No regresiones**: El formulario funciona exactamente igual si no se pasan las nuevas props
- [ ] **Documentation**: Actualizada si se introdujeron patrones nuevos
