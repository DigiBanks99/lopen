namespace Lopen.Llm;

/// <summary>
/// Tracks token usage across SDK invocations within a session.
/// </summary>
public interface ITokenTracker
{
    /// <summary>Records token usage from a single SDK invocation.</summary>
    void RecordUsage(TokenUsage usage);

    /// <summary>Returns aggregated token metrics for the current session.</summary>
    SessionTokenMetrics GetSessionMetrics();

    /// <summary>Resets all tracked metrics for a new session.</summary>
    void ResetSession();

    /// <summary>
    /// Restores cumulative metrics from a previously persisted session (LLM-13).
    /// Called during session resume so new recordings accumulate on top of prior values.
    /// </summary>
    void RestoreMetrics(int cumulativeInput, int cumulativeOutput, int premiumCount);
}
