using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of data renderer.
/// Displays tables, panels, and metadata with proper styling.
/// Respects NO_COLOR environment variable.
/// </summary>
public class SpectreDataRenderer : IDataRenderer
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;

    public SpectreDataRenderer()
        : this(AnsiConsole.Console)
    {
    }

    public SpectreDataRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
    }

    public void RenderTable<T>(IEnumerable<T> items, TableConfig<T> config)
    {
        var itemList = items.ToList();

        if (itemList.Count == 0)
        {
            RenderInfo("No items to display.");
            return;
        }

        var table = new Table();

        // Configure table border
        if (_useColors && _console.Profile.Capabilities.Interactive)
        {
            table.RoundedBorder();
            table.BorderColor(Color.Blue);
        }
        else
        {
            table.AsciiBorder();
        }

        // Set title if provided
        if (!string.IsNullOrEmpty(config.Title))
        {
            table.Title(config.Title);
        }

        // Configure expansion
        if (config.Expand)
        {
            table.Expand();
        }

        // Add columns
        foreach (var column in config.Columns)
        {
            var tableColumn = new TableColumn(column.Header);

            if (column.Width.HasValue)
            {
                tableColumn.Width(column.Width.Value);
            }

            tableColumn.Alignment(ToJustify(column.Alignment));
            table.AddColumn(tableColumn);
        }

        // Add rows
        foreach (var item in itemList)
        {
            var values = config.Columns
                .Select(c => Markup.Escape(c.Selector(item)))
                .ToArray();
            table.AddRow(values);
        }

        _console.Write(table);

        // Show row count if configured
        if (config.ShowRowCount)
        {
            var countMessage = string.Format(config.RowCountFormat, itemList.Count);
            if (_useColors)
            {
                _console.MarkupLine($"[dim]{Markup.Escape(countMessage)}[/]");
            }
            else
            {
                _console.WriteLine(countMessage);
            }
        }
    }

    public void RenderMetadata(IReadOnlyDictionary<string, string> data, string title)
    {
        if (data.Count == 0)
        {
            RenderInfo("No metadata to display.");
            return;
        }

        var grid = new Grid();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());

        foreach (var (key, value) in data)
        {
            if (_useColors)
            {
                grid.AddRow(
                    $"[bold]{Markup.Escape(key)}:[/]",
                    Markup.Escape(value)
                );
            }
            else
            {
                grid.AddRow(
                    $"{Markup.Escape(key)}:",
                    Markup.Escape(value)
                );
            }
        }

        if (_useColors && _console.Profile.Capabilities.Interactive)
        {
            var panel = new Panel(grid)
            {
                Header = new PanelHeader($" {Markup.Escape(title)} "),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Blue),
                Padding = new Padding(1, 0, 1, 0)
            };
            _console.Write(panel);
        }
        else
        {
            // ASCII fallback
            var panel = new Panel(grid)
            {
                Header = new PanelHeader($" {Markup.Escape(title)} "),
                Border = BoxBorder.Ascii,
                Padding = new Padding(1, 0, 1, 0)
            };
            _console.Write(panel);
        }
    }

    public void RenderInfo(string message)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[dim]{Markup.Escape(message)}[/]");
        }
        else
        {
            _console.WriteLine(message);
        }
    }

    private static Justify ToJustify(ColumnAlignment alignment) => alignment switch
    {
        ColumnAlignment.Center => Justify.Center,
        ColumnAlignment.Right => Justify.Right,
        _ => Justify.Left
    };
}
