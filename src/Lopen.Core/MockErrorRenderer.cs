namespace Lopen.Core;

/// <summary>
/// Mock implementation of IErrorRenderer for testing.
/// Records all rendered errors for verification.
/// </summary>
public class MockErrorRenderer : IErrorRenderer
{
    private readonly List<string> _simpleErrors = new();
    private readonly List<(string Title, string Message, List<string> Suggestions)> _panelErrors = new();
    private readonly List<(string Input, string Message, List<string> ValidOptions)> _validationErrors = new();
    private readonly List<(string Command, List<string> Suggestions)> _commandNotFoundErrors = new();
    private readonly List<ErrorInfo> _errors = new();

    /// <summary>
    /// Simple errors rendered via RenderSimpleError.
    /// </summary>
    public IReadOnlyList<string> SimpleErrors => _simpleErrors;

    /// <summary>
    /// Panel errors rendered via RenderPanelError.
    /// </summary>
    public IReadOnlyList<(string Title, string Message, List<string> Suggestions)> PanelErrors => _panelErrors;

    /// <summary>
    /// Validation errors rendered via RenderValidationError.
    /// </summary>
    public IReadOnlyList<(string Input, string Message, List<string> ValidOptions)> ValidationErrors => _validationErrors;

    /// <summary>
    /// Command not found errors rendered via RenderCommandNotFound.
    /// </summary>
    public IReadOnlyList<(string Command, List<string> Suggestions)> CommandNotFoundErrors => _commandNotFoundErrors;

    /// <summary>
    /// Full errors rendered via RenderError.
    /// </summary>
    public IReadOnlyList<ErrorInfo> Errors => _errors;

    /// <summary>
    /// All suggestions from simple errors.
    /// </summary>
    public List<string?> SimpleSuggestions { get; } = new();

    /// <summary>
    /// Total number of errors rendered.
    /// </summary>
    public int TotalErrorCount =>
        _simpleErrors.Count + _panelErrors.Count + _validationErrors.Count +
        _commandNotFoundErrors.Count + _errors.Count;

    public void RenderSimpleError(string message, string? suggestion = null)
    {
        _simpleErrors.Add(message);
        SimpleSuggestions.Add(suggestion);
    }

    public void RenderPanelError(string title, string message, IEnumerable<string>? suggestions = null)
    {
        _panelErrors.Add((title, message, suggestions?.ToList() ?? new List<string>()));
    }

    public void RenderValidationError(string input, string message, IEnumerable<string> validOptions)
    {
        _validationErrors.Add((input, message, validOptions?.ToList() ?? new List<string>()));
    }

    public void RenderCommandNotFound(string command, IEnumerable<string> suggestions)
    {
        _commandNotFoundErrors.Add((command, suggestions?.ToList() ?? new List<string>()));
    }

    public void RenderError(ErrorInfo error)
    {
        _errors.Add(error);
    }

    /// <summary>
    /// Reset all recorded errors.
    /// </summary>
    public void Reset()
    {
        _simpleErrors.Clear();
        _panelErrors.Clear();
        _validationErrors.Clear();
        _commandNotFoundErrors.Clear();
        _errors.Clear();
        SimpleSuggestions.Clear();
    }
}
