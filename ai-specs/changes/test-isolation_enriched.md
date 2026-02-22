# Historia enriquecida: Aislamiento de base de datos en tests de integración

**ID**: `test-isolation`
**Tipo**: Infraestructura de testing
**Prioridad**: Alta — afecta a todo el flujo de desarrollo TDD

---

## Problema

Los tests de integración actuales (~13 archivos) se ejecutan contra la base de datos local de
desarrollo (`abuvi` en `localhost:5432`). Cada test crea registros con GUIDs únicos para evitar
colisiones, pero **nunca limpia los datos generados**. Con el tiempo, la base de datos acumula
cientos de usuarios, campamentos, membresías y unidades familiares de prueba que nunca existieron
en producción, dificultando el debugging y la inspección manual de datos reales.

---

## Solución elegida: Testcontainers

Cada ejecución de `dotnet test` levanta un contenedor PostgreSQL efímero (`postgres:16-alpine`)
con una base de datos limpia, migra el esquema automáticamente, ejecuta todos los tests, y destruye
el contenedor al finalizar. La base de datos de desarrollo (`abuvi`) **nunca es tocada por los tests**.

---

## Contexto técnico

- **Proyecto de tests**: `src/Abuvi.Tests/` (xUnit, NSubstitute, FluentAssertions)
- **Tests de integración HTTP**: usan `WebApplicationFactory<Program>` con PostgreSQL real
- **Tests de integración directos**: usan EF Core InMemory directamente — no cambian
- **Restricción conocida**: EF Core no permite mezclar providers Npgsql + InMemory en el mismo host
  (provoca `Services for database providers 'Npgsql...' have been registered`)
- **Challenge de Serilog**: `Program.cs` configura el sink de PostgreSQL para Serilog antes de que
  `WebApplicationFactory` pueda inyectar el connection string del contenedor → requiere guard
  en código de producción

---

## Criterios de aceptación

- [ ] Ejecutar `dotnet test` no crea ni modifica ningún dato en la base de datos `abuvi`
- [ ] Los tests de integración pasan en verde tras cada ejecución desde cero
- [ ] El contenedor de test se destruye automáticamente al finalizar `dotnet test`
- [ ] Los tests unitarios (con mocks) no son afectados
- [ ] Los tests InMemory existentes (`CampsDbSchemaTests`, `MembershipDatabaseTests`, etc.) no cambian
- [ ] Un test de verificación de aislamiento confirma que los datos no se filtran entre test classes

---

## Pasos de implementación (TDD obligatorio)

### Paso 0 — Añadir dependencia NuGet

En `src/Abuvi.Tests/Abuvi.Tests.csproj`:

```xml
<PackageReference Include="Testcontainers.PostgreSql" Version="3.11.*" />
```

### Paso 1 — RED: test de aislamiento PRIMERO

Crear `src/Abuvi.Tests/Integration/Infrastructure/DatabaseIsolationTests.cs`
con dos clases `[Collection("Integration")]`:

- **Clase A** `DataCreationTests`: crea un `User` en la DB via el factory y verifica que existe
- **Clase B** `DataIsolationTests`: verifica que ese usuario NO existe al iniciar la clase

La Clase B fallará (rojo) hasta que la infraestructura esté completa. Ese fallo guía la implementación.

### Paso 2 — Modificar `Program.cs` (único cambio en código de producción)

Hacer el Serilog PostgreSQL sink condicional para el entorno `Testing`:

```csharp
var isTestingEnvironment = builder.Environment.IsEnvironment("Testing");

var logConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    // ... enrichers y Console sink (sin cambios) ...
    .WriteTo.Seq(seqServerUrl);

if (!isTestingEnvironment)
{
    logConfig = logConfig.WriteTo.Async(a => a.PostgreSQL(
        connectionString: connectionString,
        tableName: "Logs",
        // ... parámetros existentes sin cambios ...
    ));
}

Log.Logger = logConfig.CreateLogger();
```

**Por qué es necesario**: Serilog lee `builder.Configuration.GetConnectionString("DefaultConnection")`
al momento de crear `Log.Logger`, antes de que `WebApplicationFactory.ConfigureWebHost()` pueda
inyectar el connection string del contenedor efímero. Sin este guard, Serilog intentará conectarse
a la DB de desarrollo durante los tests.

### Paso 3 — Crear `src/Abuvi.API/appsettings.Testing.json`

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

El `ConnectionStrings:DefaultConnection` lo inyecta la factory dinámicamente y no debe ir aquí.

