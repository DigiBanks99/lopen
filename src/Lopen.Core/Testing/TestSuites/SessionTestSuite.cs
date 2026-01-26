namespace Lopen.Core.Testing.TestSuites;

/// <summary>
/// Test cases for session management commands.
/// </summary>
public static class SessionTestSuite
{
    /// <summary>Suite name.</summary>
    public const string SuiteName = "session";
    
    /// <summary>
    /// Get all session command tests.
    /// </summary>
    public static IEnumerable<ITestCase> GetTests()
    {
        yield return new CommandTestCase(
            testId: "T-SESSION-01",
            description: "List sessions",
            suite: SuiteName,
            commandArgs: ["sessions", "list"],
            validator: new KeywordValidator("session", "No sessions", "ID", "Modified", "Summary"),
            expectedExitCode: 0
        );
    }
}
