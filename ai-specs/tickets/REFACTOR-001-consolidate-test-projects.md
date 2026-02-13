# REFACTOR-001: Consolidate Test Projects into .NET 9

## Problem Statement

Currently, there are **two separate test projects** in the codebase:

1. **`src/Abuvi.Tests`** (NET 9.0) - Contains the majority of tests (integration and unit), uses NSubstitute for mocking
2. **`tests/Abuvi.Tests`** (NET 10.0) - Contains newer tests, uses Moq for mocking

**Issues:**

- The main API project (`Abuvi.API`) targets .NET 9.0
- Having tests in .NET 10.0 creates version incompatibility
- Mixing two mocking frameworks (NSubstitute and Moq) creates inconsistency
- Duplicate project names cause confusion
- Tests are scattered across two locations

## Goal

Consolidate all tests into a **single test project** targeting **.NET 9.0** with consistent tooling (NSubstitute for mocking).

## User Story

**As a** developer
**I want to** have all unit and integration tests in a single .NET 9.0 test project
**So that** I can maintain version compatibility with the API project and have consistent testing patterns

## Acceptance Criteria

### ✅ Test Project Consolidation

- [ ] All tests from `tests/Abuvi.Tests` (NET 10.0) are migrated to `src/Abuvi.Tests` (NET 9.0)
- [ ] Test file structure follows the existing pattern:
  - Unit tests: `src/Abuvi.Tests/Unit/Features/{Feature}/{TestFile}.cs`
  - Integration tests: `src/Abuvi.Tests/Integration/Features/{TestFile}.cs`
  - Entity tests: `src/Abuvi.Tests/Unit/Data/Entities/{TestFile}.cs`

### ✅ Mocking Framework Migration

- [ ] All Moq mocks converted to NSubstitute equivalents
- [ ] Moq package reference removed from project
- [ ] All tests pass after migration

### ✅ Package Version Alignment

- [ ] All package versions match .NET 9.0 compatibility
- [ ] Remove any .NET 10.0 specific packages
- [ ] Maintain consistent package versions across test files

### ✅ Cleanup

- [ ] Delete `tests/Abuvi.Tests` project directory completely
- [ ] Update solution file (if needed) to remove reference to deleted project
- [ ] Verify no broken references or build errors

### ✅ Validation

- [ ] All migrated tests pass successfully
- [ ] No duplicate test files exist
- [ ] Test coverage remains the same or improves
- [ ] CI/CD pipeline runs successfully with consolidated tests

## Current Test Inventory

### Files to Migrate from `tests/Abuvi.Tests` (NET 10.0)

```
tests/Abuvi.Tests/
├── Unit/
│   ├── Data/Entities/
│   │   └── UserTests.cs
│   └── Features/
│       ├── Auth/
│       │   ├── AuthEndpointsTests_Registration.cs
│       │   ├── AuthServiceTests_Registration.cs
│       │   ├── RegisterUserRequestTests.cs
│       │   └── RegisterUserValidatorTests.cs
│       └── Users/
│           └── UsersServiceRoleUpdateTests.cs
```

### Existing Tests in `src/Abuvi.Tests` (NET 9.0)

```
src/Abuvi.Tests/
├── Integration/
│   ├── HealthCheckTests.cs
│   └── Features/
│       ├── AuthIntegrationTests.cs
│       └── ProtectedEndpointsTests.cs
└── Unit/
    └── Features/
        ├── Auth/
        │   ├── AuthServiceTests.cs
        │   ├── AuthValidatorsTests.cs
        │   ├── JwtTokenServiceTests.cs
        │   └── PasswordHasherTests.cs
        ├── UsersServiceTests.cs
        └── UsersValidatorsTests.cs
```

## Migration Steps (TDD Approach)

Since this is a refactoring task, the existing tests ARE the specification. The process is:

### Step 1: Prepare Target Structure

1. Review existing test organization in `src/Abuvi.Tests`
2. Create any missing folders needed for migrated tests

### Step 2: Migrate Tests File-by-File

For each file in `tests/Abuvi.Tests`:

1. **Copy** test file to appropriate location in `src/Abuvi.Tests`
2. **Convert Moq to NSubstitute** syntax:
   - `Mock<T>` → `Substitute.For<T>()`
   - `.Setup(x => x.Method()).Returns(value)` → `.Method().Returns(value)`
   - `.Verify(x => x.Method(), Times.Once)` → `Received(1).Method()`
3. **Update namespaces** to match new location
4. **Run tests** to ensure they pass
5. **Verify no test duplication** (merge if similar tests exist)

### Step 3: Check for Conflicts/Duplicates

- Compare `AuthServiceTests.cs` (NET 9.0) vs `AuthServiceTests_Registration.cs` (NET 10.0)
- Determine if tests should be merged or kept separate
- Ensure no duplicate test coverage

### Step 4: Package Cleanup

1. Remove Moq from `src/Abuvi.Tests.csproj`
2. Ensure BCrypt.Net-Next version matches API project
3. Ensure Resend package version matches API project

### Step 5: Delete Old Project

1. Delete entire `tests/Abuvi.Tests` directory
2. Update solution file if needed
3. Run full test suite to confirm all pass

### Step 6: Validation

1. Run `dotnet test` from solution root
2. Verify test count matches or exceeds previous total
3. Check test output for any warnings or errors

## Technical Notes

### Moq to NSubstitute Conversion Guide

| Moq Syntax | NSubstitute Syntax |
|------------|-------------------|
| `var mock = new Mock<IService>();` | `var mock = Substitute.For<IService>();` |
| `mock.Setup(x => x.Get()).Returns(value);` | `mock.Get().Returns(value);` |
| `mock.Setup(x => x.Get(It.IsAny<int>())).Returns(value);` | `mock.Get(Arg.Any<int>()).Returns(value);` |
| `mock.Verify(x => x.Method(), Times.Once);` | `mock.Received(1).Method();` |
| `mock.Verify(x => x.Method(), Times.Never);` | `mock.DidNotReceive().Method();` |
| `mock.Object` | `mock` (no need to unwrap) |

### Package Versions (Target - NET 9.0)

```xml
<PackageReference Include="xunit" Version="2.9.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
<PackageReference Include="FluentAssertions" Version="7.0.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.*" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
<PackageReference Include="coverlet.collector" Version="6.*" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Resend" Version="0.2.1" />
```

## Definition of Done

- [ ] Single test project exists: `src/Abuvi.Tests` (NET 9.0)
- [ ] No `tests/Abuvi.Tests` directory exists
- [ ] All tests use NSubstitute (no Moq references)
- [ ] All tests pass: `dotnet test` returns 0 failures
- [ ] No duplicate tests exist
- [ ] Test coverage maintained or improved
- [ ] CI/CD pipeline passes
- [ ] Code review approved
- [ ] Documentation updated (if applicable)

## Estimated Effort

- **Complexity**: Low to Medium
- **Estimated Time**: 2-3 hours
- **Risk Level**: Low (refactoring with existing test coverage)

## Dependencies

None - this is a standalone refactoring task

## References

- Memory: `MEMORY.md` - Testing standards and TDD requirements
- Current test projects:
  - `src/Abuvi.Tests/Abuvi.Tests.csproj` (NET 9.0)
  - `tests/Abuvi.Tests/Abuvi.Tests.csproj` (NET 10.0) - to be removed

## Implementation Command

Use `/develop-backend` with this ticket to execute the consolidation following TDD principles.
