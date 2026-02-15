using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public sealed class ContextBudgetManagerTests
{
    private readonly ContextBudgetManager _manager = new(NullLogger<ContextBudgetManager>.Instance);

    [Fact]
    public void FitToBudget_AllFit_ReturnsAll()
    {
        var sections = new List<ContextSection>
        {
            new("Spec", "short content", EstimatedTokens: 100),
            new("Research", "more content", EstimatedTokens: 100),
        };

        var result = _manager.FitToBudget(sections, budgetTokens: 500);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FitToBudget_ExceedsBudget_OmitsLowPriority()
    {
        var sections = new List<ContextSection>
        {
            new("High Priority", "important", EstimatedTokens: 300),
            new("Low Priority", "less important", EstimatedTokens: 300),
        };

        var result = _manager.FitToBudget(sections, budgetTokens: 400);

        Assert.Equal(2, result.Count);
        Assert.Equal("High Priority", result[0].Title);
        // Second section should be truncated
        Assert.True(result[1].EstimatedTokens <= 100);
    }

    [Fact]
    public void FitToBudget_ExactFit_ReturnsAll()
    {
        var sections = new List<ContextSection>
        {
            new("A", "content", EstimatedTokens: 250),
            new("B", "content", EstimatedTokens: 250),
        };

        var result = _manager.FitToBudget(sections, budgetTokens: 500);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FitToBudget_FirstSectionExceedsBudget_Truncates()
    {
        var longContent = new string('x', 2000);
        var sections = new List<ContextSection>
        {
            new("Big", longContent, EstimatedTokens: 500),
        };

        var result = _manager.FitToBudget(sections, budgetTokens: 200);

        Assert.Single(result);
        Assert.True(result[0].Content.Length < longContent.Length);
        Assert.Contains("truncated", result[0].Content);
    }

    [Fact]
    public void FitToBudget_EmptySections_ReturnsEmpty()
    {
        var result = _manager.FitToBudget([], budgetTokens: 500);

        Assert.Empty(result);
    }

    [Fact]
    public void FitToBudget_ZeroBudget_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _manager.FitToBudget([], budgetTokens: 0));
    }

    [Fact]
    public void FitToBudget_NullSections_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _manager.FitToBudget(null!, budgetTokens: 500));
    }

    [Fact]
    public void FitToBudget_PreservesOrder()
    {
        var sections = new List<ContextSection>
        {
            new("First", "a", EstimatedTokens: 100),
            new("Second", "b", EstimatedTokens: 100),
            new("Third", "c", EstimatedTokens: 100),
        };

        var result = _manager.FitToBudget(sections, budgetTokens: 300);

        Assert.Equal("First", result[0].Title);
        Assert.Equal("Second", result[1].Title);
        Assert.Equal("Third", result[2].Title);
    }

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, ContextBudgetManager.EstimateTokens(""));
    }

    [Fact]
    public void EstimateTokens_ShortString_ReturnsOne()
    {
        Assert.Equal(1, ContextBudgetManager.EstimateTokens("abc"));
    }

    [Fact]
    public void EstimateTokens_LongerString_DividesByCharsPerToken()
    {
        // 100 chars / 4 chars per token = 25 tokens
        var content = new string('a', 100);
        Assert.Equal(25, ContextBudgetManager.EstimateTokens(content));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ContextBudgetManager(null!));
    }
}
