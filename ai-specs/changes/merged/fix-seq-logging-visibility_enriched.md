# Investigar y corregir visibilidad de eventos en Seq (despliegue)

## Problema

Al navegar por la aplicación en el entorno de despliegue, en Seq solo se visualizan las llamadas del **healthcheck** (`/health`). No aparecen los logs de las peticiones HTTP normales (endpoints de API) ni los logs de negocio generados por los servicios.

---

## Análisis de la causa raíz

### Hallazgo principal: `UseSerilogRequestLogging()` está posicionado después de middlewares que pueden cortocircuitar la petición

**Pipeline actual** ([Program.cs:291-332](src/Abuvi.API/Program.cs#L291-L332)):

```
1. GlobalExceptionMiddleware
2. UseCors()                    ← puede rechazar peticiones (preflight/origin inválido)
3. UseHttpsRedirection()        ← puede redirigir HTTP→HTTPS y terminar el pipeline
4. UseAuthentication()
5. UseAuthorization()
6. UseSerilogRequestLogging()   ← ⚠️ AQUÍ se loguean las peticiones
7. MapHealthChecks("/health")
8. Map*Endpoints()
```

**Consecuencia:** Cualquier petición que sea respondida ANTES de llegar al middleware de Serilog (paso 6) **no se registra en Seq**. Esto incluye:

- Peticiones rechazadas por CORS (paso 2)
- Redirecciones HTTPS (paso 3)
- Rechazos de autenticación/autorización (pasos 4-5) — depende de la implementación, pero el log no captura el contexto completo

**Nota:** El healthcheck SÍ aparece porque los sistemas de monitoreo/load balancers suelen hacer la petición correctamente (misma red, sin CORS, endpoint anónimo).

### Hallazgo secundario: Niveles de log hardcodeados en C#

La configuración de Serilog en [Program.cs:33-73](src/Abuvi.API/Program.cs#L33-L73) usa niveles **hardcodeados**:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()                                          // ← hardcoded
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning) // ← hardcoded
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
```

La sección `Serilog` en `appsettings.json` **no se consume** porque no se usa `.ReadFrom.Configuration(builder.Configuration)`. El JSON de configuración es "código muerto":

```json
// appsettings.json — NO TIENE EFECTO
"Serilog": {
    "MinimumLevel": {
        "Default": "Information",
        "Override": { ... }
    }
}
```

Esto impide ajustar dinámicamente los niveles de log en despliegue sin recompilar.

---

## Plan de corrección

### Paso 1: Mover `UseSerilogRequestLogging()` al inicio del pipeline

**Archivo:** [Program.cs:291-332](src/Abuvi.API/Program.cs#L291-L332)

Mover `UseSerilogRequestLogging()` para que sea el **primer middleware** después de los condicionales de desarrollo. Así captura TODAS las peticiones, incluidas las rechazadas por CORS, auth, etc.

**Antes:**

```csharp
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseSerilogRequestLogging(options => { ... }); // ← demasiado tarde
```

**Después:**

```csharp
app.UseSerilogRequestLogging(options => { ... }); // ← PRIMERO: captura todo
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
```

> **Nota:** Al moverlo antes de `UseAuthentication()`, el enrichment de `UserId` dentro de `EnrichDiagnosticContext` seguirá funcionando porque el contexto se evalúa al **completarse** la petición (cuando la respuesta vuelve por el pipeline), momento en el cual `UseAuthentication` ya ha procesado el token.

### Paso 2: Usar `ReadFrom.Configuration()` para niveles dinámicos

**Archivo:** [Program.cs:33-73](src/Abuvi.API/Program.cs#L33-L73)

Reemplazar los niveles hardcodeados con lectura desde configuración, manteniendo los valores por defecto como fallback:

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)  // ← Lee de appsettings.json
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithClientIp()
    .Enrich.WithCorrelationId()
    .WriteTo.Console()
    .WriteTo.Async(a => a.PostgreSQL(...))
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341")
    .CreateLogger();
```

**Beneficio:** Permite ajustar niveles de log en despliegue sin recompilar (por variable de entorno o appsettings override).

**Paquete necesario:** `Serilog.Settings.Configuration` (verificar si ya está incluido en `Serilog.AspNetCore`).

### Paso 3: Excluir healthcheck del log de peticiones (reducir ruido)

Añadir un filtro para no loguear las peticiones al healthcheck, que generan ruido y ocultan logs útiles:

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    // Excluir healthcheck del log de peticiones
    options.GetLevel = (httpContext, elapsed, ex) =>
        httpContext.Request.Path.StartsWithSegments("/health")
            ? LogEventLevel.Verbose  // Efectivamente lo oculta (nivel mínimo es Information)
            : LogEventLevel.Information;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        // ... enrichment existente sin cambios ...
    };
});
```

### Paso 4: Verificar configuración de Seq en despliegue

Comprobar que en el entorno de despliegue:

1. La variable `Seq:ServerUrl` apunta al Seq correcto (no a `localhost:5341`)
2. El servicio Seq es accesible desde el contenedor/servidor de la API
3. No hay firewall bloqueando el puerto de Seq

Verificación rápida en los logs de inicio de la aplicación (consola):

```
[INF] Starting Abuvi API...
```

Si no se ve este log en Seq, confirma que la URL de Seq es incorrecta o inaccesible.

### Paso 5: Crear `appsettings.Production.json` (si no existe)

```json
{
    "Serilog": {
        "MinimumLevel": {
            "Default": "Information",
            "Override": {
                "Microsoft.AspNetCore": "Warning",
                "Microsoft.EntityFrameworkCore": "Warning"
            }
        }
    },
    "Seq": {
        "ServerUrl": "http://seq:5341"
    }
}
```

---

## Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `src/Abuvi.API/Program.cs` | Mover `UseSerilogRequestLogging`, añadir `ReadFrom.Configuration()`, filtrar healthcheck |
| `src/Abuvi.API/appsettings.json` | Mantener config de Serilog (ahora sí se consumirá) |
| `src/Abuvi.API/appsettings.Production.json` | Crear si no existe, con Seq URL de producción |

---

## Criterios de aceptación

- [ ] `UseSerilogRequestLogging()` se ejecuta antes de CORS, auth y redirección HTTPS
- [ ] Los niveles de Serilog se leen desde `appsettings.json` con `ReadFrom.Configuration()`
- [ ] Las peticiones al healthcheck (`/health`) se excluyen del log de peticiones HTTP
- [ ] Al navegar por la aplicación en despliegue, las peticiones a endpoints de API aparecen en Seq
- [ ] Los logs incluyen: método HTTP, path, status code, tiempo de respuesta, userId (si autenticado)
- [ ] La URL de Seq en despliegue está correctamente configurada y verificada
- [ ] Los tests existentes siguen pasando sin modificaciones

---

## Verificación en Seq

Tras el despliegue, en Seq deberían verse eventos como:

```
HTTP GET /api/camps responded 200 in 45.2300 ms
HTTP POST /api/auth/login responded 200 in 120.5000 ms
HTTP GET /api/family-units responded 401 in 2.3000 ms
```

Con propiedades estructuradas: `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`, `UserAgent`, `UserId`, `ClientIp`, `CorrelationId`.
