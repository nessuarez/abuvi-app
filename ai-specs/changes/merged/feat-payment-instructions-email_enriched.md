# Añadir instrucciones de pago al email de confirmación de inscripción

## Descripción
Como familiar inscrito, quiero recibir en el email de confirmación de la inscripción las instrucciones claras para realizar el primer pago por transferencia bancaria, de modo que pueda realizarlo sin tener que iniciar sesión en la app.

## Contexto técnico actual

El email de confirmación se envía en `RegistrationsService.CreateAsync()` justo **después** de crear los pagos mediante `paymentsService.CreateInstallmentsAsync()`. Por tanto, en el momento de envío, ya existen en base de datos los dos plazos con sus conceptos generados.

El primer plazo tiene:
- `InstallmentNumber = 1`
- `TransferConcept` = `{prefix}-{año}-{APELLIDOFAMILIA}-1` (p.ej. `CAMP-2026-GARCIA-1`)
- `Amount` = 50% del total (con ceiling)

Los datos bancarios (IBAN, banco, titular) viven en `AssociationSettings` con clave `"payment_settings"`, cargados vía `LoadPaymentSettingsAsync()` en `PaymentsService`.

## Cambios requeridos

### 1. `IEmailService.cs` — Ampliar `CampRegistrationEmailData`

Añadir campos opcionales de pago al record existente:

```csharp
// Datos del primer plazo (opcionales — null si no hay settings configurados)
public string? FirstInstallmentConcept { get; init; }
public decimal? FirstInstallmentAmount { get; init; }
public string? Iban { get; init; }
public string? BankName { get; init; }
public string? AccountHolder { get; init; }
```

### 2. `IPaymentsService.cs` — Exponer `GetPaymentSettingsAsync`

```csharp
Task<PaymentSettingsResponse> GetPaymentSettingsAsync(CancellationToken ct);
```

### 3. `PaymentsService.cs` — Implementar `GetPaymentSettingsAsync`

Reutilizar `LoadPaymentSettingsAsync()` ya existente (método privado), exponiéndolo como public en la interfaz.

### 4. `RegistrationsService.cs` — `CreateAsync` y `BuildRegistrationEmailData`

En `CreateAsync`, tras crear los pagos, cargar también los payment settings para pasarlos al builder:

```csharp
// 12. Create payment installments
var installments = await paymentsService.CreateInstallmentsAsync(registration.Id, ct);

// 13. Reload registration (already done)
var detailed = await registrationsRepo.GetByIdWithDetailsAsync(registration.Id, ct) ...

// 14. Send confirmation email
try
{
    var paymentSettings = await paymentsService.GetPaymentSettingsAsync(ct);
    var emailData = BuildRegistrationEmailData(detailed, edition, installments, paymentSettings);
    await emailService.SendCampRegistrationConfirmationAsync(emailData, ct);
}
catch (Exception ex)
{
    logger.LogError(...);
}
```

Actualizar la firma de `BuildRegistrationEmailData`:

```csharp
private static CampRegistrationEmailData BuildRegistrationEmailData(
    Registration registration,
    CampEdition edition,
    List<PaymentResponse> installments,
    PaymentSettingsResponse paymentSettings)
{
    var first = installments.FirstOrDefault(i => i.InstallmentNumber == 1);
    var hasPaymentInfo = first != null && !string.IsNullOrWhiteSpace(paymentSettings.Iban);

    return new CampRegistrationEmailData
    {
        // ... campos existentes sin cambios ...
        FirstInstallmentConcept = hasPaymentInfo ? first!.TransferConcept : null,
        FirstInstallmentAmount  = hasPaymentInfo ? first!.Amount : null,
        Iban                    = hasPaymentInfo ? paymentSettings.Iban : null,
        BankName                = hasPaymentInfo ? paymentSettings.BankName : null,
        AccountHolder           = hasPaymentInfo ? paymentSettings.AccountHolder : null,
    };
}
```

> **Nota**: El método `BuildRegistrationEmailData` también se usa en `UpdateMembersAsync` (línea ~407). Actualizar esa llamada igualmente pasando los installments y settings cargados desde la registration.

### 5. `ResendEmailService.cs` — `SendCampRegistrationConfirmationAsync`

Añadir un bloque HTML visualmente destacado **entre** el bloque de totales y el botón "Ver mi inscripción", sólo cuando `data.Iban` no sea nulo:

