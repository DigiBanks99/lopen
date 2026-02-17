namespace Lopen.Configuration;

/// <summary>
/// Result of a budget check, including the status and details for both token and request budgets.
/// </summary>
public sealed record BudgetCheckResult
{
    /// <summary>The overall budget status (worst of token and request status).</summary>
    public required BudgetStatus Status { get; init; }

    /// <summary>Budget status for token usage specifically.</summary>
    public required BudgetStatus TokenStatus { get; init; }

    /// <summary>Budget status for premium request usage specifically.</summary>
    public required BudgetStatus RequestStatus { get; init; }

    /// <summary>Fraction of token budget consumed (0.0–1.0+). Null if unlimited.</summary>
    public double? TokenUsageFraction { get; init; }

    /// <summary>Fraction of request budget consumed (0.0–1.0+). Null if unlimited.</summary>
    public double? RequestUsageFraction { get; init; }

    /// <summary>A human-readable message describing the budget status.</summary>
    public required string Message { get; init; }
}
