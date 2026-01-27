using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Result of interactive loop configuration.
/// </summary>
public sealed record InteractiveLoopConfigResult
{
    /// <summary>Was the operation cancelled.</summary>
    public bool Cancelled { get; init; }
    
    /// <summary>Updated configuration (null if cancelled).</summary>
    public LoopConfig? Config { get; init; }
}

/// <summary>
/// Interface for interactive loop configuration prompts.
/// </summary>
public interface IInteractiveLoopConfigService
{
    /// <summary>
    /// Prompts the user to configure loop settings interactively.
    /// </summary>
    /// <param name="currentConfig">Current configuration to use as defaults.</param>
    /// <returns>Result containing updated config or cancellation status.</returns>
    InteractiveLoopConfigResult PromptForConfiguration(LoopConfig currentConfig);
}

/// <summary>
/// Spectre.Console implementation of interactive loop configuration.
/// </summary>
public class SpectreInteractiveLoopConfigService : IInteractiveLoopConfigService
{
    private readonly IAnsiConsole _console;
    
    // Available models for selection
    private static readonly string[] AvailableModels = new[]
    {
        "claude-opus-4.5",
        "claude-sonnet-4",
        "gpt-5",
        "gpt-5-mini",
        "gemini-3-pro"
    };
    
    public SpectreInteractiveLoopConfigService()
        : this(AnsiConsole.Console)
    {
    }
    
    public SpectreInteractiveLoopConfigService(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }
    
    public InteractiveLoopConfigResult PromptForConfiguration(LoopConfig currentConfig)
    {
        try
        {
            _console.MarkupLine("[bold cyan]Loop Configuration[/]");
            _console.MarkupLine("[dim]Press Ctrl+C to cancel[/]");
            _console.WriteLine();
            
            // Model selection
            var modelChoices = AvailableModels.ToList();
            if (!modelChoices.Contains(currentConfig.Model))
            {
                modelChoices.Insert(0, currentConfig.Model);
            }
            
            // Reorder to put current model first
            var orderedModels = new[] { currentConfig.Model }
                .Concat(modelChoices.Where(m => m != currentConfig.Model))
                .ToArray();
            
            var modelPrompt = new SelectionPrompt<string>()
                .Title("[cyan]Select AI model[/]")
                .AddChoices(orderedModels);
            
            var selectedModel = _console.Prompt(modelPrompt);
            
            // Plan prompt path
            var planPromptInput = new TextPrompt<string>("[cyan]Plan prompt path[/]")
                .DefaultValue(currentConfig.PlanPromptPath)
                .ShowDefaultValue(true);
            
            var planPrompt = _console.Prompt(planPromptInput);
            
            // Build prompt path
            var buildPromptInput = new TextPrompt<string>("[cyan]Build prompt path[/]")
                .DefaultValue(currentConfig.BuildPromptPath)
                .ShowDefaultValue(true);
            
            var buildPrompt = _console.Prompt(buildPromptInput);
            
            // Stream output toggle
            var streamConfirm = new ConfirmationPrompt("[cyan]Enable stream output?[/]")
            {
                DefaultValue = currentConfig.Stream
            };
            var stream = _console.Prompt(streamConfirm);
            
            // Allow all operations toggle
            var allowAllConfirm = new ConfirmationPrompt("[cyan]Allow all Copilot SDK operations?[/]")
            {
                DefaultValue = currentConfig.AllowAll
            };
            var allowAll = _console.Prompt(allowAllConfirm);
            
            // Verify after iteration toggle
            var verifyConfirm = new ConfirmationPrompt("[cyan]Run verification after each iteration?[/]")
            {
                DefaultValue = currentConfig.VerifyAfterIteration
            };
            var verify = _console.Prompt(verifyConfirm);
            
            // Auto-commit toggle
            var autoCommitConfirm = new ConfirmationPrompt("[cyan]Auto-commit changes after each iteration?[/]")
            {
                DefaultValue = currentConfig.AutoCommit
            };
            var autoCommit = _console.Prompt(autoCommitConfirm);
            
            var newConfig = currentConfig with
            {
                Model = selectedModel,
                PlanPromptPath = planPrompt,
                BuildPromptPath = buildPrompt,
                Stream = stream,
                AllowAll = allowAll,
                VerifyAfterIteration = verify,
                AutoCommit = autoCommit
            };
            
            return new InteractiveLoopConfigResult
            {
                Cancelled = false,
                Config = newConfig
            };
        }
        catch (OperationCanceledException)
        {
            return new InteractiveLoopConfigResult { Cancelled = true };
        }
    }
}

/// <summary>
/// Mock implementation for testing.
/// </summary>
public class MockInteractiveLoopConfigService : IInteractiveLoopConfigService
{
    private InteractiveLoopConfigResult? _nextResult;
    
    /// <summary>
    /// Set the result to return on next call.
    /// </summary>
    public void SetNextResult(InteractiveLoopConfigResult result)
    {
        _nextResult = result;
    }
    
    /// <summary>
    /// Whether PromptForConfiguration was called.
    /// </summary>
    public bool WasCalled { get; private set; }
    
    /// <summary>
    /// The config passed to the last call.
    /// </summary>
    public LoopConfig? LastConfigPassed { get; private set; }
    
    public InteractiveLoopConfigResult PromptForConfiguration(LoopConfig currentConfig)
    {
        WasCalled = true;
        LastConfigPassed = currentConfig;
        return _nextResult ?? new InteractiveLoopConfigResult { Cancelled = true };
    }
}
