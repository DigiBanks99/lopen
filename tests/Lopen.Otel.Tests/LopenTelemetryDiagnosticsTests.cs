using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lopen.Otel.Tests;

[Collection("ActivityListener")]
public class LopenTelemetryDiagnosticsTests
{
    // --- ActivitySource Tests ---

    [Theory]
    [InlineData("Lopen.Workflow")]
    [InlineData("Lopen.Sdk")]
    [InlineData("Lopen.Tools")]
    [InlineData("Lopen.Oracle")]
    [InlineData("Lopen.Session")]
    [InlineData("Lopen.Git")]
    public void ActivitySource_HasCorrectName(string expectedName)
    {
        var source = expectedName switch
        {
            "Lopen.Workflow" => LopenTelemetryDiagnostics.Workflow,
            "Lopen.Sdk" => LopenTelemetryDiagnostics.Sdk,
            "Lopen.Tools" => LopenTelemetryDiagnostics.Tools,
            "Lopen.Oracle" => LopenTelemetryDiagnostics.Oracle,
            "Lopen.Session" => LopenTelemetryDiagnostics.Session,
            "Lopen.Git" => LopenTelemetryDiagnostics.Git,
            _ => throw new ArgumentException($"Unknown source: {expectedName}")
        };

        Assert.NotNull(source);
        Assert.Equal(expectedName, source.Name);
    }

    [Fact]
    public void AllSourceNames_ContainsAllSixSources()
    {
        Assert.Equal(6, LopenTelemetryDiagnostics.AllSourceNames.Length);
        Assert.Contains("Lopen.Workflow", LopenTelemetryDiagnostics.AllSourceNames);
        Assert.Contains("Lopen.Sdk", LopenTelemetryDiagnostics.AllSourceNames);
        Assert.Contains("Lopen.Tools", LopenTelemetryDiagnostics.AllSourceNames);
        Assert.Contains("Lopen.Oracle", LopenTelemetryDiagnostics.AllSourceNames);
        Assert.Contains("Lopen.Session", LopenTelemetryDiagnostics.AllSourceNames);
        Assert.Contains("Lopen.Git", LopenTelemetryDiagnostics.AllSourceNames);
    }

    // --- Meter Tests ---

    [Fact]
    public void Meter_HasCorrectName()
    {
        Assert.Equal("Lopen.Metrics", LopenTelemetryDiagnostics.Meter.Name);
    }

    [Fact]
    public void Meter_HasVersion()
    {
        Assert.Equal("1.0.0", LopenTelemetryDiagnostics.Meter.Version);
    }

    // --- Counter Tests ---

