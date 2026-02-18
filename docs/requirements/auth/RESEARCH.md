---
date: 2026-02-15
sources:
  - https://cli.github.com/manual/gh_auth
  - https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow
  - https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli
---

# Auth Module Research

## 1. Authentication Architecture

Lopen's auth module delegates all authentication operations to the **GitHub CLI (`gh`)** via the `IGhCliAdapter`/`GhCliAdapter` abstraction. The `gh` CLI must be installed and available on `PATH`. There is no direct dependency on the Copilot SDK or `CopilotClient` for authentication — the auth module is a standalone layer that resolves credentials before any SDK interaction.

### Architecture

```
Lopen (.NET Application)
       ↓
  CopilotAuthService (IAuthService)
       ├── ITokenSourceResolver          → Environment variables (GH_TOKEN, GITHUB_TOKEN)
       └── IGhCliAdapter (GhCliAdapter)  → gh auth login / status / logout
              ↓ Process.Start
         gh CLI (subprocess, 30s timeout)
              ↓ HTTPS
         GitHub API
```

### Key Components

| Component | Responsibility |
|---|---|
| `IAuthService` / `CopilotAuthService` | Orchestrates auth operations: login, logout, status, pre-flight validation |
| `ITokenSourceResolver` / `EnvironmentTokenSourceResolver` | Resolves environment variable tokens (`GH_TOKEN` → `GITHUB_TOKEN` → None) |
| `IGhCliAdapter` / `GhCliAdapter` | Wraps `gh` CLI process execution with 30-second timeout |
| `AuthStatusResult` | Immutable result record: `State`, `Source`, `Username?`, `ErrorMessage?` |

### Authentication Priority (Implemented)

`CopilotAuthService.GetStatusAsync()` checks sources in this order:

1. **`GH_TOKEN`** environment variable (via `ITokenSourceResolver`)
2. **`GITHUB_TOKEN`** environment variable (via `ITokenSourceResolver`)
3. **`gh auth` stored credentials** — parsed from `gh auth status` output, then validated with `gh api user --jq .login`

## 2. Login via gh CLI Device Flow

### How `lopen auth login` Works (Implemented)

Login delegates entirely to `gh auth login --git-protocol https --web`. The `--web` flag triggers the GitHub OAuth device flow, which opens a browser for the user to authorize. The `gh` CLI handles the full device flow lifecycle: code generation, browser opening, polling, and credential storage.

`CopilotAuthService.LoginAsync()` performs these steps:

1. **Check interactivity** — blocks login in non-interactive environments (no TTY / redirected stdin) with `AuthErrorMessages.HeadlessLoginNotSupported`
2. **Check gh CLI availability** — calls `gh --version` via `IGhCliAdapter.IsAvailableAsync()`; throws `AuthErrorMessages.GhCliNotFound` if not found
3. **Run `gh auth login`** — delegates to `GhCliAdapter.LoginAsync()` which runs `gh auth login --git-protocol https --web` with stdout/stderr passed through to the user (not redirected)
4. **Verify success** — calls `gh auth status` to confirm credentials were stored, extracts username

### Key Implementation Details

- Login runs the `gh` process with **`redirectOutput: false`**, meaning the device flow UI (user code, verification URL) is displayed directly to the user's terminal
- The `gh` CLI stores credentials in its own credential store (platform keychain on macOS/Windows, encrypted file on Linux)
- After login, a verification call to `gh auth status` confirms the credentials are stored and extracts the username
- The `GhCliAdapter` enforces a **30-second process timeout** on all operations (though login runs without output redirection, so the timeout applies to `WaitForExitAsync`)

### Headless Environments

In non-interactive environments (CI/CD, containers, piped input), `LoginAsync` throws immediately with a message directing users to set `GH_TOKEN` with a fine-grained PAT that has the "Copilot Requests" permission.

### Underlying Device Flow (handled by gh CLI)

The `gh` CLI implements the standard GitHub OAuth device flow (RFC 8628):

