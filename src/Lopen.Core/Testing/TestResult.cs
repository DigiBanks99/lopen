namespace Lopen.Core.Testing;

/// <summary>
/// Result of a single test execution.
/// </summary>
public sealed record TestResult
{
    /// <summary>Unique identifier for the test.</summary>
    public required string TestId { get; init; }
    
    /// <summary>Name of the test suite.</summary>
    public required string Suite { get; init; }
    
    /// <summary>Test description.</summary>
    public required string Description { get; init; }
    
    /// <summary>Status of the test execution.</summary>
    public required TestStatus Status { get; init; }
    
    /// <summary>Duration of the test execution.</summary>
    public required TimeSpan Duration { get; init; }
    
    /// <summary>When the test started.</summary>
    public DateTimeOffset StartTime { get; init; }
    
    /// <summary>When the test ended.</summary>
    public DateTimeOffset EndTime { get; init; }
    
    /// <summary>Preview of the response (truncated).</summary>
    public string? ResponsePreview { get; init; }
    
    /// <summary>Pattern that was matched (for successful validation).</summary>
    public string? MatchedPattern { get; init; }
    
    /// <summary>Error message if test failed.</summary>
    public string? Error { get; init; }
    
    /// <summary>Input that was used for the test.</summary>
    public string? Input { get; init; }
}
