# fix: Suppress Resend API calls for test email addresses

## Problem

Unit tests that exercise registration and other flows are making real HTTP calls to the Resend API because `ResendEmailService` is wired with the real `IResendClient` in certain test scenarios. All test recipients follow the pattern `test-{guid}@example.com` (or similar prefixes like `login-`, `duplicate-`, `invalid-pw-` + guid + `@example.com`). These calls consume API quota, introduce network latency, and can fail if the API key is unavailable.

## Proposed Solution

Add a guard inside `ResendEmailService` that detects email addresses at the `@example.com` domain and skips the actual Resend API call, logging a debug message instead and returning early. This is a safe heuristic because:

- `example.com` is a reserved domain (RFC 2606); no real user will ever register with it.
- The one file that directly tests the `IResendClient` mock (`ResendEmailServiceTests.cs`) uses `@example.com` addresses but substitutes `IResendClient` with an NSubstitute mock, so it never reaches the real client — the guard will fire first but the test will still behave identically (the mock call is skipped, but the test only asserts on the mock anyway via `Received()`, which won't break because the guard returns before the mock is called). **See risk note below.**

## Acceptance Criteria

1. When `ResendEmailService.Send*Async()` is called with any recipient whose email ends in `@example.com`, the method returns immediately without calling `_resend.SendEmailAsync()`.
2. A `LogDebug` line is emitted for every suppressed send: `"Skipping email to test address {Email}"`.
3. All existing unit tests in `ResendEmailServiceTests.cs` continue to pass (see migration note below).
4. No changes to `IEmailService`, `IResendClient`, `ResendClientWrapper`, or any test files.

## Risk: `ResendEmailServiceTests.cs` Uses Mock + `@example.com`

`ResendEmailServiceTests.cs` wires up `ResendEmailService` with a mocked `IResendClient` and asserts `_resendClient.Received(1).SendEmailAsync(...)`. After this change the guard fires first and the mock is **never called**, so those assertions will fail with "received 0 times".

**Resolution:** Change the test email addresses in `ResendEmailServiceTests.cs` from `@example.com` to `@abuvi-test.internal` (or any non-`example.com` domain). This unblocks the guard. No other test files use `ResendEmailService` directly with a real/mock `IResendClient`.

## Files to Modify

| File | Change |
|------|--------|
| `src/Abuvi.API/Common/Services/ResendEmailService.cs` | Add private helper `IsTestAddress(string email)` and call it at the top of every `Send*Async` method (before building `EmailMessage`). |
| `src/Abuvi.Tests/Unit/Common/Services/ResendEmailServiceTests.cs` | Replace `@example.com` recipient addresses with `@abuvi-test.internal` so the guard does not suppress mock calls that tests assert on. |

## Implementation Detail

### ResendEmailService.cs — add private helper

```csharp
private static bool IsTestAddress(string email)
    => email.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase);
```

### Per-method guard (same pattern in all 14 `Send*Async` methods)

Apply at the very start of each method, before any variable declarations:

```csharp
public async Task SendVerificationEmailAsync(
    string toEmail, string firstName, string verificationToken, CancellationToken ct)
{
    if (IsTestAddress(toEmail))
    {
        _logger.LogDebug("Skipping email to test address {Email}", toEmail);
        return;
    }
    // ... existing implementation unchanged ...
}
```

For methods where the `toEmail` comes from a DTO (e.g., `CampRegistrationEmailData.ToEmail`), apply the same guard using that property:

```csharp
if (IsTestAddress(data.ToEmail))
{
    _logger.LogDebug("Skipping email to test address {Email}", data.ToEmail);
    return;
}
```

### ResendEmailServiceTests.cs — replace recipient addresses

```csharp
// Before
var toEmail = "test@example.com";

// After
var toEmail = "test@abuvi-test.internal";
```

Apply to all test methods in that file.

## Non-Functional Requirements

- No performance impact on production paths (string suffix check is O(n), negligible).
- No new dependencies.
- The guard is domain-based (`@example.com`), not prefix-based, so it catches all current and future test variants without needing to enumerate prefixes (`test-`, `login-`, `duplicate-`, etc.).

## Out of Scope

- Adding a config flag to disable email globally (not needed; test mocking already handles unit tests; only the `@example.com` escape hatch is needed).
- Modifying integration tests (`ResendEmailIntegrationTests.cs`) — these are already marked `[Fact(Skip = ...)]`.
