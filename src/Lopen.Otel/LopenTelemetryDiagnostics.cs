using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lopen.Otel;

/// <summary>
/// Defines all ActivitySources, Meters, and instruments for Lopen's OpenTelemetry instrumentation.
/// Instrumentation code in other modules uses these static instances directly.
/// </summary>
public static class LopenTelemetryDiagnostics
{
    // --- ActivitySources (Traces) ---

    public static readonly ActivitySource Workflow = new("Lopen.Workflow");
    public static readonly ActivitySource Sdk = new("Lopen.Sdk");
    public static readonly ActivitySource Tools = new("Lopen.Tools");
    public static readonly ActivitySource Oracle = new("Lopen.Oracle");
    public static readonly ActivitySource Session = new("Lopen.Session");
    public static readonly ActivitySource Git = new("Lopen.Git");

    /// <summary>
    /// All ActivitySource names for registration with the OTEL SDK.
    /// </summary>
    public static readonly string[] AllSourceNames =
    [
        Workflow.Name,
        Sdk.Name,
        Tools.Name,
        Oracle.Name,
        Session.Name,
        Git.Name
    ];

    // --- Meter and Instruments (Metrics) ---

    public static readonly Meter Meter = new("Lopen.Metrics", "1.0.0");

    // Counters
    public static readonly Counter<long> CommandCount =
        Meter.CreateCounter<long>("lopen.commands.count", "{invocations}", "Total CLI command invocations");

    public static readonly Counter<long> ToolCount =
        Meter.CreateCounter<long>("lopen.tools.count", "{calls}", "Total Lopen-managed tool calls");

    public static readonly Counter<long> SdkInvocationCount =
        Meter.CreateCounter<long>("lopen.sdk.invocations.count", "{invocations}", "Total SDK invocations");

    public static readonly Counter<long> TokensConsumed =
        Meter.CreateCounter<long>("lopen.sdk.tokens.consumed", "{tokens}", "Total tokens consumed");

    public static readonly Counter<long> PremiumRequestCount =
        Meter.CreateCounter<long>("lopen.sdk.premium_requests.count", "{requests}", "Total premium API requests consumed");

    public static readonly Counter<long> OracleVerdictCount =
        Meter.CreateCounter<long>("lopen.oracle.verdicts.count", "{verdicts}", "Oracle verdicts");

    public static readonly Counter<long> TasksCompletedCount =
        Meter.CreateCounter<long>("lopen.tasks.completed.count", "{tasks}", "Tasks completed");

    public static readonly Counter<long> TasksFailedCount =
        Meter.CreateCounter<long>("lopen.tasks.failed.count", "{tasks}", "Tasks failed");

    public static readonly Counter<long> BackPressureEventCount =
        Meter.CreateCounter<long>("lopen.backpressure.events.count", "{events}", "Back-pressure events");

    public static readonly Counter<long> GitCommitCount =
        Meter.CreateCounter<long>("lopen.git.commits.count", "{commits}", "Git commits made");

    // Histograms
    public static readonly Histogram<double> SdkInvocationDuration =
        Meter.CreateHistogram<double>("lopen.sdk.invocation.duration", "ms", "Duration of each SDK invocation");

    public static readonly Histogram<double> ToolDuration =
        Meter.CreateHistogram<double>("lopen.tool.duration", "ms", "Duration of each Lopen-managed tool call");

    public static readonly Histogram<double> TaskDuration =
        Meter.CreateHistogram<double>("lopen.task.duration", "ms", "Duration of each task execution");

    public static readonly Histogram<double> CommandDuration =
        Meter.CreateHistogram<double>("lopen.command.duration", "ms", "Duration of each CLI command");

    public static readonly Histogram<double> OracleDuration =
        Meter.CreateHistogram<double>("lopen.oracle.duration", "ms", "Duration of each oracle verification");

    // Gauges
    public static readonly Gauge<long> SessionIteration =
        Meter.CreateGauge<long>("lopen.session.iteration", "{iteration}", "Current iteration number within the active session");

    public static readonly Gauge<double> ContextWindowUtilization =
        Meter.CreateGauge<double>("lopen.sdk.context_window.utilization", "ratio", "Fraction of context window used");
}
