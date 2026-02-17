namespace Lopen.Configuration;

/// <summary>
/// The current budget status based on usage relative to configured limits.
/// </summary>
public enum BudgetStatus
{
    /// <summary>No budget configured (unlimited) or usage is below warning threshold.</summary>
    Ok,

    /// <summary>Usage has exceeded the warning threshold but not the confirmation threshold.</summary>
    Warning,

    /// <summary>Usage has exceeded the confirmation threshold but not the hard limit.</summary>
    ConfirmationRequired,

    /// <summary>Usage has reached or exceeded the configured budget limit.</summary>
    Exceeded,
}
