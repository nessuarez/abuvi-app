# Historia de los campamentos: crónica por edición y composición de la Junta

**Feature ID:** `feat-camp-history-summary-board`
**Fecha:** 2026-08-28
**Tipo:** Feature de producto + carga de datos histórica
**Depende de:** [`feat-anniversary-history-map`](../feat-anniversary-history-map/feat-anniversary-history-map_enriched.md) (Fases 2 y 3, ya implementadas)

---

## 1. Resumen

La petición original pide dos cosas a la vez:

1. Que cada campamento histórico muestre el **resumen que redactaron antiguos socios**.
2. Que se sepa **quién formaba la Junta Directiva**, sobre todo la presidencia.

Parecen un mismo trabajo — "más campos en los campamentos históricos" — pero **no lo son**, y conviene separarlas desde el principio porque su coste y sus bloqueos son opuestos:

| | Resúmenes | Junta Directiva |
| --- | --- | --- |
| **¿Existe el dato?** | **Sí, y completo.** 114 páginas ya redactadas | **No.** No hay ninguna fuente estructurada |
| **Bloqueo real** | Editorial y de privacidad, no técnico | Recopilación de datos, no técnico tampoco |
| **Modelo de datos** | Un campo en `CampEdition` | Entidades nuevas (`BoardTerm`, `BoardMember`) |
| **Se puede entregar ya** | Sí | No sin que alguien reúna los nombres |

**Recomendación de alcance:** son dos entregas. La primera (resúmenes) puede estar en producción en cuestión de días porque el contenido ya está escrito. La segunda (Junta) es barata de construir pero **inútil hasta que exista el dato**, así que lo primero que hay que hacer no es código: es pedir a la Junta actual que reconstruya el histórico de mandatos.

---

## 2. Historias de usuario

**Como** abuvino/a recorriendo los 50 años en el mapa,
**quiero** leer la crónica del campamento de un año concreto,
**para** recordar qué pasó allí y no quedarme sólo con el nombre del sitio y un puñado de fotos.

**Como** abuvino/a de las primeras ediciones,
**quiero** ver quién presidía la asociación cada año,
**para** situar la época y reconocer a quienes la sacaron adelante.

**Como** miembro de la Junta,
**quiero** cargar y corregir la crónica y la composición de la Junta desde la web,
**para** que el archivo se complete sin depender de nadie que toque la base de datos.

---

## 3. Estado actual verificado

### El contenido de los resúmenes ya existe

`docs/5.- Historia ABUVI.doc` — **114 páginas, 39.299 palabras**, autor **Florentino Castilla Simarro** (creado 2018, última modificación 03/08/2025). Contiene una sección por año, con encabezados del tipo `2000 - SAN JUAN DE RIÑPAR (ALBACETE)`. Se han localizado las **50 secciones**, 1976–2025.

`docs/Campamentos.doc` — el índice: 50 líneas `año: sede (provincia)`. Y lo más útil, marca explícitamente **qué años no tienen crónica**:

> 2002 San Martín del Castañar *(sin descripción)*, 2003 Espinosa de los Monteros III *(sin descripción)*, 2004 Condemios de Arriba II *(sin descripción)*, 2005 Cabañeros *(sin descripción)*, 2006 Boñar II *(sin descripción)*

Es decir: **45 de 50 ediciones tienen texto, 5 no.** Esos cinco huecos son un hallazgo aprovechable — son exactamente la llamada a la acción que la feature del aniversario ya sabe mostrar para las fotos.

> **Ambos ficheros están sin versionar** (aparecen como `??` en `git status`). Son documentos internos de la asociación. Antes de meterlos en el repositorio hay que decidir si procede (ver D4).

### El contenido de la Junta no existe en ninguna parte

- **No hay entidad, ni tabla, ni CSV.** Nada en `Features/` modela un cargo ni un mandato.
- Los roles del sistema son `Admin`, `Board` y `Member`, y describen **permisos de hoy**, no historia. Quien presidió en 1983 probablemente no tiene cuenta.
- El documento histórico menciona *"la Junta Directiva"* más de veinte veces, pero **nunca lista su composición**. Lo más cercano es un acta de asamblea que enumera *candidatos* (`"se presentan los siguientes abuvinos: Salvador Corral Rosado. Mercedes Limón Echevarría. …"`) — candidatos, no electos.
- `docs/2025 08 21 ACTA DE ASAMBLEA ORDINARIA_signed.pdf` existe, pero usa fuentes subconjuntadas y no se deja extraer el texto automáticamente.

