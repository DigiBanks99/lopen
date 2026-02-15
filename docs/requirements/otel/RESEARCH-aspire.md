# Research: .NET Aspire — OpenTelemetry & Local Dev Experience

> **Date:** 2026-02-15
> **Sources:** [github.com/dotnet/aspire](https://github.com/dotnet/aspire) (code repo), [github.com/microsoft/aspire.dev](https://github.com/microsoft/aspire.dev) (docs repo), aspire.dev

---

## 1. What is .NET Aspire?

Aspire provides tools, templates, and packages for building observable, production-ready distributed applications. At its center is the **app model** — a code-first, single source of truth that defines your app's services, resources, and connections.

Aspire gives a unified toolchain: launch and debug your entire app locally with one command (`aspire run`), then deploy anywhere — Kubernetes, the cloud, or your own servers — using the same composition.

### Key Components (from dotnet/aspire AGENTS.md)

- **Aspire.Hosting** — Application host orchestration and resource management
- **Aspire.Dashboard** — Web-based dashboard for monitoring and debugging (Blazor app)
- **Service Discovery** — Infrastructure for service-to-service communication
- **Integrations** — 40+ packages for databases, message queues, cloud services, and more
- **CLI Tools** — Command-line interface for project creation and management
- **Project Templates** — Starter templates for new Aspire applications

### Technology Stack

- .NET 10.0, C# 13 preview features
- Native AOT compilation for CLI tools
- Multi-platform (Windows, Linux, macOS, containers)

### Critical: The Aspire Workload is Obsolete

> **IMPORTANT!** The aspire workload is obsolete. You should never attempt to install or use the Aspire workload. Use the **Aspire CLI** instead.

---

## 2. The Aspire CLI

Aspire is now driven by a standalone CLI tool, not the old dotnet workload.

### Installation

```sh
# Linux / macOS
curl -sSL https://aspire.dev/install.sh | bash

# Windows
irm https://aspire.dev/install.ps1 | iex
```

### Key Commands

| Command         | Purpose                                              |
| --------------- | ---------------------------------------------------- |
| `aspire run`    | Launch and debug your entire app locally             |
| `aspire update` | Update the apphost and some Aspire-specific packages |

`aspire run` starts the AppHost, the Dashboard, and all resources defined in the app model.

---

## 3. How Aspire Integrates with OpenTelemetry (OTEL)

Aspire uses the [.NET OpenTelemetry SDK](https://github.com/open-telemetry/opentelemetry-dotnet) directly. The integration happens via the **Service Defaults** project pattern.

### The `ConfigureOpenTelemetry` method (from current template source)

Source: `src/Aspire.ProjectTemplates/templates/aspire-servicedefaults/Extensions.cs`

```csharp
public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();
        })
        .WithTracing(tracing =>
        {
            tracing.AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(tracing =>
                    tracing.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health")
                        && !context.Request.Path.StartsWithSegments("/alive")
                )
                .AddHttpClientInstrumentation();
        });

    builder.AddOpenTelemetryExporters();
    return builder;
}
```

### OTLP Export (conditional on environment variable)

```csharp
private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }
    return builder;
}
```

### Standard OTEL Environment Variables

| Variable                      | Purpose                                                |
| ----------------------------- | ------------------------------------------------------ |
| `OTEL_SERVICE_NAME`           | Service name for exported telemetry                    |
| `OTEL_RESOURCE_ATTRIBUTES`    | e.g. `service.instance.id=<guid>`                      |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector endpoint (e.g. `http://localhost:4317`) |
| `OTEL_BSP_SCHEDULE_DELAY`     | Trace batch export interval (ms)                       |
| `OTEL_BLRP_SCHEDULE_DELAY`    | Log export interval (ms)                               |
| `OTEL_METRIC_EXPORT_INTERVAL` | Metric export interval (ms)                            |

These are **automatically set** by the Aspire AppHost during local development.

---

## 4. The Aspire Dashboard

The Aspire Dashboard is a **browser-based, real-time OpenTelemetry viewer** built as a Blazor application. It ships as a Docker container image: `mcr.microsoft.com/dotnet/aspire-dashboard:latest`.

### What it provides

- **Resources page** — View all Aspire-managed resources (only when running with AppHost orchestration via resource service)
- **Console logs** — Live console log output from resources
- **Structured Logs page** — View structured log entries from all services
- **Traces page** — View distributed traces with full span waterfall views
- **Metrics page** — View metrics with numerical values and charts
- **MCP server** — Built-in MCP tools for programmatic access to logs, traces, and resources (in `src/Aspire.Dashboard/Mcp/`)

### Dashboard Default Endpoints

| Endpoint    | Default Port | Purpose                              |
| ----------- | ------------ | ------------------------------------ |
| Frontend UI | `18888`      | Dashboard web UI (browser)           |
| OTLP/gRPC   | `18889`      | Receives telemetry via gRPC          |
| OTLP/HTTP   | `18890`      | Receives telemetry via HTTP/Protobuf |

### Architecture

```sh
┌─────────────┐     OTLP/gRPC       ┌───────────────────┐
│  Your App   │ ──────────────────► │  Aspire Dashboard │
│  (with OTEL │     (port 18889)    │  (port 18888 UI)  │
│   SDK)      │                     │                   │
└─────────────┘                     │  - Resources      │
                                    │  - Console logs   │
┌─────────────┐     OTLP/gRPC       │  - Structured logs│
│  Another    │ ──────────────────► │  - Traces viewer  │
│  Service    │                     │  - Metrics viewer │
└─────────────┘                     │  - MCP server     │
                                    └───────────────────┘
```

### MCP Tools (from microsoft/aspire.dev AGENTS.md)

The Dashboard exposes MCP tools for AI-assisted debugging:

| Tool                         | Purpose                                          |
| ---------------------------- | ------------------------------------------------ |
| `list resources`             | Check status of resources in the app model       |
| `list structured logs`       | Get structured log details                       |
| `list console logs`          | Get console log details                          |
| `list traces`                | Get trace details                                |
| `list trace structured logs` | Get logs related to a specific trace             |
| `execute resource command`   | Restart or control resources                     |
| `list integrations`          | List available Aspire integrations with versions |
| `get integration docs`       | Fetch docs for a specific integration            |

### Telemetry Limits (in-memory storage)

Limits are **per-resource** (not shared across resources):

| Setting                                        | Default   | Description                             |
| ---------------------------------------------- | --------- | --------------------------------------- |
| `Dashboard:TelemetryLimits:MaxLogCount`        | 10,000    | Maximum log entries per resource        |
| `Dashboard:TelemetryLimits:MaxTraceCount`      | 10,000    | Maximum traces per resource             |
| `Dashboard:TelemetryLimits:MaxMetricsCount`    | 50,000    | Maximum metric data points per resource |
| `Dashboard:TelemetryLimits:MaxAttributeCount`  | 128       | Maximum attributes per telemetry item   |
| `Dashboard:TelemetryLimits:MaxAttributeLength` | unlimited | Maximum attribute value length          |
| `Dashboard:TelemetryLimits:MaxSpanEventCount`  | unlimited | Maximum events per span                 |

When a count limit is reached, new telemetry is added and the oldest is removed.

---

## 5. NuGet Packages for OTEL Integration

### Core packages (from current ServiceDefaults template)

The following packages are used in Aspire's ServiceDefaults template. Versions are managed centrally; current versions in `Directory.Packages.props` use MSBuild variables but the latest stable line is **1.14.0**:

```xml
<ItemGroup>
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
</ItemGroup>
```

### Additional Aspire-specific packages (for full Aspire stack)

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" />
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

### For non-ASP.NET Core projects (CLI tools, Workers, etc.)

A ServiceDefaults project can be created **without** `Microsoft.AspNetCore.App`:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
</ItemGroup>
```

### Key takeaway for Lopen

The OTEL packages are **standard OpenTelemetry .NET SDK packages** — not Aspire-specific. Aspire provides opinionated configuration on top of them. Any .NET app using these packages can send telemetry to the Aspire dashboard. Since Lopen is a CLI tool (not an ASP.NET Core app), it should use the non-ASP.NET Core package set and skip `AddAspNetCoreInstrumentation()`.

---

## 6. Standalone Dashboard (Without Full Aspire Orchestration)

**Yes, the Aspire dashboard can be used completely standalone.** This is a key capability for Lopen.

### Starting standalone via Docker

```bash
docker run --rm -d \
    -p 18888:18888 \
    -p 4317:18889 \
    -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    --name aspire-dashboard \
    mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

| Port Mapping  | Purpose                                                 |
| ------------- | ------------------------------------------------------- |
| `18888:18888` | Dashboard UI (open `http://localhost:18888` in browser) |
| `4317:18889`  | OTLP/gRPC receiver (apps send telemetry here)           |

### Connecting any OTEL-enabled app

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
export OTEL_SERVICE_NAME=lopen
```

### Dashboard Configuration (environment variables)

| Variable                                     | Purpose                                                         |
| -------------------------------------------- | --------------------------------------------------------------- |
| `ASPNETCORE_URLS`                            | Dashboard UI listen address (default: `http://localhost:18888`) |
| `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`         | OTLP/gRPC listen address (default: `http://localhost:18889`)    |
| `DOTNET_DASHBOARD_OTLP_HTTP_ENDPOINT_URL`    | OTLP/HTTP listen address (default: `http://localhost:18890`)    |
| `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` | Disable auth for local dev                                      |
| `DOTNET_DASHBOARD_CONFIG_FILE_PATH`          | Path to optional JSON config file                               |

> **Note:** Environment variable names use the `DOTNET_DASHBOARD_` prefix (not `ASPIRE_DASHBOARD_`).

### Dashboard Configuration (JSON file)

```json
{
  "Dashboard": {
    "TelemetryLimits": {
      "MaxLogCount": 50000,
      "MaxTraceCount": 50000,
      "MaxMetricsCount": 50000
    }
  }
}
```

Mount via: `-e DOTNET_DASHBOARD_CONFIG_FILE_PATH=/config/dashboard.json -v ./config:/config`

### Authentication

| Mode           | Setting                                     | Description                               |
| -------------- | ------------------------------------------- | ----------------------------------------- |
| Browser Token  | `Dashboard:Frontend:AuthMode=BrowserToken`  | Default — token printed to container logs |
| OpenID Connect | `Dashboard:Frontend:AuthMode=OpenIdConnect` | OIDC-based authentication                 |
| Unsecured      | `Dashboard:Frontend:AuthMode=Unsecured`     | No auth (local dev only)                  |

OTLP endpoint auth modes: `Unsecured`, `ApiKey`, `Certificate`.

### Standalone limitations

- **No resource list or console logs** — The Resources page requires `Dashboard:ResourceServiceClient:Url` to be set (points to Aspire's resource service gRPC endpoint)
- **In-memory only** — Telemetry is lost on restart, capped by telemetry limits
- **Designed for development** — Not a production monitoring tool

---

## 7. How Aspire Handles Traces, Metrics, and Logs

### Logging

- Uses `ILogger` → `AddOpenTelemetry()` logging provider
- Includes formatted messages and scopes by default
- Exported via OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
- Displayed on the **Structured Logs** page of the dashboard

### Tracing

- Uses `System.Diagnostics.Activity` (the .NET distributed tracing API)
- `AddSource(builder.Environment.ApplicationName)` — registers the app's ActivitySource
- Auto-instruments: ASP.NET Core requests, HttpClient calls
- Optionally instruments: gRPC client calls (`OpenTelemetry.Instrumentation.GrpcNetClient`)
- Filters out health check endpoints (`/health`, `/alive`) from traces
- Displayed on the **Traces** page with full span waterfall views

### Metrics

- Uses `System.Diagnostics.Metrics` (`Meter`, `Counter<T>`, `Histogram<T>`, `Gauge<T>`)
- Auto-instruments: ASP.NET Core metrics, HttpClient metrics, .NET Runtime metrics
- Low performance overhead — suitable for always-on telemetry
- Displayed on the **Metrics** page with numerical values and charts

### Export flow

All three pillars export via **OTLP** (OpenTelemetry Protocol) using either gRPC or HTTP:

```sh
ILogger         ──► OpenTelemetry Log Provider   ──► OTLP Exporter ──► Dashboard/Collector
Activity        ──► OpenTelemetry Trace Provider  ──► OTLP Exporter ──► Dashboard/Collector
Meter/Counter   ──► OpenTelemetry Metric Provider ──► OTLP Exporter ──► Dashboard/Collector
```

---

## 8. Relevance to Lopen

### Key takeaways

1. **The Aspire Dashboard is a free, standalone OTEL viewer** — runs as a single Docker container. No .NET required on the receiving side; any OTLP-speaking app can send telemetry.
2. **Standard OTLP protocol** — Any language/framework that exports OTLP can send telemetry to the dashboard.
3. **The OTEL packages are standard** — `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, etc. These are standard .NET OpenTelemetry SDK packages, not Aspire-proprietary.
4. **Environment variable driven** — `OTEL_EXPORTER_OTLP_ENDPOINT` activates export; unset = no export. Zero-code configuration switching.
5. **CLI tool considerations** — Lopen is a CLI app, not an ASP.NET Core web app. It should skip ASP.NET Core instrumentation and use `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.Http` + `OpenTelemetry.Instrumentation.Runtime` only.
6. **MCP integration** — The dashboard has built-in MCP tools. This could be leveraged for Lopen's own debugging workflow in the future.
7. **Dashboard env vars use `DOTNET_DASHBOARD_` prefix** — not `ASPIRE_DASHBOARD_` (the shortcut `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS` exists for convenience).
8. **Aspire CLI (`aspire run`)** is the modern entry point — the dotnet workload is obsolete and must not be used.

### Recommended approach for Lopen

- Use the **standalone Aspire Dashboard** as a local OTEL development tool
- Adopt the **ServiceDefaults `ConfigureOpenTelemetry` pattern** adapted for CLI (no ASP.NET Core instrumentation)
- Use `UseOtlpExporter()` conditioned on `OTEL_EXPORTER_OTLP_ENDPOINT` so telemetry can be routed to either the dev dashboard or production collectors with zero code changes
- Register custom `ActivitySource` for Lopen's own spans and custom `Meter` for Lopen's metrics
