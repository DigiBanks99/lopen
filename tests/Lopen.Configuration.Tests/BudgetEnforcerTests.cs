namespace Lopen.Configuration.Tests;

public sealed class BudgetEnforcerTests
{
    private static BudgetOptions Unlimited() => new();

    private static BudgetOptions WithTokenBudget(int tokens) => new()
    {
        TokenBudgetPerModule = tokens,
    };

    private static BudgetOptions WithRequestBudget(int requests) => new()
    {
        PremiumRequestBudget = requests,
    };

    private static BudgetOptions WithBothBudgets(int tokens, int requests) => new()
    {
        TokenBudgetPerModule = tokens,
        PremiumRequestBudget = requests,
    };

    // --- Constructor ---

    [Fact]
    public void Constructor_NullBudget_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BudgetEnforcer(null!));
    }

    // --- Unlimited budgets ---

    [Fact]
    public void Check_UnlimitedBudgets_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(Unlimited());

        var result = enforcer.Check(1_000_000, 500);

        Assert.Equal(BudgetStatus.Ok, result.Status);
        Assert.Null(result.TokenUsageFraction);
        Assert.Null(result.RequestUsageFraction);
    }

    [Fact]
    public void Check_ZeroUsage_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(0, 0);

        Assert.Equal(BudgetStatus.Ok, result.Status);
        Assert.Equal(0.0, result.TokenUsageFraction);
    }

    // --- Token budget ---

    [Fact]
    public void Check_TokensBelowWarning_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(7_000, 0);

        Assert.Equal(BudgetStatus.Ok, result.Status);
        Assert.Equal(BudgetStatus.Ok, result.TokenStatus);
        Assert.Equal(0.7, result.TokenUsageFraction!.Value, 2);
    }

    [Fact]
    public void Check_TokensAtWarningThreshold_ReturnsWarning()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(8_000, 0);

        Assert.Equal(BudgetStatus.Warning, result.Status);
        Assert.Equal(BudgetStatus.Warning, result.TokenStatus);
        Assert.Equal(0.8, result.TokenUsageFraction!.Value, 2);
        Assert.Contains("approaching", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check_TokensBetweenWarningAndConfirmation_ReturnsWarning()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(8_500, 0);

        Assert.Equal(BudgetStatus.Warning, result.Status);
    }

    [Fact]
    public void Check_TokensAtConfirmationThreshold_ReturnsConfirmationRequired()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(9_000, 0);

        Assert.Equal(BudgetStatus.ConfirmationRequired, result.Status);
        Assert.Equal(BudgetStatus.ConfirmationRequired, result.TokenStatus);
        Assert.Contains("confirmation", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check_TokensExceedBudget_ReturnsExceeded()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(10_000, 0);

        Assert.Equal(BudgetStatus.Exceeded, result.Status);
        Assert.Equal(BudgetStatus.Exceeded, result.TokenStatus);
        Assert.Contains("exceeded", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check_TokensOverBudget_ReturnsExceeded()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(15_000, 0);

        Assert.Equal(BudgetStatus.Exceeded, result.Status);
        Assert.True(result.TokenUsageFraction > 1.0);
    }

    // --- Request budget ---

    [Fact]
    public void Check_RequestsBelowWarning_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(WithRequestBudget(100));

        var result = enforcer.Check(0, 50);

        Assert.Equal(BudgetStatus.Ok, result.Status);
        Assert.Equal(BudgetStatus.Ok, result.RequestStatus);
        Assert.Equal(0.5, result.RequestUsageFraction!.Value, 2);
    }

    [Fact]
    public void Check_RequestsAtWarning_ReturnsWarning()
    {
        var enforcer = new BudgetEnforcer(WithRequestBudget(100));

        var result = enforcer.Check(0, 80);

        Assert.Equal(BudgetStatus.Warning, result.Status);
        Assert.Equal(BudgetStatus.Warning, result.RequestStatus);
    }

    [Fact]
    public void Check_RequestsAtConfirmation_ReturnsConfirmationRequired()
    {
        var enforcer = new BudgetEnforcer(WithRequestBudget(100));

        var result = enforcer.Check(0, 90);

        Assert.Equal(BudgetStatus.ConfirmationRequired, result.Status);
    }

    [Fact]
    public void Check_RequestsExceeded_ReturnsExceeded()
    {
        var enforcer = new BudgetEnforcer(WithRequestBudget(100));

        var result = enforcer.Check(0, 100);

        Assert.Equal(BudgetStatus.Exceeded, result.Status);
        Assert.Equal(BudgetStatus.Exceeded, result.RequestStatus);
    }

    // --- Combined budgets ---

    [Fact]
    public void Check_BothOk_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(WithBothBudgets(10_000, 100));

        var result = enforcer.Check(5_000, 50);

        Assert.Equal(BudgetStatus.Ok, result.Status);
        Assert.NotNull(result.TokenUsageFraction);
        Assert.NotNull(result.RequestUsageFraction);
    }

    [Fact]
    public void Check_TokenWarningRequestOk_ReturnsWarning()
    {
        var enforcer = new BudgetEnforcer(WithBothBudgets(10_000, 100));

        var result = enforcer.Check(8_500, 10);

        Assert.Equal(BudgetStatus.Warning, result.Status);
        Assert.Equal(BudgetStatus.Warning, result.TokenStatus);
        Assert.Equal(BudgetStatus.Ok, result.RequestStatus);
    }

    [Fact]
    public void Check_TokenOkRequestExceeded_ReturnsExceeded()
    {
        var enforcer = new BudgetEnforcer(WithBothBudgets(10_000, 100));

        var result = enforcer.Check(1_000, 100);

        Assert.Equal(BudgetStatus.Exceeded, result.Status);
        Assert.Equal(BudgetStatus.Ok, result.TokenStatus);
        Assert.Equal(BudgetStatus.Exceeded, result.RequestStatus);
    }

    [Fact]
    public void Check_WorstStatusWins()
    {
        var enforcer = new BudgetEnforcer(WithBothBudgets(10_000, 100));

        var result = enforcer.Check(8_000, 100);

        // Tokens at warning, requests exceeded => overall = exceeded
        Assert.Equal(BudgetStatus.Exceeded, result.Status);
    }

    // --- Message content ---

    [Fact]
    public void Check_OkStatus_MessageIndicatesWithinLimits()
    {
        var enforcer = new BudgetEnforcer(Unlimited());

        var result = enforcer.Check(0, 0);

        Assert.Equal("Budget usage is within limits.", result.Message);
    }

    // --- Custom thresholds ---

    [Fact]
    public void Check_CustomThresholds_Respected()
    {
        var options = new BudgetOptions
        {
            TokenBudgetPerModule = 1000,
            WarningThreshold = 0.5,
            ConfirmationThreshold = 0.7,
        };
        var enforcer = new BudgetEnforcer(options);

        Assert.Equal(BudgetStatus.Ok, enforcer.Check(400, 0).Status);
        Assert.Equal(BudgetStatus.Warning, enforcer.Check(500, 0).Status);
        Assert.Equal(BudgetStatus.ConfirmationRequired, enforcer.Check(700, 0).Status);
        Assert.Equal(BudgetStatus.Exceeded, enforcer.Check(1000, 0).Status);
    }

    // --- Input validation ---

    [Fact]
    public void Check_NegativeTokens_Throws()
    {
        var enforcer = new BudgetEnforcer(Unlimited());

        Assert.Throws<ArgumentOutOfRangeException>(() => enforcer.Check(-1, 0));
    }

    [Fact]
    public void Check_NegativeRequests_Throws()
    {
        var enforcer = new BudgetEnforcer(Unlimited());

        Assert.Throws<ArgumentOutOfRangeException>(() => enforcer.Check(0, -1));
    }

    // --- Token budget only (request unlimited) ---

    [Fact]
    public void Check_TokenBudgetOnly_RequestUnlimited()
    {
        var enforcer = new BudgetEnforcer(WithTokenBudget(10_000));

        var result = enforcer.Check(8_500, 9999);

        Assert.Equal(BudgetStatus.Warning, result.Status);
        Assert.Null(result.RequestUsageFraction);
    }

    // --- Request budget only (token unlimited) ---

    [Fact]
    public void Check_RequestBudgetOnly_TokenUnlimited()
    {
        var enforcer = new BudgetEnforcer(WithRequestBudget(50));

        var result = enforcer.Check(999_999, 45);

        Assert.Equal(BudgetStatus.ConfirmationRequired, result.Status);
        Assert.Null(result.TokenUsageFraction);
    }
}