1. **Request device code** — `POST https://github.com/login/device/code` with `client_id` and `scope`
2. **Display to user** — shows `user_code` and `verification_uri`, opens browser
3. **Poll for completion** — polls `POST https://github.com/login/oauth/access_token` at the specified interval
4. **Store token** — saves `access_token` to the platform credential store

Lopen does not implement any of this directly — it is fully owned by the `gh` CLI binary.

## 3. Environment Variable Authentication

### How GH_TOKEN / GITHUB_TOKEN Work (Implemented)

`EnvironmentTokenSourceResolver` checks environment variables synchronously. When a token is found, `CopilotAuthService.GetStatusAsync()` returns `Authenticated` immediately without calling the `gh` CLI.

```csharp
// EnvironmentTokenSourceResolver.Resolve() checks in order:
// 1. GH_TOKEN — if non-empty, returns AuthCredentialSource.GhToken
// 2. GITHUB_TOKEN — if non-empty, returns AuthCredentialSource.GitHubToken
// 3. Otherwise — returns AuthCredentialSource.None (falls through to gh CLI check)
```

### Environment Variable Priority (Implemented)

1. `GH_TOKEN` — GitHub CLI compatible (highest precedence)
2. `GITHUB_TOKEN` — GitHub Actions compatible

### Supported Token Types

| Prefix | Type | Supported |
|---|---|---|
| `gho_` | OAuth user access tokens | ✅ Yes |
| `ghu_` | GitHub App user access tokens | ✅ Yes |
| `github_pat_` | Fine-grained personal access tokens | ✅ Yes |
| `ghp_` | Classic personal access tokens | ❌ Deprecated / not supported |

### Token Requirements

Per the Lopen specification, PATs must have the **"Copilot Requests"** permission enabled. This is a fine-grained PAT permission (not a classic scope).

## 4. Credential Validation

### How Credential Validation Works (Implemented)

`CopilotAuthService.GetStatusAsync()` performs a two-step validation for `gh` CLI credentials:

1. **`gh auth status`** — checks whether credentials are stored; parses output for username, active status, and token scopes
2. **`gh api user --jq .login`** — makes a lightweight API call to verify credentials are actually functional (not expired/revoked)

If `gh auth status` succeeds but `gh api user` fails, the status is reported as `AuthState.InvalidCredentials` rather than `Authenticated`.

For environment variable tokens (`GH_TOKEN` / `GITHUB_TOKEN`), only presence is checked — no API validation is performed. Invalid tokens will fail at first use.

### Pre-flight Validation (Implemented)

`ValidateAsync()` calls `GetStatusAsync()` and maps the result:

| State | Behavior |
|---|---|
| `Authenticated` | Returns successfully — credentials are valid |
| `InvalidCredentials` | Throws `AuthenticationException` with `AuthErrorMessages.InvalidCredentials` |
| `NotAuthenticated` | Throws `AuthenticationException` with `AuthErrorMessages.PreFlightFailed` |

### Token Refresh

The auth module does **not** implement automatic token refresh. The `gh` CLI manages its own stored token lifecycle. For environment variable tokens (PATs), there is no refresh mechanism — expired tokens produce errors at the point of use.

## 5. Dependencies

### Actual NuGet Packages (Lopen.Auth.csproj)

| Package | Purpose |
|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `IServiceCollection` for DI registration via `AddLopenAuth()` |
| `Microsoft.Extensions.Logging.Abstractions` | `ILogger<T>` for structured logging in `GhCliAdapter` and `CopilotAuthService` |

### External Dependencies

| Dependency | Purpose |
|---|---|
| `gh` CLI (runtime) | Required on `PATH` for interactive login, status checks, logout, and credential validation |

### Not Needed

- **No `GitHub.Copilot.SDK`** — auth is fully independent of the Copilot SDK
- **No Octokit** — Lopen does not need direct GitHub API access for auth
- **No custom OAuth library** — the device flow is owned by the `gh` CLI

## 6. Implementation (Actual)

### Architecture

