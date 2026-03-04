# Enriched Spec: Trello Sync via Claude Code MCP

## Overview

Sincronizar el board de Trello automáticamente desde Claude Code usando un servidor MCP de Trello. **La fuente de verdad es `ai-specs/changes/`** — las tarjetas se crean desde Claude Code, y Trello es el espejo visual del estado de cada tarea.

**Esto NO es una feature de la aplicación Abuvi.** No hay cambios en backend ni frontend. Es una integración de tooling que vive en la configuración de Claude Code y los archivos de comandos.

---

## User Story

**Como** único desarrollador trabajando en múltiples branches y PRs simultáneamente,
**Quiero** que Claude Code sincronice automáticamente mi progreso con las tarjetas de Trello cuando ejecuto mis comandos de desarrollo,
**Para que** mi board de Trello refleje el estado real de cada tarea sin intervención manual.

---

## Flujo de Trabajo

```
Claude Code (fuente de verdad)              Trello (espejo visual)
─────────────────────────────               ──────────────────────

/enrich-us <descripción>                    → Crea tarjeta en columna BACKLOG
  └─ genera ai-specs/changes/{id}/            con link al spec local
     └─ {id}_enriched.md

/plan-backend-ticket {id}                   → Mueve tarjeta a TODO
  └─ genera {id}_backend.md                   + añade checklist de pasos
/plan-frontend-ticket {id}                  → Mueve tarjeta a TODO
  └─ genera {id}_frontend.md                  + añade checklist de pasos

/develop-backend {id}                       → Mueve tarjeta a DOING
  └─ crea branch, implementa, PR              + comenta branch y PR URL
/develop-frontend {id}                      → Mueve tarjeta a DOING
  └─ crea branch, implementa, PR              + comenta branch y PR URL

PR merged en main                           → Mueve tarjeta a DONE
  └─ mueve carpeta a changes/merged/          (GitHub Action o hook)
```

---

## Convenciones

### Identificadores (task_id)

El `task_id` es el nombre de la carpeta en `ai-specs/changes/`. Ejemplos:

- `feat-trello-integration`
- `feat-camp-edition-extras`
- `fix-family-member-birthdate-timezone`

Este mismo `task_id` se usa como nombre de la tarjeta en Trello.

### Vinculación Card ↔ Spec

Al crear una tarjeta, se almacena el **Trello Card ID** como metadato en la primera línea del archivo enriched:

```markdown
<!-- trello-card-id: abc123DEF -->
# Feature Name
...
```

Esto permite a los comandos posteriores localizar la tarjeta sin búsquedas ambiguas.

### Estructura de carpetas

```
ai-specs/changes/
├── feat-something/                    ← tarea activa
│   ├── feat-something_enriched.md     ← US enriquecida (con trello-card-id)
│   ├── feat-something_backend.md      ← plan backend
│   └── feat-something_frontend.md     ← plan frontend
├── merged/                            ← tareas completadas
│   └── feat-old-task/
└── ...
```

---

## Technical Design

### 1. MCP Server

