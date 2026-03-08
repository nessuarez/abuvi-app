# Rediseño del menú de Administración: de pestañas a sidebar con sub-rutas

## Problema

El panel de Administración (`AdminPage.vue`) actualmente usa un componente `Tabs` horizontal de PrimeVue con **8 pestañas**. En pantallas medianas o pequeñas, las pestañas se desbordan horizontalmente, dificultando la navegación. Además, el patrón de tabs horizontales no escala bien: cada nueva funcionalidad administrativa añade otra pestaña al overflow.

### Estado actual

```
[Campamentos] [Inscripciones] [Unidades Familiares] [Usuarios] [Almacenamiento] [Revisión medios] [Pagos] [Configuración]
```

Todos los tabs se renderizan dentro de una sola ruta `/admin` sin reflejo en la URL.

---

## Propuesta de solución: Sidebar con sub-rutas agrupadas

### Patrón recomendado

Reemplazar las pestañas horizontales por un **sidebar vertical** con secciones agrupadas semánticamente, donde cada opción navega a una **sub-ruta dedicada** bajo `/admin`.

### Layout propuesto

```
┌──────────────────────────────────────────────────────┐
│  Panel de Administración                             │
├──────────────┬───────────────────────────────────────┤
│              │                                       │
│  GESTIÓN     │   [Contenido del panel activo]        │
│  ☰ Campamentos│                                      │
│  ☰ Inscripciones                                     │
│  ☰ Unid. Familiares                                  │
│              │                                       │
│  PERSONAS    │                                       │
│  ☰ Usuarios  │                                       │
│              │                                       │
│  CONTENIDO   │                                       │
│  ☰ Revisión medios                                   │
│              │                                       │
│  FINANZAS    │                                       │
│  ☰ Pagos     │                                       │
│              │                                       │
│  SISTEMA     │                                       │
│  ☰ Almacenamiento (solo Admin)                       │
│  ☰ Configuración                                     │
│              │                                       │
└──────────────┴───────────────────────────────────────┘
```

### Comportamiento responsive (móvil)

En pantallas pequeñas (`< md`), el sidebar se convierte en un **menú desplegable/accordion** encima del contenido, o en un **drawer lateral** activado por un botón hamburguesa. Se puede usar el componente `Drawer` de PrimeVue (anteriormente `Sidebar`).

---

## Agrupación de secciones

| Grupo       | Ítems                                     | Roles         |
| ----------- | ----------------------------------------- | ------------- |
| **Gestión** | Campamentos, Inscripciones, Unid. Familiares | Board, Admin  |
| **Personas**| Usuarios                                  | Board, Admin  |
| **Contenido** | Revisión de medios                     | Board, Admin  |
| **Finanzas**| Pagos                                     | Board, Admin  |
| **Sistema** | Almacenamiento, Configuración             | Admin / Board |

---

## Cambios en rutas

### Antes (ruta única)

```ts
{ path: '/admin', component: AdminPage, meta: { requiresBoard: true } }
```

### Después (sub-rutas)

```ts
{
  path: '/admin',
  component: AdminLayout,      // Nuevo layout con sidebar
  meta: { requiresAuth: true, requiresBoard: true, title: 'ABUVI | Administración' },
  redirect: '/admin/camps',    // Redirigir a la primera sección por defecto
  children: [
    { path: 'camps',           name: 'admin-camps',           component: () => import('@/components/admin/CampsAdminPanel.vue') },
    { path: 'registrations',   name: 'admin-registrations',   component: () => import('@/components/admin/RegistrationsAdminPanel.vue') },
    { path: 'family-units',    name: 'admin-family-units',    component: () => import('@/components/admin/FamilyUnitsAdminPanel.vue') },
    { path: 'users',           name: 'admin-users',           component: () => import('@/components/admin/UsersAdminPanel.vue') },
    { path: 'media-review',    name: 'admin-media-review',    component: () => import('@/components/admin/MediaItemsReviewPanel.vue'),    meta: { requiresBoard: true } },
    { path: 'payments',        name: 'admin-payments',        component: () => import('@/components/admin/PaymentsAdminPanel.vue'),       meta: { requiresBoard: true } },
    { path: 'storage',         name: 'admin-storage',         component: () => import('@/components/admin/BlobStorageAdminPanel.vue'),    meta: { requiresAdmin: true } },
    { path: 'settings',        name: 'admin-settings',        component: () => import('@/components/admin/AssociationSettingsPanel.vue'), meta: { requiresBoard: true } },
  ]
}
```

---

## Archivos a crear / modificar

### Nuevos archivos

| Archivo | Descripción |
| ------- | ----------- |
| `frontend/src/layouts/AdminLayout.vue` | Nuevo layout con sidebar + `<router-view>` para el contenido |
| `frontend/src/components/admin/AdminSidebar.vue` | Componente sidebar con menú agrupado y control de roles |

### Archivos a modificar

