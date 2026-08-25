# Enriched User Story: 50 Aniversario — Recorrido histórico (mapa + cronología)

**Feature ID:** `feat-anniversary-history-map`
**Fecha:** 2026-08-24
**Estado:** Fases 0–1.6 implementadas · Fases 2–4 pendientes
**Referencia visual:** [`docs/CAMPAMENTOS_HISTORICOS-geocode-review.html`](../../../docs/CAMPAMENTOS_HISTORICOS-geocode-review.html) — disposición mapa + lista adoptada para la Fase 3
**Tipo:** Feature de producto + carga de datos histórica

---

## 1. Contexto y problema

La sección `/anniversary` tiene construido el flujo de **entrada** de contenido (subida de fotos, vídeo, audio e historias; blob storage; aprobación por admin; galería). Lo que no existe es la **salida**: una forma de recorrer los 50 años de campamentos.

El problema de fondo no era de interfaz. El listado histórico de año y lugar —el único dato completo que tiene la asociación— **no estaba en base de datos**. Sin ese esqueleto no se pueden ubicar las fotos, ni anclar el relato histórico, ni construir una cronología real: la que había en el código mostraba hitos inventados (`AnniversaryTimeline.vue`, comentado en la página).

> **Material disponible.** Ya existen fotografías suficientes para documentar buena parte de los 50 campamentos. Eso cambia el tono de la entrega: la demo no será un esqueleto casi vacío, sino un archivo con contenido real desde el primer día — y sube la prioridad del importador masivo descrito en `feat-photo-albums-social`.

**Objetivo de negocio:** presentar el concepto a la comunidad con una demo navegable sobre datos reales, aunque la mayoría de nodos estén vacíos. Los huecos son la llamada a la acción, no un defecto. En paralelo, habilitar la captura de audio para grabar anécdotas durante el campamento en curso, que es una ventana que no se repite este año.

---

## 2. Historia de usuario

**Como** abuvino/a,
**quiero** recorrer los 50 años de campamentos en un mapa y una cronología enlazados,
**para** reconocer los campamentos a los que fui, ver qué recuerdos se conservan de cada uno y aportar los míos donde falten.

**Como** miembro de la junta,
**quiero** revisar y afinar la ubicación exacta de cada campamento desde la web,
**para** que el mapa señale el campamento real y no el centro del municipio.

### Público objetivo

**Abuvinos y abuvinas de todas las edades, en el móvil.** Buena parte de quienes guardan las fotos y recuerdan las anécdotas de los años 70, 80 y 90 son hoy personas mayores. Son justamente el público que más tiene que aportar y el que más barreras encuentra.

Esto **no es un requisito secundario: manda sobre las decisiones de diseño**. Ante una disyuntiva entre una interfaz vistosa y una que una persona de 70 años pueda usar sin ayuda desde el móvil, gana la segunda.

---

## 3. Estado actual verificado

### Ya existía y se reutiliza

| Pieza | Ubicación |
| --- | --- |
| Entidades `Camp` y `CampEdition` con `Latitude`/`Longitude`/`PlaceTypes`/`Year`/`Status` | `Features/Camps/CampsModels.cs` |
| `MediaItem` con `Year`, `Decade`, `Context`, enum con `Audio` | `Features/MediaItems/` |
| `Memory` con autor y aprobación | `Features/Memories/` |
| Mapa Leaflet de sólo lectura | `frontend/src/components/camps/CampLocationMap.vue` |
| Cronología PrimeVue (responsive) | `frontend/src/components/anniversary/AnniversaryTimeline.vue` |
| Galería con reproductor de audio | `frontend/src/components/anniversary/AnniversaryGallery.vue` |
| Formulario de subida completo | `frontend/src/components/anniversary/AnniversaryUploadForm.vue` |
| Google Places: autocompletado y detalles | `Features/GooglePlaces/GooglePlacesService.cs` |
| Sincronización Google → `Camp` (dirección, teléfono, web, valoración, tipos y fotos) | `POST /api/camps/{id}/refresh-places` |

### Implementado en esta feature (Fases 0–1.6)

- **`CsvHelper.Parse` con comillas RFC 4180.** El parser hacía `line.Split(',')` sin tratar comillas, lo que rechazaba el 100 % de las filas de los CSV exportados. Ahora soporta campos entrecomillados, comas y saltos de línea dentro de comillas y `""` escapadas. Beneficia a los cinco importadores.
- **`CsvHelper.Write` y `CsvHelper.OptionalDecimal`.**
- **`CampImporter` extendido**: lee `latitude`, `longitude`, `googlePlaceId`, `formattedAddress` y `googleTypes` (→ `Camp.PlaceTypes`) como columnas opcionales.
- **Comando `geocode`** en `Abuvi.Setup`: geocodifica desde Google Places, verifica y genera un mapa HTML de revisión.
- **Carga del histórico**: 31 sedes y 50 ediciones (1976–2025).
- **`CampLocationPicker.vue`**: marcador arrastrable con capa de satélite y bloqueo explícito.
- **Columna de precisión** en el listado de sedes, con filtro de revisión.

