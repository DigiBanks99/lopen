using Shouldly;
using Lopen.Core.Testing;

namespace Lopen.Core.Tests.Testing;

public class CommandTestCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithNonExistentPath_ReturnsError()
    {
        var testCase = new CommandTestCase(
            testId: "T-01",
            description: "Test with invalid path",
            suite: "test",
            commandArgs: new[] { "--version" },
            validator: new KeywordValidator(new[] { "lopen" })
        );
        
        var context = new TestContext
        {
            LopenPath = "/nonexistent/lopen/binary/path",
            Timeout = TimeSpan.FromSeconds(5),
            Verbose = false
        };
        
        var result = await testCase.ExecuteAsync(context);
        
        result.Status.ShouldBe(TestStatus.Error);
        result.Error.ShouldNotBeNull();
        result.Error.ShouldNotContain("   at "); // No stack trace in non-verbose
    }
    
    [Fact]
    public async Task ExecuteAsync_WithNonExistentPath_VerboseMode_IncludesStackTrace()
    {
        var testCase = new CommandTestCase(
            testId: "T-01",
            description: "Test with invalid path verbose",
            suite: "test",
            commandArgs: new[] { "--version" },
            validator: new KeywordValidator(new[] { "lopen" })
        );
        
        var context = new TestContext
        {
            LopenPath = "/nonexistent/lopen/binary/path",
            Timeout = TimeSpan.FromSeconds(5),
            Verbose = true
        };
        
        var result = await testCase.ExecuteAsync(context);
        
        result.Status.ShouldBe(TestStatus.Error);
        result.Error.ShouldNotBeNull();
        // In verbose mode, stack trace is included (contains "at" markers)
        result.Error.ShouldContain("   at ");
    }
    
    [Fact]
    public void Constructor_SetsProperties()
    {
        var testCase = new CommandTestCase(
            testId: "T-42",
            description: "My test description",
            suite: "my-suite",
            commandArgs: new[] { "chat", "hello" },
            validator: new KeywordValidator(new[] { "world" }),
            expectedExitCode: 0
        );
        
        testCase.TestId.ShouldBe("T-42");
        testCase.Description.ShouldBe("My test description");
        testCase.Suite.ShouldBe("my-suite");
    }
    
    [Fact]
    public async Task ExecuteAsync_RecordsTimestamps()
    {
        var testCase = new CommandTestCase(
            testId: "T-01",
            description: "Timestamp test",
            suite: "test",
            commandArgs: new[] { "--version" },
            validator: new KeywordValidator(new[] { "anything" })
        );
        
        var context = new TestContext
        {
            LopenPath = "/nonexistent/path", // Will fail but that's ok
            Timeout = TimeSpan.FromSeconds(5)
        };
        
        var before = DateTimeOffset.Now;
        var result = await testCase.ExecuteAsync(context);
        var after = DateTimeOffset.Now;
        
        result.StartTime.ShouldBeGreaterThanOrEqualTo(before);
        result.EndTime.ShouldBeLessThanOrEqualTo(after);
        result.EndTime.ShouldBeGreaterThanOrEqualTo(result.StartTime);
    }
}
