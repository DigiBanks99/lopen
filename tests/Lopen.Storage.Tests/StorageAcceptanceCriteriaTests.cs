using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

/// <summary>
/// Explicit acceptance criteria coverage tests for the Storage module.
/// Each test maps to a numbered AC from docs/requirements/storage/SPECIFICATION.md.
/// </summary>
public class StorageAcceptanceCriteriaTests
{
    // STOR-01: .lopen/ directory is created in project root on first workflow run

    [Fact]
    public void AC01_StorageInitializer_CreatesAllRequiredSubdirectories()
    {
        var fs = new InMemoryFileSystem();
        var initializer = new StorageInitializer(fs, NullLogger<StorageInitializer>.Instance, "/project");

        initializer.EnsureDirectoryStructure();

        Assert.True(fs.DirectoryExists("/project/.lopen"));
        Assert.True(fs.DirectoryExists("/project/.lopen/sessions"));
        Assert.True(fs.DirectoryExists("/project/.lopen/modules"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache/sections"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache/assessments"));
        Assert.True(fs.DirectoryExists("/project/.lopen/corrupted"));
    }

    [Fact]
    public void AC01_StorageInitializer_IdempotentOnRepeatedCalls()
    {
        var fs = new InMemoryFileSystem();
        var initializer = new StorageInitializer(fs, NullLogger<StorageInitializer>.Instance, "/project");

        initializer.EnsureDirectoryStructure();
        initializer.EnsureDirectoryStructure();

        Assert.True(fs.DirectoryExists("/project/.lopen"));
        Assert.True(fs.DirectoryExists("/project/.lopen/sessions"));
        Assert.True(fs.DirectoryExists("/project/.lopen/modules"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache/sections"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache/assessments"));
        Assert.True(fs.DirectoryExists("/project/.lopen/corrupted"));
    }

    // STOR-05: latest symlink points to the most recent session directory

    [Fact]
    public async Task AC05_LatestSymlink_PointsToMostRecentSession()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var first = await manager.CreateSessionAsync("auth");
        var second = await manager.CreateSessionAsync("auth");

