namespace Lopen.Configuration;

/// <summary>
/// Validates <see cref="LopenOptions"/> and returns aggregated error messages.
/// Fail-fast: all errors are collected before reporting.
/// </summary>
public static class LopenOptionsValidator
{
    public static IReadOnlyList<string> Validate(LopenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        ValidateBudget(options.Budget, errors);
        ValidateWorkflow(options.Workflow, errors);
        ValidateSession(options.Session, errors);
        ValidateToolDiscipline(options.ToolDiscipline, errors);

        return errors;
    }

    private static void ValidateBudget(BudgetOptions budget, List<string> errors)
    {
        if (budget.TokenBudgetPerModule < 0)
            errors.Add("budget.token_budget_per_module must be >= 0.");

        if (budget.PremiumRequestBudget < 0)
            errors.Add("budget.premium_request_budget must be >= 0.");

        if (budget.WarningThreshold is < 0.0 or > 1.0)
            errors.Add("budget.warning_threshold must be between 0.0 and 1.0.");

        if (budget.ConfirmationThreshold is < 0.0 or > 1.0)
            errors.Add("budget.confirmation_threshold must be between 0.0 and 1.0.");

        if (budget.WarningThreshold >= budget.ConfirmationThreshold)
            errors.Add("budget.warning_threshold must be less than budget.confirmation_threshold.");
    }

    private static void ValidateWorkflow(WorkflowOptions workflow, List<string> errors)
    {
        if (workflow.MaxIterations <= 0)
            errors.Add("max_iterations must be > 0.");

        if (workflow.FailureThreshold <= 0)
            errors.Add("failure_threshold must be > 0.");
    }

    private static void ValidateSession(SessionOptions session, List<string> errors)
    {
        if (session.SessionRetention < 0)
            errors.Add("session_retention must be >= 0.");
    }

    private static void ValidateToolDiscipline(ToolDisciplineOptions toolDiscipline, List<string> errors)
    {
        if (toolDiscipline.MaxFileReads <= 0)
            errors.Add("tool_discipline.max_file_reads must be > 0.");

        if (toolDiscipline.MaxCommandRetries <= 0)
            errors.Add("tool_discipline.max_command_retries must be > 0.");
    }
}
