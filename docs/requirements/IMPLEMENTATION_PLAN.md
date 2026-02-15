# Implementation Plan

## Current Focus: JOB-007 — Storage Module Foundation ✅

- [x] Update `Lopen.Storage.csproj` with package references (`DI.Abstractions`, `Logging.Abstractions`) and `InternalsVisibleTo`
- [x] Update `Lopen.Storage.Tests.csproj` with needed package references (`DI`, `Logging`, `Logging.Abstractions`)
- [x] Create `IFileSystem` interface (abstraction over file I/O for testability: `CreateDirectory`, `FileExists`, `DirectoryExists`, `ReadAllTextAsync`, `WriteAllTextAsync`, `GetFiles`, `GetDirectories`, `MoveFile`, `DeleteFile`, `CreateSymlink`, `GetSymlinkTarget`, `GetLastWriteTimeUtc`)
- [x] Create `PhysicalFileSystem` internal sealed implementation wrapping `System.IO`
- [x] Create `SessionId` value object with `Parse`, `Generate` static methods (`{module}-YYYYMMDD-{counter}` format), `Module`, `Date`, `Counter` properties
- [x] Create `ISessionManager` interface (`CreateSessionAsync`, `GetLatestSessionIdAsync`, `LoadSessionStateAsync`, `SaveSessionStateAsync`, `ListSessionsAsync`, `SetLatestAsync`)
- [x] Create `SessionState` record (Phase, Step, Module, Component, TaskHierarchy, Metadata with timestamps)
- [x] Create `SessionMetrics` record (PerIterationTokens, CumulativeTokens, PremiumRequestCount)
- [x] Create `StorageException` for storage-specific failures (disk full, corrupted state)
- [x] Create `StoragePaths` static helper (resolves `.lopen/`, `sessions/`, `modules/`, `cache/` paths relative to a project root)
- [x] Create `SessionManager` internal sealed implementation using `IFileSystem` and `ILogger`
- [x] Create `ServiceCollectionExtensions` with `AddLopenStorage()` method
- [x] Wire Storage module into `Program.cs` (`AddLopenStorage`)
- [x] Add project reference from `Lopen.csproj` to `Lopen.Storage`
- [x] Write unit tests for `SessionId` (22 tests — parse, generate, format, counter, equality, roundtrip)
- [x] Write unit tests for `StoragePaths` (14 tests — all path resolution methods)
- [x] Write unit tests for `PhysicalFileSystem` (11 tests — integration tests with temp directory)
- [x] Write unit tests for `SessionManager` (19 tests — create, load, save, list, latest symlink, atomic writes)
- [x] Write unit tests for `ServiceCollectionExtensions` (3 tests — registers services, singletons, fluent return)
- [x] Write unit tests for `StorageException` (4 tests — constructors, properties, inheritance)
- [x] Verify `dotnet build` and `dotnet test` pass (77 storage tests, 154 total)
- [x] Run `dotnet format --verify-no-changes`
