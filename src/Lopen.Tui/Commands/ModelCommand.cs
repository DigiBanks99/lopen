using Lopen.Configuration;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Lopen.Tui.Commands;

public sealed class ModelCommand(IAnsiConsole console, IOptions<LopenOptions> options) : ISlashCommand
{
    private readonly IAnsiConsole _console = console;
    private readonly IOptions<LopenOptions> _options = options;

    public string Name => "model";
    public string Description => "Show or switch model";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken cancellationToken = default)
    {
        ModelOptions models = _options.Value.Models;

        if (string.IsNullOrWhiteSpace(args))
        {
            Table table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(LopenTheme.Muted)
                .AddColumn(new TableColumn(LopenTheme.Bold("Phase", LopenTheme.Secondary)))
                .AddColumn(new TableColumn(LopenTheme.Bold("Model", LopenTheme.Accent)));

            table.AddRow("Requirement Gathering", Markup.Escape(models.RequirementGathering));
            table.AddRow("Planning", Markup.Escape(models.Planning));
            table.AddRow("Building", Markup.Escape(models.Building));
            table.AddRow("Research", Markup.Escape(models.Research));
            table.AddRow(LopenTheme.Styled("Global Fallback", LopenTheme.Muted),
                Markup.Escape(models.GlobalFallback));

            _console.Write(table);
        }
        else
        {
            models.RequirementGathering = args;
            models.Planning = args;
            models.Building = args;
            models.Research = args;

            _console.MarkupLine($"{LopenTheme.SectionMarker} {LopenTheme.Styled($"Model switched to {Markup.Escape(args)}", LopenTheme.Accent)}");
        }

        return Task.FromResult(SlashCommandResult.Handled);
    }
}
