# Implementation Plan

## Current Focus: JOB-001, JOB-002, JOB-003 — Solution Foundation

### JOB-001: Solution Structure ✅

- [x] Create `global.json` pinning SDK 10.0.100 with `rollForward: latestMinor`
- [x] Create `Directory.Build.props` (LangVersion preview, Nullable enable, TreatWarningsAsErrors true, ImplicitUsings enable)
- [x] Create `Directory.Packages.props` for centralized package version management
- [x] Create `Lopen.sln` solution file
- [x] Create `src/Lopen/` console app project (OutputType Exe, net10.0)
- [x] Create class library projects: `src/Lopen.Core/`, `Lopen.Llm/`, `Lopen.Storage/`, `Lopen.Configuration/`, `Lopen.Auth/`, `Lopen.Tui/`, `Lopen.Otel/` — each as empty skeletons with a namespace marker class
- [x] Add all src projects to the solution and verify `dotnet build` succeeds

### JOB-002: CLI Host ✅

- [x] Add centralized package versions for `Microsoft.Extensions.Hosting` and `System.CommandLine` to `Directory.Packages.props`
- [x] Add `Microsoft.Extensions.Hosting` and `System.CommandLine` package references to `Lopen.Cli` (src/Lopen/) project
- [x] Add project references from `Lopen.Cli` to `Lopen.Core` (only the immediate dependency needed for DI registration)
- [x] Implement `Program.cs` with `Host.CreateApplicationBuilder`, DI container setup via `IServiceCollection`, and a placeholder root command using `System.CommandLine`
- [x] Create `IServiceCollection` extension method stubs in `Lopen.Core` for service registration pattern
- [x] Verify `dotnet run --project src/Lopen` executes without error

### JOB-003: Tests & Formatting ✅

- [x] Create test projects under `tests/` for each module (`Lopen.Core.Tests`, `Lopen.Llm.Tests`, `Lopen.Storage.Tests`, `Lopen.Configuration.Tests`, `Lopen.Auth.Tests`, `Lopen.Tui.Tests`, `Lopen.Otel.Tests`, `Lopen.Cli.Tests`) using xunit, each referencing its corresponding src project
- [x] Add centralized package versions for `xunit`, `Microsoft.NET.Test.Sdk`, and `xunit.runner.visualstudio` to `Directory.Packages.props`
- [x] Add all test projects to the solution
- [x] Add a single passing placeholder test in each test project to confirm the test runner works
- [x] Create `.editorconfig` with C# formatting rules consistent with `dotnet format`
- [x] Run `dotnet format --verify-no-changes` and fix any violations
- [x] Run `dotnet test` across the full solution and verify all tests pass