---

## 4. Modelo de datos

**No se crea ninguna entidad nueva.** Decisión deliberada: `Camp` es la sede y `CampEdition.Year` es el año, lo que encaja sin forzar nada. Se descartó crear `CampLocation` porque el contenido se ancla por `MediaItem.Year`, que ya existe.

### Campos usados en `Camp`

| Campo | Uso |
| --- | --- |
| `Name` | Clave de resolución del importador (case-insensitive) |
| `Location` | Provincia; sirve de contraste al geocodificar |
| `Latitude` / `Longitude` | `decimal?`, 6 decimales |
| `GooglePlaceId`, `FormattedAddress` | Trazabilidad del origen de la coordenada |
| `PlaceTypes` | Tipos de Google; base del indicador de precisión |
| `IsActive` | **No se toca.** Lo histórico se identifica por tener ediciones `Completed` |

### Campos usados en `CampEdition`

`CampId`, `Year`, `StartDate`, `EndDate`, `Status = Completed`, `IsArchived = false`. Precios a `0`: los históricos no se conocen.

---

## 5. Endpoints

### Nuevo — Fase 2

```
GET /api/camps/history
```

- **Autorización:** usuario autenticado (socio). Grupo propio, porque `/api/camps` completo es `Admin`/`Board` (`CampsEndpoints.cs:23`). Seguir el patrón de `campCurrentGroup` y `editionsMemberGroup` del mismo fichero.
- **Sin paginación:** son 50 filas.
- **Respuesta** `ApiResponse<List<CampHistoryResponse>>`, ordenada por año ascendente:

```jsonc
{
  "year": 2015,
  "campId": "…",
  "campName": "Espinosa de los Monteros",
  "location": "Burgos",
  "latitude": 43.077348,
  "longitude": -3.552172,
  "editionNumber": 4,          // veces acampadas allí hasta ese año, incluida
  "totalEditionsAtVenue": 4,   // total histórico en esa sede
  "photoCount": 37,            // recuerdos aprobados y publicados de ese año
  "previewPhotos": [           // hasta 3, para dar vida al mapa sin otra llamada
    { "id": "…", "thumbnailUrl": "…", "title": "Llegada al campamento" }
  ]
}
```

`editionNumber` y `totalEditionsAtVenue` se calculan en el servicio; son lo que permite al mapa contar *"aquí volvimos 4 veces"*.

#### Fotos en la respuesta

Sin fotos, el mapa es una lista de sitios. Con ellas, es un archivo. Por eso el endpoint devuelve **cuántos recuerdos hay** y **hasta tres miniaturas** por edición.

- **`photoCount` es lo que da sentido al recorrido.** Es lo que permite decir *"de 1987 en Los Palancares no conservamos nada"* y convertir el hueco en llamada a la acción. Sin el contador no hay forma de distinguir un año vacío de uno que aún no se ha cargado.
- **`previewPhotos` evita una segunda llamada** al recorrer el mapa. Sólo `id`, `thumbnailUrl` y `title`: 50 filas × 3 miniaturas es una carga trivial si no se manda la imagen completa.

**Anclaje por año, y funciona porque hay exactamente una edición por año** (50 ediciones, 1976–2025, sin huecos ni repeticiones). Se filtra por `MediaItem.Year`, `Context = "anniversary-50"`, `IsApproved` e `IsPublished`.

**Cuidado con el N+1.** Debe resolverse con una única consulta agrupada, no con 50 subconsultas. Es el riesgo de rendimiento principal de esta fase.

**No se devuelve una URL de galería.** El enlace lo construye el frontend a partir del año (`/anniversary/galeria?anio=2003`). Que la API devuelva rutas de la interfaz la acopla al enrutado del cliente y obliga a tocar el backend cada vez que cambie una ruta.

**Frontera con `feat-photo-albums-social`:** esta feature aporta el **contador y la vista previa** por edición. El álbum completo, los comentarios y la identificación de personas viven en la otra. El punto de unión es `MediaItem.Year`.

> **Consecuencia a tener presente.** Mostrar fotos en el mapa es **publicarlas** a todos los socios. Sigue siendo el escenario de menor riesgo —acceso autenticado, y la galería ya hace justo esto hoy—, pero hace más urgente el mecanismo de retirada descrito en la Fase 3.7. Recomendación: entra en cuanto la primera foto sea visible en el mapa, no después.

### Existentes que se aprovechan

| Endpoint | Uso |
| --- | --- |
| `GET /api/media-items?year=&context=anniversary-50` | Galería filtrada por año |
| `POST /api/media-items` | Subida de recuerdos |
| `PUT /api/camps/{id}` | Guardar ubicación afinada |
| `POST /api/camps/{id}/refresh-places` | Enriquecer sede desde Google |

---

## 6. Trabajo pendiente

> **Alcance acordado (2026-08-25).** Abrir la contribución a todo el mundo arrastra de golpe consentimiento de imagen, visibilidad del archivo y carga de moderación. Se acota a lo indispensable a corto plazo —**que quien esté en el campamento pueda aportar**— y se aplaza el resto.
>
> Las fases 3.5, 3.6 y 3.7 quedan **aplazadas**. Su análisis se conserva íntegro: el trabajo ya está hecho y las decisiones identificadas siguen siendo válidas cuando se retomen.