### Paso 4 — Crear `src/Abuvi.Tests/Infrastructure/PostgresContainerFixture.cs`

```csharp
using Testcontainers.PostgreSql;

namespace Abuvi.Tests.Infrastructure;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("abuvi_test")
        .WithUsername("abuvi_user")
        .WithPassword("test_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<PostgresContainerFixture> { }
```

### Paso 5 — Crear `src/Abuvi.Tests/Infrastructure/IntegrationWebApplicationFactory.cs`

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Abuvi.Tests.Infrastructure;

public sealed class IntegrationWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Inyectar connection string del contenedor antes de que el host se construya
        // Esto permite que EF Core y cualquier otra dependencia usen la DB del contenedor
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            // Desactivar background services para evitar ruido en tests
            // (AnnualFeeGenerationService, LogCleanupService)
            services.RemoveAll<IHostedService>();
        });
    }
}
```

### Paso 6 — Actualizar las clases de test HTTP

**Patrón de cambio** (aplicar a los 7 archivos listados abajo):

```csharp
// ANTES
public class GuestsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GuestsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
}

// DESPUÉS
[Collection("Integration")]
public class GuestsEndpointsTests : IDisposable
{
    private readonly IntegrationWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GuestsEndpointsTests(PostgresContainerFixture pgFixture)
    {
        _factory = new IntegrationWebApplicationFactory(pgFixture.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();
}
```

**Archivos a actualizar**:

1. `src/Abuvi.Tests/Integration/Features/AuthIntegrationTests.cs`
2. `src/Abuvi.Tests/Integration/Features/ProtectedEndpointsTests.cs`
3. `src/Abuvi.Tests/Integration/Features/Guests/GuestDatabaseTests.cs`
4. `src/Abuvi.Tests/Integration/Features/Guests/GuestsEndpointsTests.cs`
5. `src/Abuvi.Tests/Integration/Features/Memberships/MembershipsEndpointsTests.cs`
6. `src/Abuvi.Tests/Integration/Features/Memberships/MembershipFeesEndpointsTests.cs`
7. `src/Abuvi.Tests/Integration/Features/Camps/CampEditionsEndpointsTests.cs`
8. `src/Abuvi.Tests/Integration/Features/FamilyUnits/FamilyUnitsUserLinkingTests.cs` *(revisar)*

**Tests que NO cambian** (ya usan InMemory directamente):

- `Integration/Features/Camps/CampsDbSchemaTests.cs`
- `Integration/Features/Camps/CampAccommodationTests.cs`
- `Integration/Features/Memberships/MembershipDatabaseTests.cs`
- `Integration/Features/Memberships/MembershipsRepositoryIntegrationTests.cs`

### Paso 7 — GREEN: verificar aislamiento

El test `DataIsolationTests` del Paso 1 debe pasar en verde.

---

## Verificación end-to-end

```bash
# 1. Ejecutar todos los tests
dotnet test src/Abuvi.Tests/

# 2. Confirmar que la DB de dev no fue tocada
docker exec -it abuvi-postgres psql -U abuvi_user -d abuvi \
  -c "SELECT COUNT(*) FROM \"Users\";"
# El número debe ser idéntico al de antes de ejecutar los tests

# 3. Confirmar que el contenedor de test no sigue corriendo
docker ps --filter "ancestor=postgres:16-alpine"
# No debe aparecer ningún contenedor

# 4. Segunda ejecución: debe producir los mismos resultados (DB limpia en cada run)
dotnet test src/Abuvi.Tests/
```

---

## Requisitos no funcionales

- **Seguridad**: El connection string del contenedor no debe hardcodearse en código fuente; la
  `PostgresContainerFixture` lo genera dinámicamente en tiempo de ejecución
- **Performance**: Overhead de ~20-30s por el arranque del contenedor; aceptable para una sesión de
  tests completa. Los tests unitarios (que no usan el contenedor) no se ven afectados
- **CI/CD**: Compatible con GitHub Actions usando `docker` disponible en los agentes estándar;
  no requiere servicio PostgreSQL preconfigurado
- **Mantenibilidad**: El cambio en `Program.cs` es mínimo y reversible; la infraestructura de test
  queda centralizada en `src/Abuvi.Tests/Infrastructure/`

---

## Upgrade path futuro

Si se necesita aislamiento por test (no solo por sesión), añadir `Respawn` al fixture:

```xml
<PackageReference Include="Respawn" Version="6.2.*" />
```

Y exponer un `ResetAsync()` en `PostgresContainerFixture` que se llame en el `InitializeAsync()`
de cada test class vía `IAsyncLifetime`.
