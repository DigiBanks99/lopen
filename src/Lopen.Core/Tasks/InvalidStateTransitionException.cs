using Lopen.Core.Tasks;

namespace Lopen.Core.Tasks;

/// <summary>
/// Exception thrown when a work node state transition is invalid.
/// </summary>
public sealed class InvalidStateTransitionException : Exception
{
    /// <summary>The current state of the work node.</summary>
    public WorkNodeState CurrentState { get; }

    /// <summary>The target state that was attempted.</summary>
    public WorkNodeState TargetState { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidStateTransitionException"/>.
    /// </summary>
    public InvalidStateTransitionException(WorkNodeState currentState, WorkNodeState targetState)
        : base($"Cannot transition from {currentState} to {targetState}.")
    {
        CurrentState = currentState;
        TargetState = targetState;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidStateTransitionException"/> with a custom message.
    /// </summary>
    public InvalidStateTransitionException(string message, WorkNodeState currentState, WorkNodeState targetState)
        : base(message)
    {
        CurrentState = currentState;
        TargetState = targetState;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidStateTransitionException"/> with an inner exception.
    /// </summary>
    public InvalidStateTransitionException(string message, WorkNodeState currentState, WorkNodeState targetState, Exception innerException)
        : base(message, innerException)
    {
        CurrentState = currentState;
        TargetState = targetState;
    }
}