---

### Fase MVP — Aportar en el campamento ⭐ PRIORIDAD

Lo indispensable a corto plazo. Deliberadamente pequeño.

**Recoger, no publicar todavía.** Todo lo aportado entra en la cola de aprobación, que ya existe. La junta decide después qué se publica y con qué criterio. Eso reduce el problema de consentimiento a su mínima expresión sin dejar de capturar el material —que es lo urgente, porque el campamento no se repite.

#### Qué entra

| Punto | Coste |
| --- | --- |
| Activar el formulario: `comingSoon = false` en `AnniversaryUploadForm.vue:44` | Una línea |
| Selector de edición (*"2003 — Espinosa de los Monteros"*) en vez de teclear el año | Pequeño, depende de la Fase 2 |
| Deep link para el QR: `/anniversary?tipo=audio&anio=2026#subir-recuerdo` | Pequeño |
| Casilla obligatoria de derechos de imagen antes de enviar | Pequeño |
| Aportar **en nombre de otra persona**: el formulario ya tiene campo de nombre; dejar claro que puede ser el de quien recuerda, no el de quien sube | Textual |

#### Acceso: sin desarrollo nuevo

Se mantiene `requiresAuth`. Para quien no tenga cuenta, **vía asistida**: alguien de la junta con sesión abierta sube el material en el momento. No escala a un campamento entero, pero **no requiere construir nada** y desbloquea la captura ya.

Si se comprueba que la vía asistida no da abasto, se retoma la Fase 3.6.

#### Lo mínimo de consentimiento que sí entra

Aunque no se publique de inmediato, **recoger fotos ya crea la obligación**. Dos cosas, ambas baratas:

1. **Casilla obligatoria** antes de enviar: *"Tengo derecho a compartir esta imagen y respeto la privacidad de quienes aparecen."*
2. **Aviso visible** de que el material se revisa antes de publicarse y de a quién dirigirse para pedir que se retire.

El mecanismo completo de retirada se construye cuando se publique la galería, no ahora.

#### Fuera del MVP

Campaña de correo, enlaces reenviables, cuentas sin identificar, rol `Contributor`, cola de identificación, galería abierta y navegación por pestañas.

---


### Fase 2 — Endpoint de histórico

**Ficheros:** `Features/Camps/CampsEndpoints.cs`, `CampEditionsService.cs`, `CampsModels.cs`.

- [ ] DTO `CampHistoryResponse`, con `photoCount` y `previewPhotos`.
- [ ] Método de servicio que consulte ediciones `Completed` con `Include(Camp)`, ordene por año y calcule ambos contadores.
- [ ] Agregado de recuerdos por año en **una sola consulta**, sin N+1.
- [ ] Endpoint en grupo con autorización de socio.
- [ ] Tests unitarios: orden por año, cálculo de `editionNumber`, `photoCount` correcto, máximo de 3 vistas previas.
- [ ] Tests de integración: 401 sin token, 200 con socio, 50 filas; un año sin recuerdos devuelve `photoCount: 0` y lista vacía, nunca `null`.

### Fase 3 — Visualización

**Ficheros:** `composables/useCampHistory.ts`, `components/anniversary/AnniversaryJourney.vue`,
`AnniversaryVenueList.vue`, `AnniversaryTimeline.vue`, `AnniversaryGallery.vue`, `AnniversaryPage.vue`.

#### 3.1 Disposición: mapa y lista lado a lado

Se adopta la disposición de la maqueta de revisión ([`docs/CAMPAMENTOS_HISTORICOS-geocode-review.html`](../../../docs/CAMPAMENTOS_HISTORICOS-geocode-review.html)), validada en uso real: **mapa a la izquierda (~60 %) y lista desplazable a la derecha (~40 %)**, ambos sincronizados.

Funciona porque las dos mitades se explican mutuamente: se ve el pin y a la vez se lee el nombre, sin tener que pinchar para saber qué es cada punto. Para una presentación en pantalla grande es muy superior a un mapa solo.

- Clic en una fila → el mapa centra y abre ese campamento.
- Clic en un pin → la fila correspondiente se resalta y se desplaza a la vista.
- En móvil se apila: mapa arriba (~55 vh) y lista debajo.

#### 3.2 Las ediciones, visibles en cada ubicación

El mapa agrupa por **sede** (31 pines) y la cronología por **año** (50 ediciones). Las ediciones por ubicación son el puente entre ambas vistas.

**Un solo endpoint sirve para todo**: las 50 filas de `GET /api/camps/history` se agrupan por `campId` en el cliente. No hace falta endpoint adicional.

Cada sede muestra sus años tanto en la fila de la lista como en el popup del pin:

```
Espinosa de los Monteros · Burgos
1983 · 1993 · 2003 · 2015          ← 4 ediciones
```

