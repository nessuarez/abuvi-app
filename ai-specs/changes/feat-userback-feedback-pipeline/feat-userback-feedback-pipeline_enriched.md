# Enriched Spec: Userback Feedback Pipeline (Userback → Trello → Claude Code)

## Overview

Automatizar el pipeline de feedback de usuarios finales: desde que un usuario reporta un bug o sugerencia en Userback, pasando por el triage del desarrollador, hasta la generación automática de specs en `ai-specs/changes/` y el inicio del ciclo de desarrollo en Claude Code.

**Esto NO es una feature de la aplicación Abuvi.** Es una integración de tooling que conecta Userback.io, Trello y Claude Code. No hay cambios en backend ni frontend de Abuvi.

**Relación con `feat-trello-integration`**: Este spec cubre el flujo **Userback → Trello → Claude Code** (feedback entrante). El spec `feat-trello-integration` cubre el flujo opuesto: **Claude Code → Trello** (espejo del progreso de desarrollo). Ambos conviven y comparten el mismo board de Trello.

---

## User Story

**Como** único desarrollador de Abuvi,
**Quiero** que el feedback de usuarios que escalo en Userback genere automáticamente specs y tarjetas de Trello vinculadas,
**Para que** el feedback se convierta en tareas accionables sin intervención manual y sin perder el contexto original del reporte.

---

## Flujo de Trabajo

```
Usuario final                Userback              Trello                  Claude Code / Specs
──────────────              ─────────             ──────                  ───────────────────

Reporta bug/sugerencia ──→ Feedback creado
                           en Userback
                                │
                          Dev revisa y escala
                          (estado → "To Do")
                                │
                        ┌───────┴────────┐
                        │                │
                   Normal            Urgente
                   (auto-send)      (etiqueta BUGS
                        │            o lista TODO)
                        ▼                ▼
                   Card en           Card en TODO
                   BACKLOG           (o con label)
                        │                │
                        ▼                ▼
                   Butler:           Butler:
                   HTTP POST         HTTP POST
                   → GH Action       → GH Action
                        │                │
                        ▼                ▼
                   Crea spec         Crea spec +
                   _enriched.md      ejecuta /enrich-us
                   (plantilla        (enriquecimiento
                   básica)           completo)
                        │                │
                        └───────┬────────┘
                                │
                    Flujo normal de desarrollo
                    (feat-trello-integration se
                    encarga de CC → Trello sync)
                                │
                        Card llega a DONE
                                │
                        ┌───────┴────────┐
                        │   Userback:    │
                        │   Marcar como  │
                        │   resuelto     │
                        │   (manual*)    │
                        └────────────────┘

* Cerrar el ciclo en Userback requiere plan de pago (API no disponible en free tier)
```

---

## Análisis de Viabilidad por Herramienta

### Userback (free tier)

| Capacidad | Disponible | Notas |
|---|---|---|
| Widget de feedback en la app | Si | Ya integrado (JS widget) |
| Screenshots anotados | Si | Incluido en free |
| Integración nativa con Trello | Si | Envía cards automáticamente |
| REST API | **No** | Requiere plan Business Plus+ |
| Webhooks | **No** | Requiere plan Scale+ |
| Retención de feedback | 7 dias | Feedback se bloquea tras 7 dias |

**Implicaciones**:
- La integración Userback → Trello funciona (nativa, sin API)
- No es posible cerrar el ciclo Userback programáticamente (marcar como resuelto) en free tier
- Los screenshots anotados se adjuntan como attachments en la tarjeta de Trello
- Hay que triagear feedback dentro de los 7 dias de retención

### Trello (free tier)

| Capacidad | Disponible | Notas |
|---|---|---|
| Butler automations | Si | Incluido en free (limitado a ~100 ejecuciones/mes) |
| Butler HTTP requests | Si | Puede hacer POST a URLs externas |
| API webhooks | Si | Requiere endpoint propio |
| Labels/etiquetas | Si | Para clasificar urgencia |

**Ruta recomendada**: Butler automation (no requiere servidor propio).

### Claude Code

| Capacidad | Disponible | Notas |
|---|---|---|
| Trello MCP server | Si | Ya configurado en `.mcp.json` |
| Comandos `/enrich-us`, etc. | Si | Ya existentes |
| GitHub Actions | Si | Para automatización |

---

## Technical Design

### 1. Configuración de Userback → Trello (integración nativa)

En el dashboard de Userback:

1. Ir a **Integrations → Trello**
2. Conectar cuenta de Trello
3. Seleccionar el board de desarrollo
4. Configurar **dos reglas de envio**:

| Regla | Filtro en Userback | Lista destino en Trello | Modo |
|---|---|---|---|
| Feedback normal | Estado = "To Do" | BACKLOG | Manual (dev escala uno a uno) |
| Bug urgente | Estado = "To Do" + etiqueta "urgent" | TODO | Manual |

> **Nota**: La integración nativa de Userback permite elegir la lista destino. Para bugs urgentes, el desarrollador selecciona manualmente la lista TODO al enviar, o bien se configura un filtro por tipo de feedback (bug vs. general).

### 2. Datos que Userback envía a Trello

Cuando se crea la tarjeta en Trello, incluye:

| Dato | Formato | Ubicación en Trello |
|---|---|---|
| Titulo del feedback | Texto | Card name |
| Descripción del usuario | Texto | Card description |
| Screenshot anotado | Imagen adjunta | Card attachment |
| URL de la página | Texto en descripción | Card description |
| Navegador + OS + viewport | Texto en descripción | Card description |
| ID del ticket Userback | En el link | Card description (link a Userback) |

### 3. Butler Automations en Trello

#### 3.1 Regla: Card nueva en BACKLOG → Crear spec básico

```
Trigger: when a card is added to list "BACKLOG"
Action:  issue an HTTP POST request to
         https://api.github.com/repos/{owner}/{repo}/dispatches
         with headers {"Authorization": "Bearer {github_pat}", "Accept": "application/vnd.github.v3+json"}
         with payload {"event_type": "trello-feedback-backlog", "client_payload": {"card_name": "{cardname}", "card_id": "{cardid}", "card_url": "{cardlink}", "card_desc": "{carddesc}"}}
```

#### 3.2 Regla: Card nueva en TODO con label "BUGS" → Crear spec + enriquecer

```
Trigger: when a card with label "BUGS" is added to list "TODO"
Action:  issue an HTTP POST request to
         https://api.github.com/repos/{owner}/{repo}/dispatches
         with headers {"Authorization": "Bearer {github_pat}", "Accept": "application/vnd.github.v3+json"}
         with payload {"event_type": "trello-feedback-urgent", "client_payload": {"card_name": "{cardname}", "card_id": "{cardid}", "card_url": "{cardlink}", "card_desc": "{carddesc}"}}
```

> **Alternativa al label**: En vez de usar el label "BUGS", se puede usar directamente la lista destino. Si el desarrollador envia el feedback de Userback a la lista TODO (en vez de BACKLOG), Butler dispara la regla urgente. Esto es más simple y evita depender de labels.

### 4. GitHub Actions

#### 4.1 Workflow: Crear spec desde feedback (BACKLOG)

Crear `.github/workflows/trello-feedback-to-spec.yml`:

