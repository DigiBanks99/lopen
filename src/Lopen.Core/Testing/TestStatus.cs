namespace Lopen.Core.Testing;

/// <summary>
/// Status of a test execution.
/// </summary>
public enum TestStatus
{
    /// <summary>Test passed all validations.</summary>
    Pass,
    
    /// <summary>Test failed validation.</summary>
    Fail,
    
    /// <summary>Test execution timed out.</summary>
    Timeout,
    
    /// <summary>Test encountered an error during execution.</summary>
    Error,
    
    /// <summary>Test was skipped.</summary>
    Skipped
}
