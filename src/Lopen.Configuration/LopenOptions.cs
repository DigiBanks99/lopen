using System.Text.Json.Serialization;

namespace Lopen.Configuration;

/// <summary>
/// Root configuration POCO for Lopen settings.
/// All defaults match the Configuration SPECIFICATION.md.
/// </summary>
public sealed class LopenOptions
{
    public ModelOptions Models { get; set; } = new();
    public BudgetOptions Budget { get; set; } = new();
    public OracleOptions Oracle { get; set; } = new();
    public WorkflowOptions Workflow { get; set; } = new();
    public SessionOptions Session { get; set; } = new();
    public GitOptions Git { get; set; } = new();
    public ToolDisciplineOptions ToolDiscipline { get; set; } = new();
    public DisplayOptions Display { get; set; } = new();
}

public sealed class ModelOptions
{
    public string RequirementGathering { get; set; } = "claude-opus-4.6";
    public string Planning { get; set; } = "claude-opus-4.6";
    public string Building { get; set; } = "claude-opus-4.6";
    public string Research { get; set; } = "claude-opus-4.6";
}

public sealed class BudgetOptions
{
    public int TokenBudgetPerModule { get; set; }
    public int PremiumRequestBudget { get; set; }
    public double WarningThreshold { get; set; } = 0.8;
    public double ConfirmationThreshold { get; set; } = 0.9;
}

public sealed class OracleOptions
{
    public string Model { get; set; } = "gpt-5-mini";
}

public sealed class WorkflowOptions
{
    public bool Unattended { get; set; }
    public int MaxIterations { get; set; } = 100;
    public int FailureThreshold { get; set; } = 3;
}

public sealed class SessionOptions
{
    public bool AutoResume { get; set; } = true;
    public int SessionRetention { get; set; } = 10;
    public bool SaveIterationHistory { get; set; }
}

public sealed class GitOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutoCommit { get; set; } = true;
    public string Convention { get; set; } = "conventional";
}

public sealed class ToolDisciplineOptions
{
    public int MaxFileReads { get; set; } = 3;
    public int MaxCommandRetries { get; set; } = 3;
}

public sealed class DisplayOptions
{
    public bool ShowTokenUsage { get; set; } = true;
    public bool ShowPremiumCount { get; set; } = true;
}
