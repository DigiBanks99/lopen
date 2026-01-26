namespace Lopen.Core;

/// <summary>
/// Severity levels for error display.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>Critical error that prevents operation.</summary>
    Error,

    /// <summary>Warning that operation may have issues.</summary>
    Warning,

    /// <summary>Validation error with correctable input.</summary>
    Validation
}

/// <summary>
/// Error information for structured display.
/// </summary>
public record ErrorInfo
{
    /// <summary>Error title (short summary).</summary>
    public required string Title { get; init; }

    /// <summary>Detailed error message.</summary>
    public required string Message { get; init; }

    /// <summary>Primary suggestion ("Did you mean...").</summary>
    public string? DidYouMean { get; init; }

    /// <summary>List of possible suggestions.</summary>
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();

    /// <summary>Suggested command to try.</summary>
    public string? TryCommand { get; init; }

    /// <summary>Severity level.</summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
}

/// <summary>
/// Renderer for structured error display.
/// </summary>
public interface IErrorRenderer
{
    /// <summary>
    /// Render a simple single-line error with optional suggestion.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="suggestion">Optional suggestion (prefixed with "Try:").</param>
    void RenderSimpleError(string message, string? suggestion = null);

    /// <summary>
    /// Render an error in a bordered panel with suggestions.
    /// </summary>
    /// <param name="title">Panel title.</param>
    /// <param name="message">Error message.</param>
    /// <param name="suggestions">List of suggestions.</param>
    void RenderPanelError(string title, string message, IEnumerable<string>? suggestions = null);

    /// <summary>
    /// Render a validation error with inline context.
    /// </summary>
    /// <param name="input">The invalid input.</param>
    /// <param name="message">Error message.</param>
    /// <param name="validOptions">List of valid options.</param>
    void RenderValidationError(string input, string message, IEnumerable<string> validOptions);

    /// <summary>
    /// Render a command not found error with similar command suggestions.
    /// </summary>
    /// <param name="command">The unknown command.</param>
    /// <param name="suggestions">Similar commands.</param>
    void RenderCommandNotFound(string command, IEnumerable<string> suggestions);

    /// <summary>
    /// Render a structured error with full context.
    /// </summary>
    /// <param name="error">Error information.</param>
    void RenderError(ErrorInfo error);
}
