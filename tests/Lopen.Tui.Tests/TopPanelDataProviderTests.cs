using Lopen.Auth;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

public sealed class TopPanelDataProviderTests
{
    private readonly FakeTokenTracker _tokenTracker = new();
    private readonly FakeGitService _gitService = new();
    private readonly FakeAuthService _authService = new();
    private readonly FakeWorkflowEngine _workflowEngine = new();
    private readonly FakeModelSelector _modelSelector = new();

    private TopPanelDataProvider CreateProvider() =>
        new(
            _tokenTracker,
            _gitService,
            _authService,
            _workflowEngine,
            _modelSelector,
            NullLogger<TopPanelDataProvider>.Instance);

    [Fact]
    public void GetCurrentData_ReturnsVersionString()
    {
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.NotNull(data.Version);
    }

    [Fact]
    public void GetCurrentData_ReturnsModelName()
    {
        _modelSelector.Model = "claude-opus-4.6";
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.Equal("claude-opus-4.6", data.ModelName);
    }

    [Fact]
    public void GetCurrentData_ReturnsTokenUsage()
    {
        _tokenTracker.RecordUsage(new TokenUsage(1000, 500, 1500, 128_000, false));
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.Equal(1500, data.ContextUsedTokens);
        Assert.Equal(128_000, data.ContextMaxTokens);
    }

    [Fact]
    public void GetCurrentData_ReturnsPremiumCount()
    {
        _tokenTracker.RecordUsage(new TokenUsage(100, 50, 150, 128_000, true));
        _tokenTracker.RecordUsage(new TokenUsage(100, 50, 150, 128_000, true));
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.Equal(2, data.PremiumRequestCount);
    }

    [Fact]
    public void GetCurrentData_WithNoTokenUsage_ReturnsZeroContextMax()
    {
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.Equal(0, data.ContextMaxTokens);
        Assert.Equal(0, data.ContextUsedTokens);
    }

    [Fact]
    public void GetCurrentData_ReturnsCurrentPhase()
    {
        _workflowEngine.Phase = WorkflowPhase.Building;
        _workflowEngine.Step = WorkflowStep.IterateThroughTasks;
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.Equal("Building", data.PhaseName);
        Assert.Equal(6, data.CurrentStep); // IterateThroughTasks is index 5, +1 = 6
        Assert.Equal(7, data.TotalSteps);
        Assert.Equal("Iterate Tasks", data.StepLabel);
    }

    [Fact]
    public void GetCurrentData_ReturnsRequirementGatheringPhase()
    {
        _workflowEngine.Phase = WorkflowPhase.RequirementGathering;
        _workflowEngine.Step = WorkflowStep.DraftSpecification;
        var provider = CreateProvider();
        var data = provider.GetCurrentData();
        Assert.Equal("Requirement Gathering", data.PhaseName);
        Assert.Equal(1, data.CurrentStep);
        Assert.Equal("Draft Specification", data.StepLabel);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesGitBranch()
    {
        _gitService.Branch = "feat/tui-wiring";
        var provider = CreateProvider();

        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Equal("feat/tui-wiring", data.GitBranch);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesAuthStatus_Authenticated()
    {
        _authService.Status = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.SdkCredentials, "user");
        var provider = CreateProvider();

        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.True(data.IsAuthenticated);
    }

    [Fact]
    public async Task RefreshAsync_UpdatesAuthStatus_NotAuthenticated()
    {
        _authService.Status = new AuthStatusResult(AuthState.NotAuthenticated, AuthCredentialSource.SdkCredentials);
        var provider = CreateProvider();

        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.False(data.IsAuthenticated);
    }

    [Fact]
    public async Task RefreshAsync_GitFailure_SetsNullBranch()
    {
        _gitService.ShouldThrow = true;
        var provider = CreateProvider();

        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.Null(data.GitBranch);
    }

    [Fact]
    public async Task RefreshAsync_AuthFailure_SetsNotAuthenticated()
    {
        _authService.ShouldThrow = true;
        var provider = CreateProvider();

        await provider.RefreshAsync();
        var data = provider.GetCurrentData();

        Assert.False(data.IsAuthenticated);
    }

    [Fact]
    public void GetCurrentData_BeforeRefresh_ReturnsDefaultAsyncValues()
    {
        var provider = CreateProvider();
        var data = provider.GetCurrentData();

        Assert.Null(data.GitBranch);
        Assert.False(data.IsAuthenticated);
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering, "Requirement Gathering")]
    [InlineData(WorkflowPhase.Planning, "Planning")]
    [InlineData(WorkflowPhase.Building, "Building")]
    [InlineData(WorkflowPhase.Research, "Research")]
    public void FormatPhase_ReturnsExpectedString(WorkflowPhase phase, string expected)
    {
        Assert.Equal(expected, TopPanelDataProvider.FormatPhase(phase));
    }

    [Theory]
    [InlineData(WorkflowStep.DraftSpecification, "Draft Specification")]
    [InlineData(WorkflowStep.DetermineDependencies, "Determine Dependencies")]
    [InlineData(WorkflowStep.IdentifyComponents, "Identify Components")]
    [InlineData(WorkflowStep.SelectNextComponent, "Select Next Component")]
    [InlineData(WorkflowStep.BreakIntoTasks, "Break Into Tasks")]
    [InlineData(WorkflowStep.IterateThroughTasks, "Iterate Tasks")]
    [InlineData(WorkflowStep.Repeat, "Repeat")]
    public void FormatStepLabel_ReturnsExpectedString(WorkflowStep step, string expected)
    {
        Assert.Equal(expected, TopPanelDataProvider.FormatStepLabel(step));
    }

