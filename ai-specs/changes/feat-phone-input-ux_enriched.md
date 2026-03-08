# US: Mejorar UX del input de teléfono con selector de código de país

## Descripción

Actualmente, los formularios que incluyen campo de teléfono requieren que la persona usuaria introduzca manualmente el número completo en formato E.164 (ej. `+34612345678`), lo que provoca frecuentes errores de validación. Se debe crear un componente reutilizable `PhoneInput` que separe el código de país del número de teléfono, asumiendo `+34` (España) por defecto, para facilitar la introducción del número. El valor enviado al backend seguirá siendo el formato E.164 completo (`+{código}{número}`), sin cambios en backend.

## Alcance

**Solo frontend.** No se modifica backend, validadores, modelos ni base de datos.

## Componente nuevo: `PhoneInput.vue`

**Ruta:** `frontend/src/components/shared/PhoneInput.vue`

### Props

| Prop | Tipo | Requerido | Default | Descripción |
|------|------|-----------|---------|-------------|
| `modelValue` | `string \| null` | No | `null` | Valor E.164 completo (ej. `+34612345678`). Soporta v-model. |
| `invalid` | `boolean` | No | `false` | Estado de error visual. |
| `disabled` | `boolean` | No | `false` | Deshabilitar el input. |
| `id` | `string` | No | `'phone'` | ID del input para accesibilidad. |

### Emits

| Evento | Payload | Descripción |
|--------|---------|-------------|
| `update:modelValue` | `string \| null` | Emite el valor E.164 completo o `null` si vacío. |
| `blur` | — | Se emite cuando el input del número pierde el foco (para validación on blur). |

### Comportamiento

1. **Código de país:** Input de texto con prefijo `+`, ancho fijo (~70px), valor por defecto `34`. Permite editar para otros códigos (ej. `1`, `44`, `351`). Solo acepta dígitos (1-4 dígitos).
2. **Número de teléfono:** Input principal, placeholder `612345678`. Solo acepta dígitos. Ocupa el espacio restante.
3. **Composición del valor:** Al cambiar cualquiera de los dos campos, emitir `+{código}{número}` como `modelValue`. Si el número está vacío, emitir `null`.
4. **Parsing inicial:** Al recibir un `modelValue` con formato `+{dígitos}`, separar los primeros 1-3 dígitos como código de país usando la heurística:
   - Si empieza con un código conocido de 1 dígito (`1` para EEUU/Canadá, `7` para Rusia): separar 1 dígito.
   - Si empieza con un código conocido de 2 dígitos (`34`, `44`, `33`, `39`, `49`, `31`, `32`, `41`, `43`, `45`, `46`, `47`, `48`, `51`, `52`, `53`, `54`, `55`, `56`, `57`, `58`, `60`, `61`, `62`, `63`, `64`, `65`, `66`, `81`, `82`, `86`, `90`, `91`, `92`, `93`, `94`, `95`, `98`): separar 2 dígitos.
   - Por defecto asumir código de 2 dígitos (cubrir la mayoría de códigos europeos).
   - Si no tiene `+` al inicio o está vacío, usar `34` como código de país y el valor como número.
5. **Validación visual:** Aplicar clase `p-invalid` cuando `invalid=true` en ambos inputs.
6. **Restricción de entrada:** Usar `@input` para filtrar caracteres no numéricos en ambos campos.

### Layout

```
┌──────────┬─────────────────────────┐
│ +  [34]  │  [612345678           ] │
│  ~70px   │       flex-1            │
└──────────┴─────────────────────────┘
```

- Usar `flex` con `gap-0` y bordes compartidos para que parezca un solo input.
- El separador visual entre código y número puede ser un borde derecho del primer input o un divisor sutil.

## Ficheros a modificar

### 1. Crear `frontend/src/components/shared/PhoneInput.vue`

Componente reutilizable según la especificación anterior.

### 2. Modificar `frontend/src/components/family-units/FamilyMemberForm.vue`

- **Template (líneas ~323-336):** Reemplazar el `<InputText>` del teléfono por `<PhoneInput>`:

  ```vue
  <PhoneInput
    id="phone"
    v-model="phone"
    :invalid="!!phoneError"
    :disabled="loading"
    @blur="validatePhone"
  />
  ```

