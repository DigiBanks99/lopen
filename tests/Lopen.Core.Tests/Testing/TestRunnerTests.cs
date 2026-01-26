using Shouldly;
using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class TestRunnerTests
{
    [Fact]
    public async Task RunTestsAsync_ExecutesAllTests()
    {
        var runner = new TestRunner(maxParallelism: 2);
        var context = new TestContext();
        
        var tests = new List<ITestCase>
        {
            new FakeTestCase("T-01", TestStatus.Pass),
            new FakeTestCase("T-02", TestStatus.Pass),
            new FakeTestCase("T-03", TestStatus.Fail)
        };
        
        var summary = await runner.RunTestsAsync(tests, context);
        
        summary.Total.ShouldBe(3);
        summary.Passed.ShouldBe(2);
        summary.Failed.ShouldBe(1);
    }
    
    [Fact]
    public async Task RunTestsAsync_AggregatesResults()
    {
        var runner = new TestRunner();
        var context = new TestContext();
        
        var tests = new List<ITestCase>
        {
            new FakeTestCase("T-01", TestStatus.Pass),
            new FakeTestCase("T-02", TestStatus.Timeout),
            new FakeTestCase("T-03", TestStatus.Error)
        };
        
        var summary = await runner.RunTestsAsync(tests, context);
        
        summary.Passed.ShouldBe(1);
        summary.Timeouts.ShouldBe(1);
        summary.Errors.ShouldBe(1);
    }
    
    [Fact]
    public async Task RunTestsAsync_CallsProgressCallback()
    {
        var runner = new TestRunner();
        var context = new TestContext();
        var callbackResults = new List<TestResult>();
        
        var tests = new List<ITestCase>
        {
            new FakeTestCase("T-01", TestStatus.Pass),
            new FakeTestCase("T-02", TestStatus.Pass)
        };
        
        await runner.RunTestsAsync(tests, context, result => callbackResults.Add(result));
        
        callbackResults.Count.ShouldBe(2);
    }
    
    [Fact]
    public async Task RunTestsAsync_SetsModelInSummary()
    {
        var runner = new TestRunner();
        var context = new TestContext { Model = "test-model" };
        var tests = new List<ITestCase> { new FakeTestCase("T-01", TestStatus.Pass) };
        
        var summary = await runner.RunTestsAsync(tests, context);
        
        summary.Model.ShouldBe("test-model");
    }
    
    [Fact]
    public async Task RunTestsAsync_OrdersResultsById()
    {
        var runner = new TestRunner(maxParallelism: 1);
        var context = new TestContext();
        
        var tests = new List<ITestCase>
        {
            new FakeTestCase("T-03", TestStatus.Pass),
            new FakeTestCase("T-01", TestStatus.Pass),
            new FakeTestCase("T-02", TestStatus.Pass)
        };
        
        var summary = await runner.RunTestsAsync(tests, context);
        
        summary.Results[0].TestId.ShouldBe("T-01");
        summary.Results[1].TestId.ShouldBe("T-02");
        summary.Results[2].TestId.ShouldBe("T-03");
    }
    
    [Fact]
    public async Task RunTestsAsync_HandlesCancellation()
    {
        var runner = new TestRunner();
        var context = new TestContext();
        var cts = new CancellationTokenSource();
        
        var tests = new List<ITestCase>
        {
            new SlowTestCase("T-01", TimeSpan.FromSeconds(5)),
            new SlowTestCase("T-02", TimeSpan.FromSeconds(5))
        };
        
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        
        await Should.ThrowAsync<OperationCanceledException>(
            runner.RunTestsAsync(tests, context, cancellationToken: cts.Token));
    }
    
    /// <summary>
    /// Fake test case that returns a predetermined result.
    /// </summary>
    private sealed class FakeTestCase : ITestCase
    {
        private readonly TestStatus _status;
        
        public string TestId { get; }
        public string Description => "Fake test";
        public string Suite => "fake";
        
        public FakeTestCase(string testId, TestStatus status)
        {
            TestId = testId;
            _status = status;
        }
        
        public Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResult
            {
                TestId = TestId,
                Suite = Suite,
                Description = Description,
                Status = _status,
                Duration = TimeSpan.FromMilliseconds(10)
            });
        }
    }
    
    /// <summary>
    /// Slow test case for testing cancellation.
    /// </summary>
    private sealed class SlowTestCase : ITestCase
    {
        private readonly TimeSpan _delay;
        
        public string TestId { get; }
        public string Description => "Slow test";
        public string Suite => "slow";
        
        public SlowTestCase(string testId, TimeSpan delay)
        {
            TestId = testId;
            _delay = delay;
        }
        
        public async Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken);
            return new TestResult
            {
                TestId = TestId,
                Suite = Suite,
                Description = Description,
                Status = TestStatus.Pass,
                Duration = _delay
            };
        }
    }
}
