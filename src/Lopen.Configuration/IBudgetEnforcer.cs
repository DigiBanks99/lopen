namespace Lopen.Configuration;

/// <summary>
/// Checks current usage against configured budget limits.
/// </summary>
public interface IBudgetEnforcer
{
    /// <summary>
    /// Checks the current token and request usage against the configured budget.
    /// </summary>
    /// <param name="currentTokens">Total tokens consumed so far.</param>
    /// <param name="currentRequests">Total premium requests made so far.</param>
    /// <returns>A <see cref="BudgetCheckResult"/> indicating the budget status.</returns>
    BudgetCheckResult Check(long currentTokens, int currentRequests);
}