- Eliminar el texto de ayuda "Formato E.164 con código de país (ej. +34)" ya que el propio componente lo hace implícito.
- **Import:** Añadir `import PhoneInput from '@/components/shared/PhoneInput.vue'`.
- **Validación (líneas ~151-165):** Mantener la validación E.164 existente sin cambios (el componente ya emite en formato E.164).

### 3. Modificar `frontend/src/components/guests/GuestForm.vue`

- **Template (líneas ~257-270):** Mismo cambio que FamilyMemberForm: reemplazar `<InputText>` por `<PhoneInput>`.
- Eliminar texto de ayuda E.164.
- **Import:** Añadir import de `PhoneInput`.
- **Validación (líneas ~114-128):** Sin cambios.

### 4. Modificar `frontend/src/views/ProfilePage.vue`

- **Template (líneas ~395-406):** Reemplazar `<InputText>` por `<PhoneInput>`:

  ```vue
  <PhoneInput
    id="edit-phone"
    v-model="editForm.phone"
    :invalid="!!editErrors.phone"
    data-testid="edit-phone"
  />
  ```

- Eliminar texto de ayuda "Opcional. Formato internacional (ej. +34612345678).".
- **Import:** Añadir import de `PhoneInput`.
- **Validación (líneas ~141-170):** Sin cambios.

### 5. Modificar `frontend/src/components/users/UserForm.vue`

- **Template (líneas ~190-200):** Reemplazar `<InputText>` por `<PhoneInput>`:

  ```vue
  <PhoneInput
    id="phone"
    v-model="formData.phone"
  />
  ```

- **Import:** Añadir import de `PhoneInput`.

## Criterios de aceptación

- [ ] El componente `PhoneInput` se renderiza correctamente con código de país `+34` por defecto.
- [ ] Al introducir solo dígitos del número (ej. `612345678`), el `v-model` emite `+34612345678`.
- [ ] Al cambiar el código de país a `+1` e introducir `2025551234`, el `v-model` emite `+12025551234`.
- [ ] Al editar un registro existente con teléfono `+34612345678`, se muestra `34` en código y `612345678` en número.
- [ ] Al editar un registro existente con teléfono `+12025551234`, se muestra `1` en código y `2025551234` en número.
- [ ] Si el campo de número está vacío, el `v-model` emite `null`.
- [ ] Las validaciones existentes en cada formulario siguen funcionando sin cambios.
- [ ] El componente se usa en los 4 formularios: FamilyMemberForm, GuestForm, ProfilePage, UserForm.
- [ ] El componente respeta los estados `invalid` y `disabled`.
- [ ] Solo se permiten dígitos en ambos campos (código y número).
- [ ] No hay cambios en el backend ni en los datos enviados a la API.

## Requisitos no funcionales

- **Accesibilidad:** Los labels existentes deben seguir apuntando al input principal del número. El input de código debe tener `aria-label="Código de país"`.
- **Responsividad:** El componente debe funcionar bien en móvil (mínimo 320px de ancho).
- **Consistencia visual:** Usar componentes PrimeVue (`InputText`) y clases Tailwind consistentes con el resto de la app.

## Tests

- Añadir tests unitarios en `frontend/src/components/shared/__tests__/PhoneInput.spec.ts`:
  - Renderiza con valor por defecto `+34`.
  - Parsea correctamente un valor E.164 existente.
  - Emite `null` cuando el número está vacío.
  - Emite E.164 completo al escribir número.
  - Solo acepta dígitos en ambos campos.
  - Respeta prop `disabled`.
  - Respeta prop `invalid`.

## Notas técnicas

- El componente `DateInput.vue` en `frontend/src/components/shared/` es un buen referente de patrón para componentes shared reutilizables en este proyecto.
- El backend valida con pattern `^\+[1-9]\d{1,14}$` (FamilyMember, Guest) y `^\+?[0-9\s\-\(\)]+$` (User). El componente siempre emitirá en formato `^\+[1-9]\d+$`, que es compatible con ambos validators.
