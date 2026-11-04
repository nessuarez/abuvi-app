# Identificar quién está detrás de cada unidad familiar

**Feature ID:** `feat-family-unit-representative-subtitle`
**Fecha:** 2026-09-03
**Tipo:** Mejora de UX transversal (frontend) + ampliación de DTOs (backend)

---

## 1. Resumen

Los nombres de las unidades familiares son texto libre (`CreateFamilyUnitValidator`: sólo obligatorio y máximo 200 caracteres) y se han usado para poner cualquier cosa. Cuando la Junta ve *"Los Pitufos"* en una lista de pagos no sabe a quién está reclamando.

**Como** miembro de la Junta gestionando pagos, inscripciones, alojamientos o cuotas,
**quiero** ver el nombre y apellidos del representante junto al nombre de la familia,
**para** saber con quién estoy tratando sin tener que abrir la ficha de la familia.

**Lo importante de esta feature no es el campo, es la coherencia.** El patrón pedido **ya existe en el código** — [FamilyAssignmentCard.vue:47](frontend/src/components/camps/FamilyAssignmentCard.vue#L47) pinta exactamente el subtítulo descrito — pero sólo en algunas pantallas, con formas distintas de resolverlo y **dos definiciones incompatibles de "representante"**. El trabajo real es unificarlo, no inventarlo.

---

## 2. Estado actual verificado

### Ya muestran el representante (patrones distintos entre sí)

| Pantalla | Cómo | Fuente del dato |
| --- | --- | --- |
| [FamilyUnitsAdminPanel.vue:217-219](frontend/src/components/admin/FamilyUnitsAdminPanel.vue#L217-L219) | Columna aparte *"Representante"*, con guion cuando falta | `FamilyUnit.RepresentativeUserId` ✅ |
| [RegistrationsAdminPanel.vue:456-459](frontend/src/components/admin/RegistrationsAdminPanel.vue#L456-L459) | Columna aparte *"Representante"* | `Registration.RegisteredByUserId` ⚠️ |
| [FamilyAssignmentCard.vue:47](frontend/src/components/camps/FamilyAssignmentCard.vue#L47) | **Subtítulo bajo el nombre** — el patrón pedido | `FamilyUnit.RepresentativeUserId` ✅ |
| [AccommodationReportsPanel.vue:107,159](frontend/src/components/camps/AccommodationReportsPanel.vue#L107) · [AccommodationSummaryPanel.vue:120](frontend/src/components/camps/AccommodationSummaryPanel.vue#L120) | Columna aparte | `FamilyUnit.RepresentativeUserId` ✅ |

### No lo muestran — éstas son las que hay que arreglar

| Pantalla | Línea | Qué se ve hoy |
| --- | --- | --- |
| Listado de pagos | [PaymentsAllList.vue:396](frontend/src/components/admin/PaymentsAllList.vue#L396) | Columna *"Familia"* a secas |
| Cola de revisión de pagos | [PaymentsReviewQueue.vue:120](frontend/src/components/admin/PaymentsReviewQueue.vue#L120) | Sólo el nombre de familia |
| Confirmar / rechazar pago | [PaymentsReviewQueue.vue:210,240](frontend/src/components/admin/PaymentsReviewQueue.vue#L210) | Prosa: *"…confirmar el pago de **X**"* |
| Borrar pago | [PaymentsAllList.vue:614](frontend/src/components/admin/PaymentsAllList.vue#L614) | Prosa |
| Pago manual | [ManualPaymentDialog.vue:69](frontend/src/components/admin/ManualPaymentDialog.vue#L69) | Prosa |
| Pago combinado | [ConfirmCombinedPaymentsDialog.vue:119](frontend/src/components/admin/ConfirmCombinedPaymentsDialog.vue#L119) | Prosa |
| Familias asignadas a una plaza | [AccommodationSlotCard.vue:187](frontend/src/components/camps/AccommodationSlotCard.vue#L187) | Chip `Familia (4)` — **el dato ya viaja y no se usa** |
| Familias sin asignar | [AccommodationSummaryPanel.vue:67](frontend/src/components/camps/AccommodationSummaryPanel.vue#L67) | Nombres concatenados con comas |
| Familia seleccionada | [AccommodationAssignmentPanel.vue:296](frontend/src/components/camps/AccommodationAssignmentPanel.vue#L296) | Sólo el nombre |
| Enlazar familias amigas | [RegistrationFriendLinks.vue:32,125](frontend/src/components/admin/registration-accommodation-needs/RegistrationFriendLinks.vue#L32) | Desplegable sólo con nombre de familia |

> **Caso barato y llamativo:** en `AccommodationSlotCard` el `representativeName` **ya está en el objeto** ([accommodation-assignment.ts:88-89](frontend/src/types/accommodation-assignment.ts#L88-L89)) y simplemente no se pinta. Cero backend.

### Backend

- `FamilyUnitResponse` (respuesta de una unidad suelta) **no lleva** el nombre del representante, sólo `RepresentativeUserId` — [FamilyUnitsModels.cs:94-103](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs#L94-L103).
- `FamilyUnitListItemResponse` (listado admin) **sí** lo lleva — [FamilyUnitsModels.cs:149-159](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs#L149-L159).
- `AdminPaymentResponse` lleva `FamilyUnitName` y **no** el representante — [PaymentsModels.cs:59-62](src/Abuvi.API/Features/Payments/PaymentsModels.cs#L59-L62). En pagos el nombre del representante **se calcula pero sólo para el buscador**, no se devuelve ([PaymentsRepository.cs:91,118-121](src/Abuvi.API/Features/Payments/PaymentsRepository.cs#L91)).
- En el frontend, `representativeName` es **opcional** — [family-unit.ts:26](frontend/src/types/family-unit.ts#L26).

---

## 3. El hallazgo que condiciona todo: hay dos "representantes"

Conviven dos definiciones distintas, y **la que usan pagos e inscripciones no sirve para lo que se pide**:

| Fuente | Significado | Dónde se usa |
| --- | --- | --- |
| `FamilyUnit.RepresentativeUserId` | **El representante de la familia.** Propiedad de la familia, estable en el tiempo | Family units, alojamientos ([AccommodationAssignmentsRepository.cs:77](src/Abuvi.API/Features/Camps/AccommodationAssignmentsRepository.cs#L77)) |
| `Registration.RegisteredByUserId` | **Quien hizo *esa* inscripción.** Puede cambiar cada año y no tiene por qué ser el representante | Pagos ([PaymentsRepository.cs:91](src/Abuvi.API/Features/Payments/PaymentsRepository.cs#L91)), inscripciones ([RegistrationsRepository.cs:115-125](src/Abuvi.API/Features/Registrations/RegistrationsRepository.cs#L115-L125)) |

**Consecuencia si no se unifica:** la misma familia mostraría una persona distinta en la pantalla de pagos y en la de alojamientos, y otra distinta según el año de la inscripción. Justo lo contrario de *"saber quién está detrás"*.

**Decisión D1: la fuente es siempre `FamilyUnit.RepresentativeUserId`.** Es lo que pide el enunciado — quién está detrás de *la familia*, no de un trámite concreto.

> **Matiz que no hay que perder:** *"quién hizo la inscripción"* sigue siendo información útil para la Junta, sobre todo si difiere del representante. No se borra: se deja donde ya está (columna *"Representante"* de inscripciones, que pasa a llamarse **"Inscrito por"**) y el subtítulo nuevo pasa a ser el representante de la familia. Son dos datos, no uno mal puesto.

---

## 4. Segundo hallazgo: esto va a destapar representantes equivocados

`RepresentativeUserId` se fija **al crear la familia, con el id de quien la crea** ([FamilyUnitsService.cs:43](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L43)) y **no existe ningún endpoint para cambiarlo** (`FamilyUnitsEndpoints.cs` no expone ninguna ruta de representante). El representante tampoco se puede borrar como miembro ([FamilyUnitsService.cs:315-322](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsService.cs#L315-L322)).

Hoy eso no molesta porque el dato casi no se ve. **En cuanto aparezca en ocho pantallas, van a salir los casos**: la cuenta la creó el hijo adolescente, la pareja se separó, la creó una persona que ya no participa. Y la respuesta de la Junta será *"esto está mal, cámbialo"* — y no se podrá.

**Decisión D2:** añadir `PUT /api/family-units/{id}/representative` (Admin/Board), que valide que el nuevo representante es un `FamilyMember` de esa familia con `UserId` no nulo. Es pequeño y evita que la feature genere una petición de soporte por cada caso raro. **Si se decide dejarlo fuera, hay que asumirlo explícitamente**, no descubrirlo en producción.

---

## 5. Diseño

### 5.1 Un componente compartido, no ocho copias

No existe ningún helper de nombre completo: hoy se concatena `firstName + ' ' + lastName` a mano en cada sitio. Repetir el markup en ocho pantallas garantiza que dentro de tres meses haya ocho variantes.

**Nuevo:** `frontend/src/components/shared/FamilyUnitLabel.vue` — junto a `DateInput.vue` y `PhoneInput.vue`, que es donde vive lo compartido.

```ts
interface Props {
  name: string
  representativeName?: string | null
  variant?: 'block' | 'inline'   // por defecto 'block'
}
```

**Dos variantes, porque el subtítulo no cabe en todas partes** — y esto es lo que hace que el componente valga la pena:

| Variante | Render | Dónde |
| --- | --- | --- |
| `block` | Nombre en una línea, representante debajo en gris | Columnas de tabla, tarjetas, cabeceras |
| `inline` | `Los Pitufos (Ana García Pérez)` | Prosa de los diálogos de confirmación, listados concatenados |

**Casos que el componente debe resolver, no delegar:**

- **Sin representante:** `representativeName` vacío o nulo → **no se renderiza la segunda línea**. Nada de una línea fantasma que descoloca las filas de la tabla. El repositorio hace `LEFT JOIN` y devuelve cadena vacía ([FamilyUnitsRepository.cs:198-200](src/Abuvi.API/Features/FamilyUnits/FamilyUnitsRepository.cs#L198-L200)), así que el caso es real aunque sea raro.
- **Nombre de familia y representante idénticos** (*"Familia García"* / *"García"*): merece la pena no repetir. Comparación laxa y, si coinciden, sólo una línea.
- **Chips estrechos** ([AccommodationSlotCard.vue:187](frontend/src/components/camps/AccommodationSlotCard.vue#L187)): ahí no cabe ninguna de las dos variantes. **Tooltip**, no subtítulo — el componente acepta que el consumidor decida.

### 5.2 Legibilidad del subtítulo

`FamilyAssignmentCard.vue:47` usa `text-xs text-gray-500`. Es el precedente, pero conviene subirlo: el subtítulo **es contenido, no decoración** — es literalmente el dato que se añade para poder leer la pantalla. `text-sm` con contraste AA. Nada de `text-[10px]`.

### 5.3 Backend

| DTO | Cambio |
| --- | --- |
| `FamilyUnitResponse` | Añadir `string RepresentativeName` |
| `AdminPaymentResponse` | Añadir `string RepresentativeName` — el `JOIN` **ya existe** en [PaymentsRepository.cs:91](src/Abuvi.API/Features/Payments/PaymentsRepository.cs#L91); hay que **cambiar su fuente** a `fu.RepresentativeUserId` (D1) y devolverlo en vez de usarlo sólo para buscar |
| `AdminRegistrationProjection` y su respuesta | Los campos `Representative*` actuales pasan a `RegisteredBy*`; se añade `RepresentativeName` desde `fu.RepresentativeUserId` |
| `family-unit.ts:26` | `representativeName` deja de ser opcional en las respuestas que lo llevan |

**Sin migración: no hay campos nuevos en base de datos.** Todo sale de un `JOIN` con `Users` que en la mayoría de los sitios ya está escrito.

> **`representativeName?` opcional es una trampa.** Si un endpoint deja de mandarlo, el subtítulo desaparece en silencio y nadie se entera hasta que alguien lo echa de menos. Donde el contrato lo garantice, el tipo debe ser obligatorio.

---

## 6. Requisitos no funcionales

**Rendimiento**
- Ningún `JOIN` nuevo en los listados paginados: pagos, inscripciones y alojamientos **ya unen con `Users`**. En pagos es un cambio de clave de unión (`r.RegisteredByUserId` → `fu.RepresentativeUserId`), no una tabla más.
- **Prohibido resolver el nombre por fila desde el cliente.** Un `GET /api/family-units/{id}` por cada fila de la tabla de pagos es un N+1 disfrazado de frontend.

**Privacidad**
- Todas las pantallas afectadas son `Admin`/`Board`, que **ya ven nombre y apellidos** de representantes hoy. La feature no amplía el círculo.
- Si alguna superficie llegara a ser visible para socios, mostrar nombre y apellidos de terceros sí sería una ampliación y necesita decisión aparte. Verificar antes de mover cualquiera de estos componentes a una vista de socio.

**Consistencia**
- Ninguna pantalla debe quedar mostrando la familia sin el representante al terminar. Una lista a medias es peor que ninguna: enseña a no fiarse del subtítulo.
- El buscador debe seguir encontrando por nombre de familia **y** por representante en pagos, inscripciones y unidades familiares — ya lo hace; **no romperlo** al cambiar la fuente en pagos.

**Testing**
- `data-testid` en el componente (`family-unit-label`, `family-unit-representative`) siguiendo la convención del proyecto.
- Tests de componente con Vitest: con representante, sin representante (no pinta la segunda línea), nombres iguales, variante `inline`.
- Tests de backend: el listado de pagos devuelve el representante **de la familia**, no el de la inscripción — con un caso donde ambos difieren, que es el único que demuestra que D1 está aplicado.

**Documentación**
- `ai-specs/specs/api-endpoints.md`: campos nuevos en los DTOs y, si entra D2, el endpoint de cambio de representante.
- `ai-specs/changes/INDEX.md`: esta feature.

---

## 7. Criterios de aceptación

- [ ] Existe `FamilyUnitLabel.vue` en `components/shared/` con variantes `block` e `inline`.
- [ ] Las diez superficies de la tabla de §2 muestran el representante; ninguna queda a medias.
- [ ] El representante mostrado sale **siempre** de `FamilyUnit.RepresentativeUserId`, también en pagos e inscripciones.
- [ ] En inscripciones, *"quién inscribió"* sigue visible y etiquetado como **"Inscrito por"**, distinto del representante.
- [ ] Una familia sin representante resoluble **no** deja una línea vacía ni descoloca la tabla.
- [ ] Si el nombre de la familia y el del representante coinciden, no se repite.
- [ ] En los chips de plazas de alojamiento el representante se ve por tooltip, sin romper el ancho.
- [ ] Los buscadores de pagos, inscripciones y unidades familiares siguen encontrando por familia y por representante.
- [ ] Ningún listado añade una consulta por fila (verificado en los tests de recuento de consultas donde existan).
- [ ] El subtítulo es `text-sm` o mayor, con contraste AA.
- [ ] `npm run build`, `npx vitest run` y `dotnet test` en verde.

### Si entra D2

- [ ] `PUT /api/family-units/{id}/representative` permite a Admin/Board cambiar el representante.
- [ ] Sólo acepta un miembro de esa familia con cuenta de usuario; cualquier otro id se rechaza.
- [ ] Un socio no puede cambiar el representante de su propia familia (o sí, si la Junta lo decide — pero explícitamente).

---

## 8. Decisiones abiertas

1. **¿Entra D2 (cambiar representante) en esta entrega o en la siguiente?** Recomendado que sí: la feature lo va a hacer necesario, y sale más barato ahora que como parche.
2. **¿Se aprovecha para desambiguar los nombres de familia?** Se podría sugerir en el formulario un nombre por defecto tipo *"Familia García Pérez"* al crear. **No se recomienda tocar los nombres existentes**: renombrar datos de socios por decisión nuestra es intrusivo, y el subtítulo ya resuelve el problema sin pedir nada a nadie.
3. **Número de familia.** `FamilyUnit.FamilyNumber` existe y es el identificador que la Junta usa en la práctica. ¿Debería entrar también en el subtítulo (*"nº 42 · Ana García"*)? Es un dato más corto y más inequívoco que el nombre. Merece decidirse ahora, porque cambia el diseño del componente.

---

## 9. Fuera de alcance

- Validar o normalizar los nombres de unidad familiar existentes.
- Mostrar el representante en vistas de socio: hoy ninguna de las afectadas lo es.
- Fusionar o deduplicar unidades familiares creadas por error.
- Cambiar cómo se elige el representante al crear la familia (sigue siendo quien la crea).
