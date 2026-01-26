using Lopen.Core.Testing;
using Lopen.Core.Testing.TestSuites;
using Shouldly;

namespace Lopen.Core.Tests.Testing;

public class MockInteractiveTestSelectorTests
{
    [Fact]
    public void SelectTests_DefaultsToAllTests()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector();
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Tests.Count.ShouldBe(tests.Count);
        result.Model.ShouldBe("gpt-5-mini");
        result.Cancelled.ShouldBeFalse();
    }
    
    [Fact]
    public void SelectTests_WithSelectedTests_FiltersToSpecificTests()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector()
            .WithSelectedTests("T-CORE-01");
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Tests.Count.ShouldBe(1);
        result.Tests[0].TestId.ShouldBe("T-CORE-01");
    }
    
    [Fact]
    public void SelectTests_WithModel_OverridesDefault()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector()
            .WithModel("gpt-5");
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Model.ShouldBe("gpt-5");
    }
    
    [Fact]
    public void SelectTests_WithCancellation_ReturnsCancelled()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector()
            .WithCancellation();
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Cancelled.ShouldBeTrue();
        result.Tests.ShouldBeEmpty();
    }
    
    [Fact]
    public void SelectTests_RecordsCalls()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector();
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        selector.SelectTests(tests, "gpt-5-mini");
        selector.SelectTests(tests, "gpt-5");
        
        // Assert
        selector.Calls.Count.ShouldBe(2);
        selector.Calls[0].DefaultModel.ShouldBe("gpt-5-mini");
        selector.Calls[1].DefaultModel.ShouldBe("gpt-5");
    }
    
    [Fact]
    public void SelectTests_EmptyTestList_ReturnsEmptySelection()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector();
        var tests = Enumerable.Empty<ITestCase>();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Tests.ShouldBeEmpty();
        result.Cancelled.ShouldBeFalse();
    }
    
    [Fact]
    public void SelectTests_ChainedConfiguration_AppliesAll()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector()
            .WithSelectedTests("T-CORE-01")
            .WithModel("claude-sonnet-4");
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Tests.Count.ShouldBe(1);
        result.Model.ShouldBe("claude-sonnet-4");
        result.Cancelled.ShouldBeFalse();
    }
    
    [Fact]
    public void SelectTests_UnknownTestId_ReturnsEmpty()
    {
        // Arrange
        var selector = new MockInteractiveTestSelector()
            .WithSelectedTests("UNKNOWN-TEST");
        var tests = CoreTestSuite.GetTests().ToList();
        
        // Act
        var result = selector.SelectTests(tests, "gpt-5-mini");
        
        // Assert
        result.Tests.ShouldBeEmpty();
    }
}