    [Fact]
    public void CommandCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.CommandCount);
    }

    [Fact]
    public void ToolCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.ToolCount);
    }

    [Fact]
    public void SdkInvocationCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.SdkInvocationCount);
    }

    [Fact]
    public void TokensConsumed_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.TokensConsumed);
    }

    [Fact]
    public void PremiumRequestCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.PremiumRequestCount);
    }

    [Fact]
    public void OracleVerdictCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.OracleVerdictCount);
    }

    [Fact]
    public void TasksCompletedCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.TasksCompletedCount);
    }

    [Fact]
    public void TasksFailedCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.TasksFailedCount);
    }

    [Fact]
    public void BackPressureEventCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.BackPressureEventCount);
    }

    [Fact]
    public void GitCommitCount_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.GitCommitCount);
    }

    // --- Histogram Tests ---

    [Fact]
    public void SdkInvocationDuration_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.SdkInvocationDuration);
    }

    [Fact]
    public void ToolDuration_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.ToolDuration);
    }

    [Fact]
    public void TaskDuration_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.TaskDuration);
    }

    [Fact]
    public void CommandDuration_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.CommandDuration);
    }

    [Fact]
    public void OracleDuration_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.OracleDuration);
    }

    // --- Gauge Tests ---

    [Fact]
    public void SessionIteration_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.SessionIteration);
    }

    [Fact]
    public void ContextWindowUtilization_IsNotNull()
    {
        Assert.NotNull(LopenTelemetryDiagnostics.ContextWindowUtilization);
    }

    // --- Instrument Usability Tests ---

    [Fact]
    public void Counters_CanRecordValues()
    {
        // Counters should not throw when recording values, even without a listener
        var exception = Record.Exception(() =>
        {
            LopenTelemetryDiagnostics.CommandCount.Add(1,
                new KeyValuePair<string, object?>("command.name", "test"));
            LopenTelemetryDiagnostics.ToolCount.Add(1,
                new KeyValuePair<string, object?>("tool.name", "read_spec"));
            LopenTelemetryDiagnostics.SdkInvocationCount.Add(1,
                new KeyValuePair<string, object?>("model", "test-model"));
            LopenTelemetryDiagnostics.TokensConsumed.Add(100,
                new KeyValuePair<string, object?>("model", "test-model"),
                new KeyValuePair<string, object?>("direction", "input"));
            LopenTelemetryDiagnostics.PremiumRequestCount.Add(1);
            LopenTelemetryDiagnostics.OracleVerdictCount.Add(1,
                new KeyValuePair<string, object?>("scope", "task"),
                new KeyValuePair<string, object?>("verdict", "pass"));
            LopenTelemetryDiagnostics.TasksCompletedCount.Add(1,
                new KeyValuePair<string, object?>("module", "core"));
            LopenTelemetryDiagnostics.TasksFailedCount.Add(1,
                new KeyValuePair<string, object?>("module", "core"));
            LopenTelemetryDiagnostics.BackPressureEventCount.Add(1,
                new KeyValuePair<string, object?>("category", "resource_limits"));
            LopenTelemetryDiagnostics.GitCommitCount.Add(1,
                new KeyValuePair<string, object?>("operation", "auto"));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Histograms_CanRecordValues()
    {
        var exception = Record.Exception(() =>
        {
            LopenTelemetryDiagnostics.SdkInvocationDuration.Record(150.5,
                new KeyValuePair<string, object?>("model", "test-model"));
            LopenTelemetryDiagnostics.ToolDuration.Record(25.0,
                new KeyValuePair<string, object?>("tool.name", "read_spec"));
            LopenTelemetryDiagnostics.TaskDuration.Record(5000.0,
                new KeyValuePair<string, object?>("module", "core"));
            LopenTelemetryDiagnostics.CommandDuration.Record(12000.0,
                new KeyValuePair<string, object?>("command.name", "build"));
            LopenTelemetryDiagnostics.OracleDuration.Record(3000.0,
                new KeyValuePair<string, object?>("scope", "task"));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Gauges_CanRecordValues()
    {
        var exception = Record.Exception(() =>
        {
            LopenTelemetryDiagnostics.SessionIteration.Record(5);
            LopenTelemetryDiagnostics.ContextWindowUtilization.Record(0.73);
        });

        Assert.Null(exception);
    }

    // --- ActivitySource Span Creation Tests ---

    [Fact]
    public void ActivitySource_StartActivity_ReturnsNull_WhenNoListener()
    {
        // Without an ActivityListener, StartActivity returns null (zero overhead)
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => false, // Ensure no listener is active
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.None
        };
        ActivitySource.AddActivityListener(listener);

        var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.command");
        Assert.Null(activity);
    }

    [Fact]
    public void ActivitySource_StartActivity_ReturnsActivity_WhenListenerRegistered()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Lopen.Workflow",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.command");
        Assert.NotNull(activity);
        Assert.Equal("lopen.command", activity.DisplayName);
    }

    [Fact]
    public void ActivitySource_SupportsTagSetting()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Lopen.Workflow",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.command");
        activity?.SetTag("lopen.command.name", "build");
        activity?.SetTag("lopen.command.headless", true);
        activity?.SetTag("lopen.command.exit_code", 0);

        Assert.Equal("build", activity?.GetTagItem("lopen.command.name"));
        Assert.Equal(true, activity?.GetTagItem("lopen.command.headless"));
        Assert.Equal(0, activity?.GetTagItem("lopen.command.exit_code"));
    }

    [Fact]
    public void ActivitySource_SupportsParentChildHierarchy()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Lopen.Workflow" or "Lopen.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = LopenTelemetryDiagnostics.Workflow.StartActivity("lopen.command");
        Assert.NotNull(parent);

        using var child = LopenTelemetryDiagnostics.Sdk.StartActivity("lopen.sdk.invocation");
        Assert.NotNull(child);
        Assert.Equal(parent.TraceId, child.TraceId);
        Assert.Equal(parent.SpanId, child.ParentSpanId);
    }

    // --- MeterListener Acceptance Test (OTEL-08) ---

    [Fact]
    public void Counters_IncrementCorrectly_VerifiedViaMeterListener()
    {
        var measurements = new Dictionary<string, long>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, ml) =>
        {
            if (instrument.Meter.Name == "Lopen.Metrics" && instrument is Counter<long>)
                ml.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            measurements[instrument.Name] = measurements.GetValueOrDefault(instrument.Name) + measurement;
        });
        meterListener.Start();

        LopenTelemetryDiagnostics.CommandCount.Add(3);
        LopenTelemetryDiagnostics.ToolCount.Add(5);
        LopenTelemetryDiagnostics.SdkInvocationCount.Add(7);
        LopenTelemetryDiagnostics.TokensConsumed.Add(1500);
        LopenTelemetryDiagnostics.PremiumRequestCount.Add(2);
        LopenTelemetryDiagnostics.OracleVerdictCount.Add(4);
        LopenTelemetryDiagnostics.TasksCompletedCount.Add(10);
        LopenTelemetryDiagnostics.TasksFailedCount.Add(1);
        LopenTelemetryDiagnostics.BackPressureEventCount.Add(6);
        LopenTelemetryDiagnostics.GitCommitCount.Add(8);

        Assert.Equal(3, measurements["lopen.commands.count"]);
        Assert.Equal(5, measurements["lopen.tools.count"]);
        Assert.Equal(7, measurements["lopen.sdk.invocations.count"]);
        Assert.Equal(1500, measurements["lopen.sdk.tokens.consumed"]);
        Assert.Equal(2, measurements["lopen.sdk.premium_requests.count"]);
        Assert.Equal(4, measurements["lopen.oracle.verdicts.count"]);
        Assert.Equal(10, measurements["lopen.tasks.completed.count"]);
        Assert.Equal(1, measurements["lopen.tasks.failed.count"]);
        Assert.Equal(6, measurements["lopen.backpressure.events.count"]);
        Assert.Equal(8, measurements["lopen.git.commits.count"]);

        Assert.Equal(10, measurements.Count);
    }
}
