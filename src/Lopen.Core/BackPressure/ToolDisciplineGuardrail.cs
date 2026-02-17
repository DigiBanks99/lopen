using Lopen.Configuration;

namespace Lopen.Core.BackPressure;

/// <summary>
/// Back-pressure Category 4: Tool Discipline.
/// Detects wasteful tool call patterns and injects corrective instructions.
/// Configurable via <see cref="ToolDisciplineOptions"/>.
/// </summary>
internal sealed class ToolDisciplineGuardrail : IGuardrail
{
    private readonly int _maxFileReads;
    private readonly int _maxCommandRetries;
    private readonly int _toolCallThreshold;

    /// <summary>
    /// Creates a tool discipline guardrail with explicit thresholds.
    /// </summary>
    public ToolDisciplineGuardrail(int toolCallThreshold = 50, int maxFileReads = 3, int maxCommandRetries = 3)
    {
        if (toolCallThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(toolCallThreshold), "Threshold must be positive.");
        if (maxFileReads <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFileReads), "Max file reads must be positive.");
        if (maxCommandRetries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCommandRetries), "Max command retries must be positive.");

        _toolCallThreshold = toolCallThreshold;
        _maxFileReads = maxFileReads;
        _maxCommandRetries = maxCommandRetries;
    }

    /// <summary>
    /// Creates a tool discipline guardrail from configuration options.
    /// </summary>
    public ToolDisciplineGuardrail(ToolDisciplineOptions options)
        : this(toolCallThreshold: 50, maxFileReads: options.MaxFileReads, maxCommandRetries: options.MaxCommandRetries)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    public int Order => 400;
    public bool ShortCircuitOnBlock => false;

    public Task<GuardrailResult> EvaluateAsync(GuardrailContext context, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();

        // Check per-file read counts
        if (context.FileReadCounts is not null)
        {
            foreach (var (file, count) in context.FileReadCounts)
            {
                if (count > _maxFileReads)
                {
                    warnings.Add(
                        $"File '{file}' read {count} times (max {_maxFileReads}). " +
                        "Read once and reference the content instead of re-reading.");
                }
            }
        }

        // Check per-command retry counts
        if (context.CommandRetryCounts is not null)
        {
            foreach (var (command, count) in context.CommandRetryCounts)
            {
                if (count > _maxCommandRetries)
                {
                    warnings.Add(
                        $"Command retried {count} times (max {_maxCommandRetries}): '{command}'. " +
                        "Analyze the error before retrying — changing approach may be more effective.");
                }
            }
        }

        // Check total tool call count
        if (context.ToolCallCount > _toolCallThreshold * 2)
        {
            warnings.Add(
                $"Excessive tool calls ({context.ToolCallCount}) in this iteration. " +
                "Consider a more focused approach — read files once, make targeted changes, " +
                "and verify. Avoid reading the same file repeatedly.");
        }
        else if (context.ToolCallCount > _toolCallThreshold)
        {
            warnings.Add(
                $"High tool call count ({context.ToolCallCount}/{_toolCallThreshold}). " +
                "Ensure each tool call serves a purpose.");
        }

        if (warnings.Count == 0)
        {
            return Task.FromResult<GuardrailResult>(new GuardrailResult.Pass());
        }

        return Task.FromResult<GuardrailResult>(
            new GuardrailResult.Warn(string.Join(" ", warnings)));
    }
}
