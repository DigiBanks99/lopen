namespace Lopen.Core.Testing;

/// <summary>
/// Result of interactive test selection.
/// </summary>
public sealed record InteractiveTestSelection
{
    /// <summary>Selected tests to run.</summary>
    public required IReadOnlyList<ITestCase> Tests { get; init; }
    
    /// <summary>Selected model to use for tests.</summary>
    public required string Model { get; init; }
    
    /// <summary>Whether the selection was cancelled.</summary>
    public bool Cancelled { get; init; }
}

/// <summary>
/// Interface for interactive test selection UI.
/// </summary>
public interface IInteractiveTestSelector
{
    /// <summary>
    /// Prompt user to select tests and model interactively.
    /// </summary>
    /// <param name="availableTests">All available tests to select from.</param>
    /// <param name="defaultModel">Default model to suggest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Selection result with tests and model.</returns>
    InteractiveTestSelection SelectTests(
        IEnumerable<ITestCase> availableTests,
        string defaultModel,
        CancellationToken cancellationToken = default);
}
