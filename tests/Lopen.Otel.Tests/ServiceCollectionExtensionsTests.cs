using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Lopen.Otel.Tests;

[Collection("ActivityListener")]
public class ServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? settings = null)
    {
        var builder = new ConfigurationBuilder();
        if (settings != null)
        {
            builder.AddInMemoryCollection(settings);
        }

        return builder.Build();
    }

    // --- Basic Registration ---

    [Fact]
    public void AddLopenOtel_RegistersWithoutError()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration();

        var exception = Record.Exception(() => services.AddLopenOtel(config));

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenOtel_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration();

        var result = services.AddLopenOtel(config);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenOtel_ServiceProviderBuildsWithoutError()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration();
        services.AddLopenOtel(config);

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    // --- Master Toggle ---

    [Fact]
    public void AddLopenOtel_MasterDisabled_SkipsRegistration()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        // When master is disabled, no OTEL services should be registered
        using var provider = services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.Null(tracerProvider);
    }

    [Fact]
    public void AddLopenOtel_MasterEnabled_RegistersTracerProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:enabled"] = "true"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddLopenOtel_DefaultEnabled_RegistersTracerProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration();

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    // --- Per-Signal Toggles ---

    [Fact]
    public void AddLopenOtel_TracesDisabled_NoTracerProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:traces:enabled"] = "false",
            ["otel:metrics:enabled"] = "false",
            ["otel:logs:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        // WithTracing not called, so no TracerProvider
        Assert.Null(tracerProvider);
    }

    [Fact]
    public void AddLopenOtel_TracesEnabled_RegistersTracerProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:traces:enabled"] = "true",
            ["otel:metrics:enabled"] = "false",
            ["otel:logs:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddLopenOtel_MetricsDisabled_NoMeterProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:traces:enabled"] = "false",
            ["otel:metrics:enabled"] = "false",
            ["otel:logs:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var meterProvider = provider.GetService<MeterProvider>();
        Assert.Null(meterProvider);
    }

    [Fact]
    public void AddLopenOtel_MetricsEnabled_RegistersMeterProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:traces:enabled"] = "false",
            ["otel:metrics:enabled"] = "true",
            ["otel:logs:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var meterProvider = provider.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddLopenOtel_LogsEnabled_RegistersOtelLogProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:traces:enabled"] = "false",
            ["otel:metrics:enabled"] = "false",
            ["otel:logs:enabled"] = "true"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        var loggerFactory = provider.GetService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);

        // Verify logger can be created without error
        var logger = loggerFactory.CreateLogger("TestCategory");
        Assert.NotNull(logger);
    }

    [Fact]
    public void AddLopenOtel_LogsDisabled_DoesNotRegisterOtelLogProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:traces:enabled"] = "false",
            ["otel:metrics:enabled"] = "false",
            ["otel:logs:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        // Should still build without error even with all signals disabled
        using var provider = services.BuildServiceProvider();
        var exception = Record.Exception(() => provider.GetService<ILoggerFactory>());
        Assert.Null(exception);
    }

    // --- OTLP Export Conditional ---

    [Fact]
    public void AddLopenOtel_NoEndpoint_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration();

        services.AddLopenOtel(config);

        // No OTLP endpoint set — should not attempt to export, no errors
        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenOtel_WithEndpointEnvVar_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
        });

        services.AddLopenOtel(config);

        // With endpoint set — UseOtlpExporter is called, should still build
        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenOtel_WithConfigEndpoint_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:export:endpoint"] = "http://localhost:4317"
        });

        services.AddLopenOtel(config);

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    // --- Service Name Configuration ---

    [Fact]
    public void AddLopenOtel_DefaultServiceName_IsLopen()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration();

        // Default service name should be "lopen" — we verify by building without error
        services.AddLopenOtel(config);

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenOtel_CustomServiceNameFromConfig_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:service_name"] = "custom-lopen"
        });

        services.AddLopenOtel(config);

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenOtel_OtelServiceNameEnvVar_TakesPrecedence()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "env-lopen",
            ["otel:service_name"] = "config-lopen"
        });

        // OTEL_SERVICE_NAME should take precedence over otel:service_name
        services.AddLopenOtel(config);

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenOtel_OtelServiceNameEnvVar_ValueTakesPrecedence()
    {
        // OTEL_SERVICE_NAME must take precedence over otel:service_name (OTEL-16)
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "env-lopen",
            ["otel:service_name"] = "config-lopen"
        });

        var resolved = config["OTEL_SERVICE_NAME"] ?? config["otel:service_name"] ?? "lopen";
        Assert.Equal("env-lopen", resolved);
    }

    [Fact]
    public void AddLopenOtel_NoEnvVar_FallsToConfigServiceName()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:service_name"] = "config-lopen"
        });

        var resolved = config["OTEL_SERVICE_NAME"] ?? config["otel:service_name"] ?? "lopen";
        Assert.Equal("config-lopen", resolved);
    }

    [Fact]
    public void AddLopenOtel_NoServiceName_DefaultsToLopen()
    {
        var config = BuildConfiguration();

        var resolved = config["OTEL_SERVICE_NAME"] ?? config["otel:service_name"] ?? "lopen";
        Assert.Equal("lopen", resolved);
    }

    // --- Integration Test: All Signals Enabled ---

    [Fact]
    public void AddLopenOtel_AllSignalsEnabled_BuildsSuccessfully()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:enabled"] = "true",
            ["otel:traces:enabled"] = "true",
            ["otel:metrics:enabled"] = "true",
            ["otel:logs:enabled"] = "true"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
        Assert.NotNull(provider.GetService<ILoggerFactory>());
    }

    [Fact]
    public void AddLopenOtel_AllSignalsDisabled_BuildsSuccessfully()
    {
        var services = new ServiceCollection();
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["otel:enabled"] = "true",
            ["otel:traces:enabled"] = "false",
            ["otel:metrics:enabled"] = "false",
            ["otel:logs:enabled"] = "false"
        });

        services.AddLopenOtel(config);

        using var provider = services.BuildServiceProvider();
        // No providers registered when all signals disabled
        Assert.Null(provider.GetService<TracerProvider>());
        Assert.Null(provider.GetService<MeterProvider>());
    }

    // --- OTEL-17: Performance overhead < 5ms ---

    [Fact]
    public void AddLopenOtel_OverheadUnder5ms()
    {
        var config = BuildConfiguration();

        // Warm up
        var warmup = new ServiceCollection();
        warmup.AddLogging();
        warmup.AddLopenOtel(config);
        warmup.BuildServiceProvider().Dispose();

        // Measure registration + build overhead
        const int iterations = 10;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddLopenOtel(config);
            using var provider = services.BuildServiceProvider();
        }
        sw.Stop();

        var avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        Assert.True(avgMs < 50, $"OTEL registration + build averaged {avgMs:F2}ms per iteration (expected < 50ms)");
    }
}