**Conclusión: esta mitad de la petición está bloqueada por recopilación de datos, no por desarrollo.**

### Piezas que ya existen y se reutilizan

| Pieza | Ubicación |
| --- | --- |
| `CampEdition` con `Year`, `Status`, `Description`, `Notes` | [CampsModels.cs:186](src/Abuvi.API/Features/Camps/CampsModels.cs#L186) |
| `GET /api/camps/history` → `CampHistoryResponse` (50 filas, 3 consultas SQL fijas) | [CampsModels.cs:832](src/Abuvi.API/Features/Camps/CampsModels.cs#L832), `CampHistoryService.cs` |
| Panel de detalle del año seleccionado — **aquí es donde entra todo esto** | [AnniversaryJourney.vue:191-230](frontend/src/components/anniversary/AnniversaryJourney.vue#L191-L230) |
| `Memory` con `CampEditionId`, autor y aprobación | [MemoriesModels.cs:7](src/Abuvi.API/Features/Memories/MemoriesModels.cs#L7) |
| Edición administrativa de una edición | `frontend/src/views/camps/CampEditionDetailPage.vue` |
| Migración idempotente de datos históricos (patrón a copiar) | `20260826082352_SeedHistoricalCamps` |

---

## 4. Decisiones requeridas

| # | Decisión | Recomendación |
| --- | --- | --- |
| **D1** | ¿La crónica es un campo de `CampEdition` o un `Memory` con `CampEditionId`? `Memory` ya existe, tiene autor y aprobación, y "resumen escrito por un socio" encaja literalmente en su descripción. | **Campo en `CampEdition`, y `Memory` sigue como está.** No compiten: son dos cosas distintas. La crónica del documento es **una sola, canónica, editable sólo por la Junta**, y debe salir siempre en el panel del año — no en una lista de relatos entre otros. Un `Memory` es *uno de muchos* recuerdos personales, pasa por aprobación y puede haber quince del mismo año. Modelar la crónica como `Memory` obligaría a marcar uno como "el bueno", que es un campo disfrazado de otra cosa. |
| **D2** | ¿Reutilizar `CampEdition.Description` o añadir un campo nuevo? | **Campo nuevo `HistoricalSummary`.** `Description` **ya se pinta en la interfaz del campamento en curso** ([CampEditionDetails.vue:93-101](frontend/src/components/camps/CampEditionDetails.vue#L93-L101)) y `Notes` en [ActiveEditionCard.vue:86](frontend/src/components/camps/ActiveEditionCard.vue#L86). Meter 800 palabras de crónica ahí las volcaría en la ficha de inscripción. Además `Description` es editable en el flujo de propuesta de ediciones: se sobrescribiría sin querer. |
| **D3** | Volumen del texto: ~39.000 palabras / 50 ≈ **780 palabras de media por edición**. ¿Se importa el texto íntegro, un resumen corto, o ambos? | **Ambos: `HistoricalSummary` (texto íntegro, Markdown) + `HistoricalSummaryExcerpt` (2–3 frases).** Y **el íntegro no viaja en `/api/camps/history`** — ver la sección de rendimiento. El extracto puede generarse al importar tomando el primer párrafo, y corregirse después a mano. |
| **D4** | La crónica es prosa personal: nombra a gente (*"Maruja solo fines de semana"*, *"Jesús no fue"*), narra desencuentros, una denuncia a la Guardia Civil, actas de asamblea con nombres y apellidos. ¿Se publica tal cual a todos los socios? | **No sin una lectura editorial previa.** Técnicamente es trivial publicarla; el riesgo es que un texto escrito para circular en papel entre conocidos pase a ser consultable por cualquier socio autenticado. **La Junta debe leerlo y decidir qué entra**, edición por edición si hace falta. El importador debe dejar el contenido **despublicado por defecto** (ver D5) para que esa revisión sea posible sin bloquear la carga técnica. |
| **D5** | ¿Cómo se controla qué crónica está visible? | **Un booleano `HistoricalSummaryIsPublished` por edición**, por defecto `false`. La carga importa las 45 crónicas sin publicar; la Junta las va abriendo. Es más barato que una cola de aprobación y encaja con el criterio ya adoptado en la Fase MVP del aniversario ("recoger, no publicar todavía"). |
| **D6** | **Modelado de la Junta: ¿se cuelga de `CampEdition` o de un mandato propio?** | **Mandato propio (`BoardTerm`), nunca de la edición.** Ver el razonamiento abajo — es la decisión más cara de revertir de todo el documento. |
| **D7** | ¿Los miembros de la Junta son texto libre o se enlazan a `User`/`FamilyMember`? | **Texto libre obligatorio (`FullName`) + enlace opcional (`UserId` nullable).** La mayoría de quienes presidieron en los 80 no tienen cuenta y algunos han fallecido. Exigir una cuenta haría imposible cargar el histórico. El enlace opcional permite que, cuando la persona sí exista, la ficha se conecte. |
| **D8** | ¿Publicar nombres de personas asociados a un cargo y un año? | **Sí, para socios autenticados**, con dos matices: es un cargo asociativo (no un dato íntimo), y la sección no es indexable. Pero **aplica el mecanismo de retirada a petición** descrito en la Fase 3.7 del aniversario: alguien puede no querer aparecer. Que sea de bajo riesgo no lo convierte en automático. |

### Por qué la Junta no cuelga de la edición del campamento (D6)

Es tentador añadir `PresidentName` a `CampEdition` y terminar. **No hacerlo.**

Una Junta Directiva se elige en asamblea y sirve un **mandato de varios años** — el propio documento histórico habla de *"la siguiente Junta Directiva"* y de candidaturas presentadas en la asamblea del campamento. Si los cargos cuelgan de la edición:

- Los mismos cinco nombres se repiten en tres o cuatro filas, y basta corregir uno para que queden inconsistentes.
- **"¿Cuándo cambió la presidencia?" deja de ser respondible**, que es justo la pregunta interesante en un 50 aniversario.
- Un mandato que no coincide con un campamento (una dimisión a mitad de año, un año sin campamento) no se puede representar.

El modelo correcto son dos entidades pequeñas:

```
BoardTerm    (mandato)   → StartYear, EndYear?, Notes
BoardMember  (cargo)     → BoardTermId, FullName, Role, UserId?, SortOrder
```

Y la ficha del año **deriva** quién presidía buscando el mandato que cubre ese año. Coste añadido hoy: una entidad más y una consulta. Coste de retrofitarlo después de haber cargado 50 años a mano: rehacer la carga.

> **Matiz que hay que aceptar:** un mandato puede solaparse mal con los años si los datos que aporte la Junta son imprecisos ("creo que Fulano presidió a finales de los 90"). El modelo debe tolerar `EndYear` nulo (mandato en curso o desconocido) y años sin ningún mandato registrado — que serán la mayoría al principio. La interfaz dice *"no consta"*, no un hueco vacío.

---

## 5. Modelo de datos

### 5.1 `CampEdition` — campos nuevos

| Campo | Tipo | Notas |
| --- | --- | --- |
| `HistoricalSummary` | `string?` | Texto íntegro de la crónica. **Markdown**, no HTML: `marked` ya es dependencia del frontend. Sin límite práctico de longitud (`text`) |
| `HistoricalSummaryExcerpt` | `string?` | 2–3 frases, máximo 300 caracteres. Es lo que viaja en el listado |
| `HistoricalSummaryAuthor` | `string?` | *"Florentino Castilla Simarro"*. Texto libre: el autor no tiene por qué ser usuario |
| `HistoricalSummaryIsPublished` | `bool` | Por defecto `false` (D5) |

Migración EF con las cuatro columnas nullables/`default false`. **No rompe nada**: ninguna consulta existente las lee.

### 5.2 `BoardTerm` y `BoardMember` — entidades nuevas

```csharp
public class BoardTerm
{
    public Guid Id { get; set; }
    public int StartYear { get; set; }
    public int? EndYear { get; set; }        // null = en curso o desconocido
    public string? Notes { get; set; }       // "Junta gestora", "dimisión a mitad de mandato"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<BoardMember> Members { get; set; } = [];
}

public class BoardMember
{
    public Guid Id { get; set; }
    public Guid BoardTermId { get; set; }
    public string FullName { get; set; } = string.Empty;   // obligatorio (D7)
    public BoardRole Role { get; set; }
    public Guid? UserId { get; set; }                      // enlace opcional (D7)
    public int SortOrder { get; set; }
    public BoardTerm BoardTerm { get; set; } = null!;
    public Users.User? User { get; set; }
}

public enum BoardRole
{
    President,       // Presidencia — el único que la petición marca como imprescindible
    VicePresident,
    Secretary,
    Treasurer,
    Member           // Vocal
}
```

**El enum se etiqueta en femenino y masculino en la interfaz** (*"Presidenta"* / *"Presidente"*) — la petición lo dice explícitamente (*"la presidenta/e"*). Eso implica que `BoardMember` necesita saberlo, o que la etiqueta se resuelve de otra forma. **Decisión abierta 1**, abajo.

**Índice:** `BoardTerm(StartYear)`. Serán como mucho 20 filas; no hace falta más.

---

## 6. Endpoints

### 6.1 Ampliación de `GET /api/camps/history`

Se añaden a `CampHistoryResponse` **sólo campos ligeros**:

```jsonc
{
  "year": 2000,
  // … campos existentes …
  "hasHistoricalSummary": true,          // hay crónica publicada para este año
  "historicalSummaryExcerpt": "En años anteriores las distintas Juntas Gestoras habían intentado…",
  "presidentName": "…"                   // null si no consta el mandato de ese año
}
```

> **El texto íntegro NO va aquí, y es el punto que más importa de esta sección.** 50 ediciones × ~780 palabras ≈ **250–300 KB de texto** en una única respuesta, en una página cuyos requisitos no funcionales son explícitamente *móvil primero* y *pensada para personas mayores con la cobertura de un campamento*. Meter la crónica completa en el listado destruye justo el escenario para el que se diseñó.

**Restricción heredada que hay que respetar:** existe un test que intercepta los comandos que EF envía a PostgreSQL y **exige exactamente 3 consultas**, independientemente del número de ediciones. Resolver la presidencia por año **no puede introducir una consulta por edición**: se cargan los mandatos de una vez (son ~20 filas) y se resuelven en memoria. Ese test debe seguir en verde, ajustando la cifra a 4 si se añade la consulta de mandatos — nunca dejándola variable.

### 6.2 Endpoints nuevos

| Método | Ruta | Autorización | Uso |
| --- | --- | --- | --- |
| `GET` | `/api/camps/editions/{id}/summary` | Socio autenticado | Texto íntegro de una crónica. Sólo si está publicada; si no, 404 para socios y contenido para `Board`/`Admin` |
| `PUT` | `/api/camps/editions/{id}/summary` | `Board`/`Admin` | Editar crónica, extracto, autor y el flag de publicación |
| `GET` | `/api/board-terms` | Socio autenticado | Todos los mandatos con sus miembros, ordenados por `StartYear`. Son ~20 filas: sin paginar |
| `POST` / `PUT` / `DELETE` | `/api/board-terms[/{id}]` | `Board`/`Admin` | Alta y mantenimiento |

Los grupos de rutas siguen el patrón ya establecido en `CampsEndpoints.cs`: `/api/camps` completo es `Admin`/`Board`, y las lecturas de socio van en un grupo aparte (`campCurrentGroup`).

---

## 7. Plan por fases

### Fase 1 — Crónica por edición *(entregable ya; no depende de nadie)*

**Backend:** cuatro columnas en `CampEdition`, migración, ampliación de `CampHistoryResponse` con `hasHistoricalSummary` y `historicalSummaryExcerpt`, `GET`/`PUT` de la crónica.

**Frontend:** en el panel de detalle del año de [AnniversaryJourney.vue:191-230](frontend/src/components/anniversary/AnniversaryJourney.vue#L191-L230), mostrar el extracto y un *"Leer la crónica completa"* que carga el texto bajo demanda y lo renderiza con `marked`. Edición desde `CampEditionDetailPage.vue`.

**Estado vacío, y esto sí importa:** los 5 años sin crónica (2002–2006) **no muestran un hueco**. Muestran la misma llamada a la acción que ya se usa para las fotos: *"De 2003 en Espinosa de los Monteros no conservamos ninguna crónica"*, con enlace al formulario de aportación. El hueco es el argumento, no el defecto — el criterio ya adoptado en `feat-anniversary-history-map`.

### Fase 2 — Carga de las 45 crónicas

**Es un trabajo de extracción de documento, no de desarrollo.** `5.- Historia ABUVI.doc` es un `.doc` binario de 2018: no se parsea de forma fiable con las herramientas del repositorio. El camino sensato es **convertirlo a texto una sola vez** (guardar como `.docx`/`.txt` desde Word), trocearlo por los encabezados de año a un CSV `year,summary,excerpt,author`, y cargarlo con el patrón ya probado.

**Vía de carga — dos entornos, como en el aniversario:**
- **Desarrollo:** comando `import` en `Abuvi.Setup` (recordar: el flag es `--connection=<cadena>` **con signo igual**; con espacio se ignora en silencio).
- **Producción:** **migración EF idempotente**, porque `SafetyGuard.EnsureImportAllowedAsync` prohíbe importar sobre una base con datos. Copiar el patrón de `20260826082352_SeedHistoricalCamps`: `migrationBuilder.Sql`, resolución por **año** (no por `campId`, que difiere entre entornos), y `UPDATE … WHERE "HistoricalSummary" IS NULL` para que reaplicarla no pise correcciones hechas a mano.

**Todo entra con `HistoricalSummaryIsPublished = false`** (D5). La Junta lo va abriendo tras la lectura editorial de D4.

### Fase 3 — Junta Directiva *(bloqueada por datos, no por código)*

**Prerrequisito, y es de la asociación, no del equipo:** alguien tiene que reconstruir el histórico de mandatos. Sugerencia de formato mínimo, que se puede rellenar en una hoja de cálculo:

```
añoInicio, añoFin, cargo, nombre completo
```

Vale con la presidencia. Lo demás, si aparece, mejor — que es literalmente lo que pide el enunciado.

**Fuentes a revisar antes de tirar de memoria:** las actas de asamblea (`docs/2025 08 21 ACTA…pdf` y las que la Junta conserve) y el registro de asociaciones, donde los cambios de Junta se comunican oficialmente y hay traza documental de décadas.

**Cuando el dato exista:** entidades, migración, CRUD de admin, `presidentName` en el histórico y una sección *"La Junta de estos años"* en el panel del año, con enlace a un listado completo de mandatos.

**Si sólo se consigue la presidencia de algunos años**, la feature sigue siendo válida: los años sin mandato registrado dicen *"no consta"*. Un archivo incompleto y honesto vale; uno que aparenta completitud, no.

---

## 8. Requisitos no funcionales

**Rendimiento**
- El texto íntegro nunca viaja en el listado (§6.1). El extracto se limita a 300 caracteres a nivel de validador, no sólo por convención.
- El recuento de consultas SQL de `/api/camps/history` sigue siendo **fijo e independiente del número de ediciones**. Test existente; ajustar la cifra esperada, jamás relajar la comprobación.
- `/api/board-terms` es prácticamente inmutable: cachear.

**Seguridad y privacidad**
- Una crónica no publicada **no se sirve a socios**: 404, no 403 — un 403 confirma que existe.
- Markdown renderizado con `marked`: **sanear la salida**. La crónica la escribe la Junta, pero es HTML inyectado en la página; el mismo problema que ya obligó a reconstruir los popups del mapa como nodos DOM en la Fase 3.5 del aniversario.
- Nombres de miembros de la Junta: sección no indexable y **retirada a petición** operativa (Fase 3.7 del aniversario). Aplica también a los nombres propios que aparecen dentro de las crónicas.
- La revisión editorial de D4 es un requisito de publicación, no una recomendación.

**Usabilidad y accesibilidad** — se hereda íntegro lo de `feat-anniversary-history-map`, y aquí hay un riesgo concreto: una crónica de 800 palabras es **el contenido más largo de toda la sección**.
- Cuerpo mínimo **16 px**. Ningún `text-xs` para la crónica ni para los nombres de la Junta.
- Ancho de línea legible (~65–75 caracteres), interlineado holgado. Un muro de texto a ancho completo en móvil no lo lee nadie, y menos el público objetivo declarado.
- *"Leer la crónica completa"* es un **botón con texto**, nunca sólo un icono, y de 48 × 48 px como mínimo.
- Legible con zoom del navegador al 200 %.

**Calidad de datos**
- La carga verifica que **cada crónica cae en el año correcto**. Un desfase de una sección al trocear el documento colocaría 50 crónicas en el año equivocado y nadie lo notaría hasta leerlas: comprobar contra el índice de `Campamentos.doc`, que da año y sede.
- Los 5 años sin crónica quedan explícitamente a `null`, no con una cadena vacía ni un texto de relleno.
- Un mandato con `EndYear < StartYear` se rechaza en el validador.

**Documentación**
- `ai-specs/specs/data-model.md`: campos nuevos de `CampEdition`, `BoardTerm`, `BoardMember`, `BoardRole`.
- `ai-specs/specs/api-endpoints.md`: los endpoints de §6.
- `ai-specs/changes/INDEX.md`: esta feature.

---

## 9. Criterios de aceptación

### Fase 1 — Crónica

- [ ] `CampEdition` tiene los cuatro campos nuevos; la migración se aplica sobre la base existente sin romper nada.
- [ ] `GET /api/camps/history` devuelve `hasHistoricalSummary`, `historicalSummaryExcerpt` y `presidentName`, y **no** el texto íntegro.
- [ ] El recuento de consultas SQL del histórico sigue siendo fijo, verificado con el interceptor existente.
- [ ] Una crónica **no publicada** devuelve 404 a un socio y contenido a `Board`/`Admin`.
- [ ] El panel del año muestra el extracto, y *"Leer la crónica completa"* carga el texto bajo demanda.
- [ ] Un año sin crónica (2003, por ejemplo) muestra la llamada a la acción, no un hueco.
- [ ] La crónica renderizada en Markdown no ejecuta HTML inyectado (test con `<script>` en el texto).
- [ ] La Junta puede editar y publicar la crónica desde `CampEditionDetailPage.vue`, sin tocar la base de datos.

### Fase 2 — Carga

- [ ] 45 crónicas cargadas, cada una en su año, contrastadas contra el índice de `Campamentos.doc`.
- [ ] 2002–2006 quedan a `null`, no vacías.
- [ ] La migración es idempotente: reaplicarla no duplica ni pisa correcciones manuales.
- [ ] Todo entra despublicado.

### Fase 3 — Junta

- [ ] `GET /api/board-terms` devuelve los mandatos con sus miembros, ordenados por año.
- [ ] Un año cubierto por un mandato muestra la presidencia en el panel del año; uno sin mandato dice *"no consta"* y no rompe la vista.
- [ ] Un miembro de Junta sin cuenta de usuario se guarda y se muestra igual que uno con cuenta.
- [ ] Un mandato con `EndYear` nulo se interpreta como en curso o desconocido, sin excepciones.
- [ ] `Board`/`Admin` mantienen los mandatos desde la interfaz; un socio sólo lee.

---

## 10. Decisiones abiertas

1. **Género del cargo.** La petición pide *"la presidenta/e"*. Mostrar *"Presidenta"* o *"Presidente"* exige un dato de género que hoy no está en el modelo, y añadirlo por esto es desproporcionado. Tres salidas: (a) etiqueta neutra por cargo (*"Presidencia"*, *"Secretaría"*, *"Tesorería"*) — la más barata, y la que no se equivoca nunca; (b) un campo de etiqueta libre por miembro; (c) inferirlo del nombre — **descartada**, se equivoca y es exactamente el error que más molesta. **Recomendación: (a).**
2. **¿Hasta dónde llega la lectura editorial de D4?** ¿La revisa la Junta entera, una persona, o se publica por defecto lo anterior a cierta fecha? Sin dueño, las 45 crónicas se quedan despublicadas para siempre y la feature no sirve de nada.
3. **¿El autor de la crónica se cita en la interfaz?** Recomendado que sí, y con nombre completo: es de justicia, y da autoridad al texto. Conviene pedirle permiso.
4. **Crónicas de socios distintas de la canónica.** Si con esto aparecen más personas queriendo escribir la suya, eso es un `Memory` con `CampEditionId` — que ya funciona. Conviene decidir si el panel del año las lista junto a la crónica canónica o las deja en el anecdotario (Fase 3.5 del aniversario, aplazada).

---

## 11. Fuera de alcance

- Convertir el `.doc` mediante código. Se convierte una vez a mano; automatizar un parser de formato binario de Word para una carga que ocurre una sola vez no se paga.
- Digitalizar las actas de asamblea completas. Sólo se extrae de ellas la composición de la Junta.
- Traza histórica de socios (quién asistió a cada campamento) — ya declarado fuera de alcance en `feat-anniversary-history-map`.
- Buscador de texto completo sobre las crónicas. Con 50 documentos, el `Ctrl+F` del navegador basta; si se pide, es su propia feature.
- Fotografías de las Juntas. Si aparecen, entran por `MediaItem` como cualquier otra.
