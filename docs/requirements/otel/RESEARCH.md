---
date: 2026-02-15
sources:
  - https://github.com/open-telemetry/opentelemetry-dotnet
  - https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol
  - https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Extensions.Hosting
  - https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing
  - https://learn.microsoft.com/dotnet/core/diagnostics/metrics
---

# OTEL Module Research

## 1. OpenTelemetry .NET SDK Overview

The OpenTelemetry .NET SDK is **stable** across all three signals (traces, metrics, logs). The SDK builds on top of the .NET runtime's built-in `System.Diagnostics` APIs rather than introducing its own abstractions — `ActivitySource`/`Activity` map to OTEL Tracer/Span, and `Meter`/`Counter`/`Histogram`/`Gauge` map to the OTEL Metrics API.

### Package Versions

The current stable release line is **1.14.0**. All packages are published to NuGet:

| Package | Purpose |
|---|---|
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP export (traces, metrics, logs) |
| `OpenTelemetry.Extensions.Hosting` | `IHostBuilder` / `IServiceCollection` integration |
| `OpenTelemetry.Instrumentation.Http` | Auto-instrument `HttpClient` calls |
| `OpenTelemetry.Instrumentation.Runtime` | .NET runtime metrics (GC, thread pool, etc.) |

### Supported .NET Versions

The SDK supports all officially supported .NET versions including **.NET 10.0**. Since Lopen targets `net10.0`, all SDK features are available including `Gauge<T>` (introduced in .NET 9+).

### Key Architectural Insight

The Trace and Metrics APIs are implemented by the **.NET runtime itself** (`System.Diagnostics.DiagnosticSource`), not by the OpenTelemetry SDK. The SDK provides the *export pipeline* — processors, exporters, and configuration. This means:

- Instrumentation code depends only on `System.Diagnostics` (zero OTEL dependency)
- The OTEL SDK is only needed at the composition root (DI wiring)
- When no listener is registered, `ActivitySource.StartActivity()` returns `null` — zero allocation, zero overhead

### Relevance to Lopen

Lopen is a CLI tool (not ASP.NET Core), so it skips `AddAspNetCoreInstrumentation()` and uses only `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.Http` + `OpenTelemetry.Instrumentation.Runtime`. The instrumentation code in Lopen's module libraries depends only on `System.Diagnostics` — the OTEL SDK packages are referenced only by the composition root (`Lopen.Cli` or `Lopen.Otel`).

---

## 2. Creating Custom Spans (Traces)

Traces in .NET use `System.Diagnostics.Activity` (= OTEL Span) and `ActivitySource` (= OTEL Tracer).

### Defining an ActivitySource

```csharp
// Static field — one per logical component
private static readonly ActivitySource Source = new("Lopen.Workflow");
```

### Creating Spans

```csharp
// Root span (or child of current ambient span)
using var activity = Source.StartActivity("lopen.command", ActivityKind.Internal);
activity?.SetTag("lopen.command.name", "build");
activity?.SetTag("lopen.command.headless", true);
activity?.SetTag("lopen.command.exit_code", 0);
activity?.SetStatus(ActivityStatusCode.Ok);
```

### Parent-Child Hierarchy

Child spans are automatic when nested within a parent's `using` block. The .NET runtime tracks the ambient `Activity.Current` via `AsyncLocal<T>`:

```csharp
using var commandSpan = Source.StartActivity("lopen.command");
{
    // Automatically becomes a child of commandSpan
    using var phaseSpan = Source.StartActivity("lopen.workflow.phase");
    phaseSpan?.SetTag("lopen.phase.name", "building");
    {
        // Automatically becomes a child of phaseSpan
        using var sdkSpan = Source.StartActivity("lopen.sdk.invocation");
        sdkSpan?.SetTag("lopen.sdk.model", "claude-opus-4.6");
        sdkSpan?.SetTag("lopen.sdk.tokens.input", 1500);
        sdkSpan?.SetTag("lopen.sdk.tokens.output", 800);
    }
}
```

### Registering Sources with the SDK

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Lopen.Workflow")
        .AddSource("Lopen.Sdk")
        .AddSource("Lopen.Tools")
        .AddSource("Lopen.Oracle"));
