using System.Text.Json.Serialization;

namespace Lopen.Core;

/// <summary>
/// Configuration for the loop command.
/// </summary>
public record LoopConfig
{
    /// <summary>
    /// AI model to use (default: claude-opus-4.5).
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; init; } = "claude-opus-4.5";

    /// <summary>
    /// Path to the plan prompt file (default: PLAN.PROMPT.md).
    /// </summary>
    [JsonPropertyName("planPromptPath")]
    public string PlanPromptPath { get; init; } = "PLAN.PROMPT.md";

    /// <summary>
    /// Path to the build prompt file (default: BUILD.PROMPT.md).
    /// </summary>
    [JsonPropertyName("buildPromptPath")]
    public string BuildPromptPath { get; init; } = "BUILD.PROMPT.md";

    /// <summary>
    /// Allow all Copilot SDK operations (default: true).
    /// </summary>
    [JsonPropertyName("allowAll")]
    public bool AllowAll { get; init; } = true;

    /// <summary>
    /// Enable streaming output (default: true).
    /// </summary>
    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = true;

    /// <summary>
    /// Auto-commit changes after each iteration (default: false).
    /// </summary>
    [JsonPropertyName("autoCommit")]
    public bool AutoCommit { get; init; } = false;

    /// <summary>
    /// Log level: all, info, error (default: all).
    /// </summary>
    [JsonPropertyName("logLevel")]
    public string LogLevel { get; init; } = "all";

    /// <summary>
    /// Run verification after each build iteration (default: true).
    /// When enabled, jobs cannot be marked complete without passing verification.
    /// </summary>
    [JsonPropertyName("verifyAfterIteration")]
    public bool VerifyAfterIteration { get; init; } = true;

    /// <summary>
    /// Creates a new config with values from another config merged in.
    /// Non-default values from the override config take precedence.
    /// </summary>
    public LoopConfig MergeWith(LoopConfig? overrideConfig)
    {
        if (overrideConfig is null)
            return this;

        var defaults = new LoopConfig();

        return new LoopConfig
        {
            Model = overrideConfig.Model != defaults.Model ? overrideConfig.Model : Model,
            PlanPromptPath = overrideConfig.PlanPromptPath != defaults.PlanPromptPath ? overrideConfig.PlanPromptPath : PlanPromptPath,
            BuildPromptPath = overrideConfig.BuildPromptPath != defaults.BuildPromptPath ? overrideConfig.BuildPromptPath : BuildPromptPath,
            AllowAll = overrideConfig.AllowAll != defaults.AllowAll ? overrideConfig.AllowAll : AllowAll,
            Stream = overrideConfig.Stream != defaults.Stream ? overrideConfig.Stream : Stream,
            AutoCommit = overrideConfig.AutoCommit != defaults.AutoCommit ? overrideConfig.AutoCommit : AutoCommit,
            LogLevel = overrideConfig.LogLevel != defaults.LogLevel ? overrideConfig.LogLevel : LogLevel,
            VerifyAfterIteration = overrideConfig.VerifyAfterIteration != defaults.VerifyAfterIteration ? overrideConfig.VerifyAfterIteration : VerifyAfterIteration
        };
    }
}