- Los años son **pulsables**: seleccionan esa edición y filtran la galería.
- El tamaño del pin escala con el número de ediciones, de modo que las sedes repetidas destacan solas. Eso cuenta visualmente algo que la lista no cuenta: dónde volvió ABUVI una y otra vez.

#### 3.3 Cronología

Refactorizar `AnniversaryTimeline.vue` para recibir los hitos por `props` en vez del array hardcodeado, resaltar el año seleccionado y emitir al hacer clic. Descomentar su sección en `AnniversaryPage.vue`.

#### 3.4 Estado vacío y modo presentación

- Un año sin contenido no muestra un hueco: muestra una llamada a la acción con enlace al formulario.
- **Modo presentación**: recorrido automático de los 50 años (~2 s por año) moviendo mapa, lista y cronología. Sostiene la demo sin manos y es la base grabable para el vídeo.

---

### Fase 3.5 — Navegación de la sección del aniversario ⏸️ APLAZADO

Hoy `AnniversaryPage.vue` es una página larga con anclas (`#inicio`, `#subir-recuerdo`, `#galeria`, `#contacto`). Con el mapa, el listado y las galerías crece demasiado para eso.

Pasar a **navegación por secciones** (pestañas en escritorio, selector en móvil), manteniendo la barra pegajosa ámbar que ya existe:

| Sección | Estado |
| --- | --- |
| **Mapa** — recorrido histórico | Fase 3 |
| **Campamentos** — listado de las 50 ediciones con su sede y años | Fase 3 |
| **Comparte** — subida de fotos, audio e historias | Existe (desactivado por flag) |
| **Galerías** — álbumes por campamento | → `feat-photo-albums-social` |
| **Anecdotario** — relatos de la comunidad | Ver nota |

**Nota sobre el anecdotario:** la entidad `Memory` y sus endpoints (`GET/POST /api/memories`, aprobación por admin) **ya existen y están completos**. Una pestaña que liste los relatos aprobados y permita enviar uno nuevo es, con diferencia, lo más barato de esta lista. Puede entrar en esta feature o en la de fotos; conviene decidirlo al planificar.

Cada sección debe ser enlazable por URL (`/anniversary/mapa`, `/anniversary/comparte`…) para poder abrir la presentación directamente en el mapa y para que el QR del campamento apunte a la de subida.

### Fase 3.6 — Acceso por invitación para contribuir (QR del campamento) ⏸️ APLAZADO

**El problema.** `/anniversary` exige cuenta (`requiresAuth: true`) y `MediaItem.UploadedByUserId` no admite nulo, así que hace falta una identidad. Pero el registro actual —correo, contraseña y **verificación por email**— es una barrera insalvable en el campamento: obliga a salir del navegador, abrir el correo, encontrar el mensaje y volver, con mala cobertura y desde un móvil que muchos no dominan.

**El contexto que lo cambia todo:** ABUVI es una comunidad cerrada y conocida. **Las personas ya están dadas de alta** como `FamilyMember`. Lo que muchas no tienen es una **cuenta de usuario** con correo y contraseña.

Por tanto esto **no es un alta de desconocidos, es una reconciliación**: enlazar a una persona que ya existe con una cuenta que aún no tiene. Y el sistema ya sabe hacerlo: `FamilyMember` tiene `Email` y `UserId` opcional, y `FamilyUnitsService` **ya implementa la auto-vinculación por correo**.

#### Tres vías de acceso, tres niveles de confianza

No hay un único público. Conviene distinguirlas porque **no merecen el mismo trato**:

| Vía | A quién llega | Confianza | Identidad |
| --- | --- | --- | --- |
| **A. Invitación personal por correo** | Personas ya registradas, estén o no en el campamento | Alta: el token va a *su* buzón | Conocida y verificada |
| **B. QR del campamento** | Quien está presente | Media: token compartido, pero hay presencia física | Se reconcilia por correo |
| **C. Enlace reenviado** | Quien recibe el enlace de un tercero | Baja: el token circula sin control | Autodeclarada |

**La vía A es la de mayor alcance y la más barata.** Un envío a las personas ya registradas con enlace personalizado al aniversario llega a quien no va a estar en el campamento —que son la mayoría— y **no necesita reconciliación**: el token identifica a la persona con exactitud, sin riesgo de suplantación y sin teclear nada. Reutiliza `ResendEmailService`, que ya existe.

**La vía C no es un fallo, es la función más valiosa.** Que el enlace se reenvíe a un antiguo abuvino que ya no está en la base de datos es **exactamente lo que queremos**: esas personas guardan las fotos de 1976–1990, que es justo el tramo del que menos conservamos. Son las contribuyentes más valiosas y las que menos probabilidad tienen de estar dadas de alta.

Por tanto **la respuesta a un correo desconocido es aceptarlo, no rechazarlo**. Se pide correo, nombre y apellidos, se marca la cuenta como *sin identificar* y la junta la vincula después si procede. El contenido pasa por aprobación igualmente, así que el riesgo real es trabajo de moderación.