**[`@delorenj/mcp-server-trello`](https://github.com/delorenj/mcp-server-trello)**

Tools que se usarán:

| Tool | Uso en el flujo |
|---|---|
| `get_lists` | Obtener IDs de las columnas del board |
| `add_card_to_list` | Crear tarjeta nueva (`/enrich-us`) |
| `get_card` | Leer tarjeta existente |
| `get_cards_by_list_id` | Buscar tarjetas en una lista |
| `add_checklist_item` | Añadir pasos del plan como checklist |
| Comentarios (add/update) | Links a branches, PRs, specs |

> **Nota**: Hay que validar si el MCP soporta mover tarjetas entre listas (update card idList). Si no, usar Trello REST API directamente vía curl como fallback.

### 2. Credenciales

| Credencial | Cómo obtener | Storage |
|---|---|---|
| `TRELLO_API_KEY` | <https://trello.com/app-key> | Variable de entorno |
| `TRELLO_TOKEN` | Generar desde la página de API key (non-expiring) | Variable de entorno |
| `TRELLO_BOARD_ID` | URL del board: `https://trello.com/b/{BOARD_ID}/...` | Variable de entorno |

### 3. Configuración

#### 3.1 `.mcp.json` — Añadir servidor Trello

```json
{
  "mcpServers": {
    "github": { "..." },
    "primevue": { "..." },
    "resend": { "..." },
    "trello": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "@delorenj/mcp-server-trello"],
      "env": {
        "TRELLO_API_KEY": "${TRELLO_API_KEY}",
        "TRELLO_TOKEN": "${TRELLO_TOKEN}",
        "TRELLO_BOARD_ID": "${TRELLO_BOARD_ID}"
      }
    }
  }
}
```

#### 3.2 `.claude/settings.local.json`

Añadir `"trello"` al array `enabledMcpjsonServers`.

#### 3.3 Variables de entorno

```bash
export TRELLO_API_KEY="tu-api-key"
export TRELLO_TOKEN="tu-token"
export TRELLO_BOARD_ID="tu-board-id"
```

---

### 4. Columnas del Board de Trello

| Columna | Cuándo se usa |
|---|---|
| **BACKLOG** | `/enrich-us` crea la tarjeta aquí |
| **TODO** | `/plan-*-ticket` mueve aquí |
| **DOING** | `/develop-*` mueve aquí al empezar |
| **DONE** | Al mergearse el PR en main |

Los comandos buscan por nombre con `get_lists` (match flexible por keyword).

---

### 5. Cambios en los Commands

#### 5.1 `/enrich-us` — Crear tarjeta + enriched spec

**Pasos actuales (sin cambios):**

1. Leer tarea desde la fuente (descripción inline, fichero local)
2. Actuar como product expert
3. Entender el problema
4. Evaluar si la US está completa
5. Producir US enriquecida en markdown

**Nuevos pasos (añadir al final):**

1. Guardar en `ai-specs/changes/[task_id]/[task_id]_enriched.md`
2. **TRELLO**: Usar `get_lists` para obtener el ID de la columna "BACKLOG"
3. **TRELLO**: Usar `add_card_to_list` para crear tarjeta:
   - **name**: `[task_id]` (ej: `feat-trello-integration`)
   - **description**: Resumen de la US + ruta al fichero local
   - **listId**: ID de la columna BACKLOG
4. **TRELLO**: Guardar el Card ID en la primera línea del fichero enriched:
   `<!-- trello-card-id: {cardId} -->`
5. Si hay acceptance criteria, crear checklist en la tarjeta con `add_checklist_item`

---

#### 5.2 `/plan-backend-ticket` — Mover a TODO + checklist

**Al inicio (antes del paso 1):**

1. Leer `ai-specs/changes/[task_id]/[task_id]_enriched.md`
   - Extraer `trello-card-id` del HTML comment
   - Si no existe, buscar tarjeta por nombre con `get_cards_by_list_id`

**Al final (después de guardar el plan):**

N+1. **TRELLO**: Mover tarjeta a columna "TODO"
N+2. **TRELLO**: Añadir checklist "Backend Steps" con pasos del plan
N+3. **TRELLO**: Comentar "Backend plan: `ai-specs/changes/[task_id]/[task_id]_backend.md`"

---

#### 5.3 `/plan-frontend-ticket` — Mismo patrón

- Checklist: "Frontend Steps"
- Comentario: "Frontend plan: `{ruta}`"
- Misma lógica de movimiento a TODO

---

#### 5.4 `/develop-backend` — DOING + PR comment

**Al inicio:**

0. Leer enriched spec + backend plan desde `ai-specs/changes/[task_id]/`
   - Extraer `trello-card-id`
1. **TRELLO**: Mover tarjeta a "DOING"
2. **TRELLO**: Comentar "Dev started — branch: `feature/[task_id]-backend`"

**Al final (después de crear PR):**

N+1. **TRELLO**: Comentar "PR: {PR_URL}"
N+2. **TRELLO**: Marcar items del checklist "Backend Steps" como completados

---

#### 5.5 `/develop-frontend` — Mismo patrón

- Branch: `feature/[task_id]-frontend`
- Checklist: "Frontend Steps"

---

### 6. Detección de Merge → DONE + Archivar

#### Opción A: GitHub Action (recomendada — automática)

Crear `.github/workflows/trello-sync-on-merge.yml`:

```yaml
name: Trello — Move card to DONE on merge

on:
  pull_request:
    types: [closed]
    branches: [main]

jobs:
  trello-done:
    if: github.event.pull_request.merged == true
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Extract task ID from branch name
        id: task
        run: |
          BRANCH="${{ github.event.pull_request.head.ref }}"
          # feature/feat-xxx-backend → feat-xxx
          TASK_ID=$(echo "$BRANCH" | sed 's|^feature/||' | sed 's|-backend$||' | sed 's|-frontend$||')
          echo "task_id=$TASK_ID" >> "$GITHUB_OUTPUT"

      - name: Find Trello card ID from spec file
        id: card
        run: |
          FILE="ai-specs/changes/${{ steps.task.outputs.task_id }}/${{ steps.task.outputs.task_id }}_enriched.md"
          if [ -f "$FILE" ]; then
            CARD_ID=$(head -1 "$FILE" | grep -oP '(?<=trello-card-id: )\S+(?= -->)')
            echo "card_id=$CARD_ID" >> "$GITHUB_OUTPUT"
          fi

      - name: Move card to DONE in Trello
        if: steps.card.outputs.card_id != ''
        run: |
          # Get DONE list ID
          DONE_LIST_ID=$(curl -s "https://api.trello.com/1/boards/${{ secrets.TRELLO_BOARD_ID }}/lists?key=${{ secrets.TRELLO_API_KEY }}&token=${{ secrets.TRELLO_TOKEN }}" \
            | jq -r '.[] | select(.name | test("DONE"; "i")) | .id')

          # Move card
          curl -s -X PUT \
            "https://api.trello.com/1/cards/${{ steps.card.outputs.card_id }}?key=${{ secrets.TRELLO_API_KEY }}&token=${{ secrets.TRELLO_TOKEN }}&idList=$DONE_LIST_ID"

      - name: Move spec folder to merged
        run: |
          TASK_ID="${{ steps.task.outputs.task_id }}"
          if [ -d "ai-specs/changes/$TASK_ID" ]; then
            git config user.name "github-actions[bot]"
            git config user.email "github-actions[bot]@users.noreply.github.com"
            mv "ai-specs/changes/$TASK_ID" "ai-specs/changes/merged/$TASK_ID"
            git add ai-specs/changes/
            git commit -m "chore: archive completed spec $TASK_ID to merged"
            git push
          fi
```

**Secrets necesarios en GitHub repo settings:**

- `TRELLO_API_KEY`
- `TRELLO_TOKEN`
- `TRELLO_BOARD_ID`

#### Opción B: Comando manual `/complete-ticket` (fallback)

Crear `ai-specs/.commands/complete-ticket.md`:

```markdown
Complete the ticket: $ARGUMENTS

1. Read the enriched spec from ai-specs/changes/[task_id]/[task_id]_enriched.md
2. Extract the trello-card-id from the HTML comment in the first line
3. Use Trello MCP to move the card to the "DONE" list
4. Move the folder ai-specs/changes/[task_id] to ai-specs/changes/merged/[task_id]
5. Stage and commit: "chore: archive completed spec [task_id] to merged"
```

> **Recomendación**: Implementar ambas. La GitHub Action para el caso automático; el comando para archivar sin PR o cuando la Action falla.

---

### 7. Sincronización Inicial (tarjetas existentes)

Para las ~20 carpetas que ya existen en `ai-specs/changes/` sin tarjeta en Trello, crear un comando one-time:

#### Comando `/sync-existing-specs`

```markdown
Sync all existing spec folders in ai-specs/changes/ to Trello:

1. Use Trello MCP `get_lists` to get list IDs for the board
2. For each subfolder in ai-specs/changes/ (excluding "merged"):
   a. Check if enriched spec exists ({folder}/{folder}_enriched.md)
   b. Check if backend/frontend plan exists
   c. Determine the appropriate Trello list:
      - Has _backend.md or _frontend.md plan → TODO
      - Has only _enriched.md → BACKLOG
      - No enriched spec → BACKLOG
   d. Create Trello card in the appropriate list with folder name as title
   e. Write trello-card-id in enriched spec (create minimal one if needed)
3. Report summary of created cards
```

---

## Implementation Steps

### Phase 1: MCP Server Setup (~15 min, manual)

| # | Acción | Detalle |
|---|---|---|
| 1 | Obtener credenciales Trello | <https://trello.com/app-key> → API key + token non-expiring |
| 2 | Obtener Board ID | De la URL del board |
| 3 | Configurar variables de entorno | `TRELLO_API_KEY`, `TRELLO_TOKEN`, `TRELLO_BOARD_ID` |
| 4 | Actualizar `.mcp.json` | Añadir entrada `trello` (sección 3.1) |
| 5 | Actualizar `.claude/settings.local.json` | Añadir `"trello"` a `enabledMcpjsonServers` |
| 6 | Verificar conexión | Reiniciar Claude Code, ejecutar `get_lists` |
| 7 | Validar move card | Probar si el MCP soporta mover tarjetas. Si no, documentar fallback curl |

### Phase 2: Actualizar Commands (~30 min)

| # | Fichero | Cambio |
|---|---|---|
| 8 | `enrich-us.md` | Crear tarjeta en BACKLOG + guardar card-id (sección 5.1) |
| 9 | `plan-backend-ticket.md` | Leer card-id, mover a TODO, checklist (sección 5.2) |
| 10 | `plan-frontend-ticket.md` | Mismo patrón (sección 5.3) |
| 11 | `develop-backend.md` | DOING al inicio, PR comment al final (sección 5.4) |
| 12 | `develop-frontend.md` | Mismo patrón (sección 5.5) |
| 13 | **Crear** `complete-ticket.md` | DONE + archivar en merged (sección 6 Opción B) |

### Phase 3: GitHub Action (~15 min)

| # | Acción |
|---|---|
| 14 | Crear `.github/workflows/trello-sync-on-merge.yml` (sección 6 Opción A) |
| 15 | Añadir secrets al repo en GitHub |

### Phase 4: Sincronización Inicial (~10 min)

| # | Acción |
|---|---|
| 16 | Ejecutar `/sync-existing-specs` para las ~20 carpetas existentes |
| 17 | Verificar tarjetas creadas en las columnas correctas |

### Phase 5: Validación E2E (~15 min)

| # | Test |
|---|---|
| 18 | `/enrich-us` con descripción nueva → tarjeta en BACKLOG |
| 19 | `/plan-backend-ticket {id}` → tarjeta movida a TODO con checklist |
| 20 | `/develop-backend {id}` → tarjeta en DOING + comment branch |
| 21 | Crear PR → comment PR URL en tarjeta |
| 22 | Mergear PR → tarjeta en DONE + carpeta en `merged/` |
| 23 | Verificar que commands funcionan sin Trello (graceful fallback) |

---

## Out of Scope

- Cambios en código de Abuvi (backend/frontend)
- Sincronización Trello → Claude Code (unidireccional: solo CC → Trello)
- Soporte multi-board
- Labels, due dates, attachments en Trello
- Crear tarjetas manualmente en Trello (siempre desde Claude Code)

---

## Riesgos y Mitigaciones

| Riesgo | Mitigación |
|---|---|
| MCP no soporta mover tarjetas | Fallback: curl a Trello REST API |
| Rate limiting | 2-4 llamadas por comando; lejos del límite de 100/10s |
| MCP server no disponible | Commands omiten sync con warning; ficheros locales siempre funcionan |
| Branch name no sigue convención | Regex flexible en la GitHub Action |
| Tarjetas duplicadas | Buscar por card-id embebido antes de crear nueva |
