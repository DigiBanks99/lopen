namespace Lopen.Llm;

/// <summary>
/// Result of model selection, indicating whether a fallback was used.
/// </summary>
public sealed record ModelFallbackResult(
    string SelectedModel,
    bool WasFallback,
    string? OriginalModel = null);
