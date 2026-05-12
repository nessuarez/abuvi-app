# feat: E2E Test Credentials & Local Seeding Infrastructure

## Contexto

El proyecto ya tiene:

- **`Abuvi.Setup`** CLI con soporte para `run-all` (reset + import CSVs) y CSVs de seed en `src/Abuvi.Setup/seed/`
- **Cypress** configurado (`cypress.config.ts`) con base URL `http://localhost:5173`
- **Usuarios seedeados** en CSVs con los 3 roles necesarios:
  - `admin@abuvi.local` / `Admin@123456` — Admin (seeded por migración EF)
  - `board@abuvi.local` / `Board@123456` — Board
  - `member1@abuvi.local` / `Member@123456` — Member (con familia: 4 miembros)
- Cypress **sin** comando `cy.login()` ni `cypress.env.json`
- **Sin inscripciones seed** — los tests de inscripción necesitan crearlas desde cero cada vez

## Objetivo

Crear la infraestructura mínima para que:

1. Claude (o cualquier desarrollador) pueda consultar las credenciales de test sin ambigüedad
2. Cypress tests puedan autenticarse programáticamente con cualquier rol
3. Se pueda resetear + re-seedear el entorno E2E con un solo comando
4. Haya inscripciones predefinidas en distintos estados para testear acciones sobre ellas sin setup previo
5. **Los datos locales del desarrollador queden completamente aislados** — la BD de desarrollo (`abuvi_prod`) nunca se toca durante los tests E2E

---

## Arquitectura: BD sandbox `abuvi_e2e`

El entorno E2E usa una base de datos PostgreSQL separada (`abuvi_e2e`) dentro del mismo contenedor Docker. Esto garantiza:

- `abuvi_prod` → datos reales del desarrollador, intactos
- `abuvi_e2e` → sandbox desechable, reseteable sin consecuencias

