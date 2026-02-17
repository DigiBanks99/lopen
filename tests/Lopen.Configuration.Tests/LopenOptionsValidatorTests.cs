namespace Lopen.Configuration.Tests;

public class LopenOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_ReturnsNoErrors()
    {
        var options = new LopenOptions();

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NegativeTokenBudget_ReturnsError()
    {
        var options = new LopenOptions();
        options.Budget.TokenBudgetPerModule = -1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("token_budget_per_module"));
    }

    [Fact]
    public void Validate_NegativePremiumBudget_ReturnsError()
    {
        var options = new LopenOptions();
        options.Budget.PremiumRequestBudget = -1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("premium_request_budget"));
    }

    [Fact]
    public void Validate_WarningThresholdOutOfRange_ReturnsError()
    {
        var options = new LopenOptions();
        options.Budget.WarningThreshold = 1.5;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("warning_threshold"));
    }

    [Fact]
    public void Validate_ConfirmationThresholdOutOfRange_ReturnsError()
    {
        var options = new LopenOptions();
        options.Budget.ConfirmationThreshold = -0.1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("confirmation_threshold"));
    }

    [Fact]
    public void Validate_WarningNotLessThanConfirmation_ReturnsError()
    {
        var options = new LopenOptions();
        options.Budget.WarningThreshold = 0.9;
        options.Budget.ConfirmationThreshold = 0.9;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("warning_threshold must be less than"));
    }

    [Fact]
    public void Validate_ZeroMaxIterations_ReturnsError()
    {
        var options = new LopenOptions();
        options.Workflow.MaxIterations = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("max_iterations"));
    }

    [Fact]
    public void Validate_ZeroFailureThreshold_ReturnsError()
    {
        var options = new LopenOptions();
        options.Workflow.FailureThreshold = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("failure_threshold"));
    }

    [Fact]
    public void Validate_NegativeSessionRetention_ReturnsError()
    {
        var options = new LopenOptions();
        options.Session.SessionRetention = -1;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("session_retention"));
    }

    [Fact]
    public void Validate_ZeroMaxFileReads_ReturnsError()
    {
        var options = new LopenOptions();
        options.ToolDiscipline.MaxFileReads = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("max_file_reads"));
    }

    [Fact]
    public void Validate_ZeroMaxCommandRetries_ReturnsError()
    {
        var options = new LopenOptions();
        options.ToolDiscipline.MaxCommandRetries = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.Contains(errors, e => e.Contains("max_command_retries"));
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var options = new LopenOptions();
        options.Budget.TokenBudgetPerModule = -1;
        options.Budget.PremiumRequestBudget = -1;
        options.Workflow.MaxIterations = 0;

        var errors = LopenOptionsValidator.Validate(options);

        Assert.True(errors.Count >= 3);
    }

    [Fact]
    public void Validate_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LopenOptionsValidator.Validate(null!));
    }
}