Consecuencia que hay que asumir con los ojos abiertos: **el token del QR es, en la práctica, público**. Se diseña como tal —caducidad, revocación, límite de frecuencia— y no se le confía nada más que aportar contenido sujeto a aprobación.

#### Recorrido

```
escanear QR  →  escribir el correo
```

Y a partir del correo, el sistema decide:

| Situación | Qué ocurre |
| --- | --- |
| Existe un `User` con ese correo | **Inicia sesión con su propia cuenta.** Sin cuenta duplicada, y todo lo que aporte queda bajo su identidad real |
| Existe un `FamilyMember` con ese correo, sin `User` | Se crea la cuenta, se vincula a la persona y **se envía correo de bienvenida** para fijar contraseña |
| La persona está dada de alta pero con otro correo (o sin correo) | Se le añade el correo a su ficha y se sigue como en el caso anterior. Requiere identificar a la persona: ver *decisión abierta 1* |
| Correo desconocido | **Se acepta.** Pide nombre y apellidos, crea cuenta *sin identificar* y avisa a la junta para vincularla. Es la vía de los antiguos abuvinos |

**El correo es obligatorio.** Es la clave de reconciliación con las personas ya dadas de alta, y el único canal para avisarles cuando se publique lo que aportaron — que es el gancho para que vuelvan.

#### Tensión que hay que resolver: el correo de bienvenida no puede bloquear

Si para aportar hay que abrir el correo y fijar una contraseña, **volvemos exactamente a la barrera que queríamos eliminar**. En el campamento, con mala cobertura, esa ida y vuelta hace fracasar el recorrido.

**Propuesta: separar identificación de autenticación.**

- El **QR más el correo** identifican a la persona y abren de inmediato una sesión **limitada a la sección del aniversario**. Puede aportar ya, sin contraseña.
- El **correo de bienvenida sale en paralelo**, para que fije su contraseña **después, en casa y con calma**, y tenga acceso completo.

Así el correo de bienvenida deja de ser un peaje y pasa a ser lo que debe ser: la invitación a quedarse.

#### Decisión de seguridad: rol nuevo, no reutilizar `Member`

Los roles son `Admin`, `Board` y `Member`. **`Member` da acceso a inscripciones, unidades familiares y pagos.** Conceder ese rol mediante un token impreso en un cartel sería una escalada de privilegios: quien fotografíe el QR entra como socio.

La sesión abierta por invitación debe ser de rol **`Contributor`**, con un único alcance: leer el histórico y crear `MediaItem` y `Memory`.

> **Matiz importante para el caso 1.** Cuando alguien que ya tiene cuenta entra por el QR, **no basta con reconocer el correo**: eso permitiría suplantar a cualquiera sabiendo su dirección. Debe autenticarse de verdad — contraseña o enlace de acceso enviado a su correo. Reconocer el correo sirve para *encaminar*, nunca para *autorizar*.

#### Propiedades del token

| Propiedad | Motivo |
| --- | --- |
| Firmado y con caducidad (fechas del campamento) | Que no siga sirviendo en enero |
| Revocable desde admin | Si el cartel acaba en redes sociales |
| Limitación de frecuencia por token e IP | Contener el abuso automatizado |
| Un token por campaña, no por persona | Se imprime una vez y vale para todos |

**El riesgo queda contenido por lo que ya existe:** toda aportación entra sin publicar y pasa por aprobación de admin. Un token filtrado produce, como mucho, trabajo de moderación.

#### Alcance y ficheros

- **Backend:** entidad `AnniversaryInvite` (token, caducidad, activo, usos); `POST /api/auth/invite/redeem` que resuelve el correo contra `User` y `FamilyMember` y devuelve el caso correspondiente; rol `Contributor` en el enum y en las políticas; envío del correo de bienvenida reutilizando el mecanismo de `reset-password`; **auditar que ninguna política existente conceda acceso a `Contributor`**.
- **Frontend:** ruta `/anniversary/unirse?token=…` con un único campo de correo, y las tres ramas del recorrido.
- **Vía A:** generación de tokens personales y envío masivo desde admin, reutilizando `ResendEmailService`; plantilla de correo con enlace directo al aniversario.
- **Panel de admin:** cola de cuentas *sin identificar* con acción de vincular a una persona existente o dejarla como contribuyente externo.
- **Migración:** tabla de invitaciones y nuevo valor de rol.

#### Decisiones abiertas

1. **Persona dada de alta con otro correo, o sin correo.** ¿Cómo se identifica sin exponer datos? Sugerencia: pedir nombre y apellidos y que la junta lo confirme, en vez de dejar que cualquiera reclame a cualquiera.
2. **Enlace de acceso sin contraseña** para quien ya tiene cuenta pero no la recuerda: ¿se implementa, o se le manda a "he olvidado mi contraseña"?
3. **Campaña de correo (vía A):** ¿se envía a todas las personas registradas, o sólo a las de ciertas ediciones? ¿Un único envío o recordatorio posterior?
4. **Cola de identificación:** ¿quién la revisa y con qué frecuencia? Sin dueño, las cuentas *sin identificar* se acumulan y el archivo pierde autoría.

