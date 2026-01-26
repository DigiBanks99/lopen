using System.Collections.Concurrent;

namespace Lopen.Core.Testing;

/// <summary>
/// Orchestrates parallel test execution.
/// </summary>
public sealed class TestRunner
{
    private readonly int _maxParallelism;
    
    /// <summary>
    /// Creates a test runner.
    /// </summary>
    /// <param name="maxParallelism">Maximum number of concurrent tests (default: 4).</param>
    public TestRunner(int maxParallelism = 4)
    {
        _maxParallelism = maxParallelism;
    }
    
    /// <summary>
    /// Run all tests in parallel and return aggregated results.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="context">Shared test context.</param>
    /// <param name="progressCallback">Optional callback for progress updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of all test results.</returns>
    public async Task<TestRunSummary> RunTestsAsync(
        IEnumerable<ITestCase> tests,
        TestContext context,
        Action<TestResult>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var testList = tests.ToList();
        var results = new ConcurrentBag<TestResult>();
        var startTime = DateTimeOffset.Now;
        
        await Parallel.ForEachAsync(
            testList,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxParallelism,
                CancellationToken = cancellationToken
            },
            async (test, ct) =>
            {
                var result = await test.ExecuteAsync(context, ct);
                results.Add(result);
                progressCallback?.Invoke(result);
            });
        
        var endTime = DateTimeOffset.Now;
        
        return new TestRunSummary
        {
            StartTime = startTime,
            EndTime = endTime,
            Model = context.Model,
            Results = results.OrderBy(r => r.TestId).ToList()
        };
    }
}
