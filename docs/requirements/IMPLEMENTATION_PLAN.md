# Implementation Plan

## Current Job: JOB-080 — Implement `lopen session` CLI Subcommands

**Module**: cli  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Implement `lopen session list`, `show`, `resume`, `delete`, and `prune` subcommands wired to the Storage module's `ISessionManager`.

### Acceptance Criteria

- [x] AC9: `lopen session list` lists all sessions (active and completed) with latest marker
- [x] AC10: `lopen session show [id]` displays session details with optional `--format md|json|yaml` flag
- [x] AC11: `lopen session resume [id]` sets the session as latest (validates not complete)
- [x] AC12: `lopen session delete <id>` deletes a session via new `DeleteSessionAsync`
- [x] AC13: `lopen session prune` removes sessions beyond configured `session_retention` limit

### Tasks

- [x] **1. Add `DeleteDirectory` to `IFileSystem`** — New method in interface, `PhysicalFileSystem`, and all test implementations
- [x] **2. Add `DeleteSessionAsync` to `ISessionManager`** — Interface + `SessionManager` implementation using `DeleteDirectory`
- [x] **3. Create `SessionCommand.cs`** in `src/Lopen/Commands/` — 5 subcommands following AuthCommand pattern with TextWriter injection
- [x] **4. Wire in `Program.cs`** — `rootCommand.Add(SessionCommand.Create(host.Services))`
- [x] **5. Implement `FormatSession` output** — Markdown (default), JSON, and YAML formatters for session show
- [x] **6. Create `FakeSessionManager`** in `tests/Lopen.Cli.Tests/Fakes/` — Configurable test double
- [x] **7. Write `SessionCommandTests`** — 28 tests covering all subcommands, formats, error paths
- [x] **8. Write `SessionManager` delete tests** — 3 tests (happy path, not found, null)
- [x] **9. Validate** — `dotnet build`, `dotnet test` (1113 tests), `dotnet format` all pass

### Files Created/Modified

| File | Action |
|------|--------|
| `src/Lopen/Commands/SessionCommand.cs` | **Created** — 5 subcommands with format support |
| `src/Lopen/Program.cs` | **Modified** — wired SessionCommand |
| `src/Lopen.Storage/IFileSystem.cs` | **Modified** — added `DeleteDirectory` |
| `src/Lopen.Storage/PhysicalFileSystem.cs` | **Modified** — implemented `DeleteDirectory` |
| `src/Lopen.Storage/ISessionManager.cs` | **Modified** — added `DeleteSessionAsync` |
| `src/Lopen.Storage/SessionManager.cs` | **Modified** — implemented `DeleteSessionAsync` |
| `tests/Lopen.Cli.Tests/Commands/SessionCommandTests.cs` | **Created** — 28 tests |
| `tests/Lopen.Cli.Tests/Fakes/FakeSessionManager.cs` | **Created** — test double |
| `tests/Lopen.Storage.Tests/SessionManagerTests.cs` | **Modified** — 3 delete tests |
| `tests/Lopen.Storage.Tests/InMemoryFileSystem.cs` | **Modified** — `DeleteDirectory` |
| `tests/Lopen.Core.Tests/InMemoryFileSystem.cs` | **Modified** — `DeleteDirectory` |
| Various test stubs | **Modified** — added `DeleteDirectory`/`DeleteSessionAsync` |

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-080 | cli | Session CLI subcommands (list/show/resume/delete/prune) |
| JOB-079 | cli | Auth CLI subcommands (login/status/logout) |
| JOB-075 | core | Core AC tests (all 24 ACs) |
| JOB-018 | llm | Automatic token renewal and failed renewal handling |