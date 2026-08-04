using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Result of a command palette interaction.
/// </summary>
/// <param name="SelectedCommand">The slash command name (without /) that was selected, or null if dismissed.</param>
/// <param name="IsArgumentCommand">True if the command accepts arguments and should populate the prompt instead of executing.</param>
public sealed record CommandPaletteResult(string? SelectedCommand, bool IsArgumentCommand);

/// <summary>
/// Shows a filterable command palette using Spectre.Console's SelectionPrompt.
/// Bound to Ctrl+O and the '?' shortcut (TUI-10 through TUI-13).
/// </summary>
public class CommandPalette
{
    private static readonly HashSet<string> ArgumentCommands = new(StringComparer.OrdinalIgnoreCase) { "model" };

    private readonly IAnsiConsole _console;
    private readonly ISlashCommandRegistry _registry;

    public CommandPalette(IAnsiConsole console, ISlashCommandRegistry registry)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Displays the command palette and returns the user's selection.
    /// Virtual for testability — allows substitution in unit tests.
    /// </summary>
    public virtual CommandPaletteResult Show()
    {
        IReadOnlyList<SlashCommandDescriptor> commands = _registry.GetCommands();
        if (commands.Count == 0)
        {
            return new CommandPaletteResult(null, false);
        }

        // Build a lookup from display string to command name
        Dictionary<string, string> displayToName = new(commands.Count);
        List<string> choices = new(commands.Count);

        foreach (SlashCommandDescriptor cmd in commands)
        {
            string key = $"/{cmd.Name}  {cmd.Description}";
            displayToName[key] = cmd.Name;
            choices.Add(key);
        }

        SelectionPrompt<string> prompt = new SelectionPrompt<string>()
            .Title($"[{LopenTheme.Primary.ToMarkup()} bold]Commands[/]")
            .PageSize(10)
            .EnableSearch()
            .UseConverter(item =>
            {
                if (displayToName.TryGetValue(item, out string? name))
                {
                    SlashCommandDescriptor? desc = null;
                    foreach (SlashCommandDescriptor c in commands)
                    {
                        if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            desc = c;
                            break;
                        }
                    }

                    if (desc is not null)
                    {
                        return $"[{LopenTheme.Accent.ToMarkup()}]/{Markup.Escape(desc.Name)}[/]  [{LopenTheme.Muted.ToMarkup()}]{Markup.Escape(desc.Description)}[/]";
                    }
                }

                return Markup.Escape(item);
            });

        prompt.AddChoices(choices);

        string selected;
        try
        {
            selected = _console.Prompt(prompt);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C dismisses the palette (TUI-13)
            return new CommandPaletteResult(null, false);
        }

        if (displayToName.TryGetValue(selected, out string? selectedName))
        {
            bool isArgCommand = ArgumentCommands.Contains(selectedName);
            return new CommandPaletteResult(selectedName, isArgCommand);
        }

        return new CommandPaletteResult(null, false);
    }
}
