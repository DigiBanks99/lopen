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

        yield return new InteractiveTestCase(
            testId: "T-AUTH-02",
            description: "Interactive device auth flow",
            suite: SuiteName,
            executeFunc: ExecuteDeviceAuthFlowTest
        );
    }

    private static async Task<TestResult> ExecuteDeviceAuthFlowTest(
        TestContext context,
        IInteractivePrompt prompt,
        CancellationToken cancellationToken)
    {
        const int totalSteps = 5;
        var startTime = DateTimeOffset.Now;

        prompt.DisplayMessage("This test validates the complete OAuth device code flow with credential storage.");
        prompt.DisplayMessage("You will need to interact with a browser and may need to complete MFA.");
        prompt.WaitForContinue("Press any key to begin the device auth flow test...");

        // Step 1: Clear existing auth
        prompt.DisplayStep(1, totalSteps, "Clear any existing authentication");
        prompt.DisplayMessage("Run: lopen auth logout");
        
        if (!prompt.Confirm("Did you run 'lopen auth logout'?"))
        {
            return CreateSkippedResult(startTime, "User skipped logout step");
        }

        // Step 2: Initiate device flow
        prompt.DisplayStep(2, totalSteps, "Start the device code authentication");
        prompt.DisplayMessage("Run: lopen auth login");
        prompt.DisplayMessage("A device code will be displayed. Copy it.");
        
        if (!prompt.Confirm("Did you run 'lopen auth login' and see a device code?"))
        {
            return CreateFailedResult(startTime, "Device code not displayed");
        }

        // Step 3: Complete OAuth in browser
        prompt.DisplayStep(3, totalSteps, "Complete OAuth flow in browser");
        prompt.DisplayMessage("Open the URL shown and enter the device code.");
        prompt.DisplayMessage("Sign in with your GitHub account.");
        
        if (!prompt.Confirm("Did you complete the browser authentication?"))
        {
            return CreateSkippedResult(startTime, "User skipped browser auth");
        }

        // Step 4: MFA if required
        prompt.DisplayStep(4, totalSteps, "Complete MFA if prompted");
        prompt.DisplayMessage("If MFA is enabled, complete the two-factor authentication.");
        
        if (!prompt.Confirm("Is MFA complete (or not required)?"))
        {
            return CreateSkippedResult(startTime, "User skipped MFA step");
        }

        // Step 5: Verify credential storage
        prompt.DisplayStep(5, totalSteps, "Verify credentials are stored");
        prompt.DisplayMessage("Run: lopen auth status");
        prompt.DisplayMessage("Expected: Should show 'Authenticated' with token info.");
        
        bool success = prompt.ConfirmSuccess("Does 'lopen auth status' show you are authenticated?");

        return new TestResult
        {
            TestId = "T-AUTH-02",
            Suite = SuiteName,
            Description = "Interactive device auth flow",
            Status = success ? TestStatus.Pass : TestStatus.Fail,
            Duration = DateTimeOffset.Now - startTime,
            StartTime = startTime,
            EndTime = DateTimeOffset.Now,
            Error = success ? null : "User reported authentication failed",
            MatchedPattern = success ? "User confirmed authentication success" : null
        };
    }

    private static TestResult CreateSkippedResult(DateTimeOffset startTime, string reason) => new()
    {
        TestId = "T-AUTH-02",
        Suite = SuiteName,
        Description = "Interactive device auth flow",
        Status = TestStatus.Skipped,
        Duration = DateTimeOffset.Now - startTime,
        StartTime = startTime,
        EndTime = DateTimeOffset.Now,
        Error = reason
    };

    private static TestResult CreateFailedResult(DateTimeOffset startTime, string reason) => new()
    {
        TestId = "T-AUTH-02",
        Suite = SuiteName,
        Description = "Interactive device auth flow",
        Status = TestStatus.Fail,
        Duration = DateTimeOffset.Now - startTime,
        StartTime = startTime,
        EndTime = DateTimeOffset.Now,
        Error = reason
    };
}
