# Abuvi — Especificaciones: Herramienta de Asignación de Alojamientos

## Objetivo

Herramienta visual para asignar familias/grupos inscritos en un campamento a los alojamientos disponibles. Permite asignación manual (drag & drop o selección + clic), auto-asignación inteligente y vista de resumen. La unidad mínima de asignación es la familia: nunca se mezclan miembros de distintas familias en un mismo alojamiento.

---

## Modelo de datos

### Inscripción (Familia/Grupo)

Cada inscripción representa una unidad familiar. Los campos relevantes son:

- **Apellido familiar**: identificador principal visible en toda la interfaz.
- **Representante**: nombre completo de la persona de contacto (normalmente un adulto). Se muestra como referencia rápida junto al apellido.
- **Tamaño**: número total de personas en la unidad. Es el dato crítico para la asignación, ya que determina si caben en un alojamiento. Los tamaños habituales son de 2 a 6 personas, siendo 4-5 lo más frecuente.
- **Miembros**: lista de personas con nombre, edad y rol (adulto, menor, mayor). No es necesario mostrar el detalle completo de cada miembro en la vista principal, pero sí conviene poder consultarlo.
- **Preferencias de alojamiento**: lista ordenada de hasta 3 tipos de alojamiento preferidos (por ejemplo: 1º Cabaña, 2º Bungalow, 3º Hab. Compartida). El orden indica prioridad: si no hay hueco en la 1ª preferencia, se intenta la 2ª, y así sucesivamente.
- **Necesidades/preferencias de habitación**: lista de características deseadas en el alojamiento (ver sección "Características de habitación"). Son preferencias, no requisitos bloqueantes — no impiden la asignación, pero sí deben mostrarse como aviso si no se cumplen.
- **Familias amigas**: lista de otras familias con las que desean estar cerca (mismo alojamiento o misma zona/edificio). Se usa para informar al asignador y para puntuar en la auto-asignación.
- **Estado de asignación**: referencia al alojamiento asignado, o null si está pendiente.

### Alojamiento

Cada alojamiento es un recurso con capacidad fija. Los campos relevantes son:

- **Nombre**: identificador legible (por ejemplo: "Cabaña 3", "Hab. 7", "Bungalow 2").
- **Tipo**: categoría del alojamiento. Los tipos definidos actualmente son:
  - Cabaña (capacidad típica: 8 plazas)
  - Bungalow (capacidad típica: 6 plazas)
  - Habitación compartida (capacidad típica: 4 plazas)
  - Individual (capacidad típica: 2 plazas)
- **Capacidad**: número máximo de personas (no familias). Una familia de 6 en un alojamiento de 8 deja 2 plazas vacías, y eso es preferible a mezclar familias.
- **Zona/Edificio**: agrupación geográfica (por ejemplo: "Zona Bosque", "Zona Lago", "Edificio A"). Se usa para filtrar y para calcular proximidad entre familias amigas.
- **Características**: lista de features disponibles en ese alojamiento (ver sección siguiente).

### Características de habitación

Las características son propiedades del alojamiento que se cruzan con las preferencias de las familias. Son informativas, no bloqueantes. Las definidas actualmente son:

- **Cama sin litera**: importante para personas mayores o con movilidad reducida.
- **Baño cerca**: proximidad a servicios.
- **Accesible**: adaptado para movilidad reducida.
- **Planta baja**: evita escaleras.

Este catálogo es extensible. Cada característica tiene un identificador interno, una etiqueta legible y un icono.

---

## Interfaz: estructura general

La interfaz se divide en dos zonas principales en la vista de asignación, más una vista de resumen.

### Vista de Asignación (layout a dos columnas)

#### Columna izquierda: Panel de familias sin asignar

Muestra la lista de familias pendientes de asignación. Es la fuente desde la que se arrastran o seleccionan familias.

**Información visible por familia (tarjeta):**

- Apellido familiar (texto principal, en negrita).
- Nombre del representante y número de personas (texto secundario).
- Indicador numérico del tamaño (badge circular destacado).
- Preferencias de alojamiento ordenadas, mostrando tipo e icono con numeración ordinal (①②③).
- Necesidades de habitación, si las tiene, como etiquetas con icono.
- Familias amigas, si las tiene, mostrando los apellidos.

**Filtros disponibles:**

- Búsqueda por texto: filtra por apellido o nombre del representante.
- Necesidades especiales: toggle para mostrar solo familias que tienen preferencias de habitación (útil para asignarlas primero).

**Filtros que NO aplican:**

- Género: no es relevante porque la asignación es por unidad familiar.
- Edad: no es relevante por la misma razón.

**Contador:** mostrar siempre el total de familias que coinciden con los filtros activos.

#### Columna derecha: Panel de alojamientos

Muestra la cuadrícula de alojamientos disponibles. Es el destino donde se sueltan o asignan las familias.

**Información visible por alojamiento (tarjeta):**

- Nombre del alojamiento (con icono de tipo).
- Zona/edificio (texto secundario).
- Etiqueta de tipo de alojamiento.
- Barra de ocupación visual: proporción de plazas ocupadas sobre la capacidad total, con código de color (verde < 70%, ámbar 70-99%, rojo = lleno).
- Contador numérico de ocupación: "{ocupadas}/{capacidad}".
- Características del alojamiento como etiquetas.
- Lista de familias asignadas, mostrando apellido, tamaño y representante, con botón para desasignar (×).
- Zona vacía para drop, indicando plazas libres restantes.