```
Lopen.Auth (module)
├── IAuthService                          # Interface: login, logout, status, validate
├── CopilotAuthService                    # Orchestrates env vars + gh CLI delegation
├── ITokenSourceResolver                  # Interface: resolve env var token source
├── EnvironmentTokenSourceResolver        # GH_TOKEN → GITHUB_TOKEN → None
├── IGhCliAdapter                         # Interface: gh CLI process operations
├── GhCliAdapter                          # Process.Start wrapper with 30s timeout
├── AuthStatusResult                      # Record: State, Source, Username?, ErrorMessage?
├── TokenSourceResult                     # Record: Source, Token?
├── GhAuthStatusInfo                      # Record: Username, IsActive, TokenScopes?
├── AuthCredentialSource                  # Enum: None, GhToken, GitHubToken, SdkCredentials
├── AuthState                             # Enum: Authenticated, NotAuthenticated, InvalidCredentials
├── AuthErrorMessages                     # Static error message strings (what/why/fix pattern)
├── AuthenticationException               # Custom exception for auth failures
└── ServiceCollectionExtensions           # DI: AddLopenAuth() registration
```

### lopen auth login (Implemented)

```csharp
// CopilotAuthService.LoginAsync():
// 1. Block non-interactive environments (no TTY / redirected stdin)
// 2. Check gh CLI availability via IGhCliAdapter.IsAvailableAsync()
// 3. Delegate to IGhCliAdapter.LoginAsync()

// GhCliAdapter.LoginAsync():
// Runs: gh auth login --git-protocol https --web
// - redirectOutput: false (user sees device flow UI directly)
// - Verifies success via GetStatusAsync() after process exits
// - Returns authenticated username
```

### lopen auth status (Implemented)

```csharp
// CopilotAuthService.GetStatusAsync():
// 1. Check ITokenSourceResolver.Resolve() for GH_TOKEN / GITHUB_TOKEN
//    → returns Authenticated with GhToken or GitHubToken source
// 2. Call IGhCliAdapter.GetStatusAsync() (runs: gh auth status)
//    → parses output for username, active status, token scopes
// 3. If gh status found, call IGhCliAdapter.ValidateCredentialsAsync()
//    (runs: gh api user --jq .login)
//    → if invalid: returns InvalidCredentials with username
//    → if valid: returns Authenticated with SdkCredentials source
// 4. Otherwise: returns NotAuthenticated
```

### lopen auth logout (Implemented)

```csharp
// CopilotAuthService.LogoutAsync():
// 1. Delegate to IGhCliAdapter.LogoutAsync()
//    (runs: gh auth logout --hostname github.com, sends "Y\n" to confirm)
// 2. Check ITokenSourceResolver.Resolve() for leftover env vars
//    → warns if GH_TOKEN or GITHUB_TOKEN is still set
```

### Pre-flight Auth Check (Implemented)

```csharp
// CopilotAuthService.ValidateAsync():
// 1. Call GetStatusAsync()
// 2. Authenticated → return (success)
// 3. InvalidCredentials → throw AuthenticationException(InvalidCredentials)
// 4. NotAuthenticated → throw AuthenticationException(PreFlightFailed)
```

### GhCliAdapter Process Management

All `gh` CLI operations go through `GhCliAdapter.RunAsync()`:

- Spawns `gh` with `Process.Start()` via an injected `Func<ProcessStartInfo, Process?>` (testable)
- Enforces a **30-second timeout** via `WaitForExitAsync().WaitAsync(ProcessTimeout)`
- Kills the process in a `finally` block if it hasn't exited
- Captures stdout/stderr when `redirectOutput: true`; passes through to terminal when `false` (login flow)
- Supports sending stdin input (used for logout confirmation: `"Y\n"`)

### DI Registration

```csharp
// ServiceCollectionExtensions.AddLopenAuth():
services.AddSingleton<ITokenSourceResolver, EnvironmentTokenSourceResolver>();
services.AddSingleton<IGhCliAdapter, GhCliAdapter>();
services.AddSingleton<IAuthService, CopilotAuthService>();
```

---

