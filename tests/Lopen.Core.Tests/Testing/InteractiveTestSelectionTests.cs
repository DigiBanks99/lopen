using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class InteractiveTestSelectionTests
{
    [Fact]
    public void InteractiveTestSelection_RequiredProperties()
    {
        // Arrange & Act
        var selection = new InteractiveTestSelection
        {
            Tests = Array.Empty<ITestCase>(),
            Model = "gpt-5-mini",
            Cancelled = false
        };
        
        // Assert
        Assert.NotNull(selection.Tests);
        Assert.Equal("gpt-5-mini", selection.Model);
        Assert.False(selection.Cancelled);
    }
    
    [Fact]
    public void InteractiveTestSelection_WithTests()
    {
        // Arrange
        var mockTest = new TestCaseStub("T-TEST-01", "Test", "Suite");
        
        // Act
        var selection = new InteractiveTestSelection
        {
            Tests = new[] { mockTest },
            Model = "gpt-5",
            Cancelled = false
        };
        
        // Assert
        Assert.Single(selection.Tests);
        Assert.Equal("T-TEST-01", selection.Tests[0].TestId);
    }
    
    [Fact]
    public void InteractiveTestSelection_Cancelled()
    {
        // Act
        var selection = new InteractiveTestSelection
        {
            Tests = Array.Empty<ITestCase>(),
            Model = "gpt-5-mini",
            Cancelled = true
        };
        
        // Assert
        Assert.True(selection.Cancelled);
        Assert.Empty(selection.Tests);
    }
    
    private sealed class TestCaseStub : ITestCase
    {
        public string TestId { get; }
        public string Description { get; }
        public string Suite { get; }
        
        public TestCaseStub(string testId, string description, string suite)
        {
            TestId = testId;
            Description = description;
            Suite = suite;
        }
        
        public Task<TestResult> ExecuteAsync(TestContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResult
            {
                TestId = TestId,
                Suite = Suite,
                Description = Description,
                Status = TestStatus.Pass,
                Duration = TimeSpan.Zero,
                StartTime = DateTimeOffset.Now,
                EndTime = DateTimeOffset.Now
            });
        }
    }
}
