namespace Lopen.Configuration;

/// <summary>
/// Enforces budget limits by checking current usage against <see cref="BudgetOptions"/>.
/// </summary>
public sealed class BudgetEnforcer : IBudgetEnforcer
{
    private readonly BudgetOptions _budget;

    public BudgetEnforcer(BudgetOptions budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        _budget = budget;
    }

    public BudgetCheckResult Check(long currentTokens, int currentRequests)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(currentRequests);

        var tokenStatus = CheckSingle(currentTokens, _budget.TokenBudgetPerModule, out var tokenFraction);
        var requestStatus = CheckSingle(currentRequests, _budget.PremiumRequestBudget, out var requestFraction);
        var overall = (BudgetStatus)Math.Max((int)tokenStatus, (int)requestStatus);

        return new BudgetCheckResult
        {
            Status = overall,
            TokenStatus = tokenStatus,
            RequestStatus = requestStatus,
            TokenUsageFraction = _budget.TokenBudgetPerModule > 0 ? tokenFraction : null,
            RequestUsageFraction = _budget.PremiumRequestBudget > 0 ? requestFraction : null,
            Message = FormatMessage(overall, tokenStatus, requestStatus, tokenFraction, requestFraction),
        };
    }

    private BudgetStatus CheckSingle(long current, int limit, out double fraction)
    {
        if (limit <= 0)
        {
            fraction = 0;
            return BudgetStatus.Ok;
        }

        fraction = (double)current / limit;

        if (fraction >= 1.0)
            return BudgetStatus.Exceeded;
        if (fraction >= _budget.ConfirmationThreshold)
            return BudgetStatus.ConfirmationRequired;
        if (fraction >= _budget.WarningThreshold)
            return BudgetStatus.Warning;

        return BudgetStatus.Ok;
    }

    private static string FormatMessage(
        BudgetStatus overall,
        BudgetStatus tokenStatus,
        BudgetStatus requestStatus,
        double tokenFraction,
        double requestFraction) => overall switch
        {
            BudgetStatus.Ok => "Budget usage is within limits.",
            BudgetStatus.Exceeded => tokenStatus == BudgetStatus.Exceeded
                ? $"Token budget exceeded ({tokenFraction:P0} used)."
                : $"Premium request budget exceeded ({requestFraction:P0} used).",
            BudgetStatus.ConfirmationRequired => tokenStatus == BudgetStatus.ConfirmationRequired
                ? $"Token usage at {tokenFraction:P0} — confirmation required to continue."
                : $"Premium request usage at {requestFraction:P0} — confirmation required to continue.",
            BudgetStatus.Warning => tokenStatus == BudgetStatus.Warning
                ? $"Token usage at {tokenFraction:P0} — approaching budget limit."
                : $"Premium request usage at {requestFraction:P0} — approaching budget limit.",
            _ => "Budget status unknown.",
        };
}