    [Fact]
    public void GetCurrentData_MultipleTokenUsages_UsesLatestContextWindow()
    {
        _tokenTracker.RecordUsage(new TokenUsage(100, 50, 150, 64_000, false));
        _tokenTracker.RecordUsage(new TokenUsage(200, 100, 300, 128_000, false));
        var provider = CreateProvider();
        var data = provider.GetCurrentData();

        Assert.Equal(450, data.ContextUsedTokens); // 100+200 input + 50+100 output
        Assert.Equal(128_000, data.ContextMaxTokens); // Latest context window
    }

    [Fact]
    public void Constructor_ThrowsOnNullTokenTracker()
    {
        Assert.Throws<ArgumentNullException>(() => new TopPanelDataProvider(
            null!, _gitService, _authService, _workflowEngine, _modelSelector,
            NullLogger<TopPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullGitService()
    {
        Assert.Throws<ArgumentNullException>(() => new TopPanelDataProvider(
            _tokenTracker, null!, _authService, _workflowEngine, _modelSelector,
            NullLogger<TopPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullAuthService()
    {
        Assert.Throws<ArgumentNullException>(() => new TopPanelDataProvider(
            _tokenTracker, _gitService, null!, _workflowEngine, _modelSelector,
            NullLogger<TopPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullWorkflowEngine()
    {
        Assert.Throws<ArgumentNullException>(() => new TopPanelDataProvider(
            _tokenTracker, _gitService, _authService, null!, _modelSelector,
            NullLogger<TopPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullModelSelector()
    {
        Assert.Throws<ArgumentNullException>(() => new TopPanelDataProvider(
            _tokenTracker, _gitService, _authService, _workflowEngine, null!,
            NullLogger<TopPanelDataProvider>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        Assert.Throws<ArgumentNullException>(() => new TopPanelDataProvider(
            _tokenTracker, _gitService, _authService, _workflowEngine, _modelSelector,
            null!));
    }

    // --- Fakes ---

    private sealed class FakeTokenTracker : ITokenTracker
    {
        private readonly List<TokenUsage> _usages = [];
        private int _premiumCount;

        public void RecordUsage(TokenUsage usage)
        {
            _usages.Add(usage);
            if (usage.IsPremiumRequest) _premiumCount++;
        }

        public SessionTokenMetrics GetSessionMetrics()
        {
            return new SessionTokenMetrics
            {
                PerIterationTokens = _usages.AsReadOnly(),
                CumulativeInputTokens = _usages.Sum(u => u.InputTokens),
                CumulativeOutputTokens = _usages.Sum(u => u.OutputTokens),
                PremiumRequestCount = _premiumCount,
            };
        }

        public void ResetSession()
        {
            _usages.Clear();
            _premiumCount = 0;
        }
    }

    private sealed class FakeGitService : IGitService
    {
        public string? Branch { get; set; }
        public bool ShouldThrow { get; set; }

        public Task<string?> GetCurrentBranchAsync(CancellationToken cancellationToken = default)
        {
            if (ShouldThrow) throw new GitException("failed", "git branch", 1, "error");
            return Task.FromResult(Branch);
        }

        public Task<GitResult> CommitAllAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "ok", ""));
        public Task<GitResult> CreateBranchAsync(string branchName, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "ok", ""));
        public Task<GitResult> ResetToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
            => Task.FromResult(new GitResult(0, "ok", ""));
        public Task<DateTimeOffset?> GetLastCommitDateAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DateTimeOffset?>(DateTimeOffset.UtcNow);
        public Task<string> GetDiffAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("");
        public Task<string?> GetCurrentCommitShaAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>("abc123");
    }

    private sealed class FakeAuthService : IAuthService
    {
        public AuthStatusResult Status { get; set; } =
            new(AuthState.NotAuthenticated, AuthCredentialSource.SdkCredentials);
        public bool ShouldThrow { get; set; }

        public Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("auth failed");
            return Task.FromResult(Status);
        }

        public Task LoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeWorkflowEngine : IWorkflowEngine
    {
        public WorkflowStep Step { get; set; } = WorkflowStep.DraftSpecification;
        public WorkflowPhase Phase { get; set; } = WorkflowPhase.RequirementGathering;

        public WorkflowStep CurrentStep => Step;
        public WorkflowPhase CurrentPhase => Phase;
        public bool IsComplete => false;

        public Task InitializeAsync(string moduleName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public bool Fire(WorkflowTrigger trigger) => true;
        public IReadOnlyList<WorkflowTrigger> GetPermittedTriggers() => [];
    }

    private sealed class FakeModelSelector : IModelSelector
    {
        public string Model { get; set; } = "gpt-4.1";

        public ModelFallbackResult SelectModel(WorkflowPhase phase) =>
            new(Model, false);

        public IReadOnlyList<string> GetFallbackChain(WorkflowPhase phase) =>
            [Model];
    }
}
