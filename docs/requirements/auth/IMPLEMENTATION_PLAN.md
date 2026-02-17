# AUTH-10 Implementation Plan: Pre-flight Auth Check

## Objective
Wire `IAuthService.ValidateAsync` into all workflow entry points so credentials are validated before any workflow phase begins.

## Approach
Option A from RESEARCH.md: Add `ValidateAuthAsync` helper in `PhaseCommands` following existing validation patterns (`ValidateHeadlessPromptAsync`, `ValidateSpecExistsAsync`, `ValidatePlanExistsAsync`).

## Changes Required

### 1. Add `ValidateAuthAsync` helper to `PhaseCommands.cs`
- Pattern: `internal static async Task<string?> ValidateAuthAsync(IServiceProvider, CancellationToken)`
- Uses `GetService<IAuthService>()` (nullable — graceful when auth module not registered)
- Catches `AuthenticationException` and returns `ex.Message`
- Returns `null` when auth is valid

### 2. Wire into all command handlers
- **`PhaseCommands.CreateSpec`**: After `ValidateHeadlessPromptAsync`, before `ResolveSessionAsync`
- **`PhaseCommands.CreatePlan`**: After `ValidateHeadlessPromptAsync`, before `ResolveSessionAsync`  
- **`PhaseCommands.CreateBuild`**: After `ValidateHeadlessPromptAsync`, before `ResolveSessionAsync`
- **`RootCommandHandler` (headless)**: After `ValidateHeadlessPromptAsync`, before `RunHeadlessAsync`
- **`RootCommandHandler` (interactive)**: Before `ResolveSessionAsync`

### 3. Update `FakeAuthService` in tests
- Add `ValidateException` property for injectable exceptions
- Update `ValidateAsync` to throw when set

### 4. Add tests
- `PhaseCommandTests`: Spec/Plan/Build fail when auth fails
- `PhaseCommandTests`: Spec succeeds when auth not registered
- `RootCommandTests`: Headless fails when auth fails
- `RootCommandTests`: Interactive fails when auth fails

## Acceptance Criteria
- [AUTH-10] Pre-flight auth check blocks workflow start when credentials are missing or invalid
