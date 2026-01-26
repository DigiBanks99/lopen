using Shouldly;
using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class TestRunSummaryTests
{
    [Fact]
    public void TestRunSummary_CalculatesPassedCount()
    {
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(5),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Pass),
                CreateResult("T-03", TestStatus.Fail)
            }
        };
        
        summary.Total.ShouldBe(3);
        summary.Passed.ShouldBe(2);
        summary.Failed.ShouldBe(1);
        summary.AllPassed.ShouldBeFalse();
    }
    
    [Fact]
    public void TestRunSummary_AllPassed_WhenNoFailures()
    {
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(3),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Pass)
            }
        };
        
        summary.AllPassed.ShouldBeTrue();
    }
    
    [Fact]
    public void TestRunSummary_CountsTimeouts()
    {
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(30),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Timeout)
            }
        };
        
        summary.Timeouts.ShouldBe(1);
        summary.AllPassed.ShouldBeFalse();
    }
    
    [Fact]
    public void TestRunSummary_CountsErrors()
    {
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(5),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Error)
            }
        };
        
        summary.Errors.ShouldBe(1);
    }
    
    [Fact]
    public void TestRunSummary_CalculatesDuration()
    {
        var start = DateTimeOffset.Now;
        var end = start.AddSeconds(10.5);
        
        var summary = new TestRunSummary
        {
            StartTime = start,
            EndTime = end,
            Model = "gpt-5-mini",
            Results = new List<TestResult>()
        };
        
        summary.Duration.TotalSeconds.ShouldBe(10.5);
    }
    
    [Fact]
    public void TestRunSummary_CalculatesSuccessRate()
    {
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(5),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Pass),
                CreateResult("T-03", TestStatus.Fail),
                CreateResult("T-04", TestStatus.Fail)
            }
        };
        
        summary.SuccessRate.ShouldBe(50.0);
    }
    
    [Fact]
    public void TestRunSummary_EmptyResults_HasZeroSuccessRate()
    {
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now,
            Model = "gpt-5-mini",
            Results = new List<TestResult>()
        };
        
        summary.SuccessRate.ShouldBe(0);
        summary.AllPassed.ShouldBeFalse();
    }
    
    private static TestResult CreateResult(string testId, TestStatus status) => new()
    {
        TestId = testId,
        Suite = "test",
        Description = "Test",
        Status = status,
        Duration = TimeSpan.FromSeconds(1)
    };
}
