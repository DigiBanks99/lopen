# Implementation Plan

## Current Focus: Auth Module P2 — JOB-012 through JOB-016, JOB-019, JOB-020, JOB-021

### Context

- Auth foundation exists: `IAuthService`, `ITokenSourceResolver`, `EnvironmentTokenSourceResolver`, `StubAuthService`, `AuthState`, `AuthCredentialSource`, `AuthStatusResult`, `TokenSourceResult`, `AuthenticationException`
- 30 existing auth tests pass
- Copilot SDK (v0.1.23) wraps CLI; no direct device flow API
- SDK auth chain: explicit token → env vars (`COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN`) → stored OAuth creds → `gh auth` creds
- Strategy: delegate device flow to `gh` CLI — well-tested, already in SDK fallback chain, available in environment
- Lopen stores zero credentials; all credential storage delegated to `gh` CLI or env vars

### Architecture

| Type | Location | Purpose |
|------|----------|---------|
| `IGhCliAdapter` | `Lopen.Auth/` | Interface wrapping `gh` CLI operations |
| `GhCliAdapter` | `Lopen.Auth/` | Implementation via `System.Diagnostics.Process` |
| `GhAuthStatusInfo` | `Lopen.Auth/` | Record — parsed `gh auth status` output |
| `CopilotAuthService` | `Lopen.Auth/` | Real `IAuthService` using `IGhCliAdapter` + `ITokenSourceResolver` |
| `AuthErrorMessages` | `Lopen.Auth/` | Static class — what/why/how-to-fix error constants |

### Tasks

- [x] Create `IGhCliAdapter` interface and `GhAuthStatusInfo` record
- [x] Create `GhCliAdapter` implementation (`gh auth login`, `gh auth status`, `gh auth logout`, `gh api user` validation)
- [x] Create `AuthErrorMessages` static class (what/why/how-to-fix pattern for all failure modes)
- [x] Create `CopilotAuthService` implementation (login delegates to `gh auth login --web`, status merges env var + `gh auth status` + credential validation, logout calls `gh auth logout` with env var warning)
- [x] Update `ServiceCollectionExtensions` to register `IGhCliAdapter`→`GhCliAdapter`, `IAuthService`→`CopilotAuthService`
- [x] Write `CopilotAuthServiceTests` — 28 tests covering login/status/logout flows, env var precedence, headless error, invalid credentials, error messages
- [x] Write `GhCliAdapterTests` — 12 tests covering output parsing, error handling, record equality
- [x] Write `AuthErrorMessagesTests` — 12 tests verifying what/why/how-to-fix pattern, Copilot Requests guidance
- [x] Update `ServiceCollectionExtensionsTests` for new registrations (IGhCliAdapter, CopilotAuthService)
- [x] Update auth `SPECIFICATION.md` Notes section re: `gh` CLI delegation strategy
- [x] Update `jobs-to-be-done.json` with completion status — JOB-012 through JOB-016, JOB-019, JOB-020, JOB-021 ✅
- [x] All 490 tests pass (82 auth, 408 other modules)

### Job Coverage

| Job | Scope | Key ACs |
|-----|-------|---------|
| JOB-012 | Device flow login | `gh auth login --web` delegation, interactive TTY check |
| JOB-013 | Auth status | Merge env var detection + `gh auth status` parsing |
| JOB-014 | Logout | `gh auth logout` delegation, env var override warning |
| JOB-015 | Headless error | Non-interactive detection, clear error with instructions |
| JOB-016 | Token precedence | `COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN` → `gh auth` |
| JOB-019 | Error messages | `AuthErrorMessages` with what/why/how-to-fix for every failure mode |
| JOB-020 | No stored creds | Verify Lopen writes zero credential files; delegation only |
| JOB-021 | Unit tests | Full coverage of all auth ACs via mocked dependencies |
