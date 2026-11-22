# Desglose del pago en la pantalla de verificación

**Feature ID:** `feat-payment-breakdown-in-review`
**Fecha:** 2026-09-03
**Tipo:** Mejora de UX (frontend) + tres correcciones puntuales de backend

---

## 1. Resumen

**Como** miembro de la Junta verificando justificantes de pago,
**quiero** ver a qué conceptos corresponde cada importe sin salir de la cola de revisión,
**para** poder confirmar o rechazar con criterio en vez de sólo comprobar que el número cuadra.

**El hallazgo que cambia el trabajo: el desglose ya existe y ya está conectado a las dos pantallas de administración.** El componente `PaymentConceptLines.vue` está montado tanto en la cola de revisión como en el listado general, y el backend ya envía las líneas de concepto en `AdminPaymentResponse`.

El problema no es que falte el dato. Es que **está escondido detrás de hasta dos capas de plegado, y cuando no hay dato desaparece sin decir nada**.

Esto rebaja mucho el coste: no hay que construir un desglose, hay que **acercarlo al momento de la decisión**. Sin migración y casi sin backend.

---

## 2. Estado actual verificado

### El desglose existe y está bien hecho

[`PaymentConceptLines.vue`](frontend/src/components/payments/PaymentConceptLines.vue) ya renderiza los tres tipos de línea:

- **Líneas por persona** (P1/P2): nombre, categoría de edad, período de asistencia, importe individual, porcentaje y parte imputada a ese plazo.
- **Líneas de extras** (P3): nombre del extra, cantidad, precio unitario, total, texto introducido por el socio y si es por familia o por persona.
- **Línea manual**: descripción e importe, con distintivo *Manual*.