```

### Error Handling on Spans

```csharp
try
{
    // ... work
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

### Relevance to Lopen

Each span type from the OTEL specification (`lopen.command`, `lopen.workflow.phase`, `lopen.sdk.invocation`, `lopen.tool.<name>`, `lopen.oracle.verification`, `lopen.task.execution`) maps to a `StartActivity()` call with the appropriate attributes set via `SetTag()`. The `null` propagation pattern (`activity?.`) ensures zero overhead when no exporter is listening.

---

## 3. Creating Custom Metrics

Metrics in .NET use `System.Diagnostics.Metrics` — `Meter`, `Counter<T>`, `Histogram<T>`, and `Gauge<T>`.

### Defining a Meter and Instruments

```csharp
// Static field — one Meter per logical component
private static readonly Meter LOpenMeter = new("Lopen.Metrics", "1.0.0");

// Counters — monotonically increasing values
private static readonly Counter<long> CommandCount =
    LOpenMeter.CreateCounter<long>("lopen.commands.count", "{invocations}", "Total CLI command invocations");

private static readonly Counter<long> TokensConsumed =
    LOpenMeter.CreateCounter<long>("lopen.sdk.tokens.consumed", "{tokens}", "Total tokens consumed");

private static readonly Counter<long> PremiumRequests =
    LOpenMeter.CreateCounter<long>("lopen.sdk.premium_requests.count", "{requests}", "Premium API requests");

// Histograms — distribution of values (durations, sizes)
private static readonly Histogram<double> SdkDuration =
    LOpenMeter.CreateHistogram<double>("lopen.sdk.invocation.duration", "ms", "SDK invocation duration");

private static readonly Histogram<double> ToolDuration =
    LOpenMeter.CreateHistogram<double>("lopen.tool.duration", "ms", "Tool call duration");

// Gauges — point-in-time values (.NET 9+)
private static readonly Gauge<double> ContextUtilization =
    LOpenMeter.CreateGauge<double>("lopen.sdk.context_window.utilization", "ratio", "Context window utilization");
```

### Recording Measurements

```csharp
// Counter — add with tags
CommandCount.Add(1, new KeyValuePair<string, object?>("command.name", "build"));

// Counter — multiple tags
TokensConsumed.Add(1500,
    new("model", "claude-opus-4.6"),
    new("direction", "input"));

// Histogram — record a duration
SdkDuration.Record(elapsed.TotalMilliseconds, new("model", "claude-opus-4.6"));

// Gauge — record current value
ContextUtilization.Record(0.73);
```

### Registering Meters with the SDK

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("Lopen.Metrics")
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation());
```

### Relevance to Lopen

All counters and histograms from the specification (`lopen.commands.count`, `lopen.tools.count`, `lopen.sdk.invocations.count`, `lopen.sdk.tokens.consumed`, `lopen.sdk.invocation.duration`, `lopen.tool.duration`, etc.) map to `Counter<long>` or `Histogram<double>` instruments on a shared `Meter`. The `Gauge<double>` type (available on .NET 9+) covers `lopen.session.iteration` and `lopen.sdk.context_window.utilization`.

---

## 4. OTLP Exporter Configuration

### Unified Exporter with `UseOtlpExporter()`

The simplest setup uses the cross-cutting `UseOtlpExporter()` extension which registers OTLP export for all three signals in a single call:

```csharp
builder.Services.AddOpenTelemetry()
    .UseOtlpExporter();
```

This method:
- Enables export for **all three signals** (traces, metrics, logs)
- Reads configuration from standard OTEL environment variables automatically
- Can only be called **once** (subsequent calls throw `NotSupportedException`)
- Cannot be mixed with signal-specific `AddOtlpExporter()` calls

### Conditional Export (Lopen Pattern)

```csharp
var useOtlpExporter = !string.IsNullOrWhiteSpace(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

if (useOtlpExporter)
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}
```

When `OTEL_EXPORTER_OTLP_ENDPOINT` is not set, instrumentation is still active in-process (for internal metric tracking) but no telemetry leaves the process and no export errors occur.

### Protocol Options

| Protocol | Default Port | `OTEL_EXPORTER_OTLP_PROTOCOL` Value |
|---|---|---|
| gRPC | 4317 | `grpc` (default) |
| HTTP/Protobuf | 4318 | `http/protobuf` |

When using `http/protobuf`, signal-specific paths are appended automatically (e.g., `/v1/traces`, `/v1/metrics`, `/v1/logs`).

### OtlpExporterOptions

For programmatic configuration beyond environment variables:

```csharp
builder.Services.AddOpenTelemetry()
    .UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri("http://localhost:4317"));
