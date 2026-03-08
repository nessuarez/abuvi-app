# Limpieza de páginas legales: Estatutos y Transparencia

## Descripción

Se requieren dos cambios en la sección legal de la aplicación:

1. **Página de Estatutos (`/legal/bylaws`)**: Eliminar todo el contenido de prueba (artículos, capítulos, acordeón) y reemplazarlo por un mensaje placeholder indicando que en el futuro se publicará la documentación oficial de la asociación aquí.
2. **Página de Transparencia (`/legal/transparency`)**: Eliminar completamente la página, incluyendo ruta, enlace en footer y tests E2E.

## Cambios requeridos

### 1. Simplificar la página de Estatutos

**Archivo:** `frontend/src/views/legal/BylawsPage.vue`

- Eliminar todo el contenido del `<script setup>`: las interfaces, el array `chapters`, y las importaciones de componentes de Accordion y Button.
- Reemplazar el `<template>` actual por un diseño simple usando `LegalPageLayout` con:
  - Título: "Estatutos de la Asociación ABUVI"
  - Un mensaje centrado y con estilo informativo (por ejemplo, un bloque con icono de información) que indique:
    - "Próximamente publicaremos aquí la documentación oficial de la asociación."
    - Opcionalmente, un texto secundario tipo: "Si necesitas consultar los estatutos, contacta con la Junta Directiva en junta.abuvi@gmail.com."
  - Eliminar: metadatos de fecha, botón de descarga PDF, acordeón de capítulos, nota de borrador al pie.

### 2. Eliminar la página de Transparencia

**Archivos a modificar:**

| Archivo | Cambio |
|---------|--------|
| `frontend/src/views/legal/TransparencyPage.vue` | **Eliminar** el archivo completo |
| `frontend/src/router/index.ts` | Eliminar la ruta `/legal/transparency` (líneas 192-197) |
| `frontend/src/components/layout/AppFooter.vue` | Eliminar el enlace `{ label: 'Transparencia', path: '/legal/transparency' }` del array `linkGroups` (línea 23) |
| `frontend/cypress/e2e/legal-pages.cy.ts` | Eliminar el bloque `describe('Transparencia (/legal/transparency)')` completo (líneas 121-164) |

### 3. Actualizar tests E2E de Estatutos

**Archivo:** `frontend/cypress/e2e/legal-pages.cy.ts`

- Actualizar el bloque `describe('Estatutos (/legal/bylaws)')`:
  - Eliminar el test `should render accordion with chapters` (ya no hay acordeón).
  - Eliminar el test `should expand accordion panels on click` (ya no hay acordeón).
  - Añadir un test que verifique que el mensaje placeholder es visible (e.g., verificar que contiene "Próximamente" o similar).

## Criterios de aceptación

- [ ] La página `/legal/bylaws` muestra un mensaje placeholder en lugar del contenido de prueba con artículos.
- [ ] La página `/legal/bylaws` sigue siendo accesible sin autenticación.
- [ ] La ruta `/legal/transparency` ya no existe (devuelve 404 o redirige).
- [ ] El enlace "Transparencia" ya no aparece en el footer.
- [ ] El archivo `TransparencyPage.vue` ha sido eliminado.
- [ ] Los tests E2E se han actualizado y pasan correctamente.
- [ ] La aplicación compila sin errores (`npm run build`).

## Notas técnicas

- No se necesitan cambios en el backend.
- El componente `LegalPageLayout` se sigue usando para mantener consistencia visual.
- Los componentes `TableOfContents.vue` y `LegalPageLayout.vue` no necesitan modificación ya que son compartidos con otras páginas legales.
- No hay imports de `TransparencyPage` en ningún otro archivo fuera de los listados.
