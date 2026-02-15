using Lopen.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lopen.Llm.Tests;

public class DefaultModelSelectorTests
{
    private static DefaultModelSelector CreateSelector(ModelOptions? options = null)
    {
        var lopenOptions = new LopenOptions();
        if (options is not null)
        {
            lopenOptions.Models = options;
        }
        return new DefaultModelSelector(
            Options.Create(lopenOptions),
            NullLogger<DefaultModelSelector>.Instance);
    }

    [Theory]
    [InlineData(WorkflowPhase.RequirementGathering, "claude-opus-4.6")]
    [InlineData(WorkflowPhase.Planning, "claude-opus-4.6")]
    [InlineData(WorkflowPhase.Building, "claude-opus-4.6")]
    [InlineData(WorkflowPhase.Research, "claude-opus-4.6")]
    public void SelectModel_DefaultConfig_ReturnsDefaultModels(WorkflowPhase phase, string expectedModel)
    {
        var selector = CreateSelector();

        var result = selector.SelectModel(phase);

        Assert.Equal(expectedModel, result.SelectedModel);
        Assert.False(result.WasFallback);
        Assert.Null(result.OriginalModel);
    }

    [Fact]
    public void SelectModel_CustomModel_ReturnsConfiguredModel()
    {
        var options = new ModelOptions { Building = "claude-sonnet-4" };
        var selector = CreateSelector(options);

        var result = selector.SelectModel(WorkflowPhase.Building);

        Assert.Equal("claude-sonnet-4", result.SelectedModel);
        Assert.False(result.WasFallback);
    }

    [Fact]
    public void SelectModel_EmptyModel_FallsBack()
    {
        var options = new ModelOptions { Research = "" };
        var selector = CreateSelector(options);

        var result = selector.SelectModel(WorkflowPhase.Research);

        Assert.Equal(DefaultModelSelector.FallbackModel, result.SelectedModel);
        Assert.True(result.WasFallback);
    }

    [Fact]
    public void SelectModel_WhitespaceModel_FallsBack()
    {
        var options = new ModelOptions { Planning = "   " };
        var selector = CreateSelector(options);

        var result = selector.SelectModel(WorkflowPhase.Planning);

        Assert.Equal(DefaultModelSelector.FallbackModel, result.SelectedModel);
        Assert.True(result.WasFallback);
        Assert.Equal("   ", result.OriginalModel);
    }

    [Fact]
    public void SelectModel_InvalidPhase_ThrowsArgumentOutOfRange()
    {
        var selector = CreateSelector();

        Assert.Throws<ArgumentOutOfRangeException>(() => selector.SelectModel((WorkflowPhase)99));
    }

    [Fact]
    public void SelectModel_DifferentPhasesReturnDifferentModels()
    {
        var options = new ModelOptions
        {
            RequirementGathering = "claude-opus-4.6",
            Planning = "claude-sonnet-4",
            Building = "claude-opus-4.6",
            Research = "gpt-5-mini",
        };
        var selector = CreateSelector(options);

        Assert.Equal("claude-opus-4.6", selector.SelectModel(WorkflowPhase.RequirementGathering).SelectedModel);
        Assert.Equal("claude-sonnet-4", selector.SelectModel(WorkflowPhase.Planning).SelectedModel);
        Assert.Equal("claude-opus-4.6", selector.SelectModel(WorkflowPhase.Building).SelectedModel);
        Assert.Equal("gpt-5-mini", selector.SelectModel(WorkflowPhase.Research).SelectedModel);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultModelSelector(null!, NullLogger<DefaultModelSelector>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultModelSelector(Options.Create(new LopenOptions()), null!));
    }

    [Fact]
    public void FallbackModel_IsClaurdeSonnet()
    {
        Assert.Equal("claude-sonnet-4", DefaultModelSelector.FallbackModel);
    }
}