```

Or via the Options API:

```csharp
builder.Services.Configure<OtlpExporterOptions>(o =>
{
    o.Endpoint = new Uri("http://localhost:4317");
    o.Protocol = OtlpExportProtocol.Grpc;
    o.TimeoutMilliseconds = 10_000;
    o.Headers = "x-custom-header=value";
});
```

### Relevance to Lopen

Lopen uses the conditional `UseOtlpExporter()` pattern — a single call that activates all three signals when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. This aligns with the specification's "always-on capability" principle. The Aspire AppHost sets this environment variable automatically during `aspire run`.

---

## 5. ILogger Integration with OTEL Log Provider

### Setup

```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});
```

### How It Works

- The OTEL log provider intercepts `ILogger` calls and converts them to `LogRecord` objects
- When an active `Activity` (span) exists, the `TraceId` and `SpanId` are **automatically** attached to the log record — no manual correlation needed
- Structured log parameters (e.g., `logger.LogInformation("Processing {CommandName}", name)`) are preserved as attributes on the `LogRecord`
- Scopes (`using logger.BeginScope(...)`) are exported as additional attributes when `IncludeScopes = true`

### Log Categories in Lopen

```csharp
// Injected via DI — category is the class name by default
public class WorkflowOrchestrator(ILogger<WorkflowOrchestrator> logger)
{
    public void ExecutePhase(string phaseName)
    {
        logger.LogInformation("Entering phase {PhaseName}", phaseName);
        // This log automatically carries TraceId/SpanId of the ambient Activity
    }
}
```

### Compile-Time Logging Source Generation (Recommended)

For high-performance logging, use the `[LoggerMessage]` source generator:

```csharp
internal static partial class LogMessages
{
    [LoggerMessage(LogLevel.Information, "SDK invocation complete: model={Model}, tokens={TotalTokens}")]
    public static partial void SdkInvocationComplete(this ILogger logger, string model, int totalTokens);

    [LoggerMessage(LogLevel.Warning, "Back-pressure triggered: {Category} — {Trigger}")]
    public static partial void BackPressureTriggered(this ILogger logger, string category, string trigger);
}
```

This generates zero-allocation logging code at compile time, which is ideal for a performance-sensitive CLI tool.

### Export Flow

```
ILogger call → OpenTelemetry Log Provider → BatchLogRecordProcessor → OTLP Exporter → Dashboard/Collector
```

Logs are batched and exported on a background thread. The batch interval is controlled by `OTEL_BLRP_SCHEDULE_DELAY` (default: 5000ms).

### Relevance to Lopen

All Lopen log categories (`Lopen.Workflow`, `Lopen.Sdk`, `Lopen.Tools`, `Lopen.Oracle`, `Lopen.BackPressure`, `Lopen.Session`, `Lopen.Git`, `Lopen.Config`) use standard `ILogger<T>`. The OTEL log provider automatically correlates logs with the active trace context. The `[LoggerMessage]` source generator should be used for all high-frequency log sites to minimize allocation overhead.

---

## 6. Configuration Toggles for Enabling/Disabling Signals

### Master Switch

The Lopen specification defines `otel.enabled` as a master switch. When `false`, no instrumentation is registered:

```csharp
if (!config.GetValue<bool>("otel:enabled", defaultValue: true))
{
    // Skip all OTEL registration — zero overhead
    return;
}
```

### Per-Signal Toggles

Individual signals can be toggled independently:

```csharp
var otelBuilder = services.AddOpenTelemetry();

if (config.GetValue<bool>("otel:traces:enabled", defaultValue: true))
{
    otelBuilder.WithTracing(tracing => tracing
        .AddSource("Lopen.Workflow")
        .AddSource("Lopen.Sdk")
        .AddSource("Lopen.Tools")
        .AddSource("Lopen.Oracle")
        .AddHttpClientInstrumentation());
}

