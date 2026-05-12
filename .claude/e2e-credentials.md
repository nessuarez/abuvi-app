# E2E Local Test Credentials

Frontend URL:  http://localhost:5173
API URL (E2E): http://localhost:5080/api
DB sandbox:    abuvi_e2e (NEVER touch abuvi_prod)

## Test Users

| Role   | Email               | Password      | FamilyUnit    |
| ------ | ------------------- | ------------- | ------------- |
| Admin  | admin@abuvi.local   | Admin@123456  | —             |
| Board  | board@abuvi.local   | Board@123456  | Lopez Family  |
| Member | member1@abuvi.local | Member@123456 | Garcia Family |

## Pre-seeded Registrations (Camp Costa 2027)

| FamilyUnit    | Status    | Total | Payments               |
| ------------- | --------- | ----- | ---------------------- |
| Garcia Family | Pending   | 600 € | 0                      |
| Lopez Family  | Confirmed | 180 € | 1 Transfer (Completed) |

## E2E Setup (first time or after reset)

1. `docker compose up -d`
2. `cd src/Abuvi.API && dotnet run --launch-profile E2E`  ← creates abuvi_e2e + runs migrations
3. `cd frontend && npm run e2e:seed`                      ← seeds test data
4. `cd frontend && npm run dev`
5. `cd frontend && npx cypress open`

## Reset the sandbox

`cd frontend && npm run e2e:seed`  (idempotent — resets and re-seeds abuvi_e2e)
