# Informe de Estado — Aplicación ABUVI

**Fecha:** 27 de febrero de 2026
**Para:** Miembros de la Junta Directiva
**Asunto:** Estado actual de la aplicación — Sistema de inscripciones completado

---

## Resumen para la Junta

**Las inscripciones al campamento ya están operativas.** Desde el último informe (22 de febrero) se ha completado la pantalla de inscripción completa, incluyendo selección de participantes, extras, preferencias de alojamiento y confirmación. El sistema está listo para que los socios se inscriban al Campamento 2026.

---

## 1. ¿Qué está funcionando hoy?

### Acceso a la aplicación

Los socios pueden entrar con su correo y contraseña. Si alguien olvida su contraseña, puede recuperarla directamente desde la pantalla de acceso.

![Pantalla de acceso](01-login-updated.png)

---

### Página principal

Al entrar, el socio ve acceso rápido al campamento, al 50 aniversario y a su perfil.

![Inicio](27feb-home.png)

Los miembros de la Junta y administradores ven el botón rojo **"Administración"** en el menú para gestionar la plataforma.

![Menú con Administración](27feb-menu.png)

---

### Mi Perfil con Unidad Familiar y Membresías

La sección de perfil muestra los datos del socio, seguridad, y su unidad familiar con el estado de membresía de cada miembro. Cada miembro muestra si es **socio activo** y el estado de su cuota del año en curso.

![Mi Perfil con membresías](27feb-perfil.png)

> La unidad familiar es clave para las inscripciones: el socio define aquí qué miembros forman parte de su familia, y el sistema usa esa información para calcular el precio del campamento automáticamente.

---

### Campamento 2026 — Información y acceso a inscripción

El socio ve la información del campamento activo: ubicación, fechas, precios por categoría (adulto, niño, bebé) y el número de inscripciones actuales. Desde aquí puede pulsar **"Inscribirse al campamento"**.

![Página del Campamento 2026](27feb-camp.png)

---

## 2. Sistema de Inscripciones *(completado)*

El flujo de inscripción es un asistente de 4 pasos que guía al socio:

### Paso 1: Selección de participantes

El socio elige qué miembros de su unidad familiar se inscriben. Para menores de edad, el sistema solicita automáticamente los **datos del tutor legal** (nombre y DNI).

![Paso 1 — Selección de participantes](27feb-registro-paso1-seleccion.png)

---

### Paso 2: Extras y preferencias

El socio puede seleccionar extras opcionales (uso del camión, menú vegetariano, etc.) e indicar **necesidades especiales** (dietas, movilidad) y **preferencia de acampantes** (con quién le gustaría acampar cerca).

![Paso 2 — Extras y preferencias](27feb-registro-paso2-extras.png)

---

### Paso 3: Preferencia de alojamiento

El socio puede indicar su preferencia de alojamiento ordenada por prioridad (hasta 3 opciones). Las opciones configuradas actualmente son: Albergue/Refugio, Autocaravana/Carrotienda y Tienda propia.

![Paso 3 — Alojamiento](27feb-registro-paso3-alojamiento-opciones.png)

---

### Paso 4: Revisión y confirmación

El socio ve un resumen con los participantes seleccionados, los precios de referencia por categoría, y puede añadir notas adicionales. Al confirmar, la inscripción queda registrada en estado **"Pendiente de pago"**.

![Paso 4 — Confirmación](27feb-registro-paso4-confirmar.png)

---

## 3. Panel de Administración *(mejorado)*

### Gestión de ediciones de campamento

La Junta puede ver y gestionar todas las ediciones de campamento con filtros por año, estado y ubicación. Cada edición se puede ver en detalle, cambiar de estado o editar.

![Gestión de ediciones](27feb-ediciones.png)

---

### Detalle de una edición — Alojamientos y Extras *(nuevo)*

Dentro del detalle de cada edición, la Junta puede gestionar:

- **Alojamientos**: Tipos de alojamiento disponibles (Albergue, Autocaravana, Tienda) con estadísticas de preferencias.
- **Extras**: Servicios adicionales con precio, tipo de cobro y estado. Se pueden activar, desactivar, editar y eliminar.

![Detalle de edición — Alojamientos](27feb-edicion-detalle.png)

![Detalle de edición — Extras](27feb-edicion-detalle-extras.png)

---

## 4. Lo que se ha completado esta semana

Desde el último informe (22 de febrero) se han completado estas mejoras:

| Mejora | Completada |
| ------ | ---------- |
| Pantalla de inscripción completa (asistente de 4 pasos) | 27 feb |
| Datos de tutor legal obligatorios para menores | 26 feb |
| Preferencias de alojamiento (backend + frontend) | 26-27 feb |
| Campos adicionales: necesidades especiales y preferencia de acampantes | 26 feb |
| Gestión de extras de edición (frontend completo) | 27 feb |
| Gestión de alojamientos de edición (backend + frontend) | 27 feb |
| Sistema de membresías familiares en perfil | 27 feb |
| Mejoras de UX: navegación, formularios, mapas, carrusel | 26 feb |

---

## 5. ¿Qué falta?

Con las inscripciones ya funcionando, los próximos pasos son:

| Tarea pendiente | Prioridad |
| --------------- | --------- |
| Pago online (integración con pasarela de pago) | Alta |
| Panel de administración de inscripciones (ver quién se ha inscrito) | Alta |
| Notificaciones por correo al inscribirse | Media |
| Herramienta de importación masiva de socios (CSV) | Media |
| Onboarding / guía de primer uso para socios | Baja |

---

## 6. Estado actualizado de la aplicación

| Sección | Estado |
|---------|--------|
| Acceso (login / registro) | ✅ Funcionando |
| Recuperación de contraseña | ✅ Funcionando |
| Verificación de socios en el registro | ✅ Funcionando |
| Mi Perfil con unidad familiar y membresías | ✅ Funcionando *(mejorado)* |
| Gestión de usuarios (Junta) | ✅ Funcionando |
| Gestión de ubicaciones de campamento | ✅ Funcionando |
| Galería de fotos de campamentos | ✅ Funcionando |
| Backend de inscripciones | ✅ Funcionando |
| **Pantalla de inscripción (asistente de 4 pasos)** | ✅ **Funcionando** *(nuevo)* |
| **Extras de edición (gestión + selección en inscripción)** | ✅ **Funcionando** *(nuevo)* |
| **Preferencias de alojamiento** | ✅ **Funcionando** *(nuevo)* |
| **Datos de tutor legal para menores** | ✅ **Funcionando** *(nuevo)* |
| **Gestión de ediciones de campamento** | ✅ **Funcionando** *(mejorado)* |
| Pago online | 📋 Planificado (siguiente paso) |
| Panel de administración de inscripciones | 📋 Planificado |

---

## Conclusión

**El objetivo comprometido del 28 de febrero se ha cumplido un día antes.** Las inscripciones al Campamento 2026 están operativas. Los socios pueden:

1. Seleccionar qué miembros de su familia asisten
2. Indicar datos de tutor legal para menores
3. Elegir extras opcionales
4. Indicar necesidades especiales y preferencias de acampantes
5. Seleccionar preferencia de alojamiento por orden de prioridad
6. Revisar el desglose de precios y confirmar

El siguiente paso prioritario es la integración del pago online para que los socios puedan completar el proceso de inscripción de forma autónoma.

Si la Junta desea ver una demostración en directo, estamos disponibles.

---

*Informe actualizado el 27 de febrero de 2026*