> **Alternativa descartada:** aportaciones anónimas haciendo nullable `UploadedByUserId`. Rompe la trazabilidad, impide avisar a quien aportó y toca una FK usada en varias consultas. La autoría es justo lo que da valor al archivo: saber quién aportó cada recuerdo.

---

### Fase 3.7 — Qué puede ver quien contribuye, y consentimiento de imagen ⏸️ APLAZADO

Dos consecuencias directas de abrir las vías de acceso. La segunda condiciona si esta feature puede publicarse tal cual.

#### Leer también forma parte de contribuir

Quien aporta una foto quiere ver las de los demás: es lo que cierra el círculo y lo que hará que vuelva. Y por esta vía habrá **más visitas que nunca**, muchas de personas semi-identificadas.

Eso obliga a decidir **qué alcanza a ver un `Contributor`**, y aquí hay una tensión real:

> Cuanto más abierta es la vía de aportación, más gente sin identificar accede a fotografías de familias y de menores.

Tres opciones, de más a menos abierta:

| Opción | Qué ve un contribuyente sin identificar | Coste |
| --- | --- |  --- |
| **Simétrica** | Todo lo publicado, igual que un socio | Ninguno. Máximo riesgo de exposición |
| **Asimétrica por identificación** | Aporta libremente, pero sólo ve la galería completa una vez la junta lo vincula a una persona | Requiere la cola de identificación viva |
| **Asimétrica por contenido** | Sólo lo marcado como visible para contribuyentes | Añade un campo y una decisión por cada elemento |

**Recomendación:** la asimétrica por identificación. Mantiene la aportación sin fricción —que es el objetivo— sin abrir el archivo entero a alguien de quien sólo sabemos un correo autodeclarado. Y da a la cola de identificación una razón de existir que no depende de la buena voluntad de nadie: sin revisarla, la gente no ve el archivo y lo reclama.

**Rendimiento.** Se esperan picos de visitas concentrados durante el campamento y tras el envío de la campaña de correo. Servir siempre miniaturas desde blob storage —nunca redimensionar al vuelo—, paginar todas las galerías y cachear el histórico, que es inmutable.

#### Consentimiento de imagen: condición para publicar

**Una fotografía de una persona identificable es un dato personal.** Este archivo son cinco décadas de fotos de campamentos, es decir, **fotos llenas de menores**. Publicarlas en una plataforma con acceso ampliado no es lo mismo que tenerlas en un disco duro.

**Lo que ya juega a favor y hay que preservar:**

- Toda aportación pasa por **aprobación humana** antes de publicarse. Ninguna foto se publica sola.
- El acceso exige autenticación: **no es una galería pública**, y esa diferencia es sustancial.
- Añadir `noindex` y asegurar que las URL de blob no sean adivinables ni compartibles fuera de sesión.

**Lo que hace falta decidir, y no es una decisión técnica:**

1. **Declaración de quien sube.** Casilla obligatoria antes de enviar: *"Tengo derecho a compartir esta imagen y respeto la privacidad de quienes aparecen."* Deja constancia y traslada una responsabilidad que hoy nadie asume explícitamente.
2. **Mecanismo de retirada, imprescindible.** Cualquiera debe poder pedir que se retire una foto en la que aparece, sin dar explicaciones y sin fricción. Es lo mínimo exigible y es barato: un enlace *"Pedir que se retire"* en cada elemento que abra un aviso al admin. **Sin esto no debería publicarse la sección.**
3. **Menores.** Es el punto delicado. Hay que decidir criterio para las fotos recientes con menores identificables: ¿se publican con el mismo criterio que las de 1985, se limitan a personas identificadas, o quedan fuera del alcance de los contribuyentes sin identificar?
4. **Consentimientos que la asociación ya tenga.** Muchas asociaciones recogen cesión de imagen en la inscripción al campamento. Si ABUVI ya lo hace, cubre buena parte de lo reciente y conviene comprobarlo **antes** de diseñar nada nuevo. Lo antiguo, por definición, no está cubierto.
5. **Aviso visible** en la sección explicando qué se hace con las imágenes, quién puede verlas y cómo pedir su retirada.

> **Esto excede lo técnico.** El equipo de desarrollo puede construir la declaración, la retirada y los controles de visibilidad, pero **el criterio sobre menores y sobre el alcance de los consentimientos existentes lo tiene que fijar la junta**, y conviene que lo consulte con quien le asesore legalmente. Conviene resolverlo antes de la presentación, porque es justo entonces cuando se invita a todo el mundo a subir fotos.

---

### Fase 4 — Captura de audio

**Fichero:** `AnniversaryUploadForm.vue`.

- [ ] `comingSoon = false` y retirar el aviso.
- [ ] Selector de edición alimentado por `useCampHistory` en lugar del año suelto.
- [ ] Prefill por query string para el QR: `/anniversary?tipo=audio&anio=2026#subir-recuerdo`.

### Pendiente menor de la Fase 1.6

- [ ] Botón en la interfaz que dispare `refresh-places`. La API y el mapeo existen; sólo falta exponerlo.

---

