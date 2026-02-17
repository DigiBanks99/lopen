namespace Lopen.Tui;

/// <summary>
/// Data model for file picker with tree view.
/// </summary>
public sealed record FilePickerData
{
    /// <summary>Root directory path.</summary>
    public required string RootPath { get; init; }

    /// <summary>Tree nodes.</summary>
    public IReadOnlyList<FileNode> Nodes { get; init; } = [];

    /// <summary>Currently selected index.</summary>
    public int SelectedIndex { get; init; }
}

/// <summary>A node in the file tree.</summary>
public sealed record FileNode(string Name, bool IsDirectory, int Depth, bool IsExpanded = false);

/// <summary>
/// Data model for module selection modal.
/// </summary>
public sealed record ModuleSelectionData
{
    /// <summary>Title of the selection.</summary>
    public required string Title { get; init; }

    /// <summary>Available options.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Currently selected index.</summary>
    public int SelectedIndex { get; init; }
}

/// <summary>
/// Data model for confirmation modals (Yes/No/Always/Other).
/// </summary>
public sealed record ConfirmationData
{
    /// <summary>Modal title/question.</summary>
    public required string Title { get; init; }

    /// <summary>Detailed message.</summary>
    public string? Message { get; init; }

    /// <summary>Available options (e.g., "Yes", "No", "Always").</summary>
    public IReadOnlyList<string> Options { get; init; } = ["Yes", "No"];

    /// <summary>Currently selected option index.</summary>
    public int SelectedIndex { get; init; }
}

/// <summary>
/// Data model for error/failure modals with recovery options.
/// </summary>
public sealed record ErrorModalData
{
    /// <summary>Error title.</summary>
    public required string Title { get; init; }

    /// <summary>Error message.</summary>
    public required string Message { get; init; }

    /// <summary>Recovery options.</summary>
    public IReadOnlyList<string> RecoveryOptions { get; init; } = ["Retry", "Skip", "Abort"];

    /// <summary>Currently selected recovery option index.</summary>
    public int SelectedIndex { get; init; }

    /// <summary>Callback invoked when user selects a recovery option. Parameter is the selected index.</summary>
    public Action<int>? OnSelected { get; init; }
}
