namespace Lopen.Core;

/// <summary>
/// Service that orchestrates the plan/build loop workflow.
/// </summary>
public class LoopService
{
    private readonly ICopilotService _copilotService;
    private readonly LoopStateManager _stateManager;
    private readonly LoopOutputService _outputService;
    private readonly LoopConfig _config;
    private readonly IVerificationService? _verificationService;

    /// <summary>
    /// Creates a new LoopService.
    /// </summary>
    public LoopService(
        ICopilotService copilotService,
        LoopStateManager stateManager,
        LoopOutputService outputService,
        LoopConfig config,
        IVerificationService? verificationService = null)
    {
        _copilotService = copilotService;
        _stateManager = stateManager;
        _outputService = outputService;
        _config = config;
        _verificationService = verificationService;
    }

    /// <summary>
    /// Run the full loop workflow (plan then build phases).
    /// </summary>
    public async Task<int> RunAsync(bool skipPlan = false, bool skipBuild = false, CancellationToken ct = default)
    {
        try
        {
            // Check we're not on main branch
            if (_stateManager.IsOnMainBranch())
            {
                _outputService.Error("Cannot run loop on main/master branch. Create a feature branch first.");
                return ExitCodes.GeneralError;
            }

            // Run PLAN phase (once)
            if (!skipPlan)
            {
                await RunPlanPhaseAsync(ct);
            }

            // Run BUILD phase (loop until done or cancelled)
            if (!skipBuild)
            {
                return await RunBuildPhaseAsync(ct);
            }

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            _outputService.Warning("Loop cancelled by user.");
            return ExitCodes.Success;
        }
    }

    /// <summary>
    /// Run the plan phase once.
    /// </summary>
    public async Task RunPlanPhaseAsync(CancellationToken ct = default)
    {
        _outputService.WritePhaseHeader("PLAN");

        // Remove lopen.loop.done if it exists
        _stateManager.RemoveDoneFile();

        // Load plan prompt
        string prompt;
        try
        {
            prompt = await _stateManager.LoadPromptAsync(_config.PlanPromptPath, ct);
        }
        catch (FileNotFoundException ex)
        {
            _outputService.Error($"Plan prompt not found: {ex.Message}");
            _outputService.Muted($"Expected file: {_config.PlanPromptPath}");
            return;
        }

        // Create session and stream
        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions
            {
                Model = _config.Model,
                Streaming = _config.Stream,
                AllowAll = _config.AllowAll
            }, ct);

        // Stream prompt execution
        await foreach (var chunk in session.StreamAsync(prompt, ct))
        {
            _outputService.WriteChunk(chunk);
        }

        _outputService.WriteLine();
        _outputService.WriteIterationComplete();
    }

    /// <summary>
    /// Run the build phase loop until complete or cancelled.
    /// </summary>
    public async Task<int> RunBuildPhaseAsync(CancellationToken ct = default)
    {
        // Load build prompt
        string prompt;
        try
        {
            prompt = await _stateManager.LoadPromptAsync(_config.BuildPromptPath, ct);
        }
        catch (FileNotFoundException ex)
        {
            _outputService.Error($"Build prompt not found: {ex.Message}");
            _outputService.Muted($"Expected file: {_config.BuildPromptPath}");
            return ExitCodes.GeneralError;
        }

        while (!ct.IsCancellationRequested)
        {
            // Check if loop is complete
            if (_stateManager.IsLoopComplete())
            {
                _outputService.Success("Loop complete! All jobs finished.");
                return ExitCodes.Success;
            }

            _outputService.WritePhaseHeader("BUILD");

            // Create session and stream
            await using var session = await _copilotService.CreateSessionAsync(
                new CopilotSessionOptions
                {
                    Model = _config.Model,
                    Streaming = _config.Stream,
                    AllowAll = _config.AllowAll
                }, ct);

            // Stream prompt execution
            await foreach (var chunk in session.StreamAsync(prompt, ct))
            {
                _outputService.WriteChunk(chunk);
            }

            _outputService.WriteLine();

            // Run verification if enabled and service is available
            if (_config.VerifyAfterIteration && _verificationService is not null)
            {
                await RunVerificationAsync(ct);
            }

            _outputService.WriteIterationComplete();

            // Brief pause between iterations
            await Task.Delay(100, ct);
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// Run verification to ensure build quality.
    /// </summary>
    private async Task RunVerificationAsync(CancellationToken ct)
    {
        _outputService.WritePhaseHeader("VERIFY");

        try
        {
            // Verify build succeeds
            var buildResult = await _verificationService!.VerifyBuildAsync(ct);
            if (!buildResult.Complete)
            {
                _outputService.Warning("Build verification failed");
                foreach (var issue in buildResult.Issues)
                {
                    _outputService.Muted($"  - {issue}");
                }
            }
            else
            {
                _outputService.Success("Build verified");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _outputService.Warning($"Verification error: {ex.Message}");
        }
    }
}
