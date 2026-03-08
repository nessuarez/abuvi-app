# Desactivar envío de contenido del 50º Aniversario — "Coming soon"

## Contexto

La funcionalidad de subida de contenido para el 50º aniversario está completamente implementada (formulario, backend, galería, panel de revisión admin), pero **aún no se va a permitir el envío de contenido por parte de los usuarios**. Se necesita desactivar el botón de envío del formulario y mostrar un mensaje de tipo "Coming soon" hasta que se decida habilitar la funcionalidad.

## User Story

**Como** producto,
**quiero** que el botón de envío del formulario de recuerdos del 50º aniversario esté desactivado y muestre un mensaje "Coming soon",
**para que** los usuarios vean que la funcionalidad existe pero aún no está disponible.

## Alcance

### Cambios requeridos

#### 1. Frontend — `AnniversaryUploadForm.vue`

**Archivo:** `frontend/src/components/anniversary/AnniversaryUploadForm.vue`

**Cambios en `<script setup>`:**

- Añadir una constante `comingSoon` con valor `true` que actúe como feature flag local para controlar el estado del formulario.

**Cambios en `<template>` — Botón de envío (líneas 265-274):**

- El `Button` de envío debe estar **siempre deshabilitado** (`disabled`) cuando `comingSoon` es `true`.
- Cambiar el `label` del botón a `"Próximamente"` (o `"Coming soon"` si se prefiere inglés; dado que la app está en español, usar `"Próximamente"`).
- Cambiar el `icon` a `pi pi-clock` para reforzar visualmente que no está disponible aún.
- Añadir un mensaje informativo debajo o encima del botón indicando que la funcionalidad estará disponible próximamente. Ejemplo:

```html
<p class="text-center text-sm text-amber-700">
  La subida de recuerdos estará disponible próximamente. ¡Estate atento!
</p>
```

- Los campos del formulario pueden seguir visibles (para que el usuario vea qué se podrá subir), pero opcionalmente se pueden deshabilitar también para evitar que el usuario rellene datos que no podrá enviar. **Decisión recomendada:** deshabilitar también los campos del formulario cuando `comingSoon` es `true` para mejor UX.

**Cambios en `<template>` — Formulario:**

- Añadir atributo `:class="{ 'opacity-60 pointer-events-none': comingSoon }"` al `<form>` para atenuar visualmente el formulario cuando está en modo "coming soon", o alternativamente deshabilitar cada campo individualmente con `:disabled="comingSoon"`.

#### 2. Tests — `AnniversaryUploadForm.test.ts`

**Archivo:** `frontend/src/components/anniversary/__tests__/AnniversaryUploadForm.test.ts`

- Añadir un test que verifique que cuando `comingSoon` es `true`, el botón de envío muestra "Próximamente" y está deshabilitado.
- Añadir un test que verifique que `handleSubmit` no se ejecuta cuando el botón está deshabilitado.

## Criterios de aceptación

- [ ] El botón "Enviar recuerdo" aparece como "Próximamente" y está deshabilitado.
- [ ] Se muestra un icono de reloj (`pi pi-clock`) en lugar del icono de enviar.
- [ ] Aparece un mensaje informativo indicando que la funcionalidad estará disponible próximamente.
- [ ] El formulario queda visualmente atenuado o con los campos deshabilitados.
- [ ] No se puede enviar el formulario de ninguna manera (ni con Enter, ni haciendo click).
- [ ] Los tests existentes siguen pasando.
- [ ] Se añaden tests para el estado "coming soon".
- [ ] Cuando se quiera habilitar la funcionalidad, basta con cambiar `comingSoon` a `false`.

## Notas técnicas

- **No se requieren cambios en backend.** El backend ya está listo; simplemente se bloquea el envío desde el frontend.
- **No se requieren cambios en rutas ni navegación.** La página `/anniversary` sigue accesible para que los usuarios la vean.
- **Feature flag local:** Se usa una constante en el componente (`const comingSoon = true`). Si en el futuro se quiere controlar remotamente, se puede migrar a una variable de entorno o configuración de backend, pero por ahora una constante es suficiente.
- **Galería:** La galería (`AnniversaryGallery.vue`) puede seguir visible (mostrará estado vacío ya que no habrá contenido aprobado). No requiere cambios.

## Archivos a modificar

| Archivo | Tipo de cambio |
|---|---|
| `frontend/src/components/anniversary/AnniversaryUploadForm.vue` | Deshabilitar botón y formulario, añadir mensaje "coming soon" |
| `frontend/src/components/anniversary/__tests__/AnniversaryUploadForm.test.ts` | Añadir tests para estado "coming soon" |
