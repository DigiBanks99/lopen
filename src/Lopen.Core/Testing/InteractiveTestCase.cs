namespace Lopen.Core.Testing;

/// <summary>
/// Interface for prompting user during interactive tests.
/// </summary>
public interface IInteractivePrompt
{
    /// <summary>
    /// Display a message to the user.
    /// </summary>
    void DisplayMessage(string message);

    /// <summary>
    /// Display a step instruction to the user.
    /// </summary>
    void DisplayStep(int stepNumber, int totalSteps, string instruction);

    /// <summary>
    /// Ask user to confirm they completed a step.
    /// </summary>
    /// <param name="prompt">The confirmation prompt.</param>
    /// <returns>True if user confirms, false otherwise.</returns>
    bool Confirm(string prompt);

    /// <summary>
    /// Ask user to confirm the test passed.
    /// </summary>
    /// <param name="prompt">The confirmation prompt.</param>
    /// <returns>True if test passed, false if failed.</returns>
    bool ConfirmSuccess(string prompt);

    /// <summary>
    /// Wait for user to press any key to continue.
    /// </summary>
    void WaitForContinue(string message = "Press any key to continue...");
}

/// <summary>
/// Test case that requires user interaction.
/// Guides the user through a series of steps and asks for confirmation.
/// </summary>
public sealed class InteractiveTestCase : ITestCase
{
    private readonly Func<TestContext, IInteractivePrompt, CancellationToken, Task<TestResult>> _executeFunc;

    /// <inheritdoc/>
    public string TestId { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public string Suite { get; }

    /// <summary>
    /// Gets whether this test requires interactive input.
    /// </summary>
    public bool RequiresInteraction => true;

    /// <summary>
    /// Creates an interactive test case.
    /// </summary>
    /// <param name="testId">Unique test identifier.</param>
    /// <param name="description">Test description.</param>
    /// <param name="suite">Test suite name.</param>
    /// <param name="executeFunc">Function that executes the test with user prompts.</param>
    public InteractiveTestCase(
        string testId,
        string description,
        string suite,
        Func<TestContext, IInteractivePrompt, CancellationToken, Task<TestResult>> executeFunc)
    {
        TestId = testId;
        Description = description;
        Suite = suite;
        _executeFunc = executeFunc ?? throw new ArgumentNullException(nameof(executeFunc));
    }

    /// <inheritdoc/>
    public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
    {
        // Requires interactive prompt from context
        if (context.InteractivePrompt is null)
        {
            return new TestResult
            {
                TestId = TestId,
                Suite = Suite,
                Description = Description,
                Status = TestStatus.Skipped,
                Duration = TimeSpan.Zero,
                StartTime = DateTimeOffset.Now,
                EndTime = DateTimeOffset.Now,
                Error = "Interactive test requires -i/--interactive mode"
            };
        }

        var startTime = DateTimeOffset.Now;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await _executeFunc(context, context.InteractivePrompt, cancellationToken);
            stopwatch.Stop();

            // Ensure timing info is set
            return result with
            {
                Duration = stopwatch.Elapsed,
                StartTime = startTime,
                EndTime = DateTimeOffset.Now
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new TestResult
            {
                TestId = TestId,
                Suite = Suite,
                Description = Description,
                Status = TestStatus.Skipped,
                Duration = stopwatch.Elapsed,
                StartTime = startTime,
                EndTime = DateTimeOffset.Now,
                Error = "Test cancelled by user"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new TestResult
            {
                TestId = TestId,
                Suite = Suite,
                Description = Description,
                Status = TestStatus.Error,
                Duration = stopwatch.Elapsed,
                StartTime = startTime,
                EndTime = DateTimeOffset.Now,
                Error = ex.Message
            };
        }
    }
}