## Relevance to Lopen

### Key Findings

1. **Auth delegates to `gh` CLI, not the Copilot SDK.** `CopilotAuthService` orchestrates `ITokenSourceResolver` (environment variables) and `IGhCliAdapter` (`gh` CLI subprocess). There is no dependency on `GitHub.Copilot.SDK` or `CopilotClient` for authentication.

2. **Device flow uses `gh auth login --web`.** Login runs the `gh` CLI with stdout/stderr passed through to the terminal, giving users the standard `gh` authentication UX. No custom OAuth implementation is needed.

3. **Environment variable precedence is `GH_TOKEN` → `GITHUB_TOKEN`.** Checked synchronously by `EnvironmentTokenSourceResolver`. The `COPILOT_GITHUB_TOKEN` variable is **not** supported by the current implementation.

4. **Credential validation is two-step.** `gh auth status` confirms credentials exist; `gh api user --jq .login` confirms they are functional. This catches expired/revoked tokens before workflow execution.

5. **Pre-flight validation via `ValidateAsync()`.** Maps `GetStatusAsync()` results to success/exception, providing actionable what/why/fix error messages via `AuthErrorMessages`.

6. **30-second process timeout.** All `gh` CLI operations are bounded by `TimeSpan.FromSeconds(30)` in `GhCliAdapter.RunAsync()`. Processes that exceed this are killed.

7. **Testability via constructor injection.** `GhCliAdapter` accepts a `Func<ProcessStartInfo, Process?>` for process creation. `EnvironmentTokenSourceResolver` accepts a `Func<string, string?>` for environment variable access. `CopilotAuthService` accepts a `Func<bool>` for interactivity checking.

### Resolved Questions

- **Auth commands**: `gh auth login`, `gh auth status`, `gh auth logout` — all standard `gh` CLI subcommands, not TUI slash commands
- **PAT auth method**: Set `GH_TOKEN` or `GITHUB_TOKEN` with a fine-grained PAT that has "Copilot Requests" permission
- **Credential storage**: Managed entirely by the `gh` CLI (platform keychain on macOS/Windows, encrypted file on Linux)
- **Username retrieval**: Parsed from `gh auth status` output using regex (`account\s+(?<username>\S+)`)
- **Logout confirmation**: `GhCliAdapter` sends `"Y\n"` to stdin to confirm `gh auth logout --hostname github.com`

### Open Questions

- **`SdkCredentials` enum naming**: The `AuthCredentialSource.SdkCredentials` value is used for `gh` CLI stored credentials — consider renaming to `GhCliCredentials` for clarity
- **Token scope validation**: `GhAuthStatusInfo` captures token scopes but they are not currently validated against required permissions (e.g., "Copilot Requests")
- **Environment variable token validation**: Tokens from `GH_TOKEN`/`GITHUB_TOKEN` are accepted on presence alone without an API call — invalid tokens only fail at point of use

---

## AUTH-10: Pre-flight Auth Check Wiring

### Current State Analysis

`IAuthService.ValidateAsync()` is fully implemented in `CopilotAuthService` (lines 120–137 of `CopilotAuthService.cs`). It calls `GetStatusAsync()`, returns on `Authenticated`, and throws `AuthenticationException` for `InvalidCredentials` or `NotAuthenticated` states. Error messages follow the what/why/how-to-fix pattern via `AuthErrorMessages`.

**The problem: `ValidateAsync` is never called.** No consumer in the entire codebase invokes it — not the `WorkflowOrchestrator`, not `RootCommandHandler`, not `PhaseCommands`, and not any middleware or pipeline. This means a user with missing or invalid credentials will only discover the failure when the first LLM call fails deep inside the workflow loop, producing a confusing error instead of a clear upfront message.

#### Key files involved

