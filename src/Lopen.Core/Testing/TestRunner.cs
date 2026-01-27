using System.Collections.Concurrent;

namespace Lopen.Core.Testing;

/// <summary>
/// Orchestrates parallel test execution.
/// </summary>
public sealed class TestRunner
{
    private readonly int _maxParallelism;
    private readonly IProgressRenderer? _progressRenderer;
    
    /// <summary>
    /// Creates a test runner.
    /// </summary>
    /// <param name="maxParallelism">Maximum number of concurrent tests (default: 4).</param>
    /// <param name="progressRenderer">Optional progress renderer for showing progress bar.</param>
    public TestRunner(int maxParallelism = 4, IProgressRenderer? progressRenderer = null)
    {
        _maxParallelism = maxParallelism;
        _progressRenderer = progressRenderer;
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
        
        // If progress renderer is available and no callback provided, use progress bar
        if (_progressRenderer != null && progressCallback == null && testList.Count > 0)
        {
            await _progressRenderer.ShowProgressBarAsync(
                "Running tests",
                testList.Count,
                async progressBar =>
                {
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
                            progressBar.Increment();
                            progressBar.UpdateDescription($"Running tests ({results.Count}/{testList.Count})");
                        });
                },
                cancellationToken);
        }
        else
        {
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
        }
        
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
