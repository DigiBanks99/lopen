namespace Lopen.Llm;

/// <summary>
/// Exception thrown for LLM-specific failures (auth failure, rate limit, model unavailable).
/// </summary>
public class LlmException : Exception
{
    /// <summary>The LLM model involved in the failure, if applicable.</summary>
    public string? Model { get; }

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
}
