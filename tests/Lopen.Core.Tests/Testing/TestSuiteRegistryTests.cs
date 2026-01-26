using Shouldly;
using Lopen.Core.Testing;
using Lopen.Core.Testing.TestSuites;

namespace Lopen.Core.Tests.Testing;

public class TestSuiteRegistryTests
{
    [Fact]
    public void GetAllTests_ReturnsAllTests()
    {
        var tests = TestSuiteRegistry.GetAllTests().ToList();
        
        tests.Count.ShouldBeGreaterThan(0);
        tests.ShouldAllBe(t => !string.IsNullOrEmpty(t.TestId));
        tests.ShouldAllBe(t => !string.IsNullOrEmpty(t.Suite));
    }
    
    [Fact]
    public void GetSuiteNames_ReturnsAllSuites()
    {
        var suites = TestSuiteRegistry.GetSuiteNames().ToList();
        
        suites.ShouldContain("core");
        suites.ShouldContain("auth");
        suites.ShouldContain("session");
        suites.ShouldContain("chat");
    }
    
    [Fact]
    public void FilterByPattern_FiltersBySuite()
    {
        var tests = TestSuiteRegistry.FilterByPattern("chat").ToList();
        
        tests.Count.ShouldBeGreaterThan(0);
        tests.ShouldAllBe(t => t.Suite == "chat");
    }
    
    [Fact]
    public void FilterByPattern_FiltersByTestId()
    {
        var tests = TestSuiteRegistry.FilterByPattern("T-CORE").ToList();
        
        tests.Count.ShouldBeGreaterThan(0);
        tests.ShouldAllBe(t => t.TestId.Contains("T-CORE"));
    }
    
    [Fact]
    public void FilterByPattern_IsCaseInsensitive()
    {
        var testsLower = TestSuiteRegistry.FilterByPattern("core").ToList();
        var testsUpper = TestSuiteRegistry.FilterByPattern("CORE").ToList();
        
        testsLower.Count.ShouldBe(testsUpper.Count);
    }
    
    [Fact]
    public void FilterByPattern_EmptyPattern_ReturnsAll()
    {
        var filtered = TestSuiteRegistry.FilterByPattern("").ToList();
        var all = TestSuiteRegistry.GetAllTests().ToList();
        
        filtered.Count.ShouldBe(all.Count);
    }
    
    [Fact]
    public void FilterByPattern_NoMatch_ReturnsEmpty()
    {
        var tests = TestSuiteRegistry.FilterByPattern("nonexistent-xyz").ToList();
        
        tests.Count.ShouldBe(0);
    }
    
    [Fact]
    public void GetTestsBySuite_ReturnsCorrectTests()
    {
        var coreTests = TestSuiteRegistry.GetTestsBySuite("core").ToList();
        
        coreTests.Count.ShouldBeGreaterThan(0);
        coreTests.ShouldAllBe(t => t.Suite == "core");
    }
    
    [Fact]
    public void GetTestsBySuite_UnknownSuite_ReturnsEmpty()
    {
        var tests = TestSuiteRegistry.GetTestsBySuite("unknown").ToList();
        
        tests.Count.ShouldBe(0);
    }
    
    [Fact]
    public void AllTests_HaveUniqueIds()
    {
        var tests = TestSuiteRegistry.GetAllTests().ToList();
        var uniqueIds = tests.Select(t => t.TestId).Distinct().ToList();
        
        uniqueIds.Count.ShouldBe(tests.Count);
    }
    
    [Fact]
    public void AllTests_FollowNamingConvention()
    {
        var tests = TestSuiteRegistry.GetAllTests().ToList();
        
        // All test IDs should follow T-SUITE-NN pattern
        tests.ShouldAllBe(t => t.TestId.StartsWith("T-"));
    }
}