## 7. Operación: cómo se cargan los datos

Punto importante, porque **desarrollo y producción usan vías distintas**.

### Desarrollo — herramienta `Abuvi.Setup`

```bash
dotnet run --project src/Abuvi.Setup -- geocode --file=docs/CAMPAMENTOS_HISTORICOS.csv
dotnet run --project src/Abuvi.Setup -- import camps         --dir=<seed> --connection=<conn>
dotnet run --project src/Abuvi.Setup -- import camp-editions --dir=<seed> --connection=<conn>
```

El flag es `--connection=<cadena>` **con signo igual**; con espacio se ignora en silencio y cae al usuario `postgres` por defecto.

### Producción — migración EF

`SafetyGuard.EnsureImportAllowedAsync` prohíbe importar en producción si `camps` tiene datos, y la tendrá. Por tanto la vía correcta es una **migración EF idempotente** que se aplica sola en el despliegue:

- `INSERT` de las 31 sedes y las 50 ediciones con **UUID deterministas** (los `id`/`campId` que ya traen los CSV), no con `Guid.NewGuid()`.
- `ON CONFLICT ("Id") DO NOTHING` para que reaplicarla no duplique.
- Método `Down` que borre ambos conjuntos por sus identificadores.
- Patrón `migrationBuilder.Sql`, como `SeedInitialAdminUser_v2`. **No usar `HasData`**: obliga a arrastrar los UUID en todos los snapshots futuros.

**Consecuencia a tener en cuenta:** el importador de desarrollo **descarta la columna `id`** y genera UUID aleatorios, así que hoy los identificadores de local y los de la migración no coincidirían. Para que ambos entornos sean equivalentes, la migración debe ser la fuente de verdad y conviene recargar local desde ella.

---

## 8. Requisitos no funcionales

**Seguridad**
- `/api/camps/history` exige autenticación; devuelve sólo datos públicos de la asociación (año, sede, coordenadas). Sin datos personales.
- La edición de ubicaciones sigue restringida a `Board`/`Admin` por el grupo `/api/camps`.
- La clave de Google Places vive en **User Secrets** (`GooglePlaces:ApiKey`). **Nunca** en `appsettings.Development.json`, que está versionado.

**Rendimiento**
- 50 filas: sin paginar, una consulta con `Include`. Evitar N+1 al calcular los contadores.
- El mapa usa teselas externas (OSM y Esri); degradar con elegancia si no cargan.

**Usabilidad y accesibilidad — requisito de primer orden**

*Móvil primero, y pensado para quien no da nada por supuesto.*

**Tamaños y legibilidad**
- Cuerpo de texto mínimo **16 px**. **Nunca `text-xs` para contenido** (año, autor, descripción, notas): reservado, como mucho, a etiquetas decorativas.
- Objetivos táctiles de **48 × 48 px** como mínimo, con separación suficiente para no encadenar pulsaciones erróneas.
- Contraste **AA (4.5:1)** en todo texto. Revisar en concreto el ámbar sobre blanco y el gris claro sobre blanco.
- La maquetación debe aguantar **zoom del navegador al 200 %** y el tamaño de fuente grande del sistema sin romperse ni recortar contenido.

**Interacción**
- **Nada puede depender de `hover`.** En táctil no existe: cualquier información que hoy se muestre al pasar el ratón debe estar visible o accesible con una pulsación.
- **Botones con texto, no sólo icono.** Un icono de lápiz o de chincheta no es evidente para todo el mundo; el texto sí.
- **El mapa no puede ser la única vía.** Arrastrar, hacer pinza y afinar un pin son gestos difíciles. Toda acción alcanzable desde el mapa debe estarlo también desde la lista, que es la vía principal en móvil.
- **Modo presentación pausable** y sin límites de tiempo impuestos al usuario.

**Aportar contenido: el camino más corto posible**
- Subir una foto, un audio o una anécdota debe estar a **dos pulsaciones como máximo** desde cualquier sección.
- El formulario pide **el mínimo imprescindible**; todo lo demás, opcional y plegado.
- Acceso directo a **cámara y micrófono del móvil**, sin obligar a navegar por el gestor de archivos.
- **Confirmación inequívoca** tras enviar, en lenguaje llano: *"Hemos recibido tu recuerdo. Lo revisaremos y lo publicaremos pronto."*
- Los errores dicen qué hacer, no qué ha fallado por dentro.

**Lenguaje**
- Sin jerga ni anglicismos en la interfaz: *"Comparte tu recuerdo"*, no *"Upload"*. Sin nombres técnicos como *media item* o *edición* donde quepa *campamento*.

**Deuda concreta ya detectada en el código**
- `AnniversaryGallery.vue` y `AnniversaryTimeline.vue` usan `text-xs` para contenido real (año, autor, descripción). Corregir al implementar la Fase 3.
- `CampLocationsPage.vue` tiene 6 botones sólo con icono, y 6 interacciones del aniversario dependen de `hover` o de `v-tooltip`.