| File | Role |
|---|---|
| `src/Lopen.Auth/IAuthService.cs` | Interface defining `ValidateAsync` |
| `src/Lopen.Auth/CopilotAuthService.cs` | Production implementation (singleton) |
| `src/Lopen/Commands/RootCommandHandler.cs` | Root `lopen` command — launches TUI or headless workflow |
| `src/Lopen/Commands/PhaseCommands.cs` | `spec`, `plan`, `build` subcommands — each calls `orchestrator.RunAsync()` |
| `src/Lopen.Core/Workflow/WorkflowOrchestrator.cs` | Orchestration loop — `RunAsync()` entry point |
| `src/Lopen.Auth/ServiceCollectionExtensions.cs` | DI: `AddSingleton<IAuthService, CopilotAuthService>()` |

#### What already works

- `IAuthService` is registered as a singleton via `AddLopenAuth()` in `ServiceCollectionExtensions`
- `ValidateAsync` correctly delegates to `GetStatusAsync()` and throws `AuthenticationException` with actionable messages
- `FakeAuthService` in tests already tracks `ValidateCalled` and supports configurable exceptions
- `AuthenticationException` is caught by the generic `catch (Exception ex)` in all command handlers (writes `ex.Message` to stderr, returns `ExitCodes.Failure`)

### Recommended Approach

#### Option A: Inject into `PhaseCommands` as a shared helper (Recommended)

Add a `ValidateAuthAsync` helper in `PhaseCommands` following the exact pattern of `ValidateSpecExistsAsync` and `ValidatePlanExistsAsync`:

```csharp
// In PhaseCommands.cs
internal static async Task<string?> ValidateAuthAsync(
    IServiceProvider services, CancellationToken cancellationToken)
{
    var authService = services.GetService<IAuthService>();
    if (authService is null)
        return null; // Auth module not registered; skip check

    try
    {
        await authService.ValidateAsync(cancellationToken);
        return null; // Auth valid
    }
    catch (AuthenticationException ex)
    {
        return ex.Message;
    }
}
```

Then call it early in each command handler (spec, plan, build) and in `RootCommandHandler.RunHeadlessAsync`:

```csharp
// At the top of each phase command action, after headless validation:
var authError = await ValidateAuthAsync(services, cancellationToken);
if (authError is not null)
{
    await stderr.WriteLineAsync(authError);
    return ExitCodes.AuthFailure; // New exit code, or ExitCodes.Failure
}
```

**Why this approach:**

1. **Follows existing patterns** — identical to `ValidateSpecExistsAsync` / `ValidatePlanExistsAsync` / `ValidateHeadlessPromptAsync`
2. **Fails fast** — blocks before any session resolution, module scanning, or orchestrator creation
3. **Graceful degradation** — `GetService<IAuthService>()` returns null if auth module isn't registered (e.g., test environments without auth)
4. **Clear error messages** — `AuthErrorMessages` already has actionable what/why/fix text
5. **No orchestrator changes** — keeps `WorkflowOrchestrator` focused on workflow logic

#### Option B: Inject into `WorkflowOrchestrator.RunAsync` (Alternative)

Add `IAuthService?` as an optional constructor parameter and call `ValidateAsync` at the top of `RunAsync`, before git branch setup:

```csharp
// In WorkflowOrchestrator constructor (add optional parameter):
IAuthService? authService = null

// At the top of RunAsync, after argument validation:
if (_authService is not null)
{
    await _authService.ValidateAsync(cancellationToken);
    // Throws AuthenticationException on failure — caught by command handler
}
```

**Why this is less ideal:**

- Adds a cross-cutting concern (auth) to the workflow layer, which currently depends only on `Lopen.Core` abstractions
- Would require adding a project reference from `Lopen.Core` to `Lopen.Auth` (or defining `IAuthService` in Core)
- `AuthenticationException` would propagate up to command handlers anyway, so the error handling is less explicit
- The orchestrator has 11 required + 10 optional constructor parameters already; adding more increases complexity

#### Option C: Inject into `RootCommandHandler` only (Insufficient)

Only adding the check to `RootCommandHandler` would miss the `spec`, `plan`, and `build` subcommands which each independently call the orchestrator.

### Recommended Injection Points (Option A)

Call `ValidateAuthAsync` at these exact locations:

