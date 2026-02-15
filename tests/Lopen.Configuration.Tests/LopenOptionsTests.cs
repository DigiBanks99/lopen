namespace Lopen.Configuration.Tests;

public class LopenOptionsTests
{
    [Fact]
    public void Defaults_Models_AllSetToPremiumTier()
    {
        var options = new LopenOptions();

        Assert.Equal("claude-opus-4.6", options.Models.RequirementGathering);
        Assert.Equal("claude-opus-4.6", options.Models.Planning);
        Assert.Equal("claude-opus-4.6", options.Models.Building);
        Assert.Equal("claude-opus-4.6", options.Models.Research);
    }

    [Fact]
    public void Defaults_Budget_ZeroBudgetsWithThresholds()
    {
        var options = new LopenOptions();

        Assert.Equal(0, options.Budget.TokenBudgetPerModule);
        Assert.Equal(0, options.Budget.PremiumRequestBudget);
        Assert.Equal(0.8, options.Budget.WarningThreshold);
        Assert.Equal(0.9, options.Budget.ConfirmationThreshold);
    }

    [Fact]
    public void Defaults_Oracle_GptMini()
    {
        var options = new LopenOptions();

        Assert.Equal("gpt-5-mini", options.Oracle.Model);
    }

    [Fact]
    public void Defaults_Workflow_StandardValues()
    {
        var options = new LopenOptions();

        Assert.False(options.Workflow.Unattended);
        Assert.Equal(100, options.Workflow.MaxIterations);
        Assert.Equal(3, options.Workflow.FailureThreshold);
    }

    [Fact]
    public void Defaults_Session_AutoResumeEnabledRetention10()
    {
        var options = new LopenOptions();

        Assert.True(options.Session.AutoResume);
        Assert.Equal(10, options.Session.SessionRetention);
        Assert.False(options.Session.SaveIterationHistory);
    }

    [Fact]
    public void Defaults_Git_EnabledAutoCommitConventional()
    {
        var options = new LopenOptions();

        Assert.True(options.Git.Enabled);
        Assert.True(options.Git.AutoCommit);
        Assert.Equal("conventional", options.Git.Convention);
    }

    [Fact]
    public void Defaults_ToolDiscipline_ThreeRetries()
    {
        var options = new LopenOptions();

        Assert.Equal(3, options.ToolDiscipline.MaxFileReads);
        Assert.Equal(3, options.ToolDiscipline.MaxCommandRetries);
    }

    [Fact]
    public void Defaults_Display_AllEnabled()
    {
        var options = new LopenOptions();

        Assert.True(options.Display.ShowTokenUsage);
        Assert.True(options.Display.ShowPremiumCount);
    }
}
