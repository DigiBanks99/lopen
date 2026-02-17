namespace Lopen.Tui;

/// <summary>
/// Executes slash commands entered in the TUI prompt area.
/// </summary>
public interface ISlashCommandExecutor
{
    /// <summary>
    /// Executes a slash command from the given input text.
    /// Returns a result indicating success/failure and output message.
    /// </summary>
    Task<SlashCommandResult> ExecuteAsync(string input, CancellationToken cancellationToken = default);
}
