# Implementation Plan

## Current Focus: JOB-011 — OTEL Module Foundation ✅

- [x] Add OpenTelemetry packages to `Directory.Packages.props` (v1.15.0)
- [x] Upgrade `Microsoft.Extensions.*` packages from preview to stable 10.0.3
- [x] Update `Lopen.Otel.csproj` with InternalsVisibleTo, packages, project reference
- [x] Create `LopenTelemetryDiagnostics.cs` with 6 ActivitySources, 10 counters, 5 histograms, 2 gauges
- [x] Create `ServiceCollectionExtensions.cs` with `AddLopenOtel()` — master toggle, per-signal toggles, conditional OTLP export
- [x] Update `Lopen.Otel.Tests.csproj` with test dependencies
- [x] Write 54 tests: diagnostics (instruments, spans, hierarchy), DI registration, toggles, OTLP export, env var precedence
- [x] `dotnet build` (0 warnings, 0 errors), `dotnet test` (414 total, all pass)

## Current Focus: JOB-010 — TUI Module Foundation ✅

### Context

- Spectre.Tui 0.0.0-preview.0.46 is the primary TUI rendering library (game-loop + double-buffered)
- Spectre.Console 0.54.0 retained for non-TUI output
- Depends on: Lopen.Core (workflow/tasks), Lopen.Llm (token metrics), Lopen.Configuration (display settings)

### Tasks

- [x] Update `Directory.Packages.props` — Add Spectre.Tui, Spectre.Console, Spectre.Console.Json
- [x] Update `Lopen.Tui.csproj` — InternalsVisibleTo, packages, ProjectReferences (Core, Llm, Configuration)
- [x] Create core interfaces: `ITuiApplication`, `ITuiComponent`, `IComponentGallery`
- [x] Create stub implementations: `StubTuiApplication`, `ComponentGallery`
- [x] Create `ServiceCollectionExtensions.cs` — `AddLopenTui()` registering singletons
- [x] Update test csproj — Add DI, Logging, Options test packages
- [x] Write 24 tests — DI, stubs, gallery registration/lookup/duplicates/case-insensitive
- [x] `dotnet build` (0 warnings, 0 errors), `dotnet test` (437 total, all pass)