```csharp
var ibanFormatted = string.IsNullOrWhiteSpace(data.Iban) ? "" :
    string.Join(" ", Enumerable.Range(0, (data.Iban.Replace(" ", "").Length + 3) / 4)
        .Select(i => { var s = data.Iban.Replace(" ", ""); return s.Substring(i * 4, Math.Min(4, s.Length - i * 4)); }));

var paymentInstructionsHtml = (data.Iban != null && data.FirstInstallmentConcept != null)
    ? $@"
        <div style='background-color: #fefce8; border: 2px solid #eab308; border-radius: 8px; padding: 20px; margin: 25px 0;'>
            <h3 style='color: #92400e; margin-top: 0; margin-bottom: 12px;'>Instrucciones para el primer pago</h3>
            <p style='margin: 6px 0;'><strong>IBAN:</strong> {WebUtility.HtmlEncode(ibanFormatted)}</p>
            <p style='margin: 6px 0;'><strong>Banco:</strong> {WebUtility.HtmlEncode(data.BankName ?? "")}</p>
            <p style='margin: 6px 0;'><strong>Destinatario:</strong> {WebUtility.HtmlEncode(data.AccountHolder ?? "")}</p>
            <p style='margin: 6px 0;'><strong>Importe:</strong> {data.FirstInstallmentAmount!.Value.ToString("N2", culture)} €</p>
            <div style='background-color: #fde68a; border-radius: 6px; padding: 14px; margin-top: 14px;'>
                <p style='margin: 0 0 6px; font-size: 13px; font-weight: bold; color: #78350f;'>
                    CONCEPTO — Usa este código exacto en tu transferencia:
                </p>
                <p style='margin: 0; font-family: monospace; font-size: 18px; letter-spacing: 1px; color: #1e3a5f;'>
                    {WebUtility.HtmlEncode(data.FirstInstallmentConcept)}
                </p>
                <p style='margin: 8px 0 0; font-size: 12px; color: #78350f;'>
                    Es imprescindible incluir este código exacto en el campo &quot;Concepto&quot; o &quot;Referencia&quot; de la transferencia para que podamos identificar tu pago.
                </p>
            </div>
        </div>"
    : "";
```

Insertar `{paymentInstructionsHtml}` en el HtmlBody entre el bloque de totales y el botón CTA.

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `src/Abuvi.API/Common/Services/IEmailService.cs` | Añadir 5 campos opcionales a `CampRegistrationEmailData` |
| `src/Abuvi.API/Features/Payments/IPaymentsService.cs` | Añadir `GetPaymentSettingsAsync` |
| `src/Abuvi.API/Features/Payments/PaymentsService.cs` | Implementar `GetPaymentSettingsAsync` (wrapping `LoadPaymentSettingsAsync`) |
| `src/Abuvi.API/Features/Registrations/RegistrationsService.cs` | Actualizar `CreateAsync` y `BuildRegistrationEmailData` para incluir datos de pago |
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Añadir bloque HTML de instrucciones de pago en `SendCampRegistrationConfirmationAsync` |

## Criterios de aceptación

- [ ] El email de confirmación contiene una sección visualmente destacada (fondo amarillo) con las instrucciones del primer plazo
- [ ] Se muestran: IBAN (formateado en grupos de 4), Banco, Destinatario, Importe (formato `N2` es-ES + €) y Concepto
- [ ] El Concepto aparece en tipografía monoespaciada, tamaño grande, con aviso explícito de que es obligatorio usarlo exactamente
- [ ] Si `payment_settings` tiene IBAN vacío o el primer plazo no existe, el bloque de pago NO aparece (null-safe)
- [ ] El email de cancelación NO incluye instrucciones de pago (no tocar `SendCampRegistrationCancellationAsync`)
- [ ] La llamada en `UpdateMembersAsync` (segundo uso de `BuildRegistrationEmailData`) también se actualiza correctamente
- [ ] Los tests unitarios existentes del email service siguen pasando

## Consideraciones no funcionales

- **Sin datos hardcodeados**: IBAN y datos bancarios siempre provienen de `AssociationSettings`
- **Resistencia a fallos**: si `GetPaymentSettingsAsync` lanza excepción, capturarla dentro del `try/catch` existente y loggear — no bloquear la inscripción
- **HTML email-safe**: solo estilos inline; sin CSS externo; compatible con Gmail y Outlook