        var latest = await manager.GetLatestSessionIdAsync();
        Assert.NotNull(latest);
        Assert.Equal(second.Module, latest.Module);
        Assert.Equal(second.Counter, latest.Counter);
    }

    [Fact]
    public async Task AC05_SetLatestAsync_UpdatesSymlink()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var first = await manager.CreateSessionAsync("core");
        var second = await manager.CreateSessionAsync("core");
        await manager.SetLatestAsync(first);

        var latest = await manager.GetLatestSessionIdAsync();
        Assert.NotNull(latest);
        Assert.Equal(first.Counter, latest.Counter);
    }

    // STOR-06: State is auto-saved after: step completion, task completion/failure,
    //          phase transition, component completion, user pause/switch

    [Fact]
    public async Task AC06_AutoSave_TriggersOnAllDefinedEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = SessionId.Generate("test", DateOnly.FromDateTime(DateTime.UtcNow), 1);
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "test",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var fakeManager = new FakeSessionManager();
        var autoSave = new AutoSaveService(fakeManager, NullLogger<AutoSaveService>.Instance);

        var triggers = new[]
        {
            AutoSaveTrigger.StepCompletion,
            AutoSaveTrigger.TaskCompletion,
            AutoSaveTrigger.TaskFailure,
            AutoSaveTrigger.PhaseTransition,
            AutoSaveTrigger.ComponentCompletion,
            AutoSaveTrigger.UserPause,
        };

        foreach (var trigger in triggers)
        {
            fakeManager.SaveCount = 0;
            await autoSave.SaveAsync(trigger, sessionId, state);
            Assert.True(fakeManager.SaveCount > 0, $"Auto-save did not trigger for {trigger}");
        }
    }

    // STOR-07: Session resume loads state from .lopen/sessions/latest

    [Fact]
    public async Task AC07_SessionResume_LoadsFromLatestSymlink()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var sessionId = await manager.CreateSessionAsync("auth");
        var now = DateTimeOffset.UtcNow;
        var savedState = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "auth",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await manager.SaveSessionStateAsync(sessionId, savedState);

        var latestId = await manager.GetLatestSessionIdAsync();
        Assert.NotNull(latestId);

        var loadedState = await manager.LoadSessionStateAsync(latestId);
        Assert.NotNull(loadedState);
        Assert.Equal("building", loadedState.Phase);
        Assert.Equal("iterate", loadedState.Step);
        Assert.Equal("auth", loadedState.Module);
    }

    // STOR-10: Plan checkboxes updated programmatically by Lopen, not by the LLM

    [Fact]
    public async Task AC10_PlanCheckboxes_UpdatedProgrammatically()
    {
        var fs = new InMemoryFileSystem();
        var planManager = new PlanManager(fs, NullLogger<PlanManager>.Instance, "/project");

        var plan = "# Plan\n- [ ] Implement feature A\n- [ ] Implement feature B\n";
        await planManager.WritePlanAsync("core", plan);

        await planManager.UpdateCheckboxAsync("core", "Implement feature A", true);

        var updated = await planManager.ReadPlanAsync("core");
        Assert.NotNull(updated);
        Assert.Contains("- [x] Implement feature A", updated);
        Assert.Contains("- [ ] Implement feature B", updated);
    }

    [Fact]
    public async Task AC10_PlanCheckboxes_CanUncheckProgrammatically()
    {
        var fs = new InMemoryFileSystem();
        var planManager = new PlanManager(fs, NullLogger<PlanManager>.Instance, "/project");

        var plan = "# Plan\n- [x] Implement feature A\n";
        await planManager.WritePlanAsync("core", plan);

        await planManager.UpdateCheckboxAsync("core", "Implement feature A", false);

        var updated = await planManager.ReadPlanAsync("core");
        Assert.NotNull(updated);
        Assert.Contains("- [ ] Implement feature A", updated);
    }

    // STOR-18: Completed sessions retained up to configured session_retention limit, then pruned

    [Fact]
    public async Task AC18_SessionPruning_RetainsNewestAndDeletesOldest()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        await manager.CreateSessionAsync("auth");
        await manager.CreateSessionAsync("auth");
        await manager.CreateSessionAsync("auth");

        var pruned = await manager.PruneSessionsAsync(1);

        Assert.Equal(2, pruned);
    }

    [Fact]
    public async Task AC18_SessionPruning_FewerThanRetention_PrunesNothing()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        await manager.CreateSessionAsync("core");
        await manager.CreateSessionAsync("core");

        var pruned = await manager.PruneSessionsAsync(5);

        Assert.Equal(0, pruned);
        var remaining = await manager.ListSessionsAsync();
        Assert.Equal(2, remaining.Count);
    }

    // STOR-04: Session IDs follow the {module}-YYYYMMDD-{counter} format

    [Fact]
    public void AC04_SessionId_FollowsExpectedFormat()
    {
        var id = SessionId.Generate("auth", DateOnly.FromDateTime(DateTime.UtcNow), 1);

        Assert.Equal("auth", id.Module);
        Assert.Equal(1, id.Counter);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), id.Date);
        Assert.Matches(@"^auth-\d{8}-\d+$", id.ToString());
    }

    // STOR-02: Session state persists workflow phase, step, module, component, and task hierarchy

    [Fact]
    public async Task AC02_SessionState_PersistsWorkflowState()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var sessionId = await manager.CreateSessionAsync("core");
        var now = DateTimeOffset.UtcNow;
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "core",
            Component = "workflow_engine",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await manager.SaveSessionStateAsync(sessionId, state);

        var loaded = await manager.LoadSessionStateAsync(sessionId);
        Assert.NotNull(loaded);
        Assert.Equal("building", loaded.Phase);
        Assert.Equal("iterate", loaded.Step);
        Assert.Equal("core", loaded.Module);
        Assert.Equal("workflow_engine", loaded.Component);
    }

    // STOR-14: Corrupted session state is detected, warned, and excluded from resume options

    [Fact]
    public async Task AC14_CorruptedSession_DetectedAndExcluded()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var goodId = await manager.CreateSessionAsync("auth");
        var badId = await manager.CreateSessionAsync("auth");

        // Corrupt the second session's state file
        var badStatePath = $"/project/.lopen/sessions/{badId}/state.json";
        await fs.WriteAllTextAsync(badStatePath, "NOT VALID JSON{{{");

        await Assert.ThrowsAsync<StorageException>(() => manager.LoadSessionStateAsync(badId));

        // Good session still loads fine
        var goodState = await manager.LoadSessionStateAsync(goodId);
        Assert.NotNull(goodState);
    }

    // STOR-03: Session metrics persists per-iteration and cumulative token counts and premium request counts

    [Fact]
    public async Task AC03_SessionMetrics_PersistsTokenCountsAndPremiumRequests()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var sessionId = await manager.CreateSessionAsync("auth");
        var now = DateTimeOffset.UtcNow;
        var metrics = new SessionMetrics
        {
            SessionId = sessionId.ToString(),
            CumulativeInputTokens = 5000,
            CumulativeOutputTokens = 3000,
            PremiumRequestCount = 2,
            IterationCount = 2,
            Iterations = new List<IterationMetric>
            {
                new() { InputTokens = 2000, OutputTokens = 1500, TotalTokens = 3500, ContextWindowSize = 128000, IsPremiumRequest = true },
                new() { InputTokens = 3000, OutputTokens = 1500, TotalTokens = 4500, ContextWindowSize = 128000, IsPremiumRequest = true },
            },
            UpdatedAt = now,
        };
        await manager.SaveSessionMetricsAsync(sessionId, metrics);

        var loaded = await manager.LoadSessionMetricsAsync(sessionId);
        Assert.NotNull(loaded);
        Assert.Equal(5000, loaded.CumulativeInputTokens);
        Assert.Equal(3000, loaded.CumulativeOutputTokens);
        Assert.Equal(2, loaded.PremiumRequestCount);
        Assert.Equal(2, loaded.IterationCount);
        Assert.Equal(2, loaded.Iterations.Count);
        Assert.True(loaded.Iterations[0].IsPremiumRequest);
        Assert.Equal(2000, loaded.Iterations[0].InputTokens);
        Assert.Equal(3000, loaded.Iterations[1].InputTokens);
    }

    // STOR-08: --resume {id} resumes a specific session (storage layer: load by explicit ID)

    [Fact]
    public async Task AC08_ResumeSpecificSession_LoadsByExplicitId()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var first = await manager.CreateSessionAsync("auth");
        var second = await manager.CreateSessionAsync("auth");
        var now = DateTimeOffset.UtcNow;

        var firstState = new SessionState
        {
            SessionId = first.ToString(),
            Phase = "research",
            Step = "gather",
            Module = "auth",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var secondState = new SessionState
        {
            SessionId = second.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "auth",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await manager.SaveSessionStateAsync(first, firstState);
        await manager.SaveSessionStateAsync(second, secondState);

        // Resume by explicit first session ID (not latest)
        var loaded = await manager.LoadSessionStateAsync(first);
        Assert.NotNull(loaded);
        Assert.Equal("research", loaded.Phase);
        Assert.Equal("gather", loaded.Step);
        Assert.Equal(first.ToString(), loaded.SessionId);
    }

    // STOR-09: Plans stored at .lopen/modules/{module}/plan.md with checkbox task hierarchy

    [Fact]
    public async Task AC09_PlanStorage_WrittenToCorrectPath()
    {
        var fs = new InMemoryFileSystem();
        var planManager = new PlanManager(fs, NullLogger<PlanManager>.Instance, "/project");

        var plan = "# Plan\n- [ ] Task A\n  - [ ] Subtask A1\n- [ ] Task B\n";
        await planManager.WritePlanAsync("auth", plan);

        var expectedPath = StoragePaths.GetModulePlanPath("/project", "auth");
        Assert.True(fs.FileExists(expectedPath));

        var content = await fs.ReadAllTextAsync(expectedPath);
        Assert.Contains("- [ ] Task A", content);
        Assert.Contains("  - [ ] Subtask A1", content);
    }

    // STOR-11: Section cache is keyed by file path + section header + modification timestamp

    [Fact]
    public async Task AC11_SectionCache_KeyedByPathAndHeaderAndTimestamp()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory(StoragePaths.GetSectionsCacheDirectory("/project"));
        var cache = new SectionCache(fs, NullLogger<SectionCache>.Instance, "/project");

        // Create two source files
        await fs.WriteAllTextAsync("/src/a.md", "content a");
        await fs.WriteAllTextAsync("/src/b.md", "content b");

        // Same header, different files
        await cache.SetAsync("/src/a.md", "Overview", "content from a");
        await cache.SetAsync("/src/b.md", "Overview", "content from b");

        // Different headers, same file
        await cache.SetAsync("/src/a.md", "Details", "details from a");

        var resultA = await cache.GetAsync("/src/a.md", "Overview");
        var resultB = await cache.GetAsync("/src/b.md", "Overview");
        var resultDetails = await cache.GetAsync("/src/a.md", "Details");

        Assert.NotNull(resultA);
        Assert.NotNull(resultB);
        Assert.NotNull(resultDetails);
        Assert.Equal("content from a", resultA.Content);
        Assert.Equal("content from b", resultB.Content);
        Assert.Equal("details from a", resultDetails.Content);
    }

    // STOR-12: Section cache is invalidated when the source file changes

    [Fact]
    public async Task AC12_SectionCache_InvalidatedOnSourceFileChange()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory(StoragePaths.GetSectionsCacheDirectory("/project"));
        var cache = new SectionCache(fs, NullLogger<SectionCache>.Instance, "/project");

        await fs.WriteAllTextAsync("/src/spec.md", "original content");
        await cache.SetAsync("/src/spec.md", "Overview", "cached section");

        // Modify the source file
        await Task.Delay(10);
        await fs.WriteAllTextAsync("/src/spec.md", "modified content");

        var result = await cache.GetAsync("/src/spec.md", "Overview");
        Assert.Null(result);
    }

    // STOR-13: Assessment cache is short-lived and invalidated on any file change in the assessed scope

    [Fact]
    public async Task AC13_AssessmentCache_InvalidatedOnAnyFileChangeInScope()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory(StoragePaths.GetAssessmentsCacheDirectory("/project"));
        var cache = new AssessmentCache(fs, NullLogger<AssessmentCache>.Instance, "/project");

        await fs.WriteAllTextAsync("/src/auth/login.cs", "code1");
        await fs.WriteAllTextAsync("/src/auth/register.cs", "code2");
        var timestamps = new Dictionary<string, DateTime>
        {
            ["/src/auth/login.cs"] = fs.GetLastWriteTimeUtc("/src/auth/login.cs"),
            ["/src/auth/register.cs"] = fs.GetLastWriteTimeUtc("/src/auth/register.cs"),
        };

        await cache.SetAsync("auth:assessment", "cached assessment", timestamps);

        // Modify just one file in scope
        await Task.Delay(10);
        await fs.WriteAllTextAsync("/src/auth/register.cs", "modified code");

        var result = await cache.GetAsync("auth:assessment");
        Assert.Null(result);
    }

    // STOR-15: Corrupted files are moved to .lopen/corrupted/ for manual inspection

    [Fact]
    public async Task AC15_CorruptedSession_QuarantinedToCorruptedDirectory()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");
        fs.CreateDirectory(StoragePaths.GetCorruptedDirectory("/project"));

        var sessionId = await manager.CreateSessionAsync("auth");
        var statePath = $"/project/.lopen/sessions/{sessionId}/state.json";
        await fs.WriteAllTextAsync(statePath, "CORRUPTED{{{");

        await manager.QuarantineCorruptedSessionAsync(sessionId);

        var corruptedDir = StoragePaths.GetCorruptedDirectory("/project");
        var quarantinedDirs = fs.GetDirectories(corruptedDir).ToList();
        Assert.NotEmpty(quarantinedDirs);
    }

    // STOR-16: Disk full / write failure is treated as critical system error (wraps IOException in StorageException)

    [Fact]
    public async Task AC16_WriteFailure_WrappedInStorageException()
    {
        var fs = new FailingWriteFileSystem();
        fs.CreateDirectory("/project/.lopen/sessions");
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        var sessionId = SessionId.Generate("auth", DateOnly.FromDateTime(DateTime.UtcNow), 1);
        var now = DateTimeOffset.UtcNow;
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "auth",
            CreatedAt = now,
            UpdatedAt = now,
        };

        var ex = await Assert.ThrowsAsync<StorageException>(() => manager.SaveSessionStateAsync(sessionId, state));
        Assert.IsType<IOException>(ex.InnerException);
    }

    // STOR-17: Corrupted cache entries are silently invalidated and regenerated

    [Fact]
    public async Task AC17_CorruptedSectionCacheEntry_SilentlyInvalidated()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory(StoragePaths.GetSectionsCacheDirectory("/project"));
        var cache = new SectionCache(fs, NullLogger<SectionCache>.Instance, "/project");

        await fs.WriteAllTextAsync("/src/spec.md", "content");
        await cache.SetAsync("/src/spec.md", "Overview", "valid content");

        // Corrupt the cache file on disk
        var cacheDir = StoragePaths.GetSectionsCacheDirectory("/project");
        var files = fs.GetFiles(cacheDir, "*.json").ToList();
        Assert.NotEmpty(files);
        await fs.WriteAllTextAsync(files[0], "not valid json{{{");

        // Fresh instance reads from disk only
        var freshCache = new SectionCache(fs, NullLogger<SectionCache>.Instance, "/project");
        var result = await freshCache.GetAsync("/src/spec.md", "Overview");
        Assert.Null(result);
    }

    [Fact]
    public async Task AC17_CorruptedAssessmentCacheEntry_SilentlyInvalidated()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory(StoragePaths.GetAssessmentsCacheDirectory("/project"));
        var cache = new AssessmentCache(fs, NullLogger<AssessmentCache>.Instance, "/project");

        await fs.WriteAllTextAsync("/src/auth/login.cs", "code");
        var timestamps = new Dictionary<string, DateTime>
        {
            ["/src/auth/login.cs"] = fs.GetLastWriteTimeUtc("/src/auth/login.cs"),
        };
        await cache.SetAsync("auth:assessment", "valid", timestamps);

        // Corrupt the cache file
        var cacheDir = StoragePaths.GetAssessmentsCacheDirectory("/project");
        var files = fs.GetFiles(cacheDir, "*.json").ToList();
        Assert.NotEmpty(files);
        await fs.WriteAllTextAsync(files[0], "corrupted{{{");

        var freshCache = new AssessmentCache(fs, NullLogger<AssessmentCache>.Instance, "/project");
        var result = await freshCache.GetAsync("auth:assessment");
        Assert.Null(result);
    }

    // STOR-19: Individual sessions can be deleted via DeleteSessionAsync

    [Fact]
    public async Task AC19_DeleteSession_RemovesSessionDirectoryAndFiles()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var sessionId = await manager.CreateSessionAsync("auth");
        var now = DateTimeOffset.UtcNow;
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "auth",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await manager.SaveSessionStateAsync(sessionId, state);

        var sessionDir = StoragePaths.GetSessionDirectory("/project", sessionId);
        Assert.True(fs.DirectoryExists(sessionDir));

        await manager.DeleteSessionAsync(sessionId);

        Assert.False(fs.DirectoryExists(sessionDir));
    }

    // STOR-20: Research documents stored at docs/requirements/{module}/RESEARCH-{topic}.md

    [Fact]
    public void AC20_ResearchDocumentPath_FollowsExpectedFormat()
    {
        var path = StoragePaths.GetResearchDocumentPath("/project", "auth", "oauth-flows");

        Assert.Equal(
            Path.Combine("/project", "docs", "requirements", "auth", "RESEARCH-oauth-flows.md"),
            path);
    }

    // STOR-21: Storage format is compact JSON by default (WriteIndented = false, snake_case properties)

    [Fact]
    public void AC21_CompactJsonOptions_NotIndentedAndSnakeCase()
    {
        Assert.False(SessionManager.CompactJsonOptions.WriteIndented);
        Assert.Equal(JsonNamingPolicy.SnakeCaseLower, SessionManager.CompactJsonOptions.PropertyNamingPolicy);
    }

    [Fact]
    public async Task AC21_SavedJsonOnDisk_IsCompactFormat()
    {
        var fs = new InMemoryFileSystem();
        var manager = new SessionManager(fs, NullLogger<SessionManager>.Instance, "/project");
        fs.CreateDirectory("/project/.lopen/sessions");

        var sessionId = await manager.CreateSessionAsync("auth");
        var now = DateTimeOffset.UtcNow;
        var state = new SessionState
        {
            SessionId = sessionId.ToString(),
            Phase = "building",
            Step = "iterate",
            Module = "auth",
            CreatedAt = now,
            UpdatedAt = now,
        };
        await manager.SaveSessionStateAsync(sessionId, state);

        var statePath = StoragePaths.GetSessionStatePath("/project", sessionId);
        var json = await fs.ReadAllTextAsync(statePath);

        // Compact JSON should not contain newlines
        Assert.DoesNotContain("\n", json);
        // Should use snake_case properties
        Assert.Contains("session_id", json);
    }

    // Fake session manager for auto-save tests
    private class FakeSessionManager : ISessionManager
    {
        public int SaveCount { get; set; }

        public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default) =>
            Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));

        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default) =>
            Task.FromResult<SessionId?>(null);

        public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken ct = default) =>
            Task.FromResult<SessionState?>(null);

        public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken ct = default) =>
            Task.FromResult<SessionMetrics?>(null);

        public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SessionId>>(Array.Empty<SessionId>());

        public Task SetLatestAsync(SessionId sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task DeleteSessionAsync(SessionId sessionId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// File system that fails on write operations (simulates disk full/write failure).
    /// </summary>
    private sealed class FailingWriteFileSystem : IFileSystem
    {
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

        public void CreateDirectory(string path) => _directories.Add(path);
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => _directories.Contains(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct) => throw new FileNotFoundException();
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct) => throw new IOException("Disk full");
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string src, string dst) => throw new IOException("Disk full");
        public void DeleteFile(string path) { }
        public void DeleteDirectory(string path, bool recursive = true) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.MinValue;
    }
}
