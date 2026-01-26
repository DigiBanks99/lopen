namespace Lopen.Core.Testing.TestSuites;

/// <summary>
/// Test cases for core CLI functionality (version, help).
/// </summary>
public static class CoreTestSuite
{
    /// <summary>Suite name.</summary>
    public const string SuiteName = "core";
    
    /// <summary>
    /// Get all core CLI tests.
    /// </summary>
    public static IEnumerable<ITestCase> GetTests()
    {
        yield return new CommandTestCase(
            testId: "T-CORE-01",
            description: "Version command",
            suite: SuiteName,
            commandArgs: ["version"],
            validator: new KeywordValidator("Version", "lopen", "."),
            expectedExitCode: 0
        );
        
        yield return new CommandTestCase(
            testId: "T-CORE-02",
            description: "Help command",
            suite: SuiteName,
            commandArgs: ["help"],
            validator: new KeywordValidator("lopen", "command", "version", "help", "chat"),
            expectedExitCode: 0
        );
    }
}
