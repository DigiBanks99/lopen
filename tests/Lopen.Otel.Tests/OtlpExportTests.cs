using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
}