1. **`PhaseCommands.CreateSpec`** — after `ValidateHeadlessPromptAsync` (line 29), before `ResolveSessionAsync` (line 32)
2. **`PhaseCommands.CreatePlan`** — after `ValidateHeadlessPromptAsync` (line 97), before `ResolveSessionAsync` (line 100)
3. **`PhaseCommands.CreateBuild`** — after `ValidateHeadlessPromptAsync` (line 172), before `ResolveSessionAsync` (line 175)
4. **`RootCommandHandler.Configure`** (headless branch) — after `ValidateHeadlessPromptAsync` (line 40), before `RunHeadlessAsync` (line 48)
5. **`RootCommandHandler.Configure`** (interactive branch) — before `ResolveSessionAsync` (line 52), so the TUI never launches without valid auth

### Code Patterns to Follow

#### Validation helper pattern (from `PhaseCommands`)

All existing validators follow this contract:
- Return `string?` — `null` means valid, non-null is an error message
- Use `services.GetService<T>()` (nullable) not `GetRequiredService<T>()`
- Caller writes error to stderr and returns exit code

#### Exception-to-message conversion

`ValidateAsync` throws `AuthenticationException`. The helper catches it and returns `ex.Message`, which contains the what/why/fix text from `AuthErrorMessages`. This matches how `AuthCommand` handles auth errors.

#### Exit code consideration

Currently `ExitCodes` has `Success`, `Failure`, and `UserInterventionRequired`. Consider adding `AuthFailure = 4` (or similar) for machine-readable exit codes in headless/CI mode. If not, reuse `ExitCodes.Failure`.

#### DI wiring

No additional DI changes needed. `IAuthService` is already registered as a singleton in `AddLopenAuth()`. The `Lopen` CLI project already references `Lopen.Auth` (it hosts `AuthCommand`).

### Test Strategy

#### Unit Tests for `ValidateAuthAsync` helper (in `Lopen.Cli.Tests`)

| Test | Description |
|---|---|
| `ValidateAuthAsync_ReturnsNull_WhenAuthServiceNotRegistered` | `GetService<IAuthService>()` returns null → skip check |
| `ValidateAuthAsync_ReturnsNull_WhenAuthenticated` | `FakeAuthService` with default status → returns null |
| `ValidateAuthAsync_ReturnsErrorMessage_WhenNotAuthenticated` | `FakeAuthService` throws `AuthenticationException` → returns message |
| `ValidateAuthAsync_ReturnsErrorMessage_WhenInvalidCredentials` | `FakeAuthService` throws with `InvalidCredentials` message → returns message |

#### Integration Tests for Phase Commands (in `Lopen.Cli.Tests`)

| Test | Description |
|---|---|
| `Spec_ReturnsFailure_WhenAuthFails` | Wire `FakeAuthService` that throws → spec command returns `ExitCodes.Failure` and writes error to stderr |
| `Plan_ReturnsFailure_WhenAuthFails` | Same for plan |
| `Build_ReturnsFailure_WhenAuthFails` | Same for build |
| `Spec_Succeeds_WhenAuthValid` | `FakeAuthService` with valid status → command proceeds normally |
| `Headless_ReturnsFailure_WhenAuthFails` | Root command in headless mode with failing auth → returns failure |
| `Interactive_ReturnsFailure_WhenAuthFails` | Root command in interactive mode with failing auth → returns failure before TUI launches |

#### Existing test infrastructure to leverage

- `FakeAuthService` already has `ValidateCalled` tracking and exception injection
- `FakeWorkflowOrchestrator` exists for verifying the orchestrator is never called when auth fails
- `PhaseCommandTests` and `RootCommandTests` have established patterns for exit code assertions
- All tests use xUnit with hand-rolled fakes (no mocking framework)

#### What NOT to test

- Do not add auth tests to `WorkflowOrchestratorTests` — the orchestrator should remain unaware of auth (Option A)
- Do not test `CopilotAuthService.ValidateAsync` further — it's already tested in `Lopen.Auth.Tests`