Y el backend lo envía ya: `AdminPaymentResponse` incluye `ConceptLines`, `ExtraConceptLines` y `ManualConceptLine` ([PaymentsModels.cs:76-78](src/Abuvi.API/Features/Payments/PaymentsModels.cs#L76-L78)), rellenados en `MapToAdminResponse` ([PaymentsService.cs:621-637](src/Abuvi.API/Features/Payments/PaymentsService.cs#L621-L637)), que es el que usan **tanto** `GetPendingReviewAsync` **como** `GetAllPaymentsAsync`.

### Dónde está montado, y por qué no se ve

| Pantalla | Montado en | Cuántos clics para verlo |
| --- | --- | --- |
| Cola de revisión | [PaymentsReviewQueue.vue:169-173](frontend/src/components/admin/PaymentsReviewQueue.vue#L169-L173) | **1** — pero el disparador es invisible en la práctica |
| Listado de pagos | [PaymentsAllList.vue:495-501](frontend/src/components/admin/PaymentsAllList.vue#L495-L501) | **2** — expandir la fila ([`Column expander`](frontend/src/components/admin/PaymentsAllList.vue#L395)) y *luego* abrir el desglose |
| Diálogo de confirmar | [PaymentsReviewQueue.vue:209-214](frontend/src/components/admin/PaymentsReviewQueue.vue#L209-L214) | **No está.** Sólo familia e importe |
| Diálogo de rechazar | [PaymentsReviewQueue.vue:238-242](frontend/src/components/admin/PaymentsReviewQueue.vue#L238-L242) | **No está** |

---

## 3. Diagnóstico: cuatro razones concretas por las que no se ve

### 3.1 Está plegado por defecto, tras un disparador diminuto

`expanded = ref(false)` ([PaymentConceptLines.vue:11](frontend/src/components/payments/PaymentConceptLines.vue#L11)). Se abre con un enlace `text-xs` azul, con un chevron a `text-[0.6rem]` (≈9,6 px), rotulado *"Detalle del pago"*, situado **entre la vista previa del justificante y los botones de acción**.

En una cola donde el gesto natural es *mirar el justificante → pulsar Confirmar*, ese enlace cae justo en la trayectoria del ratón y aun así pasa desapercibido: parece un pie de tarjeta, no el contenido principal.

### 3.2 En el listado general hay doble plegado

Fila colapsada + desglose colapsado. Dos interacciones para responder *"¿de qué es este pago?"*, que es la pregunta más frecuente de esa pantalla.

### 3.3 En el momento de decidir, el desglose no está

Los diálogos de confirmar y rechazar muestran **sólo el nombre de la familia y el importe**. Es exactamente el instante en el que hace falta el detalle, y es el único sitio donde no aparece. Quien llega al diálogo ya ha perdido de vista la tarjeta.

### 3.4 Cuando no hay líneas, no se muestra nada — ni siquiera que no hay nada

`hasContent` envuelve todo el componente ([PaymentConceptLines.vue:14-18](frontend/src/components/payments/PaymentConceptLines.vue#L14-L18)): sin líneas, el componente **no pinta absolutamente nada**. Para quien mira la pantalla, un pago sin desglose y un pago cuyo desglose falló son indistinguibles — y ambos parecen "aquí no hay botón de detalle".

Y hay al menos dos caminos que producen ese vacío:

- `SerializeBaseConceptLines` devuelve **una lista vacía** cuando `baseTotalAmount == 0` ([PaymentsService.cs:655-657](src/Abuvi.API/Features/Payments/PaymentsService.cs#L655-L657)).
- `DeserializeConceptLines` **se traga el `JsonException` y devuelve nulos, sin log** ([PaymentsService.cs:646-651](src/Abuvi.API/Features/Payments/PaymentsService.cs#L646-L651)). Un JSON corrupto es silencioso e indistinguible de un pago legítimamente sin conceptos. Compárese con `GetPaymentSettings`, cinco líneas más arriba, que **sí** escribe `LogWarning` en el mismo caso — la incoherencia está dentro del mismo fichero.

---

## 4. Dos huecos reales que sí son de backend

### 4.1 *"Confirmado por"* es una columna permanentemente vacía

`MapToAdminResponse` pasa `null` literal con el comentario *"would need user lookup, kept null for now"* ([PaymentsService.cs:631](src/Abuvi.API/Features/Payments/PaymentsService.cs#L631)), pero el listado **ya pinta esa columna** ([PaymentsAllList.vue:460-462](frontend/src/components/admin/PaymentsAllList.vue#L460-L462)). Hoy no muestra nada nunca.

`Payment.ConfirmedByUserId` sí se guarda. Es un `JOIN` con `Users`, igual que el que ya hace el repositorio de pagos. Y es información de verificación pura: *quién dio por bueno este justificante* es justo lo que se quiere saber cuando algo se revisa después.

### 4.2 No hay forma de llegar a la inscripción desde el pago

Ni la cola de revisión ni el listado enlazan a la inscripción, **aunque `registrationId` viaja en cada pago**. Quien necesita el contexto completo tiene que salir, ir a Inscripciones, buscar la familia y abrirla.

El enunciado lo dice literalmente — *"cuando en realidad están dentro de la inscripción/pagos/P1 pues ahí está el desglose"*. Un enlace *"Ver inscripción"* es una línea de código y cierra el hueco para todo lo que el desglose no cubra.

---

## 5. Decisiones

| # | Decisión | Recomendación |
| --- | --- | --- |
| **D1** | ¿El desglose se abre por defecto? | **Sí en la cola de revisión, no en el listado general.** La cola tiene un propósito único —verificar— y allí el desglose *es* el contenido. El listado es una tabla larga de consulta: abrirlo todo la haría ilegible. El componente gana una prop `defaultExpanded` en vez de bifurcarse en dos componentes. |
| **D2** | ¿Desglose también en los diálogos de confirmar/rechazar? | **Sí, en versión compacta y de sólo lectura.** Es el momento de la decisión. Basta con las líneas y el total, sin plegado y sin acciones. |
| **D3** | ¿Qué se muestra cuando no hay líneas? | **Un texto explícito**, *"Este pago no tiene desglose de conceptos"*, en gris. Nunca la nada actual: el vacío hace pensar que el detalle está en otro sitio. |
| **D4** | ¿Se rellena `ConfirmedByUserName`? | **Sí.** La columna ya existe vacía en producción, y el dato ya está guardado. Dejarla así es peor que no tenerla. |
| **D5** | ¿Se trae a la cola el contexto completo de la inscripción (total, resto de plazos, pagado)? | **No. Un enlace, no una copia.** Duplicar el estado de la inscripción en la cola de revisión multiplica las consultas y crea dos sitios donde mirar lo mismo, que es el problema que esta feature intenta resolver. El desglose responde *"¿de qué es este pago?"*; el enlace responde todo lo demás. Si con el enlace se sigue echando algo en falta, se añade **ese** dato concreto, con la petición delante. |

---

## 6. Alcance del trabajo

### Frontend

| Fichero | Cambio |
| --- | --- |
| `components/payments/PaymentConceptLines.vue` | Prop `defaultExpanded` (por defecto `false`); prop `variant: 'collapsible' \| 'static'` para los diálogos; estado vacío explícito (D3) |
| `components/admin/PaymentsReviewQueue.vue` | `default-expanded`; mover el desglose **encima** de los botones de acción y por debajo del importe, no como pie de tarjeta; desglose estático en los diálogos de confirmar y rechazar; enlace *"Ver inscripción"* |
| `components/admin/PaymentsAllList.vue` | Desglose desplegado dentro de la fila expandida (quitar el segundo plegado); enlace *"Ver inscripción"* |
| `components/admin/ConfirmCombinedPaymentsDialog.vue` | Hoy sólo resuelve etiqueta para pagos manuales ([línea 38](frontend/src/components/admin/ConfirmCombinedPaymentsDialog.vue#L38)); mostrar el desglose de cada pago del grupo |

### Backend

| Fichero | Cambio |
| --- | --- |
| `PaymentsService.cs:646-651` | `LogWarning` al fallar la deserialización, con el `PaymentId`. Sigue devolviendo nulos: no debe romper la pantalla, pero **tiene que dejar rastro** |
| `PaymentsService.cs:631` + `PaymentsRepository` | Resolver `ConfirmedByUserName` desde `ConfirmedByUserId` con `JOIN`, sin consulta por fila |

**Sin migración.** No hay campos nuevos en base de datos.

---

## 7. Requisitos no funcionales

**Rendimiento**
- El desglose **ya viaja en la respuesta**: abrirlo por defecto no añade ni una petición.
- `ConfirmedByUserName` se resuelve con un `JOIN` en la consulta paginada existente. **Nunca una consulta por fila** — el listado de pagos ya está paginado y un N+1 ahí se nota.

**Legibilidad**
- Todo el desglose está hoy a `text-xs`, y hay un chevron a `text-[0.6rem]`. Al pasar a ser contenido principal de la cola de revisión debe subir a `text-sm` con contraste AA. **Es el dato sobre el que se toma una decisión de dinero**: no puede ser el texto más pequeño de la pantalla.
- El desglose no puede empujar los botones de acción fuera de la vista en móvil. Si una inscripción de doce personas lo hace, el desglose lleva su propio scroll.

**Trazabilidad**
- Un desglose que no se puede leer por JSON corrupto deja `LogWarning` con el `PaymentId`, para poder encontrar los pagos afectados sin ir uno a uno.

**Testing**
- Tests de componente (Vitest) de `PaymentConceptLines`: expandido por defecto, plegado por defecto, variante estática, y **estado vacío mostrando el texto explícito**.
- Test de la cola de revisión: el desglose se ve sin interacción previa.
- Test de backend: `ConfirmedByUserName` viene relleno en un pago confirmado y nulo en uno pendiente; y el recuento de consultas del listado no crece con el número de filas.
- `data-testid` siguiendo la convención del proyecto (`payment-concept-lines`, `payment-concept-empty`).

**Documentación**
- `ai-specs/specs/api-endpoints.md`: `ConfirmedByUserName` pasa a estar realmente poblado.
- `ai-specs/changes/INDEX.md`: esta feature.

---

## 8. Criterios de aceptación

- [ ] En la cola de revisión, el desglose de conceptos se ve **sin ninguna interacción previa**.
- [ ] El desglose aparece antes de los botones Confirmar/Rechazar, no como pie de tarjeta.
- [ ] Los diálogos de confirmar y de rechazar muestran el desglose del pago afectado.
- [ ] El diálogo de pago combinado muestra el desglose de cada pago del grupo.
- [ ] En el listado general, expandir la fila muestra el desglose directamente, sin un segundo clic.
- [ ] Un pago sin líneas de concepto muestra un texto explícito, no un hueco.
- [ ] Un `ConceptLinesSerialized` corrupto deja un `LogWarning` con el `PaymentId` y no rompe la pantalla.
- [ ] La columna *"Confirmado por"* muestra el nombre en los pagos confirmados.
- [ ] Existe un enlace a la inscripción desde la cola de revisión y desde el listado.
- [ ] El listado de pagos no añade consultas por fila (verificado por recuento).
- [ ] El desglose es legible en móvil y no desplaza los botones de acción fuera de la vista.
- [ ] `npm run build`, `npx vitest run` y `dotnet test` en verde.

---

## 9. Fuera de alcance

- Cambiar cómo se calculan o se serializan las líneas de concepto: el cálculo es correcto, sólo no se ve.
- Regenerar el desglose de pagos antiguos que se guardaron sin líneas. Si tras el estado vacío explícito resulta que son muchos, eso es su propia tarea, con el número delante.
- Traer el estado completo de la inscripción a la cola de revisión (D5).
- Editar el desglose desde la pantalla de verificación. Ajustar importes ya vive en `feat-payment-adjustments`.
