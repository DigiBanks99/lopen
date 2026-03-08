using Lopen.Storage;

namespace Lopen.Core.Workflow;

/// <summary>
/// Drives the workflow loop: assess state → invoke LLM → evaluate guardrails → transition.
/// Separate from IWorkflowEngine (SRP: engine handles state machine, orchestrator drives the loop).
/// </summary>
public interface IWorkflowOrchestrator
{
    /// <summary>
    /// The active module name after initialization.
    /// </summary>
    string? ActiveModule { get; }

    /// <summary>
    /// Initializes the orchestrator for step-by-step interactive mode.
    /// Must be called before RunStepAsync in TUI mode.
    /// Optionally resumes a specific session, restoring token metrics.
    /// </summary>
    Task InitializeAsync(string moduleName, SessionId? resumeSessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the orchestration loop for a module until completion or interruption.
    /// </summary>
    /// <param name="moduleName">The module to orchestrate.</param>
    /// <param name="userPrompt">Optional user prompt to inject into LLM context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final result of the orchestration run.</returns>
    Task<OrchestrationResult> RunAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a single step of the orchestration loop.
    /// </summary>
    /// <param name="moduleName">The module being orchestrated.</param>
    /// <param name="userPrompt">Optional user prompt to inject into LLM context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of executing the current step.</returns>
    Task<StepResult> RunStepAsync(string moduleName, string? userPrompt = null, CancellationToken cancellationToken = default);
}
