namespace Lopen.Llm;

/// <summary>
/// Exception thrown for LLM-specific failures (auth failure, rate limit, model unavailable).
/// </summary>
public class LlmException : Exception
{
    /// <summary>The LLM model involved in the failure, if applicable.</summary>
    public string? Model { get; }

    /// <summary>
    /// Indicates the failure was due to the requested model being unavailable.
    /// When true, callers may retry with a fallback model (LLM-11).
    /// </summary>
    public bool IsModelUnavailable { get; init; }

    public LlmException(string message)
        : base(message) { }

    public LlmException(string message, string? model)
        : base(message)
    {
        Model = model;
    }

    public LlmException(string message, string? model, Exception innerException)
        : base(message, innerException)
    {
        Model = model;
    }

    /// <summary>
    /// Inspects exception messages for model-unavailability indicators.
    /// </summary>
    internal static bool LooksLikeModelUnavailable(Exception ex)
    {
        var msg = ex.Message + (ex.InnerException?.Message ?? "");
        return msg.Contains("model", StringComparison.OrdinalIgnoreCase)
            && (msg.Contains("unavailable", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("not available", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }
}
