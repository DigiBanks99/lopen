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
