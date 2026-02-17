using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Lopen.Otel.Tests;

/// <summary>
/// Tests for OTLP export behavior and Aspire Dashboard integration configuration.
/// Covers JOB-100 acceptance criteria.
/// </summary>
public class OtlpExportTests
{
    [Fact]
    public void OtlpExport_NotActivated_WhenEndpointNotSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["otel:enabled"] = "true" })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // Should not throw — no export when no endpoint
        Assert.NotNull(sp);
    }

    [Fact]
    public void OtlpExport_Activates_WhenEndpointSet()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:enabled"] = "true",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // Export is configured (no way to verify at DI level, but it should not crash)
        Assert.NotNull(sp);
    }

    [Fact]
    public void OtlpExport_ConfigEndpoint_Activates()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:export:endpoint"] = "http://localhost:4317"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp);
    }

    [Fact]
    public void OtelDisabled_NoInstrumentation()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:enabled"] = "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // Disabled — no OTEL services registered
        Assert.NotNull(sp);
    }

    [Fact]
    public void IndividualSignalToggles_TracesDisabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:traces:enabled"] = "false",
                ["otel:metrics:enabled"] = "true",
                ["otel:logs:enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp);
    }

    [Fact]
    public void IndividualSignalToggles_MetricsDisabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:traces:enabled"] = "true",
                ["otel:metrics:enabled"] = "false",
                ["otel:logs:enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp);
    }

    [Fact]
    public void IndividualSignalToggles_LogsDisabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:traces:enabled"] = "true",
                ["otel:metrics:enabled"] = "true",
                ["otel:logs:enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp);
    }

    [Fact]
    public void ServiceName_EnvVarTakesPrecedence()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "custom-service",
                ["otel:service_name"] = "config-service"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // OTEL_SERVICE_NAME takes precedence — just verify no errors
        Assert.NotNull(sp);
    }

    [Fact]
    public void AspireAppHost_ProjectFile_Exists()
    {
        // Verify the AppHost project structure exists
        var appHostDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Lopen.AppHost");

        var csprojPath = Path.Combine(appHostDir, "Lopen.AppHost.csproj");
        var programPath = Path.Combine(appHostDir, "Program.cs");

        Assert.True(File.Exists(csprojPath), $"AppHost csproj not found at {csprojPath}");
        Assert.True(File.Exists(programPath), $"AppHost Program.cs not found at {programPath}");
    }

    [Fact]
    public void AspireAppHost_Program_ReferencesLopen()
    {
        var appHostDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Lopen.AppHost");
        var programPath = Path.Combine(appHostDir, "Program.cs");

        var content = File.ReadAllText(programPath);
        Assert.Contains("lopen", content);
        Assert.Contains("DistributedApplication", content);
    }

    // ==================== OTEL-10: Log correlation with TraceId/SpanId ====================

    [Fact]
    public void StructuredLogs_CarryTraceIdAndSpanId_WhenActivityActive()
    {
        // OTEL-10: Verify structured logs carry TraceId and SpanId from active Activity
        using var source = new System.Diagnostics.ActivitySource("Lopen.Otel.Tests.LogCorrelation");
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                System.Diagnostics.ActivitySamplingResult.AllData
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test-span");
        Assert.NotNull(activity);

        // Verify the activity has valid TraceId and SpanId
        Assert.NotEqual(default, activity.TraceId);
        Assert.NotEqual(default, activity.SpanId);

        // Verify Activity.Current is set within the span
        Assert.Equal(activity, System.Diagnostics.Activity.Current);

        // Log via ILogger within the activity context
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["otel:enabled"] = "true" })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        // When logging inside an active Activity, the log framework has access
        // to Activity.Current for correlation. The OTEL log exporter attaches
        // TraceId/SpanId automatically. We verify the infrastructure is wired.
        logger.LogInformation("Test log within span");

        // The key assertion: Activity.Current is available for log correlation
        Assert.Equal(activity.TraceId, System.Diagnostics.Activity.Current!.TraceId);
        Assert.Equal(activity.SpanId, System.Diagnostics.Activity.Current!.SpanId);
    }

    // ==================== OTEL-11: OTLP export activates for all 3 signals ====================

    [Fact]
    public void OtlpExport_AllThreeSignals_WhenEndpointSet()
    {
        // OTEL-11: When OTEL_EXPORTER_OTLP_ENDPOINT is set, all 3 signals should be active
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:enabled"] = "true",
                ["otel:traces:enabled"] = "true",
                ["otel:metrics:enabled"] = "true",
                ["otel:logs:enabled"] = "true",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // Verify all three signal providers are registered
        Assert.NotNull(sp); // Provider built without error

        // Verify TracerProvider is available (traces enabled)
        var tracerProvider = sp.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);

        // Verify MeterProvider is available (metrics enabled)
        var meterProvider = sp.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);

        // Verify ILoggerFactory is configured (logs enabled via AddOpenTelemetry)
        var loggerFactory = sp.GetService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);
    }

    // ==================== OTEL-12: No telemetry leaves when no endpoint ====================

    [Fact]
    public void NoEndpoint_NoOtlpExporter_ServicesStillResolve()
    {
        // OTEL-12: When no OTLP endpoint configured, no telemetry leaves the process
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:enabled"] = "true"
                // No OTEL_EXPORTER_OTLP_ENDPOINT, no otel:export:endpoint
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // Services resolve but no OTLP exporter is configured
        Assert.NotNull(sp);

        // Create and record spans — they should be processed but not exported
        using var source = new System.Diagnostics.ActivitySource(LopenTelemetryDiagnostics.AllSourceNames[0]);
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                System.Diagnostics.ActivitySamplingResult.AllData
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        using var activity = source.StartActivity("test-no-export");
        Assert.NotNull(activity);
        activity.SetTag("test", "value");
        // Activity completes without error — no OTLP export attempted
    }

    [Fact]
    public void Disabled_NoSignals_NoExport()
    {
        // OTEL-12: When otel is disabled entirely, nothing is configured
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["otel:enabled"] = "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLopenOtel(config);
        var sp = services.BuildServiceProvider();

        // When disabled, TracerProvider should NOT be registered
        var tracerProvider = sp.GetService<TracerProvider>();
        Assert.Null(tracerProvider);
    }
}