if (config.GetValue<bool>("otel:metrics:enabled", defaultValue: true))
{
    otelBuilder.WithMetrics(metrics => metrics
        .AddMeter("Lopen.Metrics")
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation());
}

if (config.GetValue<bool>("otel:logs:enabled", defaultValue: true))
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });
}
```

### Behavior When Disabled

When a signal is disabled:
- **Traces disabled**: `ActivitySource.StartActivity()` returns `null`; the `activity?.` pattern short-circuits — zero allocation
- **Metrics disabled**: `Counter.Add()` / `Histogram.Record()` are no-ops when no `MeterProvider` is listening — negligible overhead
- **Logs disabled**: Standard `ILogger` filtering applies; OTEL log provider is not registered

### Relevance to Lopen

The three toggles (`otel.traces.enabled`, `otel.metrics.enabled`, `otel.logs.enabled`) map to conditionally calling `WithTracing()`, `WithMetrics()`, and `AddOpenTelemetry()` on the logging builder. The master `otel.enabled = false` skips all OTEL registration entirely.

---

## 7. Environment Variable Precedence

The OpenTelemetry .NET SDK reads configuration through `IConfiguration`, which means OTEL environment variables are resolved through the standard .NET configuration hierarchy.

### Standard OTEL Environment Variables

| Variable | Purpose | Default |
|---|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint | `http://localhost:4317` (gRPC) |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Transport protocol | `grpc` |
| `OTEL_EXPORTER_OTLP_HEADERS` | Custom headers (key=value pairs) | *(none)* |
| `OTEL_EXPORTER_OTLP_TIMEOUT` | Export timeout (ms) | `10000` |
| `OTEL_SERVICE_NAME` | Service identity in telemetry | `unknown_service:<process>` |
| `OTEL_RESOURCE_ATTRIBUTES` | Additional resource attributes | *(none)* |

### Signal-Specific Overrides (with `UseOtlpExporter` only)

| Variable | Signal |
|---|---|
| `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` | Traces |
| `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` | Metrics |
| `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` | Logs |
| `OTEL_EXPORTER_OTLP_TRACES_PROTOCOL` | Traces |
| `OTEL_EXPORTER_OTLP_METRICS_PROTOCOL` | Metrics |
| `OTEL_EXPORTER_OTLP_LOGS_PROTOCOL` | Logs |

### Batch/Reader Tuning

| Variable | Signal | Property |
|---|---|---|
| `OTEL_BSP_SCHEDULE_DELAY` | Traces | Batch export interval (ms) |
| `OTEL_BLRP_SCHEDULE_DELAY` | Logs | Batch export interval (ms) |
| `OTEL_METRIC_EXPORT_INTERVAL` | Metrics | Periodic reader interval (ms) |

### Precedence Order (Lopen-Specific)

The Lopen configuration module defines this precedence:

1. **Standard OTEL environment variables** (`OTEL_EXPORTER_OTLP_ENDPOINT`, etc.) — highest priority
2. **Lopen config settings** (`otel.export.endpoint`, `otel.export.protocol`, etc.) — in `.lopen/config.json` or global config
3. **Built-in defaults** — `otel.enabled = true`, endpoint = `null` (no export), protocol = `grpc`

This follows the OpenTelemetry specification convention: environment variables always win.

### How This Works in .NET

The OTEL SDK reads environment variables through `IConfiguration`. Since the Lopen configuration module layers environment variables above JSON config files, the precedence is automatic:

```csharp
var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    ?? builder.Configuration["otel:export:endpoint"];
```

### Relevance to Lopen