```yaml
name: Trello — Create spec from user feedback

on:
  repository_dispatch:
    types: [trello-feedback-backlog, trello-feedback-urgent]

jobs:
  create-spec:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Normalize task ID from card name
        id: task
        run: |
          # Convertir "Bug: login no funciona en Safari" → "bug-login-no-funciona-en-safari"
          CARD_NAME="${{ github.event.client_payload.card_name }}"
          TASK_ID=$(echo "$CARD_NAME" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]/-/g' | sed 's/--*/-/g' | sed 's/^-//' | sed 's/-$//' | head -c 60)
          echo "task_id=$TASK_ID" >> "$GITHUB_OUTPUT"

      - name: Create spec folder and basic enriched file
        run: |
          TASK_ID="${{ steps.task.outputs.task_id }}"
          CARD_ID="${{ github.event.client_payload.card_id }}"
          CARD_URL="${{ github.event.client_payload.card_url }}"
          CARD_DESC="${{ github.event.client_payload.card_desc }}"
          EVENT_TYPE="${{ github.event.action }}"

          mkdir -p "ai-specs/changes/$TASK_ID"

          cat > "ai-specs/changes/$TASK_ID/${TASK_ID}_enriched.md" << EOF
          <!-- trello-card-id: $CARD_ID -->
          # ${{ github.event.client_payload.card_name }}

          ## Origen

          - **Fuente**: Feedback de usuario (Userback → Trello)
          - **Trello card**: $CARD_URL
          - **Prioridad**: $([ "$EVENT_TYPE" = "trello-feedback-urgent" ] && echo "URGENTE" || echo "Normal")

          ## Descripcion del usuario

          $CARD_DESC

          ## Estado

          $([ "$EVENT_TYPE" = "trello-feedback-urgent" ] && echo "Pendiente de enriquecimiento automatico (/enrich-us)" || echo "Pendiente de revision y planificacion por el desarrollador")
          EOF

      - name: Commit and push spec
        run: |
          TASK_ID="${{ steps.task.outputs.task_id }}"
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add "ai-specs/changes/$TASK_ID/"
          git commit -m "feat(specs): auto-create spec from user feedback — $TASK_ID"
          git push

      - name: Notify if urgent (needs /enrich-us)
        if: github.event.action == 'trello-feedback-urgent'
        run: |
          TASK_ID="${{ steps.task.outputs.task_id }}"
          echo "::warning::URGENT feedback received: $TASK_ID — Run '/enrich-us $TASK_ID' in Claude Code to enrich the spec"
          # Opcionalmente: crear un issue de GitHub para visibilidad
          gh issue create \
            --title "URGENTE: Enriquecer spec $TASK_ID (feedback de usuario)" \
            --body "Se ha creado un spec basico en \`ai-specs/changes/$TASK_ID/\` desde feedback urgente de usuario.\n\nEjecutar en Claude Code:\n\`\`\`\n/enrich-us $TASK_ID\n\`\`\`\n\nTrello: ${{ github.event.client_payload.card_url }}" \
            --label "urgent,feedback"
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### 5. Cierre del ciclo: Trello DONE → Userback resuelto

| Opcion | Viabilidad | Coste |
|---|---|---|
| API de Userback (marcar resuelto) | No disponible en free tier | Requiere plan Business Plus ($79/mes) |
| Integración nativa bidireccional | No existe (Userback → Trello es unidireccional) | N/A |
| Manual | Si | $0 — el dev marca como resuelto en Userback manualmente |
| Upgrade futuro | Si | Cuando se justifique el coste, usar API para cerrar ciclo |

**Decisión**: En free tier, el cierre en Userback es **manual**. El desarrollador marca el feedback como resuelto en Userback cuando la tarjeta llega a DONE en Trello. Se puede añadir un recordatorio en el flujo de `feat-trello-integration` (cuando card → DONE, comentar "Recuerda cerrar en Userback").

---

## Convenciones

### Vinculación Card ↔ Spec ↔ Userback

El archivo enriched incluye en la primera linea el `trello-card-id` (compatible con `feat-trello-integration`):

```markdown
<!-- trello-card-id: abc123DEF -->
# Titulo del feedback
```

La descripción de la tarjeta en Trello contiene el link a Userback (puesto automáticamente por la integración nativa).

### Nomenclatura de task_id

Para specs generados desde feedback, el `task_id` se deriva del nombre de la tarjeta normalizado:
- Minusculas, sin caracteres especiales, guiones como separador
- Maximo 60 caracteres
- Ejemplos: `bug-login-no-funciona-en-safari`, `mejora-filtro-inscripciones`

### Estructura de carpetas

```
ai-specs/changes/
├── bug-login-safari/                     ← generado desde feedback
│   └── bug-login-safari_enriched.md      ← con trello-card-id + datos de Userback
├── feat-trello-integration/              ← generado manualmente
│   └── feat-trello-integration_enriched.md
└── merged/
```

---

## Configuración Necesaria

### 1. Userback Dashboard

- [ ] Configurar integración Trello: seleccionar board y lista BACKLOG como destino por defecto
- [ ] Verificar que screenshots anotados se adjuntan correctamente en Trello

### 2. Trello Board

- [ ] Crear label "BUGS" (color rojo) para feedback urgente
- [ ] Crear Butler rule: card en BACKLOG → HTTP POST a GitHub `repository_dispatch`
- [ ] Crear Butler rule: card con label BUGS en TODO → HTTP POST a GitHub `repository_dispatch`
- [ ] Almacenar GitHub PAT como variable en Butler (o en la URL directamente)

### 3. GitHub

- [ ] Crear workflow `.github/workflows/trello-feedback-to-spec.yml`
- [ ] Verificar que `GITHUB_TOKEN` tiene permisos para crear issues (para feedback urgente)
- [ ] Crear labels `urgent` y `feedback` en el repositorio

### 4. Variables y Secrets

| Variable | Donde | Proposito |
|---|---|---|
| GitHub PAT | Trello Butler HTTP headers | Autenticar `repository_dispatch` |
| `TRELLO_API_KEY` | GitHub Secrets (ya existe) | Para futuras integraciones |
| `TRELLO_TOKEN` | GitHub Secrets (ya existe) | Para futuras integraciones |
| `TRELLO_BOARD_ID` | GitHub Secrets (ya existe) | Para futuras integraciones |

---

## Implementation Steps

### Phase 1: Userback → Trello (~10 min, manual)

| # | Accion |
|---|---|
| 1 | Configurar integración Userback → Trello en dashboard de Userback |
| 2 | Probar envio manual: escalar un feedback → verificar card creada en BACKLOG |
| 3 | Probar envio a TODO (para flujo urgente) |
| 4 | Crear label "BUGS" en Trello |

### Phase 2: Butler Automations (~15 min, manual)

| # | Accion |
|---|---|
| 5 | Crear Butler rule para BACKLOG → `repository_dispatch` |
| 6 | Crear Butler rule para TODO + BUGS → `repository_dispatch` |
| 7 | Probar ambas reglas manualmente (crear card en cada lista) |

### Phase 3: GitHub Action (~20 min)

| # | Accion |
|---|---|
| 8 | Crear `.github/workflows/trello-feedback-to-spec.yml` |
| 9 | Probar `repository_dispatch` manualmente con `gh api` |
| 10 | Verificar que el spec se crea correctamente en `ai-specs/changes/` |
| 11 | Verificar que el issue de GitHub se crea para feedback urgente |

### Phase 4: Validación E2E (~15 min)

| # | Test |
|---|---|
| 12 | Feedback en Userback → escalar → card en BACKLOG → spec basico creado |
| 13 | Feedback urgente → card en TODO con label BUGS → spec + issue creado |
| 14 | Ejecutar `/enrich-us {task_id}` sobre spec generado → verificar enriquecimiento |
| 15 | Continuar con `/plan-backend-ticket` → verificar que `feat-trello-integration` sincroniza |
| 16 | Completar flujo hasta DONE → verificar recordatorio de cerrar en Userback |

---

## Acceptance Criteria

- [ ] Feedback escalado en Userback crea una tarjeta en BACKLOG de Trello con screenshots y datos del reporte
- [ ] Card en BACKLOG dispara GitHub Action que crea spec basico en `ai-specs/changes/`
- [ ] El spec generado incluye `trello-card-id` compatible con `feat-trello-integration`
- [ ] Feedback urgente (label BUGS o lista TODO) ademas de crear spec, genera un issue de GitHub como recordatorio para ejecutar `/enrich-us`
- [ ] El spec generado contiene la descripción del usuario, URL, y metadatos del reporte
- [ ] El flujo se integra con los comandos existentes (`/enrich-us`, `/plan-*-ticket`, `/develop-*`)
- [ ] Butler rules no exceden el limite free tier (~100 ejecuciones/mes)
- [ ] El desarrollador puede cerrar manualmente el feedback en Userback al completar la tarea

## Non-Functional Requirements

- **Seguridad**: El GitHub PAT usado en Butler debe tener scope minimo (`repo` para `repository_dispatch`). Rotarlo periodicamente.
- **Resiliencia**: Si Butler o la GitHub Action fallan, las tarjetas de Trello siguen existiendo. El desarrollador puede crear specs manualmente como fallback.
- **Limites**: Butler free tier tiene ~100 ejecuciones/mes. Con un volumen bajo de feedback (~10-20 tickets/mes) esto es suficiente.
- **Retención**: Triagear feedback en Userback dentro de 7 dias (limite free tier). Una vez escalado a Trello, los datos persisten en la tarjeta.

---

## Dependencias

| Spec | Relacion |
|---|---|
| `feat-trello-integration` | Comparte board de Trello. Este spec genera cards; aquel las mueve segun progreso de desarrollo |
| `feat-bug-tracking-tools` (merged) | Userback ya está integrado como widget. Este spec extiende su uso como fuente de feedback → specs |

## Riesgos y Mitigaciones

| Riesgo | Mitigacion |
|---|---|
| Butler free tier agota ejecuciones | Monitorear uso. Si se excede, migrar a Trello API webhooks + endpoint propio |
| Nombres de cards generan task_ids duplicados o invalidos | Regex de normalización + verificar si carpeta ya existe antes de crear |
| GitHub PAT en Butler expira o se revoca | Documentar proceso de rotación. El fallo es silencioso (no se crea spec) pero la tarjeta persiste |
| Userback free tier se queda corto (7 dias, sin API) | Upgrade a plan de pago cuando el volumen lo justifique. Mientras, escalar rapido a Trello |
| Card de Trello no tiene descripción suficiente | El spec basico es una plantilla minima. El `/enrich-us` posterior la completa |

## Out of Scope

- Cambios en codigo de Abuvi (backend/frontend)
- Cerrar feedback en Userback programaticamente (requiere plan de pago)
- Procesamiento de session replays o console logs de Userback (solo datos textuales y screenshots)
- Sincronización bidireccional Trello → Userback
- Multi-board en Trello
