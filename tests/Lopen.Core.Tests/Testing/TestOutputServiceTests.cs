using Shouldly;
using Lopen.Core.Testing;
using Spectre.Console.Testing;

namespace Lopen.Core.Tests.Testing;

public class TestOutputServiceTests
{
    [Fact]
    public void DisplayHeader_ShowsModelAndCount()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        service.DisplayHeader("gpt-5-mini", 10);
        
        console.Output.ShouldContain("Self-Test");
        console.Output.ShouldContain("gpt-5-mini");
        console.Output.ShouldContain("10");
    }
    
    [Fact]
    public void DisplaySummary_ShowsPassedWhenAllPass()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(5),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Pass)
            }
        };
        
        service.DisplaySummary(summary);
        
        console.Output.ShouldContain("All");
        console.Output.ShouldContain("passed");
    }
    
    [Fact]
    public void DisplaySummary_ShowsFailureCount()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(5),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Fail)
            }
        };
        
        service.DisplaySummary(summary);
        
        console.Output.ShouldContain("failed");
    }
    
    [Fact]
    public void FormatAsJson_ReturnsValidJson()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(5),
            Model = "gpt-5-mini",
            Results = new List<TestResult>
            {
                CreateResult("T-01", TestStatus.Pass),
                CreateResult("T-02", TestStatus.Fail)
            }
        };
        
        var json = service.FormatAsJson(summary);
        
        json.ShouldContain("\"summary\"");
        json.ShouldContain("\"results\"");
        json.ShouldContain("\"total\": 2");
        json.ShouldContain("\"passed\": 1");
        json.ShouldContain("\"failed\": 1");
        json.ShouldContain("\"T-01\"");
        json.ShouldContain("\"T-02\"");
    }
    
    [Fact]
    public void FormatAsJson_IncludesModel()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        var summary = new TestRunSummary
        {
            StartTime = DateTimeOffset.Now,
            EndTime = DateTimeOffset.Now.AddSeconds(1),
            Model = "test-model",
            Results = new List<TestResult>()
        };
        
        var json = service.FormatAsJson(summary);
        
        json.ShouldContain("\"model\": \"test-model\"");
    }
    
    [Fact]
    public void DisplayVerboseResult_ShowsPassingTest()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        var result = new TestResult
        {
            TestId = "T-01",
            Suite = "test",
            Description = "Test description",
            Status = TestStatus.Pass,
            Duration = TimeSpan.FromSeconds(1.5),
            MatchedPattern = "hello"
        };
        
        service.DisplayVerboseResult(result);
        
        console.Output.ShouldContain("✓");
        console.Output.ShouldContain("T-01");
        console.Output.ShouldContain("Test description");
    }
    
    [Fact]
    public void DisplayVerboseResult_ShowsErrorForFailedTest()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var service = new TestOutputService(output);
        
        var result = new TestResult
        {
            TestId = "T-02",
            Suite = "test",
            Description = "Failed test",
            Status = TestStatus.Fail,
            Duration = TimeSpan.FromSeconds(0.5),
            Error = "No match found"
        };
        
        service.DisplayVerboseResult(result);
        
        console.Output.ShouldContain("✗");
        console.Output.ShouldContain("No match found");
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
