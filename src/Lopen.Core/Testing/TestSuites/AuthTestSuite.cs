namespace Lopen.Core.Testing.TestSuites;

/// <summary>
/// Test cases for authentication commands.
/// </summary>
public static class AuthTestSuite
{
    /// <summary>Suite name.</summary>
    public const string SuiteName = "auth";
    
    /// <summary>
    /// Get all auth command tests.
    /// </summary>
    public static IEnumerable<ITestCase> GetTests()
    {
        yield return new CommandTestCase(
            testId: "T-AUTH-01",
            description: "Check auth status",
            suite: SuiteName,
            commandArgs: ["auth", "status"],
            validator: new KeywordValidator("Authenticated", "Not authenticated", "token", "status"),
            expectedExitCode: 0
        );
    }
}