The Aspire AppHost sets `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, and `OTEL_RESOURCE_ATTRIBUTES` automatically during `aspire run`. For standalone use, operators set these variables manually. Lopen's own config settings (`otel.export.endpoint`, etc.) provide a fallback for environments where setting environment variables is inconvenient.

---

## 8. Performance Considerations

### Target: < 5ms Overhead on Command Startup

The OTEL specification requires that instrumentation does not measurably degrade CLI responsiveness.

### Why .NET OTEL Is Fast

1. **Runtime-native APIs** — `Activity` and `Meter` are built into the .NET runtime. No reflection, no dynamic proxies.

2. **Null propagation** — `ActivitySource.StartActivity()` returns `null` when no listener is registered. The `activity?.SetTag()` pattern short-circuits at zero cost.

3. **Lock-free metrics** — `Counter.Add()` and `Histogram.Record()` use lock-free atomic operations internally. No contention under concurrent access.

4. **Background export** — All export happens on background threads via batching processors. The hot path (command execution) is never blocked by serialization or network I/O.

5. **Lazy initialization** — The OTEL SDK initializes export pipelines lazily. If no telemetry is generated, no export infrastructure spins up.

### Overhead Breakdown

| Operation | Estimated Overhead |
|---|---|
| `StartActivity()` (with listener) | ~1–2μs |
| `StartActivity()` (no listener) | ~50ns (returns null) |
| `SetTag()` per attribute | ~100ns |
| `Counter.Add()` | ~50–100ns |
| `Histogram.Record()` | ~100–200ns |
| OTEL SDK initialization (DI registration) | ~1–3ms |
| `UseOtlpExporter()` first export | Background thread, non-blocking |

### Mitigation Strategies for CLI

1. **Conditional registration** — Only call `UseOtlpExporter()` when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. This avoids gRPC channel initialization overhead when no collector is available.

2. **Master switch** — `otel.enabled = false` skips all OTEL registration, reducing startup time to zero OTEL overhead.

3. **Source generator logging** — Use `[LoggerMessage]` attributes to eliminate boxing and string interpolation at log call sites.

4. **Batch export tuning** — Increase `OTEL_BSP_SCHEDULE_DELAY` and `OTEL_BLRP_SCHEDULE_DELAY` for CLI workloads where real-time export is not critical. The default 5000ms is already suitable.

### Relevance to Lopen

The < 5ms startup overhead target is achievable because OTEL SDK initialization is ~1–3ms, and the `UseOtlpExporter()` gRPC channel setup happens asynchronously on a background thread. With `otel.enabled = false`, there is zero OTEL overhead. Instrumentation code in hot paths uses the null-propagation pattern, adding negligible cost even under active tracing.

---

## 9. Recommended NuGet Packages

### Required (Lopen.Otel Project)

| Package | Purpose |
|---|---|
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP export for all signals |
| `OpenTelemetry.Extensions.Hosting` | `AddOpenTelemetry()` and `UseOtlpExporter()` |
| `OpenTelemetry.Instrumentation.Http` | Auto-instrument `HttpClient` (Copilot SDK HTTP calls) |
| `OpenTelemetry.Instrumentation.Runtime` | .NET runtime metrics (GC, thread pool, assemblies) |

### Not Needed

| Package | Reason |
|---|---|
| `OpenTelemetry.Instrumentation.AspNetCore` | Lopen is a CLI, not a web app |
| `OpenTelemetry.Exporter.Console` | Development only; use Aspire Dashboard instead |
| `OpenTelemetry.Exporter.Prometheus.*` | Pull-based; Lopen uses push-based OTLP export |

### Instrumentation Code Only (Module Libraries)

Module libraries (`Lopen.Core`, `Lopen.Llm`, etc.) that emit spans and metrics need only:

```xml
<!-- No OTEL SDK dependency needed — these are part of the .NET runtime -->
<PackageReference Include="System.Diagnostics.DiagnosticSource" />
```

In practice, `System.Diagnostics.DiagnosticSource` is already implicitly referenced by .NET 10.0 projects, so no explicit package reference is required.

---

## 10. Full Registration Pattern for Lopen

### Composition Root (Lopen.Otel Service Extension)

```csharp
public static class OtelServiceExtensions
{
    public static IHostApplicationBuilder AddLopenTelemetry(
        this IHostApplicationBuilder builder)
    {
        if (!builder.Configuration.GetValue<bool>("otel:enabled", defaultValue: true))
            return builder;

        var otel = builder.Services.AddOpenTelemetry();

        // Service name
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "lopen";
        otel.ConfigureResource(r => r.AddService(serviceName));

        // Traces
        if (builder.Configuration.GetValue<bool>("otel:traces:enabled", defaultValue: true))
        {
            otel.WithTracing(tracing => tracing
                .AddSource("Lopen.Workflow")
                .AddSource("Lopen.Sdk")
                .AddSource("Lopen.Tools")
                .AddSource("Lopen.Oracle")
                .AddSource("Lopen.Session")
                .AddSource("Lopen.Git")
                .AddHttpClientInstrumentation());
        }

        // Metrics
        if (builder.Configuration.GetValue<bool>("otel:metrics:enabled", defaultValue: true))
        {
            otel.WithMetrics(metrics => metrics
                .AddMeter("Lopen.Metrics")
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation());
        }

        // Logs
        if (builder.Configuration.GetValue<bool>("otel:logs:enabled", defaultValue: true))
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });
        }

        // OTLP export (conditional on endpoint)
        var useOtlp = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? builder.Configuration["otel:export:endpoint"]);

        if (useOtlp)
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }
}
```

### Usage in Program.cs

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.AddLopenTelemetry();
```

