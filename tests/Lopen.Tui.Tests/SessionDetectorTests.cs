using Lopen.Storage;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for SessionDetector — maps ISessionManager session state to SessionResumeData.
/// Covers JOB-039 (TUI-07) acceptance criteria.
/// </summary>
public class SessionDetectorTests
{
    // ==================== DetectActiveSessionAsync ====================

    [Fact]
    public async Task DetectActiveSession_NoSession_ReturnsNull()
    {
        var manager = new StubSessionManager(latestId: null);
        var detector = new SessionDetector(manager);

        var result = await detector.DetectActiveSessionAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task DetectActiveSession_CompletedSession_ReturnsNull()
    {
        var state = CreateState(isComplete: true);
        var manager = new StubSessionManager(latestId: "test-20260217-1", sessionState: state);
        var detector = new SessionDetector(manager);

        var result = await detector.DetectActiveSessionAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task DetectActiveSession_ActiveSession_ReturnsResumeData()
    {
        var state = CreateState(module: "auth", phase: "Building", step: "IterateThroughTasks");
        var manager = new StubSessionManager(latestId: "test-20260217-1", sessionState: state);
        var detector = new SessionDetector(manager);

        var result = await detector.DetectActiveSessionAsync();

        Assert.NotNull(result);
        Assert.Equal("auth", result.ModuleName);
        Assert.Equal("Building", result.PhaseName);
        Assert.Equal("6/7", result.StepProgress);
        Assert.Equal(0, result.SelectedOption);
    }

    [Fact]
    public async Task DetectActiveSession_WithComponent_ShowsComponent()
    {
        var state = CreateState(component: "http-client");
        var manager = new StubSessionManager(latestId: "test-20260217-1", sessionState: state);
        var detector = new SessionDetector(manager);

        var result = await detector.DetectActiveSessionAsync();

        Assert.NotNull(result);
        Assert.Equal("Component: http-client", result.TaskProgress);
    }

    [Fact]
    public async Task DetectActiveSession_NoComponent_ShowsNoComponent()
    {
        var state = CreateState(component: null);
        var manager = new StubSessionManager(latestId: "test-20260217-1", sessionState: state);
        var detector = new SessionDetector(manager);

        var result = await detector.DetectActiveSessionAsync();

        Assert.NotNull(result);
        Assert.Equal("No component selected", result.TaskProgress);
    }

    [Fact]
    public async Task DetectActiveSession_CancellationRespected()
    {
        var manager = new StubSessionManager(latestId: "test-20260217-1");
        var detector = new SessionDetector(manager);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => detector.DetectActiveSessionAsync(cts.Token));
    }

    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionDetector(null!));
    }

    // ==================== MapToResumeData ====================

    [Theory]
    [InlineData("DraftSpecification", 1)]
    [InlineData("DetermineDependencies", 2)]
    [InlineData("IdentifyComponents", 3)]
    [InlineData("SelectNextComponent", 4)]
    [InlineData("BreakIntoTasks", 5)]
    [InlineData("IterateThroughTasks", 6)]
    [InlineData("Repeat", 7)]
    public void ParseStepNumber_MapsCorrectly(string stepName, int expected)
    {
        Assert.Equal(expected, SessionDetector.ParseStepNumber(stepName));
    }

    [Fact]
    public void ParseStepNumber_UnknownStep_ReturnsZero()
    {
        Assert.Equal(0, SessionDetector.ParseStepNumber("Unknown"));
    }

    [Fact]
    public void MapToResumeData_CalculatesProgress()
    {
        var state = CreateState(step: "BreakIntoTasks");
        var data = SessionDetector.MapToResumeData(state);

        Assert.Equal("5/7", data.StepProgress);
        Assert.Equal(71, data.ProgressPercent); // 5/7 * 100 = 71
    }

    [Fact]
    public void MapToResumeData_ProgressClampedTo100()
    {
        var state = CreateState(step: "Repeat");
        var data = SessionDetector.MapToResumeData(state);

        Assert.Equal("7/7", data.StepProgress);
        Assert.Equal(100, data.ProgressPercent);
    }

    // ==================== FormatRelativeTime ====================

    [Fact]
    public void FormatRelativeTime_JustNow()
    {
        var result = SessionDetector.FormatRelativeTime(DateTimeOffset.UtcNow.AddSeconds(-30));
        Assert.Equal("just now", result);
    }

    [Fact]
    public void FormatRelativeTime_Minutes()
    {
        var result = SessionDetector.FormatRelativeTime(DateTimeOffset.UtcNow.AddMinutes(-15));
        Assert.Equal("15 minutes ago", result);
    }

    [Fact]
    public void FormatRelativeTime_Hours()
    {
        var result = SessionDetector.FormatRelativeTime(DateTimeOffset.UtcNow.AddHours(-3));
        Assert.Equal("3 hours ago", result);
    }

    [Fact]
    public void FormatRelativeTime_Days()
    {
        var result = SessionDetector.FormatRelativeTime(DateTimeOffset.UtcNow.AddDays(-5));
        Assert.Equal("5 days ago", result);
    }

    // ==================== Helpers ====================

    private static SessionState CreateState(
        string module = "test-module",
        string phase = "Planning",
        string step = "IdentifyComponents",
        string? component = null,
        bool isComplete = false)
    {
        return new SessionState
        {
            SessionId = "test-session-1",
            Module = module,
            Phase = phase,
            Step = step,
            Component = component,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            IsComplete = isComplete
        };
    }

    private sealed class StubSessionManager : ISessionManager
    {
        private readonly SessionId? _latestId;
        private readonly SessionState? _state;

        public StubSessionManager(string? latestId = null, SessionState? sessionState = null)
        {
            _latestId = latestId is not null ? SessionId.Parse(latestId) : null;
            _state = sessionState;
        }

        public Task<SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_latestId);
        }

        public Task<SessionState?> LoadSessionStateAsync(SessionId sessionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_state);
        }

        public Task<SessionId> CreateSessionAsync(string module, CancellationToken ct = default)
            => Task.FromResult(SessionId.Generate(module, DateOnly.FromDateTime(DateTime.UtcNow), 1));

        public Task SaveSessionStateAsync(SessionId sessionId, SessionState state, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<SessionMetrics?> LoadSessionMetricsAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.FromResult<SessionMetrics?>(null);

        public Task SaveSessionMetricsAsync(SessionId sessionId, SessionMetrics metrics, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<SessionId>> ListSessionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionId>>([]);

        public Task SetLatestAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task QuarantineCorruptedSessionAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> PruneSessionsAsync(int retentionCount, CancellationToken ct = default)
            => Task.FromResult(0);

        public Task DeleteSessionAsync(SessionId sessionId, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
