namespace Lopen.Tui.Commands;

/// <summary>
/// A slash command that can be executed from the TUI prompt.
/// </summary>
public interface ISlashCommand
{
    /// <summary>Command name without the leading slash.</summary>
    string Name { get; }

    /// <summary>Human-readable description shown in help and command palette.</summary>
    string Description { get; }

    /// <summary>Executes the command with the given arguments.</summary>
    /// <param name="args">Arguments after the command name, or empty string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a slash command execution.
/// </summary>
public enum SlashCommandResult
{
    /// <summary>Command was handled successfully.</summary>
    Handled,

    /// <summary>Input was not a slash command.</summary>
    NotACommand,

    /// <summary>User requested exit.</summary>
    ExitRequested,
}