**Responsive**
- La cronología ya alterna vertical en móvil y horizontal en escritorio.
- En la vista mapa + lista, **en móvil la lista es la protagonista**: el recorrido por años con el pulgar es más natural que manipular un mapa. El mapa queda arriba como contexto, no como control principal.

**Privacidad e imagen**
- Las fotografías de personas identificables son datos personales; el archivo contiene menores. Ver Fase 3.7.
- Retirada a petición, sin justificación, como requisito mínimo de publicación.
- Sección no indexable; URL de blob no adivinables.

**Calidad de datos**
- Ninguna ubicación entra en base de datos sin pasar las verificaciones automáticas del comando `geocode` (provincia contrastada, caja peninsular, tipo de lugar plausible, ausencia de homónimos en otras provincias).
- El indicador de precisión mantiene visible qué sedes siguen apuntando al municipio.

---

## 9. Criterios de aceptación

### Del MVP (corto plazo)

- [ ] Alguien presente en el campamento puede subir una foto y un audio desde el móvil.
- [ ] No se puede enviar sin marcar la declaración de derechos de imagen.
- [ ] El contenido queda **sin publicar**, en la cola de aprobación.
- [ ] La aportación se ancla a una edición concreta elegida de una lista, no tecleando un año.
- [ ] El deep link del QR abre el formulario ya preparado para audio.
- [ ] Recorrido completo probado en un móvil real.

### Del resto de la feature


- [ ] `GET /api/camps/history` devuelve 50 filas ordenadas por año; 401 sin token.
- [ ] `editionNumber` correcto: Espinosa de los Monteros 2015 es la 4.ª.
- [ ] Cada edición devuelve su `photoCount` y hasta 3 vistas previas en la misma llamada.
- [ ] Un año sin recuerdos devuelve `photoCount: 0` y lista vacía, y la interfaz muestra la llamada a la acción.
- [ ] El endpoint no dispara una consulta por edición (verificado con registro de SQL o contador de consultas).
- [ ] Los históricos **no** aparecen en `GET /api/camps/editions/active` ni en `/current`.
- [ ] En `/anniversary`, seleccionar un año o un pin sincroniza mapa, lista, cronología y galería.
- [ ] La lista lateral muestra cada sede con todos sus años, y los años son pulsables.
- [ ] Las sedes con varias ediciones se distinguen visualmente en el mapa.
- [ ] Un año sin contenido muestra una llamada a la acción con enlace al formulario.
- [ ] El modo presentación recorre los 50 años sin saltos.
- [ ] La navegación por secciones es enlazable por URL.
- [ ] Todo legible y usable en móvil.
- [ ] Ningún texto de contenido por debajo de 16 px; ningún objetivo táctil por debajo de 48 px.
- [ ] Ninguna información o acción accesible **sólo** por `hover`.
- [ ] Aportar contenido está a dos pulsaciones o menos desde cualquier sección.
- [ ] La página se mantiene usable con zoom del navegador al 200 %.
- [ ] Recorrido completo de aportación (foto y audio) probado en un móvil real, no sólo en el emulador.
- [ ] Desde escanear el QR hasta poder subir algo: **sólo el correo**, sin contraseña ni verificación previa.
- [ ] Quien ya tiene cuenta entra **con la suya**, no con una duplicada.
- [ ] Reconocer un correo **no** autoriza por sí solo: hace falta autenticación real para entrar en una cuenta existente.
- [ ] Un `Contributor` **no** puede acceder a inscripciones, unidades familiares ni pagos (verificado con test de integración).
- [ ] El correo de bienvenida **no bloquea** la aportación: se puede subir contenido antes de fijar contraseña.
- [ ] Un token caducado o revocado no permite entrar.
- [ ] Un correo desconocido **puede aportar**: queda como cuenta sin identificar, nunca rechazado.
- [ ] La invitación personal por correo entra sin teclear el correo ni reconciliar nada.
- [ ] Las cuentas sin identificar aparecen en una cola de admin con acción de vinculación.
- [ ] Cada elemento publicado ofrece **pedir su retirada** sin necesidad de justificarse.
- [ ] No se puede enviar contenido sin marcar la declaración de derechos de imagen.
- [ ] La sección no es indexable y las URL de blob no son adivinables.
- [ ] El alcance de lectura de un `Contributor` sin identificar es el decidido, verificado con test de integración.
- [ ] Subir un audio, aprobarlo y verlo en la galería en su año.
- [ ] `npm run build` y la suite de tests en verde.

---

## 10. Fuera de alcance

- Álbumes de fotos, comentarios e identificación de personas → `feat-photo-albums-social`.
- El **anecdotario** puede entrar aquí o en la feature de fotos: la decisión queda abierta (ver Fase 3.5).
- Botón "Yo estuve en este campamento" → misma feature.
- Importar quién asistió a cada campamento histórico.
- Acceso totalmente anónimo (sin ninguna identidad) — ver Fase 3.6, se resuelve con cuenta ligera.
- Importar las 68 sedes candidatas (`docs/CAMPAMENTOS_CANDIDATAS.csv` queda preparado).
- Edición y montaje del vídeo del aniversario: esta feature produce el material, no la pieza.