---

## 11. Aspire Dashboard Integration

For Aspire Dashboard specifics — installation, standalone Docker usage, AppHost configuration, dashboard endpoints, MCP tools, and telemetry limits — see **[RESEARCH-aspire.md](./RESEARCH-aspire.md)**.

Key points summarized here:

- **`aspire run`** launches the Dashboard and Lopen together with OTEL pre-configured
- The Dashboard receives telemetry on port **18889** (gRPC) / **18890** (HTTP)
- The Dashboard UI is at **`http://localhost:18888`**
- Environment variables are set automatically by the AppHost — no manual configuration needed for local dev
- The Dashboard is a **development tool** — for production, route OTLP to a durable collector (Jaeger, Grafana, Azure Monitor, etc.)

---

## Relevance to Lopen

### Key Findings

1. **Standard .NET APIs for instrumentation.** Lopen module libraries use `System.Diagnostics.Activity` and `System.Diagnostics.Metrics` directly — no OTEL SDK dependency in library code. Only the composition root (`Lopen.Otel`) references the SDK packages.

2. **Single-call export setup.** `UseOtlpExporter()` registers OTLP export for all three signals. Combined with conditional activation on `OTEL_EXPORTER_OTLP_ENDPOINT`, this delivers the specification's "always-on capability" with "zero-config local dev" principles.

3. **Environment variables take precedence.** The OTEL SDK reads `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, etc. through `IConfiguration`, which naturally respects the Lopen configuration hierarchy. The Aspire AppHost sets these automatically.

4. **Performance target is achievable.** SDK initialization is ~1–3ms, instrumentation calls are sub-microsecond, and all export is background-threaded. The < 5ms startup overhead target is well within reach.

5. **`Gauge<T>` requires .NET 9+.** Since Lopen targets .NET 10.0, all metric instrument types (`Counter<T>`, `Histogram<T>`, `Gauge<T>`) are available.

6. **ILogger correlation is automatic.** The OTEL log provider attaches `TraceId` and `SpanId` to every log record emitted within an active span. No manual correlation code is needed.

7. **Four NuGet packages.** The complete OTEL stack for Lopen requires only `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.Http`, and `OpenTelemetry.Instrumentation.Runtime`.

### Open Questions

- **Flush on CLI exit**: CLI processes are short-lived. The OTLP exporter batches telemetry on background threads. Lopen must call `TracerProvider.ForceFlush()` / `MeterProvider.ForceFlush()` (or dispose the host) before process exit to avoid losing the final batch. Verify that `IHost.StopAsync()` handles this automatically.
- **gRPC vs HTTP/Protobuf for CLI**: gRPC requires HTTP/2, which adds connection setup time. For short-lived CLI invocations, `http/protobuf` may have lower latency for the first export. Benchmark both protocols.

---

## References

- [OTEL Specification](./SPECIFICATION.md) — Lopen's OTEL specification (traces, metrics, logs, configuration)
- [RESEARCH-aspire.md](./RESEARCH-aspire.md) — Aspire Dashboard and AppHost integration research
- [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet) — Source repository
- [OTLP Exporter README](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol) — Configuration, environment variables, `UseOtlpExporter()`
- [.NET Distributed Tracing](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing) — `ActivitySource`, `Activity` API reference
- [.NET Metrics](https://learn.microsoft.com/dotnet/core/diagnostics/metrics) — `Meter`, `Counter`, `Histogram`, `Gauge` API reference
