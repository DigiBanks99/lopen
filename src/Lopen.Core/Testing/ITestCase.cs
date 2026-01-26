namespace Lopen.Core.Testing;

/// <summary>
/// Represents a test case that can be executed.
/// </summary>
public interface ITestCase
{
    /// <summary>Unique identifier for the test (e.g., T-CHAT-01).</summary>
    string TestId { get; }
    
    /// <summary>Human-readable description of what the test validates.</summary>
    string Description { get; }
    
    /// <summary>Name of the test suite this test belongs to.</summary>
    string Suite { get; }
    
    /// <summary>
    /// Execute the test and return the result.
    /// </summary>
    /// <param name="context">Shared test execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the test execution.</returns>
    Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default);
}
