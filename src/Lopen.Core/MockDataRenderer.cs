namespace Lopen.Core;

/// <summary>
/// Mock data renderer for testing. Records all render calls for verification.
/// </summary>
public class MockDataRenderer : IDataRenderer
{
    private readonly List<TableRenderCall> _tableCalls = new();
    private readonly List<MetadataRenderCall> _metadataCalls = new();
    private readonly List<string> _infoCalls = new();

    /// <summary>
    /// Gets all table render calls.
    /// </summary>
    public IReadOnlyList<TableRenderCall> TableCalls => _tableCalls.AsReadOnly();

    /// <summary>
    /// Gets all metadata render calls.
    /// </summary>
    public IReadOnlyList<MetadataRenderCall> MetadataCalls => _metadataCalls.AsReadOnly();

    /// <summary>
    /// Gets all info message calls.
    /// </summary>
    public IReadOnlyList<string> InfoCalls => _infoCalls.AsReadOnly();

    public void RenderTable<T>(IEnumerable<T> items, TableConfig<T> config)
    {
        var itemList = items.ToList();
        var rows = new List<List<string>>();

        foreach (var item in itemList)
        {
            var row = config.Columns.Select(c => c.Selector(item)).ToList();
            rows.Add(row);
        }

        _tableCalls.Add(new TableRenderCall
        {
            Title = config.Title,
            Headers = config.Columns.Select(c => c.Header).ToList(),
            Rows = rows,
            ItemCount = itemList.Count,
            ShowRowCount = config.ShowRowCount,
            RowCountFormat = config.RowCountFormat
        });
    }

    public void RenderMetadata(IReadOnlyDictionary<string, string> data, string title)
    {
        _metadataCalls.Add(new MetadataRenderCall
        {
            Title = title,
            Data = new Dictionary<string, string>(data)
        });
    }

    public void RenderInfo(string message)
    {
        _infoCalls.Add(message);
    }

    /// <summary>
    /// Clears all recorded calls.
    /// </summary>
    public void Reset()
    {
        _tableCalls.Clear();
        _metadataCalls.Clear();
        _infoCalls.Clear();
    }

    /// <summary>
    /// Record of a table render call.
    /// </summary>
    public record TableRenderCall
    {
        public string? Title { get; init; }
        public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
        public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<List<string>>();
        public int ItemCount { get; init; }
        public bool ShowRowCount { get; init; }
        public string RowCountFormat { get; init; } = string.Empty;
    }

    /// <summary>
    /// Record of a metadata render call.
    /// </summary>
    public record MetadataRenderCall
    {
        public string Title { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
    }
}
