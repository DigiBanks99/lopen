using System.CommandLine;

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

    /// <summary>
    /// Registers all global options on the root command.
    /// </summary>
    public static void AddTo(RootCommand root)
    {
        root.Options.Add(Headless);
        root.Options.Add(Prompt);
        root.Options.Add(Resume);
        root.Options.Add(NoResume);
    }
}
