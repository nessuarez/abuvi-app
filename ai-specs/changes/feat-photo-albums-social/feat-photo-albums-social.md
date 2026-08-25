# Álbumes de fotos por campamento y memoria colectiva

**Estado:** enriquecido — ver [`feat-photo-albums-social_enriched.md`](./feat-photo-albums-social_enriched.md)
**Nota:** las cuatro decisiones abiertas al final de este documento ya están resueltas en el spec enriquecido
**Depende de:** el histórico de campamentos ya cargado (31 sedes, 50 ediciones 1976–2025)
**Relacionado:** `feat-media-50-aniversary` (merged), `feat-blob-storage` (merged)

---

## Por qué

La sección del 50 aniversario ya tiene el **esqueleto**: 50 ediciones con año, sede y coordenadas. Lo que le falta es carne, y la carne son las fotos y lo que la gente sabe sobre ellas.

Hoy ese conocimiento no está en ningún sitio. Las anécdotas se cuentan en las actuaciones y en las reuniones, pero no quedan grabadas. Las fotos existen en discos duros y carpetas personales, muchas sin fecha ni lugar. El objetivo de este bloque es convertir a la comunidad en el motor del archivo, en vez de intentar catalogarlo desde dentro.

---

## Alcance

### 1. Clasificación e ingesta masiva

Herramienta de importación que ubique fotos en su edición a partir de lo que ya traen:

- **Nombre de carpeta**: patrones tipo `2003 Espinosa`, `Campa 1998`, `Selva de Oza 77`. Se resuelve contra las 50 ediciones ya cargadas.
- **Metadatos EXIF**: fecha de captura, y coordenadas GPS cuando existan — contrastables contra la ubicación de la sede.
- **Aportación directa**: subida desde la web, que ya funciona (`MediaItem`, blob storage, aprobación admin).

Las fotos sin año determinable no se descartan: entran en un montón de *"sin ubicar"* que la comunidad ayuda a datar (ver punto 4).

### 2. Galerías por edición

Un álbum por cada uno de los 50 campamentos, integrado en el frontend. Cada edición muestra sus fotos, sus audios y sus relatos. Reutiliza `MediaItem.Year` como ancla, que ya existe y ya rellena el formulario.

### 3. Identificar personas y anotar

Que cualquier abuvino pueda comentar sobre una foto concreta: *"este es mi padre"*, *"esto son las fiestas de San Abuvino"*, *"aquí tendría yo doce años"*.

Dos niveles, y conviene no confundirlos:

- **Comentario libre** sobre la foto. Barato, sin modelo de datos nuevo más allá de una tabla de comentarios.
- **Etiquetado de personas** con región de la imagen y vínculo opcional a un `User` o `FamilyMember`. Mucho más potente — habilita *"todas las fotos en las que sale mi madre"* — pero arrastra consideraciones de privacidad que hay que decidir antes de construir: quién puede etiquetar a quién, cómo se retira una etiqueta, y qué pasa con menores.

### 4. Datación colaborativa

Para las fotos sin año: *"¿de qué año es esta?"*. Propuestas de la comunidad, y la edición se fija cuando hay acuerdo suficiente. Convierte el problema del archivo desordenado en la propia mecánica de participación.

### 5. "Yo estuve en este campamento"

Botón por edición. Es la función más barata de todo el bloque y probablemente la de mayor retorno:

- Da a cada persona su propia línea de tiempo: *"tú has ido a 14 campamentos"*.
- Genera el dato que permite sugerir a quién preguntar por una foto sin datar.
- Convierte el mapa en algo personal: *"estos son tus campamentos"* sobre los 50.

---

## Lo que ya está construido y hay que reutilizar

| Pieza | Estado |
| --- | --- |
| `MediaItem` (foto/vídeo/audio/documento) con `Year`, `Decade`, `Context` | Completo |
| `Memory` (relato escrito) con autor y aprobación | Completo |
| Blob storage y generación de miniaturas | Completo |
| Formulario de subida y panel de aprobación admin | Completo (desactivado por flag) |
| Galería del aniversario con reproductor de audio | Completo |
| 50 ediciones con sede y coordenadas | Cargado |

**Nada de esto hay que rehacerlo.** El bloque nuevo es: importador masivo, álbumes por edición, comentarios/etiquetas, datación colaborativa y asistencia.

---

## Decisiones que hay que tomar antes de planificar

1. **Etiquetado de personas**: ¿comentario libre solamente, o etiquetas vinculadas a personas? La segunda opción exige una política de privacidad explícita.
2. **Quién modera**: ¿toda aportación pasa por aprobación como ahora, o los comentarios sobre fotos ya publicadas son directos?
3. **Volumen real**: cuántas fotos hay y en qué formato están, porque decide si el importador es un script puntual o una función permanente.
4. **Acceso**: la sección es hoy sólo para usuarios registrados. ¿Se mantiene?

---

## Fuera de alcance de esta nota

Esto es un enunciado de intenciones, no un plan de implementación. El plan de implementación está en [`feat-photo-albums-social_enriched.md`](./feat-photo-albums-social_enriched.md), donde las cuatro decisiones de arriba se resolvieron así: solo comentarios (etiquetado de personas diferido a una fase B), comentarios directos con denuncia y borrado por admin, importador como comando CLI de `Abuvi.Setup`, y acceso restringido a usuarios registrados.

Además se resolvió una quinta cuestión que no estaba en esta nota: todo el contenido pertenece a algún campamento, así que **no tener edición asignada es siempre un estado temporal**, nunca una categoría permanente. Subir sin saber la edición es un flujo de primera clase — es justo lo que alimenta la datación colaborativa. Los asuntos recurrentes que atraviesan años (San Abuvino, actuaciones, asambleas) se modelan como **temas**, una dimensión transversal N:M, no como un tipo de ubicación.

Y una sexta: se registra la **procedencia** de cada aportación mediante una entidad `MediaSource`, separada de la cuenta que sube el archivo. Permite acreditar a quien nos facilitó el material aunque no sea socio ni esté registrado, y guarda la ruta de carpeta original como pista de datación. Quien te dio la foto suele ser la mejor persona a quien preguntarle de qué año es.
