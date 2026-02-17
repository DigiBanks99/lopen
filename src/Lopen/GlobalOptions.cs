using System.CommandLine;
using Lopen.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

/// <summary>
/// Global CLI options shared across all commands via <see cref="Option{T}.Recursive"/>.
/// </summary>
public static class GlobalOptions
{
    /// <summary>Disables TUI; outputs plain text to stdout/stderr.</summary>
    public static Option<bool> Headless { get; } = new("--headless")
    {
        Description = "Run without TUI; output plain text to stdout/stderr",
        Recursive = true,
        Aliases = { "-q", "--quiet" },
    };

    /// <summary>Injects user instructions into LLM context (headless) or populates input field (TUI).</summary>
    public static Option<string?> Prompt { get; } = new("--prompt")
    {
        Description = "Inject user instructions into the LLM context or populate TUI input",
        Recursive = true,
        Aliases = { "-p" },
    };

    /// <summary>Resumes a specific session by ID instead of starting fresh.</summary>
    public static Option<string?> Resume { get; } = new("--resume")
    {
        Description = "Resume a specific session by ID",
        Recursive = true,
    };

    /// <summary>Forces starting a fresh session, skipping the resume prompt.</summary>
    public static Option<bool> NoResume { get; } = new("--no-resume")
    {
        Description = "Start a fresh session; skip the resume prompt",
        Recursive = true,
    };

    /// <summary>Suppresses the TUI landing page modal on startup (CLI-27).</summary>
    public static Option<bool> NoWelcome { get; } = new("--no-welcome")
    {
        Description = "Suppress the TUI landing page modal on startup",
        Recursive = true,
    };

    /// <summary>Overrides the model for all workflow phases (CFG-08).</summary>
    public static Option<string?> Model { get; } = new("--model")
    {
        Description = "Override model for all phases",
        Recursive = true,
    };

    /// <summary>Suppresses intervention prompts on repeated failures (CFG-09).</summary>
    public static Option<bool> Unattended { get; } = new("--unattended")
    {
        Description = "Suppress intervention prompts on repeated failures",
        Recursive = true,
    };

    /// <summary>Maximum loop iterations before pausing (CFG-11).</summary>
    public static Option<int?> MaxIterations { get; } = new("--max-iterations")
    {
        Description = "Maximum loop iterations before pausing",
        Recursive = true,
    };

    /// <summary>
    /// Registers all global options on the root command.
    /// </summary>
    public static void AddTo(RootCommand root)
    {
        root.Options.Add(Headless);
        root.Options.Add(Prompt);
        root.Options.Add(Resume);
        root.Options.Add(NoResume);
        root.Options.Add(NoWelcome);
        root.Options.Add(Model);
        root.Options.Add(Unattended);
        root.Options.Add(MaxIterations);
    }

    /// <summary>
    /// Applies --model, --unattended, and --max-iterations overrides to DI-registered configuration singletons.
    /// Must be called after command parsing but before orchestrator usage.
    /// </summary>
    public static void ApplyConfigOverrides(IServiceProvider services, ParseResult parseResult)
    {
        var model = parseResult.GetValue(Model);
        var unattended = parseResult.GetValue(Unattended);
        var maxIterations = parseResult.GetValue(MaxIterations);

        if (model is not null)
        {
            var modelOptions = services.GetRequiredService<ModelOptions>();
            modelOptions.RequirementGathering = model;
            modelOptions.Planning = model;
            modelOptions.Building = model;
            modelOptions.Research = model;
        }

        if (unattended)
        {
            var workflowOptions = services.GetRequiredService<WorkflowOptions>();
            workflowOptions.Unattended = true;
        }

        if (maxIterations is not null)
        {
            var workflowOptions = services.GetRequiredService<WorkflowOptions>();
            workflowOptions.MaxIterations = maxIterations.Value;
        }
    }
}
