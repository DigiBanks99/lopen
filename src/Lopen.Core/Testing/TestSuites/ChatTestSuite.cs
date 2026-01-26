namespace Lopen.Core.Testing.TestSuites;

/// <summary>
/// Test cases for the chat command.
/// </summary>
public static class ChatTestSuite
{
    /// <summary>Suite name.</summary>
    public const string SuiteName = "chat";
    
    /// <summary>
    /// Get all chat command tests.
    /// </summary>
    public static IEnumerable<ITestCase> GetTests()
    {
        yield return new CommandTestCase(
            testId: "T-CHAT-01",
            description: "Basic math question",
            suite: SuiteName,
            commandArgs: ["chat", "What is 2+2? Answer with just the number."],
            validator: new KeywordValidator("4", "four")
        );
        
        yield return new CommandTestCase(
            testId: "T-CHAT-02",
            description: "Simple greeting",
            suite: SuiteName,
            commandArgs: ["chat", "Say hello"],
            validator: new KeywordValidator("hello", "hi", "greetings", "hey")
        );
    }
}
