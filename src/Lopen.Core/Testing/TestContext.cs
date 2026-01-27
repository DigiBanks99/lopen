namespace Lopen.Core.Testing;

/// <summary>
/// Shared context for test execution.
/// </summary>
public sealed record TestContext
{
    /// <summary>AI model to use for tests (default: gpt-5-mini).</summary>
    public string Model { get; init; } = "gpt-5-mini";
    
    /// <summary>Timeout per test (default: 30 seconds).</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>Whether to show verbose output.</summary>
    public bool Verbose { get; init; }
    
    /// <summary>Path to the lopen executable (default: "lopen").</summary>
    public string LopenPath { get; init; } = "lopen";

    /// <summary>
    /// Optional interactive prompt for tests requiring user interaction.
    /// Null when running in non-interactive mode.
    /// </summary>
    public IInteractivePrompt? InteractivePrompt { get; init; }
}
