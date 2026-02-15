# Implementation Plan

## Current Focus: JOB-006 — Configuration Module ✅

- [x] Add `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Configuration.Json`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Configuration.EnvironmentVariables`, `Microsoft.Extensions.Options` to `Directory.Packages.props`
- [x] Add package references to `Lopen.Configuration.csproj`
- [x] Create `LopenOptions.cs` — Root options POCO with nested classes: `ModelOptions`, `BudgetOptions`, `OracleOptions`, `WorkflowOptions`, `SessionOptions`, `GitOptions`, `ToolDisciplineOptions`, `DisplayOptions`. All with sensible defaults from the spec.
- [x] Create `LopenOptionsValidator.cs` — Manual validation with aggregated errors (budget thresholds 0–1, warning < confirmation, positive integers, etc.)
- [x] Create `LopenConfigurationBuilder.cs` — Builds `IConfigurationRoot` with layered sources: built-in defaults → global config → project config → environment variables → CLI overrides. Binds to `LopenOptions` and validates.
- [x] Create `ConfigurationDiagnostics.cs` — Helper to iterate `IConfigurationRoot.Providers` and report each setting's value and source
- [x] Create `ServiceCollectionExtensions.cs` in `Lopen.Configuration` with `AddLopenConfiguration()` method
- [x] Write unit tests for `LopenOptions` defaults (8 tests)
- [x] Write unit tests for `LopenOptionsValidator` — valid and invalid scenarios (12 tests)
- [x] Write unit tests for `LopenConfigurationBuilder` — layered resolution, higher priority wins (9 tests)
- [x] Write unit tests for `ConfigurationDiagnostics` (7 tests)
- [x] Write unit tests for `ServiceCollectionExtensions` (3 tests + 1 existing placeholder)
- [x] Wire `Lopen.Configuration` into `Program.cs` (call `AddLopenConfiguration`)
- [x] Verify `dotnet build` and `dotnet test` pass (43 configuration tests, 50 total)
- [x] Run `dotnet format --verify-no-changes`
