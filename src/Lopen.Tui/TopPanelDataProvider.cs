using System.Reflection;
using Lopen.Auth;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Microsoft.Extensions.Logging;

namespace Lopen.Tui;

/// <summary>
/// Aggregates live service data into <see cref="TopPanelData"/> snapshots.
/// Async data (git branch, auth) is cached and refreshed periodically.
/// Synchronous data (tokens, workflow state) is read fresh on each call.
/// </summary>
internal sealed class TopPanelDataProvider : ITopPanelDataProvider
{
    private readonly ITokenTracker _tokenTracker;
    private readonly IGitService _gitService;
    private readonly IAuthService _authService;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly IModelSelector _modelSelector;
    private readonly ILogger<TopPanelDataProvider> _logger;
    private readonly string _version;

    // Cached async data — updated by RefreshAsync
    private volatile string? _cachedBranch;
    private volatile bool _cachedIsAuthenticated;

    public TopPanelDataProvider(
        ITokenTracker tokenTracker,
        IGitService gitService,
        IAuthService authService,
        IWorkflowEngine workflowEngine,
        IModelSelector modelSelector,
        ILogger<TopPanelDataProvider> logger)
    {
        _tokenTracker = tokenTracker ?? throw new ArgumentNullException(nameof(tokenTracker));
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _modelSelector = modelSelector ?? throw new ArgumentNullException(nameof(modelSelector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Split('+')[0] ?? "0.0.0";
    }

    public TopPanelData GetCurrentData()
    {
        var metrics = _tokenTracker.GetSessionMetrics();
        var phase = _workflowEngine.CurrentPhase;
        var step = _workflowEngine.CurrentStep;
        var modelResult = _modelSelector.SelectModel(phase);

        // Context window size from latest token usage, or 0 if no invocations yet
        var contextMax = metrics.PerIterationTokens.Count > 0
            ? metrics.PerIterationTokens[^1].ContextWindowSize
            : 0;

        return new TopPanelData
        {
            Version = _version,
            ModelName = modelResult.SelectedModel,
            ContextUsedTokens = metrics.CumulativeInputTokens + metrics.CumulativeOutputTokens,
            ContextMaxTokens = contextMax,
            PremiumRequestCount = metrics.PremiumRequestCount,
            GitBranch = _cachedBranch,
            IsAuthenticated = _cachedIsAuthenticated,
            PhaseName = FormatPhase(phase),
            CurrentStep = (int)step + 1,
            TotalSteps = 7,
            StepLabel = FormatStepLabel(step),
        };
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cachedBranch = await _gitService.GetCurrentBranchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh git branch");
            _cachedBranch = null;
        }

        try
        {
            var status = await _authService.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            _cachedIsAuthenticated = status.State == AuthState.Authenticated;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh auth status");
            _cachedIsAuthenticated = false;
        }
    }

    internal static string FormatPhase(WorkflowPhase phase) => phase switch
    {
        WorkflowPhase.RequirementGathering => "Requirement Gathering",
        WorkflowPhase.Planning => "Planning",
        WorkflowPhase.Building => "Building",
        WorkflowPhase.Research => "Research",
        _ => phase.ToString(),
    };

    internal static string FormatStepLabel(WorkflowStep step) => step switch
    {
        WorkflowStep.DraftSpecification => "Draft Specification",
        WorkflowStep.DetermineDependencies => "Determine Dependencies",
        WorkflowStep.IdentifyComponents => "Identify Components",
        WorkflowStep.SelectNextComponent => "Select Next Component",
        WorkflowStep.BreakIntoTasks => "Break Into Tasks",
        WorkflowStep.IterateThroughTasks => "Iterate Tasks",
        WorkflowStep.Repeat => "Repeat",
        _ => step.ToString(),
    };
}
