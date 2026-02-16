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

    /// <summary>
    /// Registers all global options on the root command.
    /// </summary>
    public static void AddTo(RootCommand root)
    {
        root.Options.Add(Headless);
        root.Options.Add(Prompt);
    }
}
