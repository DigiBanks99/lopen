using Shouldly;
using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class TestResultTests
{
    [Fact]
    public void TestResult_CanBeCreated()
    {
        var startTime = DateTimeOffset.Now;
        
        var result = new TestResult
        {
            TestId = "T-TEST-01",
            Suite = "test",
            Description = "Test description",
            Status = TestStatus.Pass,
            Duration = TimeSpan.FromSeconds(1.5),
            StartTime = startTime,
            EndTime = startTime + TimeSpan.FromSeconds(1.5),
            MatchedPattern = "hello"
        };
        
        result.TestId.ShouldBe("T-TEST-01");
        result.Suite.ShouldBe("test");
        result.Description.ShouldBe("Test description");
        result.Status.ShouldBe(TestStatus.Pass);
        result.Duration.TotalSeconds.ShouldBe(1.5);
        result.MatchedPattern.ShouldBe("hello");
    }
    
    [Fact]
    public void TestResult_OptionalProperties_AreNull()
    {
        var result = new TestResult
        {
            TestId = "T-TEST-01",
            Suite = "test",
            Description = "Test",
            Status = TestStatus.Fail,
            Duration = TimeSpan.FromSeconds(1)
        };
        
        result.ResponsePreview.ShouldBeNull();
        result.MatchedPattern.ShouldBeNull();
        result.Error.ShouldBeNull();
        result.Input.ShouldBeNull();
    }
}
