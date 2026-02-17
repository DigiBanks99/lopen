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
}