| Archivo | Cambio |
| ------- | ------ |
| `frontend/src/router/index.ts` | Reemplazar ruta `/admin` plana por ruta con `children` y `AdminLayout` |
| `frontend/src/views/AdminPage.vue` | **Eliminar** o vaciar — su lógica se mueve a `AdminLayout.vue` |
| `frontend/src/components/layout/AppHeader.vue` | Sin cambios funcionales (el enlace `/admin` sigue funcionando gracias al `redirect`) |

### Archivos que NO se modifican

Los 8 componentes panel (`CampsAdminPanel.vue`, `RegistrationsAdminPanel.vue`, etc.) **no requieren cambios** — ya son componentes independientes que se pueden renderizar directamente como vistas de ruta.

---

## Especificación del componente AdminSidebar

### Props / Dependencias

- Usa `useAuthStore()` para controlar visibilidad por rol (`isAdmin`, `isBoard`)
- Usa `useRoute()` para resaltar el ítem activo
- Usa `<router-link>` para navegación

### Estructura de datos del menú

```ts
interface AdminMenuGroup {
  label: string            // Nombre del grupo (ej. "Gestión")
  items: AdminMenuItem[]
}

interface AdminMenuItem {
  label: string            // Texto visible (ej. "Campamentos")
  icon: string             // Clase de icono PrimeVue (ej. "pi pi-map")
  to: string               // Ruta destino (ej. "/admin/camps")
  testId: string           // data-testid para testing
  visible: boolean         // Controlado por roles
}
```

### Componentes PrimeVue sugeridos

- **Desktop**: `PanelMenu` o implementación custom con Tailwind (preferible para control total del estilo)
- **Móvil**: `Drawer` (PrimeVue) para el menú lateral colapsable, o un simple accordion con Tailwind

### Estilos

- Ancho sidebar desktop: `w-64` (256px) fijo
- Sidebar con `sticky top-0` para que permanezca visible al hacer scroll
- Ítem activo: fondo destacado con el color primario de la app (`bg-red-50 text-red-700 border-l-4 border-red-600`)
- Grupos separados con etiquetas en mayúsculas y `text-xs text-gray-500 font-semibold`

---

## Criterios de aceptación

1. **Sidebar visible en desktop** (`≥ md`): Menú lateral con secciones agrupadas y los 8 ítems (filtrados por rol)
2. **Navegación por URL**: Cada sección tiene su propia URL (ej. `/admin/camps`, `/admin/payments`), permitiendo enlaces directos y navegación con botón atrás del navegador
3. **Ítem activo resaltado**: El ítem correspondiente a la ruta actual se muestra como seleccionado
4. **Responsive en móvil** (`< md`): El sidebar se oculta y se accede mediante un botón que abre un `Drawer` o menú desplegable
5. **Control de acceso por roles**: Los ítems respetan las mismas reglas de visibilidad actuales (Admin vs Board)
6. **Redirect por defecto**: Navegar a `/admin` redirige a `/admin/camps`
7. **Sin regresiones**: Los 8 paneles existentes funcionan igual que antes, sin modificaciones internas
8. **Tests E2E actualizados**: Actualizar los tests de Cypress existentes que naveguen al panel de administración para usar las nuevas rutas

---

## Requisitos no funcionales

- **Rendimiento**: Las sub-rutas usan lazy loading (`() => import(...)`) para cargar cada panel solo cuando se necesita
- **Accesibilidad**: El sidebar debe usar `<nav>` semántico con `aria-current="page"` en el ítem activo
- **Escalabilidad**: El patrón de grupos + ítems permite añadir nuevas secciones sin desbordamiento visual

---

## Alternativas consideradas

| Alternativa | Motivo de descarte |
| ----------- | ------------------ |
| Tabs verticales (PrimeVue `Tabs` con orientación vertical) | No permite agrupar secciones ni tiene sub-rutas; sigue sin escalar bien con muchos ítems |
| Dashboard de cards con navegación | Mayor esfuerzo de desarrollo y añade un click extra para llegar al contenido |
| Tabs con scroll horizontal | Solución parche — no mejora la experiencia de usuario ni la arquitectura |
| Mega menú desplegable | Patrón inusual para paneles de administración; confuso para personas usuarias |

---

## Notas de implementación

- El componente `AdminLayout.vue` reemplaza a `AdminPage.vue` como contenedor. La estructura sería:
  ```html
  <Container>
    <h1>Panel de Administración</h1>
    <div class="flex">
      <AdminSidebar class="hidden md:block" />      <!-- Desktop -->
      <AdminSidebarMobile class="md:hidden" />       <!-- Móvil: botón + Drawer -->
      <main class="flex-1">
        <router-view />
      </main>
    </div>
  </Container>
  ```
- Usar `useRoute().path` para determinar el ítem activo en el sidebar
- La migración es limpia porque los paneles ya son componentes autocontenidos