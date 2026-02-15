using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Validates task completion by checking that a passing oracle verification
/// exists in <see cref="IVerificationTracker"/> for the given scope and identifier.
/// </summary>
internal sealed class TaskStatusGate : ITaskStatusGate
{
    private readonly IVerificationTracker _verificationTracker;
    private readonly ILogger<TaskStatusGate> _logger;

    public TaskStatusGate(
        IVerificationTracker verificationTracker,
        ILogger<TaskStatusGate> logger)
    {
        _verificationTracker = verificationTracker ?? throw new ArgumentNullException(nameof(verificationTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TaskStatusGateResult ValidateCompletion(VerificationScope scope, string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (_verificationTracker.IsVerified(scope, identifier))
        {
            _logger.LogDebug(
                "Task completion allowed for {Scope}/{Identifier} — passing verification exists",
                scope, identifier);
            return TaskStatusGateResult.Allowed();
        }

        var toolName = scope switch
        {
            VerificationScope.Task => "verify_task_completion",
            VerificationScope.Component => "verify_component_completion",
            VerificationScope.Module => "verify_module_completion",
            _ => "verify_task_completion",
        };

        var reason = $"Cannot mark {scope.ToString().ToLowerInvariant()} '{identifier}' as complete: " +
                     $"no passing oracle verification found. Call {toolName} first and ensure it passes.";

        _logger.LogWarning(
            "Task completion rejected for {Scope}/{Identifier} — no passing verification",
            scope, identifier);

        return TaskStatusGateResult.Rejected(reason);
    }
}