**Cómo funciona la creación automática:**
La API hace `await dbContext.Database.MigrateAsync()` en arranque ([Program.cs:328](src/Abuvi.API/Program.cs#L328)). Con Npgsql, `MigrateAsync` crea la BD si no existe. Esto significa que **el primer arranque con `ASPNETCORE_ENVIRONMENT=E2E` crea y migra `abuvi_e2e` automáticamente** — sin scripts manuales de inicialización.

**Flujo de setup E2E:**

```
1. docker compose up -d           ← infraestructura (Postgres, MinIO, Seq)
2. dotnet run --launch-profile E2E  ← API crea abuvi_e2e + migra (primer arranque)
3. npm run e2e:seed               ← seeder puebla abuvi_e2e con datos de prueba
4. npm run dev (frontend)         ← frontend apunta a API E2E
5. npx cypress open               ← tests contra el sandbox
```

---

## Tareas

### 1. Crear `.claude/e2e-credentials.md` (nuevo fichero, NO gitignoreado)

Fichero en texto plano donde el usuario documenta las credenciales locales de test para Claude.
Claude lo lee al inicio de una sesión de E2E antes de ejecutar tests.

```markdown
# E2E Local Test Credentials

Frontend URL: http://localhost:5173
API URL (E2E): http://localhost:5080/api
BD sandbox:    abuvi_e2e (NO tocar abuvi_prod)

## Usuarios de test

| Rol    | Email               | Password      | FamilyUnit    |
| ------ | ------------------- | ------------- | ------------- |
| Admin  | admin@abuvi.local   | Admin@123456  | —             |
| Board  | board@abuvi.local   | Board@123456  | Lopez Family  |
| Member | member1@abuvi.local | Member@123456 | Garcia Family |

## Inscripciones preseedadas (Camp Costa 2027)

| FamilyUnit    | Estado    | Total | Pagos                  |
| ------------- | --------- | ----- | ---------------------- |
| Garcia Family | Pending   | 600 € | 0                      |
| Lopez Family  | Confirmed | 180 € | 1 Transfer (Completed) |

## Setup E2E (primera vez o tras reset)

1. `docker compose up -d`
2. `cd src/Abuvi.API && dotnet run --launch-profile E2E`  ← crea abuvi_e2e + migra
3. `cd frontend && npm run e2e:seed`                      ← siembra datos de test
4. `cd frontend && npm run dev`
5. `cd frontend && npx cypress open`

## Reset del sandbox

`cd frontend && npm run e2e:seed`  (idempotente, borra y re-siembra)
```

> **Nota:** Este fichero contiene sólo credenciales de desarrollo local. No contiene datos reales de producción.

---

### 2. Añadir `cypress.env.json` al frontend (gitignoreado)

Fichero: `frontend/cypress.env.json`

```json
{
  "ADMIN_EMAIL": "admin@abuvi.local",
  "ADMIN_PASSWORD": "Admin@123456",
  "BOARD_EMAIL": "board@abuvi.local",
  "BOARD_PASSWORD": "Board@123456",
  "MEMBER_EMAIL": "member1@abuvi.local",
  "MEMBER_PASSWORD": "Member@123456",
  "API_URL": "http://localhost:5080/api"
}
```

Añadir `cypress.env.json` a `frontend/.gitignore`.

---

### 3. Crear `src/Abuvi.API/appsettings.E2E.json`

Sólo sobreescribe la conexión y el nivel de log. El resto hereda de `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=abuvi_e2e;Username=abuvi_user;Password=dev_password"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning"
    }
  }
}
```

> El primer arranque con este perfil crea `abuvi_e2e` y aplica todas las migraciones automáticamente gracias a `MigrateAsync()` ([Program.cs:328](src/Abuvi.API/Program.cs#L328)).

---

### 4. Añadir perfil `E2E` en `src/Abuvi.API/Properties/launchSettings.json`

Puerto 5080 (distinto del dev 5079) para poder tener ambas instancias corriendo a la vez:

```json
"E2E": {
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "http://localhost:5080",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "E2E"
  }
}
```

Arrancar con: `dotnet run --launch-profile E2E`

---

### 5. Añadir comando `cy.login()` en `frontend/cypress/support/commands.ts`

Login programático vía API (evita pasar por la UI en cada test, más rápido y estable):

```typescript
Cypress.Commands.add('login', (role: 'admin' | 'board' | 'member' = 'member') => {
  const emailKey = `${role.toUpperCase()}_EMAIL` as keyof typeof Cypress.env
  const passwordKey = `${role.toUpperCase()}_PASSWORD` as keyof typeof Cypress.env

  cy.request({
    method: 'POST',
    url: `${Cypress.env('API_URL')}/auth/login`,
    body: {
      email: Cypress.env(emailKey),
      password: Cypress.env(passwordKey),
    },
  }).then((response) => {
    expect(response.status).to.eq(200)
    const token = response.body.data.token
    window.localStorage.setItem('auth_token', token)
  })
})
```

Añadir la declaración de tipo en el bloque `declare global`:

```typescript
login(role?: 'admin' | 'board' | 'member'): Chainable<void>
```

**Uso en tests:**

```typescript
beforeEach(() => {
  cy.login('admin')   // autenticarse como Admin
  cy.visit('/')
})
```

---

### 6. Añadir script npm `e2e:seed` en `frontend/package.json`

Pasa `--connection` apuntando a `abuvi_e2e` para que el seeder nunca toque `abuvi_prod`:

```json
"scripts": {
  "e2e:seed": "dotnet run --project ../src/Abuvi.Setup run-all --connection \"Host=localhost;Port=5432;Database=abuvi_e2e;Username=abuvi_user;Password=dev_password\""
}
```

---

### 7. Actualizar `frontend/cypress.config.ts`

Default `API_URL` apunta al puerto E2E (5080). `cypress.env.json` lo sobreescribe si hace falta:

```typescript
import { defineConfig } from 'cypress'

export default defineConfig({
  e2e: {
    projectId: '2oziu4',
    baseUrl: 'http://localhost:5173',
    specPattern: 'cypress/e2e/**/*.cy.{js,jsx,ts,tsx}',
    supportFile: 'cypress/support/e2e.ts',
    env: {
      API_URL: 'http://localhost:5080/api',
    },
  },
})
```

---

### 8. (Opcional) Añadir `cy.task('resetDb')` para tests que necesiten estado limpio

En `cypress.config.ts`, dentro de `setupNodeEvents`. Pasa la connection string de `abuvi_e2e`:

```typescript
setupNodeEvents(on) {
  on('task', {
    async resetDb() {
      const { execSync } = require('child_process')
      execSync(
        'dotnet run --project ../src/Abuvi.Setup run-all --connection "Host=localhost;Port=5432;Database=abuvi_e2e;Username=abuvi_user;Password=dev_password"',
        { stdio: 'inherit' }
      )
      return null
    },
  })
},
```

Uso en test:

```typescript
before(() => {
  cy.task('resetDb')
})
```

> Sólo necesario si el test modifica datos de forma destructiva. Para la mayoría de tests de lectura, `cy.login()` es suficiente.

---

## Protocolo de uso con Claude

Cuando se inicie una sesión de E2E con Claude:

1. Claude lee `.claude/e2e-credentials.md` para conocer las credenciales y URLs
2. Claude verifica que la BD está seedeada (si no, indica `npm run e2e:seed`)
3. Claude ejecuta Playwright/Cypress apuntando a `http://localhost:5173`
4. Claude usa las credenciales del rol apropiado según la funcionalidad a testear

---

### 9. Añadir `RegistrationSeeder` a `Abuvi.Setup` (C# hardcoded, no CSV)

Las inscripciones **no se implementan como CSV** porque implican lógica de negocio compleja: cálculo de precios por categoría de edad, edad en el momento del campamento, historial de estados. Duplicar esta lógica en un parser CSV sería frágil y difícil de mantener.

En su lugar, se añade un `RegistrationSeeder.cs` en `src/Abuvi.Setup/` que se ejecuta después de los importadores CSV y crea inscripciones usando EF Core directamente.

**Inscripciones a seedear** (todas sobre `Camp Costa 2027`, edición `Open`):

| # | FamilyUnit | Registrado por | Estado | Miembros | Total | Pagos |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Garcia Family | `member1@abuvi.local` | `Pending` | Carlos (Adult), Laura (Adult), Pablo (Child), Sofia (Child) | 600 € | 0 |
| 2 | Lopez Family | `board@abuvi.local` | `Confirmed` | Ana (Adult) | 180 € | 1 pago completo (180 €, Transfer) |

Cálculo de precios (Camp Costa 2027: Adult=180€, Child=120€):

- Garcia: 2×180 + 2×120 = **600 €**
- Lopez: 1×180 = **180 €**

**Lógica del seeder:**

1. Busca `CampEdition` por `campName="Camp Costa"` y `year=2027`
2. Busca cada `FamilyUnit` por nombre
3. Busca `RegisteredByUser` por email
4. Busca cada `FamilyMember` por firstName+lastName dentro de la FamilyUnit
5. Calcula `AgeAtCamp` desde `DateOfBirth` + `StartDate` del camp
6. Inserta `Registration` + `RegistrationMember`s + `RegistrationStatusHistory` (entrada inicial)
7. Para Lopez: añade un `Payment` con `Status=Completed`, `Method=Transfer`, `InstallmentNumber=1`

**Integración en `SeedRunner.cs`:**

```csharp
// Añadir al final de ImportAllAsync(), tras los importadores CSV
var registrationSeeder = new RegistrationSeeder(db);
await registrationSeeder.SeedAsync();
```

También incluir en `ResetAsync()` el borrado de inscripciones seed (ya está cubierto porque `ResetAsync` hace `ExecuteDeleteAsync` sobre todas las tablas excepto el admin).

**Firma de `RegistrationSeeder`:**

```csharp
public class RegistrationSeeder(AbuviDbContext db)
{
    public async Task SeedAsync() { ... }
}
```

---

## Archivos a crear/modificar

| Fichero | Acción |
| --- | --- |
| `src/Abuvi.API/appsettings.E2E.json` | Crear (connection string → `abuvi_e2e`) |
| `src/Abuvi.API/Properties/launchSettings.json` | Modificar (añadir perfil `E2E` en puerto 5080) |
| `.claude/e2e-credentials.md` | Crear (credenciales + contexto E2E para Claude) |
| `frontend/cypress.env.json` | Crear (gitignoreado, credenciales para Cypress) |
| `frontend/.gitignore` | Modificar (añadir `cypress.env.json`) |
| `frontend/cypress/support/commands.ts` | Modificar (añadir `cy.login()`) |
| `frontend/package.json` | Modificar (añadir script `e2e:seed` con `--connection abuvi_e2e`) |
| `frontend/cypress.config.ts` | Modificar (añadir `env.API_URL` → puerto 5080) |
| `src/Abuvi.Setup/RegistrationSeeder.cs` | Crear (inscripciones hardcoded con EF Core) |
| `src/Abuvi.Setup/SeedRunner.cs` | Modificar (llamar a `RegistrationSeeder.SeedAsync()`) |

---

## Criterios de aceptación

- [ ] `dotnet run --launch-profile E2E` crea `abuvi_e2e` en la primera ejecución y no toca `abuvi_prod`
- [ ] `npm run e2e:seed` resetea y re-siembra `abuvi_e2e` en < 30s sin afectar `abuvi_prod`
- [ ] `cy.login('admin')`, `cy.login('board')`, `cy.login('member')` funcionan sin pasar por la UI
- [ ] Ambas APIs pueden correr simultáneamente: dev en 5079 y E2E en 5080, cada una con su BD
- [ ] `.claude/e2e-credentials.md` existe y es legible por Claude al inicio de la sesión
- [ ] `cypress.env.json` está en `.gitignore` (no se sube al repositorio)
- [ ] Los tests existentes (`auth.cy.ts`, `users.cy.ts`, etc.) siguen pasando
- [ ] Tras `e2e:seed`, hay 2 inscripciones en `abuvi_e2e`: `Pending` (Garcia, 600 €) y `Confirmed` con pago (Lopez, 180 €)

---

## Datos de seed disponibles tras `e2e:seed`

| Entidad | Datos |
|---|---|
| Users | admin, board, member1, member2 |
| FamilyUnits | Garcia Family, Lopez Family, Martin Family |
| FamilyMembers | 5 miembros (2 padres, 1 cónyuge, 2 hijos en Garcia; Ana en Lopez) |
| Camps | Camp Sierra, Camp Costa |
| CampEditions | Camp Sierra 2027 (Draft), Camp Costa 2027 (Open) |
| **Registrations** | Garcia Family → Camp Costa 2027 (`Pending`, 600 €); Lopez Family → Camp Costa 2027 (`Confirmed`, 180 €, 1 pago) |