**Filtros disponibles:**

- Por tipo de alojamiento: todos, o filtrar por cabaña/bungalow/compartida/individual.
- Por zona/edificio: todos, o filtrar por zona específica.
- Solo disponibles: toggle para ocultar alojamientos que ya están llenos.

**Acciones globales:**

- Auto-asignar: ejecuta el algoritmo de asignación automática.
- Reset: desasigna todas las familias.

### Señales de compatibilidad

Cuando una familia está seleccionada (o siendo arrastrada), cada tarjeta de alojamiento debe mostrar señales contextuales que ayuden al asignador a tomar decisiones:

**Señales positivas (verde):**

- "1ª preferencia de alojamiento": el tipo coincide con la primera preferencia de la familia.
- "Cumple todas las preferencias": todas las necesidades de habitación de la familia están cubiertas por las características del alojamiento.
- "¡Familia amiga ya aquí!": una familia amiga ya está asignada a ese mismo alojamiento.

**Señales informativas (azul):**

- "3ª preferencia": coincide con la tercera opción.
- "Familia amiga en misma zona": una familia amiga está en otro alojamiento pero dentro de la misma zona/edificio.

**Señales de advertencia (ámbar):**

- "2ª preferencia": coincide con la segunda opción.
- "Pref. no cubierta: {lista}": alguna necesidad de habitación no está disponible en ese alojamiento. No es bloqueante.

**Señales bloqueantes (rojo):**

- "No caben (necesitan X, quedan Y)": la familia es más grande que las plazas disponibles.

**Comportamiento visual:** los alojamientos con señales positivas deben destacarse visualmente (borde verde, sombra sutil) para guiar la atención. Los que no tienen espacio deben atenuarse.

### Barra de selección activa

Cuando hay una familia seleccionada, mostrar una barra superior en la zona de alojamientos que indique qué familia está activa (apellido y tamaño), con opción de cancelar la selección.

### Vista de Resumen

Organizada por zonas/edificios. Cada zona muestra:

- Nombre de la zona con contador de ocupación total.
- Cuadrícula de alojamientos con las familias asignadas listadas (apellido, tamaño, representante).
- Alojamientos vacíos aparecen atenuados.
- Al final, sección de familias sin asignar si quedan pendientes, con listado completo.

---

## Panel de estadísticas (cabecera)

Visible en ambas vistas. Muestra métricas globales:

- **Total familias**: número de inscripciones.
- **Personas asignadas/total**: ratio de personas ya ubicadas sobre el total.
- **Familias sin asignar**: pendientes (en ámbar si > 0, en verde si = 0).
- **Capacidad total**: suma de plazas de todos los alojamientos.
- **Ocupación**: porcentaje de plazas ocupadas sobre la capacidad total.

---

## Interacción: modos de asignación

### Drag & drop

El usuario arrastra una tarjeta de familia desde el panel izquierdo y la suelta sobre un alojamiento del panel derecho. Solo se permite el drop si hay plazas suficientes para toda la familia.

### Selección + clic

El usuario hace clic en una familia (queda seleccionada, con fondo destacado). Luego hace clic en un alojamiento compatible. Esto asigna la familia y deselecciona automáticamente. Útil cuando el drag & drop es incómodo (pantallas táctiles, distancias grandes).

### Desasignar

Cada familia asignada dentro de un alojamiento tiene un botón × que la devuelve al panel de no asignadas.

---

## Algoritmo de auto-asignación

Principios:

1. **Nunca mezclar familias de forma forzada**: es preferible dejar plazas vacías a dividir o combinar familias arbitrariamente en un mismo alojamiento. Si una familia de 6 entra en un alojamiento de 8, quedan 2 plazas vacías y eso está bien.
2. **Respetar prioridad de preferencias**: intentar la 1ª preferencia de tipo de alojamiento, si no hay espacio pasar a la 2ª, luego la 3ª.
3. **Familias grandes primero**: ordenar las familias pendientes de mayor a menor tamaño (son las más difíciles de ubicar).
4. **Puntuar candidatos**: para cada alojamiento candidato, calcular una puntuación basada en:
   - Necesidades de habitación cubiertas (+5 por cada una).
   - Familia amiga en el mismo alojamiento (+15).
   - Familia amiga en la misma zona/edificio (+10).
   - Ajuste de tamaño: penalizar ligeramente el desperdicio de plazas para optimizar el espacio (pero nunca a costa de mezclar familias).
5. **Fallback**: si ninguna preferencia tiene espacio, buscar cualquier alojamiento con capacidad suficiente, priorizando el menor desperdicio.

---

## Tema visual

- Tema claro por defecto (fondo gris cálido, tarjetas blancas, bordes suaves).
- Colores semánticos consistentes: verde para éxito/disponible, ámbar para advertencias, rojo para lleno/error, azul para información.
- Cada tipo de alojamiento tiene un color propio para su etiqueta.
- Tipografía limpia, jerarquía clara con pesos de fuente.
- Las tarjetas deben tener hover sutil y transiciones suaves.
