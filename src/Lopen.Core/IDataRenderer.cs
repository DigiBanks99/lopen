namespace Lopen.Core;

/// <summary>
/// Configuration for a table column.
/// </summary>
/// <typeparam name="T">The type of items in the table.</typeparam>
public record TableColumn<T>
{
    /// <summary>Column header text.</summary>
    public required string Header { get; init; }

    /// <summary>Function to extract the value from an item.</summary>
    public required Func<T, string> Selector { get; init; }

    /// <summary>Column alignment (left, center, right).</summary>
    public ColumnAlignment Alignment { get; init; } = ColumnAlignment.Left;

    /// <summary>Optional fixed width for the column.</summary>
    public int? Width { get; init; }

    /// <summary>Minimum width for responsive columns (default: 10).</summary>
    public int MinWidth { get; init; } = 10;

    /// <summary>Maximum width for responsive columns (null = unlimited).</summary>
    public int? MaxWidth { get; init; }

    /// <summary>
    /// Column priority for responsive display.
    /// 1 = highest priority (always shown), higher numbers = lower priority (may be hidden).
    /// </summary>
    public int Priority { get; init; } = 1;
}

/// <summary>
/// Column alignment options.
/// </summary>
public enum ColumnAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Configuration for a table.
/// </summary>
/// <typeparam name="T">The type of items in the table.</typeparam>
public record TableConfig<T>
{
    /// <summary>Table title (optional).</summary>
    public string? Title { get; init; }

    /// <summary>Column definitions.</summary>
    public IReadOnlyList<TableColumn<T>> Columns { get; init; } = Array.Empty<TableColumn<T>>();

    /// <summary>Whether to expand table to terminal width.</summary>
    public bool Expand { get; init; } = false;

    /// <summary>Whether to show row count after table.</summary>
    public bool ShowRowCount { get; init; } = true;

    /// <summary>Format string for row count (use {0} for count).</summary>
    public string RowCountFormat { get; init; } = "{0} item(s)";

    /// <summary>
    /// Whether to enable responsive column widths based on terminal width.
    /// When true, columns may be truncated or hidden based on available space.
    /// </summary>
    public bool ResponsiveColumns { get; init; } = false;
}

/// <summary>
/// Renderer for structured data display (tables, panels, metadata).
/// </summary>
public interface IDataRenderer
{
    /// <summary>
    /// Render a table of items with the specified configuration.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="items">The items to display.</param>
    /// <param name="config">Table configuration.</param>
    void RenderTable<T>(IEnumerable<T> items, TableConfig<T> config);

    /// <summary>
    /// Render key-value metadata in a panel.
    /// </summary>
    /// <param name="data">Key-value pairs to display.</param>
    /// <param name="title">Panel title.</param>
    void RenderMetadata(IReadOnlyDictionary<string, string> data, string title);

    /// <summary>
    /// Render an informational message (for empty results, etc.).
    /// </summary>
    /// <param name="message">The message to display.</param>
    void RenderInfo(string message);
}
