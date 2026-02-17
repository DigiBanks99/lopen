namespace Lopen.Llm;

/// <summary>
/// Tracks token usage in memory across SDK invocations.
/// </summary>
internal sealed class InMemoryTokenTracker : ITokenTracker
{
    private readonly List<TokenUsage> _iterations = [];
    private int _cumulativeInput;
    private int _cumulativeOutput;
    private int _premiumCount;
    private readonly object _lock = new();

    public void RecordUsage(TokenUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        lock (_lock)
        {
            _iterations.Add(usage);
            _cumulativeInput += usage.InputTokens;
            _cumulativeOutput += usage.OutputTokens;
            if (usage.IsPremiumRequest)
            {
                _premiumCount++;
            }
        }
    }

    public SessionTokenMetrics GetSessionMetrics()
    {
        lock (_lock)
        {
            return new SessionTokenMetrics
            {
                PerIterationTokens = _iterations.ToList().AsReadOnly(),
                CumulativeInputTokens = _cumulativeInput,
                CumulativeOutputTokens = _cumulativeOutput,
                PremiumRequestCount = _premiumCount,
            };
        }
    }

    public void ResetSession()
    {
        lock (_lock)
        {
            _iterations.Clear();
            _cumulativeInput = 0;
            _cumulativeOutput = 0;
            _premiumCount = 0;
        }
    }

    public void RestoreMetrics(int cumulativeInput, int cumulativeOutput, int premiumCount)
    {
        lock (_lock)
        {
            _cumulativeInput = cumulativeInput;
            _cumulativeOutput = cumulativeOutput;
            _premiumCount = premiumCount;
        }
    }
}
