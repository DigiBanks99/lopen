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
    private readonly ITerminalCapabilities? _terminalCapabilities;
    private readonly bool _useColors;

    /// <summary>
    /// Minimum terminal width for full table display.
    /// Below this, columns may be hidden based on priority.
    /// </summary>
    private const int NarrowThreshold = 80;

    /// <summary>
    /// Border and padding overhead per column (approximate).
    /// </summary>
    private const int ColumnOverhead = 3;

    public SpectreDataRenderer()
        : this(AnsiConsole.Console, null)
    {
    }

    public SpectreDataRenderer(IAnsiConsole console)
        : this(console, null)
    {
    }

    public SpectreDataRenderer(IAnsiConsole console, ITerminalCapabilities? terminalCapabilities)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _terminalCapabilities = terminalCapabilities;
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

        // Filter and configure columns based on responsive settings
        var columnsToShow = config.ResponsiveColumns
            ? GetResponsiveColumns(config.Columns)
            : config.Columns;

        // Add columns
        foreach (var column in columnsToShow)
        {
            var tableColumn = new TableColumn(column.Header);

            // Apply width based on responsive settings
            var effectiveWidth = GetEffectiveWidth(column, config.ResponsiveColumns);
            if (effectiveWidth.HasValue)
            {
                tableColumn.Width(effectiveWidth.Value);
            }

            tableColumn.Alignment(ToJustify(column.Alignment));
            table.AddColumn(tableColumn);
        }

        // Add rows
        foreach (var item in itemList)
        {
            var values = columnsToShow
                .Select(c => TruncateValue(c.Selector(item), GetEffectiveWidth(c, config.ResponsiveColumns)))
                .Select(v => Markup.Escape(v))
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

    /// <summary>
    /// Gets columns to display based on terminal width and column priorities.
    /// </summary>
    private IReadOnlyList<TableColumn<T>> GetResponsiveColumns<T>(IReadOnlyList<TableColumn<T>> columns)
    {
        var terminalWidth = GetTerminalWidth();
        
        // If terminal is wide enough, show all columns
        if (terminalWidth >= NarrowThreshold)
        {
            return columns;
        }

        var columnList = columns.ToList();

        // Start with priority 1 columns and add more until we run out of space
        var result = new List<TableColumn<T>>();
        var usedWidth = 0;
        var borderOverhead = 4; // Left and right border

        foreach (var column in columnList.OrderBy(c => c.Priority).ThenBy(c => columnList.IndexOf(c)))
        {
            var columnWidth = column.MinWidth + ColumnOverhead;
            if (usedWidth + columnWidth + borderOverhead <= terminalWidth || column.Priority == 1)
            {
                result.Add(column);
                usedWidth += columnWidth;
            }
        }

        // Preserve original column order
        return columns.Where(c => result.Contains(c)).ToList();
    }

    /// <summary>
    /// Gets the effective width for a column based on responsive settings.
    /// </summary>
    private int? GetEffectiveWidth<T>(TableColumn<T> column, bool responsive)
    {
        // If fixed width is set, use it
        if (column.Width.HasValue)
        {
            return column.Width.Value;
        }

        // If not responsive, no width constraint
        if (!responsive)
        {
            return null;
        }

        var terminalWidth = GetTerminalWidth();

        // For narrow terminals, use MaxWidth if set, otherwise use MinWidth
        if (terminalWidth < NarrowThreshold && column.MaxWidth.HasValue)
        {
            return Math.Min(column.MaxWidth.Value, column.MinWidth + 10);
        }

        return column.MaxWidth;
    }

    /// <summary>
    /// Truncates a value to fit within a maximum width.
    /// </summary>
    private static string TruncateValue(string value, int? maxWidth)
    {
        if (!maxWidth.HasValue || value.Length <= maxWidth.Value)
        {
            return value;
        }

        if (maxWidth.Value <= 3)
        {
            return value.Substring(0, maxWidth.Value);
        }

        return value.Substring(0, maxWidth.Value - 3) + "...";
    }

    /// <summary>
    /// Gets the terminal width from capabilities or console profile.
    /// </summary>
    private int GetTerminalWidth()
    {
        if (_terminalCapabilities != null)
        {
            return _terminalCapabilities.Width;
        }

        return _console.Profile.Width;
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
