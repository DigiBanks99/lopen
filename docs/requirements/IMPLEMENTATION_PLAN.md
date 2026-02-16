# Implementation Plan

## Current Job: JOB-018 — Automatic Token Renewal and Failed Renewal Handling

**Module**: llm (with touches to core/storage interfaces)  
**Priority**: P1  
**Description**: Implement automatic token renewal via SDK's `OnErrorOccurred` hook. Transparently retry on 401/403 when recoverable; trigger critical error, save session state, and inform user when renewal fails.

### Acceptance Criteria

- AC1: Automatic token renewal transparently refreshes expired credentials during active sessions
- AC2: Failed automatic renewal (revoked token) triggers a critical error, saves session state, and informs the user

### Tasks

- [ ] **1. Define `ISessionStateSaver`** in `Lopen.Llm` — single-method callback interface (`SaveAsync(CancellationToken)`) to decouple from `Lopen.Storage`. This avoids a circular dependency; the host wires the real `ISessionManager.SaveSessionStateAsync` call at composition root.
- [ ] **2. Define `IAuthErrorHandler`** in `Lopen.Llm` — interface with `Task<ErrorOccurredHookOutput> HandleAuthErrorAsync(ErrorOccurredHookInput input)`.
- [ ] **3. Implement `AuthErrorHandler`** in `Lopen.Llm`:
  - Inject `IFailureHandler`, `ISessionStateSaver`, `ILogger<AuthErrorHandler>`.
  - **Detect auth errors**: status 401/403 in `input.Error` text (SDK serialises status into the error string) or keywords like `"unauthorized"`, `"forbidden"`, `"auth"`.
  - **Recoverable path** (AC1): Return `ErrorHandling = "retry"` with a max retry count of 1 (SDK handles the actual token refresh on retry).
  - **Non-recoverable path** (AC2): Call `IFailureHandler.RecordCriticalError()`, call `ISessionStateSaver.SaveAsync()`, return `ErrorHandling = "abort"` with `UserNotification` message explaining the auth failure.
  - **Non-auth errors**: Return `ErrorHandling = "skip"` to let existing error handling in `CopilotLlmService` deal with them.
  - Track retry attempts per session to prevent infinite retry loops (simple counter reset per `HandleAuthErrorAsync` sequence).
- [ ] **4. Wire `OnErrorOccurred` hook in `CopilotLlmService.cs`** — set `config.Hooks` when constructing `SessionConfig` (~line 60). Delegate to injected `IAuthErrorHandler`. Add `IAuthErrorHandler` as a constructor dependency.
- [ ] **5. Register in DI** (`Lopen.Llm/ServiceCollectionExtensions.cs`):
  - `TryAddSingleton<ISessionStateSaver, NullSessionStateSaver>` (no-op default; host overrides).
  - `AddSingleton<IAuthErrorHandler, AuthErrorHandler>`.
- [ ] **6. Wire `ISessionStateSaver` at composition root** — in the CLI host's DI setup, register a real implementation that delegates to `ISessionManager.SaveSessionStateAsync` with the active session ID.
- [ ] **7. Write unit tests** (`Lopen.Llm.Tests`):
  - `AuthErrorHandlerTests.cs`: auth error detected → retry (AC1), non-recoverable → critical error + save + abort (AC2), non-auth error → skip, retry count capped at 1.
  - `CopilotLlmServiceTests.cs`: verify `OnErrorOccurred` hook is set on `SessionConfig`.
  - AC-mapped tests in `LlmAcceptanceCriteriaTests.cs` if pattern is followed.
- [ ] **8. Validate** — `dotnet build` and `dotnet test` pass.

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| `ISessionStateSaver` callback in Llm module | Avoids Llm→Storage dependency; composition root wires the real impl |
| Max 1 retry for auth errors | SDK refreshes the token on retry; more than 1 retry means the token is truly revoked |
| String-match on error text for 401/403 | SDK exposes errors as strings in `ErrorOccurredHookInput.Error`; no typed status code available |
| `TryAddSingleton` for `ISessionStateSaver` | Allows host to override the no-op default; matches existing DI pattern for `IGitHubTokenProvider` |
| Non-auth errors return "skip" | Preserves existing catch-and-wrap-to-`LlmException` behavior in `CopilotLlmService` |

### Files to Create/Modify

| File | Action |
|------|--------|
| `src/Lopen.Llm/ISessionStateSaver.cs` | Create interface |
| `src/Lopen.Llm/NullSessionStateSaver.cs` | Create no-op default |
| `src/Lopen.Llm/IAuthErrorHandler.cs` | Create interface |
| `src/Lopen.Llm/AuthErrorHandler.cs` | Create implementation |
| `src/Lopen.Llm/CopilotLlmService.cs` | Add `IAuthErrorHandler` dep, wire `Hooks.OnErrorOccurred` |
| `src/Lopen.Llm/ServiceCollectionExtensions.cs` | Register new services |
| `tests/Lopen.Llm.Tests/AuthErrorHandlerTests.cs` | Create tests |
| `tests/Lopen.Llm.Tests/CopilotLlmServiceTests.cs` | Add hook-wiring test |

### Recently Completed Jobs

| Job | Module | Description | Tests |
|-----|--------|-------------|-------|
| JOB-057 | llm | LLM AC tests (all 14 ACs) | 22 tests |
| JOB-101 | llm | Fix IsPremiumModel for -mini variants | bugfix |
| JOB-052 | llm | Task status rejection gate | 14 tests |
| JOB-029 | configuration | Config passthrough | 12 tests |
| JOB-047 | llm | Fresh context window | 2 tests |
| JOB-046 | llm | Copilot SDK auth | 30 tests |
