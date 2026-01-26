using Shouldly;

namespace Lopen.Core.Tests;

public class LoopConfigTests
{
    [Fact]
    public void DefaultConfig_HasExpectedValues()
    {
        var config = new LoopConfig();

        config.Model.ShouldBe("gpt-5");
        config.PlanPromptPath.ShouldBe("PLAN.PROMPT.md");
        config.BuildPromptPath.ShouldBe("BUILD.PROMPT.md");
        config.AllowAll.ShouldBeTrue();
        config.Stream.ShouldBeTrue();
        config.AutoCommit.ShouldBeFalse();
        config.LogLevel.ShouldBe("all");
    }

    [Fact]
    public void MergeWith_NullOverride_ReturnsOriginal()
    {
        var config = new LoopConfig { Model = "claude-opus-4.5" };

        var result = config.MergeWith(null);

        result.Model.ShouldBe("claude-opus-4.5");
    }

    [Fact]
    public void MergeWith_OverridesNonDefaultValues()
    {
        var config = new LoopConfig();
        var overrideConfig = new LoopConfig { Model = "claude-opus-4.5" };

        var result = config.MergeWith(overrideConfig);

        result.Model.ShouldBe("claude-opus-4.5");
        result.PlanPromptPath.ShouldBe("PLAN.PROMPT.md"); // Default unchanged
    }

    [Fact]
    public void MergeWith_PreservesBaseForDefaultOverrideValues()
    {
        var config = new LoopConfig { Model = "custom-model" };
        var overrideConfig = new LoopConfig(); // All defaults

        var result = config.MergeWith(overrideConfig);

        result.Model.ShouldBe("custom-model"); // Base value preserved
    }

    [Fact]
    public void MergeWith_MultipleFields()
    {
        var config = new LoopConfig();
        var overrideConfig = new LoopConfig
        {
            Model = "custom-model",
            PlanPromptPath = "custom-plan.md",
            Stream = false
        };

        var result = config.MergeWith(overrideConfig);

        result.Model.ShouldBe("custom-model");
        result.PlanPromptPath.ShouldBe("custom-plan.md");
        result.Stream.ShouldBeFalse();
        result.BuildPromptPath.ShouldBe("BUILD.PROMPT.md"); // Default
    }

    [Fact]
    public void Config_IsImmutableRecord()
    {
        var config = new LoopConfig { Model = "model1" };
        var modified = config with { Model = "model2" };

        config.Model.ShouldBe("model1");
        modified.Model.ShouldBe("model2");
    }
}
