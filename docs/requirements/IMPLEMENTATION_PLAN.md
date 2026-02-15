# Implementation Plan

## Current Focus: JOB-005 — Auth Module Foundation ✅

- [x] Add `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.Logging` to `Directory.Packages.props`
- [x] Update `Lopen.Auth.csproj` with package references (`DI.Abstractions`, `Logging.Abstractions`)
- [x] Create `AuthState` enum (`Authenticated`, `NotAuthenticated`, `InvalidCredentials`)
- [x] Create `AuthCredentialSource` enum (`None`, `GhToken`, `GitHubToken`, `SdkCredentials`)
- [x] Create `AuthStatusResult` sealed record (`AuthState State`, `AuthCredentialSource Source`, `string? Username`, `string? ErrorMessage`)
- [x] Create `IAuthService` interface (`LoginAsync`, `LogoutAsync`, `GetStatusAsync`, `ValidateAsync`)
- [x] Create `AuthenticationException` for validation failures
- [x] Create `TokenSourceResult` sealed record
- [x] Create `ITokenSourceResolver` interface and `EnvironmentTokenSourceResolver` implementation (resolves `GH_TOKEN` > `GITHUB_TOKEN` precedence, `IsNullOrWhiteSpace` guard)
- [x] Create `StubAuthService` — pre-SDK implementation using `ITokenSourceResolver`
- [x] Create `ServiceCollectionExtensions` with `AddLopenAuth()` method
- [x] Add `InternalsVisibleTo` for test project access
- [x] Update `Lopen.Auth.Tests.csproj` with needed package references
- [x] Write unit tests for `AuthStatusResult` (7 tests — equality, properties, error messages)
- [x] Write unit tests for `EnvironmentTokenSourceResolver` (9 tests — precedence, fallback, whitespace, null accessor, default constructor)
- [x] Write unit tests for `ServiceCollectionExtensions` (5 tests — registers services, singletons, fluent return)
- [x] Write unit tests for `StubAuthService` (7 tests — status, validate, login, logout)
- [x] Wire Auth module into `Program.cs` (`AddLopenAuth`)
- [x] Add project reference from `Lopen.csproj` to `Lopen.Auth`
- [x] Verify `dotnet build` and `dotnet test` pass (29 auth tests, 78 total)
- [x] Run `dotnet format --verify-no-changes`
