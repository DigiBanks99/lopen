---
name: otel
description: OpenTelemetry observability throughout Lopen using the Aspire Dashboard for local development
---

# OpenTelemetry Specification

## Overview

Lopen emits structured telemetry — traces, metrics, and logs — using the OpenTelemetry (OTEL) standard. This enables developers and operators to observe Lopen's internal behavior: which commands are invoked, which tools are called, how the LLM orchestration loop performs, and where time is spent.

For local development, Lopen includes an **Aspire AppHost** that launches the Aspire Dashboard and Lopen together via `aspire run`. For production or CI, the same OTLP export can be routed to any compliant collector (Jaeger, Prometheus, Azure Monitor, etc.) with zero code changes.

### Design Principles

1. **Standards-Based** — Uses the OpenTelemetry .NET SDK and OTLP protocol exclusively; no proprietary telemetry APIs
2. **Zero-Config Local Dev** — `aspire run` starts the Dashboard and Lopen with OTEL pre-configured
3. **Always-On Capability** — Telemetry instrumentation is always present in code; export is activated only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set
4. **Low Overhead** — Instrumentation must not measurably degrade CLI responsiveness or token throughput
5. **Actionable Signals** — Every trace, metric, and log must answer a question a developer or operator would actually ask

> Token and cost tracking are first-class metrics defined in the [LLM module](../llm/SPECIFICATION.md#token--cost-tracking). This module defines the **telemetry infrastructure** that transports those metrics (and all other observability signals) to external systems. Configuration of OTEL settings is handled by the [Configuration module](../configuration/SPECIFICATION.md).

---

## Traces

Traces capture the causal flow through Lopen's execution. Each trace represents a logical unit of work, with spans forming a parent-child hierarchy.

### Trace Hierarchy

```
lopen.command (root span)
├─ lopen.workflow.phase
│  ├─ lopen.sdk.invocation
│  │  ├─ lopen.tool.<tool_name>        (Lopen-managed tool execution)
│  │  ├─ lopen.oracle.verification     (oracle sub-agent dispatch)
│  │  └─ copilot.sdk.request           (HTTP call to Copilot API)
│  ├─ lopen.sdk.invocation
│  │  └─ ...
│  └─ lopen.task.execution
│     ├─ lopen.sdk.invocation
│     └─ lopen.git.commit
├─ lopen.workflow.phase
│  └─ ...
└─ lopen.session.save
```

### Required Spans

#### `lopen.command`

Root span for every CLI invocation. Created when a command begins, ended when the process exits.

| Attribute                  | Type    | Description                                                                |
| -------------------------- | ------- | -------------------------------------------------------------------------- |
| `lopen.command.name`       | string  | Command name (e.g., `spec`, `plan`, `build`, `auth login`, `session list`) |
| `lopen.command.headless`   | boolean | Whether `--headless` mode is active                                        |
| `lopen.command.has_prompt` | boolean | Whether `--prompt` was provided                                            |
| `lopen.command.exit_code`  | int     | Process exit code (set on span end)                                        |

#### `lopen.workflow.phase`

Span per workflow phase execution.

| Attribute                  | Type   | Description                                                  |
| -------------------------- | ------ | ------------------------------------------------------------ |
| `lopen.phase.name`         | string | Phase name (`requirement_gathering`, `planning`, `building`) |
| `lopen.phase.module`       | string | Target module name                                           |
| `lopen.phase.step_entered` | int    | Workflow step number entered (1–7)                           |
| `lopen.phase.step_exited`  | int    | Workflow step number exited                                  |

#### `lopen.sdk.invocation`

Span per Copilot SDK call. This is the core unit of LLM interaction.

| Attribute                 | Type    | Description                                  |
| ------------------------- | ------- | -------------------------------------------- |
| `lopen.sdk.model`         | string  | Model used (e.g., `claude-opus-4.6`)         |
| `lopen.sdk.tokens.input`  | int     | Input tokens consumed                        |
| `lopen.sdk.tokens.output` | int     | Output tokens generated                      |
| `lopen.sdk.tokens.total`  | int     | Total tokens consumed                        |
| `lopen.sdk.is_premium`    | boolean | Whether this was a premium request           |
| `lopen.sdk.tool_calls`    | int     | Number of tool calls made in this invocation |

#### `lopen.tool.<tool_name>`

Span per Lopen-managed tool invocation (e.g., `lopen.tool.read_spec`, `lopen.tool.update_task_status`, `lopen.tool.verify_task_completion`).

| Attribute            | Type    | Description                                         |
| -------------------- | ------- | --------------------------------------------------- |
| `lopen.tool.name`    | string  | Tool name (e.g., `read_spec`, `update_task_status`) |
| `lopen.tool.module`  | string  | Module context when the tool was called             |
| `lopen.tool.success` | boolean | Whether the tool call succeeded                     |
| `lopen.tool.error`   | string  | Error message if the tool call failed (optional)    |

#### `lopen.oracle.verification`

Span per oracle sub-agent dispatch.

| Attribute              | Type   | Description                                        |
| ---------------------- | ------ | -------------------------------------------------- |
| `lopen.oracle.scope`   | string | Verification scope (`task`, `component`, `module`) |
| `lopen.oracle.model`   | string | Oracle model used                                  |
| `lopen.oracle.verdict` | string | `pass` or `fail`                                   |
| `lopen.oracle.attempt` | int    | Attempt number within the retry loop               |

#### `lopen.task.execution`

Span per task in the building phase.

| Attribute               | Type   | Description                             |
| ----------------------- | ------ | --------------------------------------- |
| `lopen.task.name`       | string | Task description                        |
| `lopen.task.component`  | string | Parent component name                   |
| `lopen.task.module`     | string | Parent module name                      |
| `lopen.task.outcome`    | string | `complete`, `failed`, or `skipped`      |
| `lopen.task.iterations` | int    | Number of SDK invocations for this task |

#### `lopen.session.save`

Span per session state persistence operation.

| Attribute                 | Type   | Description                |
| ------------------------- | ------ | -------------------------- |
| `lopen.session.id`        | string | Session identifier         |
| `lopen.session.operation` | string | `save`, `load`, or `prune` |

#### `lopen.git.commit`

Span per git commit operation (auto-commit or manual).

| Attribute             | Type   | Description                     |
| --------------------- | ------ | ------------------------------- |
| `lopen.git.operation` | string | `commit`, `revert`, or `branch` |
| `lopen.git.branch`    | string | Branch name                     |

#### `lopen.backpressure.event`

Span when a back-pressure guardrail fires.

| Attribute                     | Type   | Description                                                                     |
| ----------------------------- | ------ | ------------------------------------------------------------------------------- |
| `lopen.backpressure.category` | string | `resource_limits`, `progress_integrity`, `quality_gates`, `tool_discipline`     |
| `lopen.backpressure.trigger`  | string | Specific trigger (e.g., `churn_detected`, `budget_warning`, `false_completion`) |
| `lopen.backpressure.action`   | string | Action taken (e.g., `warn`, `pause`, `reject`, `inject_correction`)             |

---

## Metrics

Metrics capture aggregate, low-cardinality measurements. All metrics use the `lopen.` namespace prefix.

### Counters

| Metric                             | Unit            | Description                                                                 |
| ---------------------------------- | --------------- | --------------------------------------------------------------------------- |
| `lopen.commands.count`             | `{invocations}` | Total CLI command invocations, tagged by `command.name`                     |
| `lopen.tools.count`                | `{calls}`       | Total Lopen-managed tool calls, tagged by `tool.name`                       |
| `lopen.sdk.invocations.count`      | `{invocations}` | Total SDK invocations, tagged by `model`                                    |
| `lopen.sdk.tokens.consumed`        | `{tokens}`      | Total tokens consumed, tagged by `model` and `direction` (`input`/`output`) |
| `lopen.sdk.premium_requests.count` | `{requests}`    | Total premium API requests consumed                                         |
| `lopen.oracle.verdicts.count`      | `{verdicts}`    | Oracle verdicts, tagged by `scope` and `verdict` (`pass`/`fail`)            |
| `lopen.tasks.completed.count`      | `{tasks}`       | Tasks completed, tagged by `module`                                         |
| `lopen.tasks.failed.count`         | `{tasks}`       | Tasks failed, tagged by `module`                                            |
| `lopen.backpressure.events.count`  | `{events}`      | Back-pressure events, tagged by `category`                                  |
| `lopen.git.commits.count`          | `{commits}`     | Git commits made, tagged by `operation` (`auto`/`manual`/`revert`)          |

### Histograms

| Metric                          | Unit | Description                                                     |
| ------------------------------- | ---- | --------------------------------------------------------------- |
| `lopen.sdk.invocation.duration` | `ms` | Duration of each SDK invocation, tagged by `model`              |
| `lopen.tool.duration`           | `ms` | Duration of each Lopen-managed tool call, tagged by `tool.name` |
| `lopen.task.duration`           | `ms` | Duration of each task execution, tagged by `module`             |
| `lopen.command.duration`        | `ms` | Duration of each CLI command, tagged by `command.name`          |
| `lopen.oracle.duration`         | `ms` | Duration of each oracle verification, tagged by `scope`         |

### Gauges

| Metric                                 | Unit          | Description                                                                       |
| -------------------------------------- | ------------- | --------------------------------------------------------------------------------- |
| `lopen.session.iteration`              | `{iteration}` | Current iteration number within the active session                                |
| `lopen.sdk.context_window.utilization` | `ratio`       | Fraction of context window used (`tokens_used / tokens_available`) per invocation |

---

## Logs

Lopen uses `ILogger` with the OpenTelemetry log provider. When OTLP export is active, structured logs are exported alongside traces and metrics. Logs are correlated with the active trace context automatically.

### Log Correlation

- All log entries emitted within a span carry the span's `TraceId` and `SpanId`
- The Aspire Dashboard displays logs correlated with their parent traces
- No manual correlation is needed — the OTEL log provider handles this

### Log Categories

Lopen log categories map to functional areas:

| Category             | Content                                                 |
| -------------------- | ------------------------------------------------------- |
| `Lopen.Workflow`     | Phase transitions, step changes, module selection       |
| `Lopen.Sdk`          | SDK invocation start/end, model selection, token counts |
| `Lopen.Tools`        | Tool call requests and results                          |
| `Lopen.Oracle`       | Verification dispatch, verdicts, gap findings           |
| `Lopen.BackPressure` | Guardrail triggers, corrective actions                  |
| `Lopen.Session`      | Session lifecycle (create, save, load, resume, prune)   |
| `Lopen.Git`          | Commit, revert, branch operations                       |
| `Lopen.Config`       | Configuration resolution, source precedence             |

---

## Local Development with Aspire Dashboard

### Aspire AppHost (Primary Approach)

Lopen includes a lightweight Aspire AppHost project that orchestrates the local development experience. The Aspire CLI launches Lopen, the Aspire Dashboard, and any supporting resources with a single command.

#### Prerequisites

Install the Aspire CLI (the old Aspire workload is obsolete and must not be used):

```sh
# Linux / macOS
curl -sSL https://aspire.dev/install.sh | bash

# Windows
irm https://aspire.dev/install.ps1 | iex
```

#### Running

```sh
aspire run
```

This starts:

- The **Aspire Dashboard** (UI at `http://localhost:18888`)
- **Lopen** with OTEL environment variables pre-configured (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, etc.)

The AppHost automatically configures Lopen to export telemetry to the Dashboard — no manual environment variable setup required.

#### AppHost Project Structure

```sh
src/Lopen.AppHost/
├── Lopen.AppHost.csproj
└── Program.cs
```

The AppHost references Lopen as a project resource:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.Lopen>("lopen");
builder.Build().Run();
```

### OTEL Export Behavior

When `OTEL_EXPORTER_OTLP_ENDPOINT` is set (either automatically by the AppHost or manually), Lopen activates OTLP export. When unset, instrumentation is still active (for in-process metrics like token tracking) but no telemetry leaves the process.

### Dashboard Capabilities

Once connected, the Aspire Dashboard provides:

- **Traces** — Full span waterfall view of command execution, SDK invocations, tool calls, and oracle verifications
- **Metrics** — Real-time counters and histograms for commands, tools, tokens, and tasks
- **Structured Logs** — Filterable log viewer correlated with traces
- **MCP Server** — Built-in programmatic access to logs, traces, and resources

---

## Configuration

OTEL configuration follows the [Configuration module](../configuration/SPECIFICATION.md) hierarchy. All OTEL-specific settings are optional — the module works with zero configuration when standard OTEL environment variables are set.

### Settings

| Setting                | Type    | Default   | Description                                                                                    |
| ---------------------- | ------- | --------- | ---------------------------------------------------------------------------------------------- |
| `otel.enabled`         | boolean | `true`    | Master switch for OTEL instrumentation (disabling removes all overhead)                        |
| `otel.export.endpoint` | string  | `null`    | OTLP endpoint; overridden by `OTEL_EXPORTER_OTLP_ENDPOINT` env var                             |
| `otel.export.protocol` | string  | `"grpc"`  | OTLP protocol (`grpc` or `http/protobuf`); overridden by `OTEL_EXPORTER_OTLP_PROTOCOL` env var |
| `otel.service_name`    | string  | `"lopen"` | Service name for telemetry; overridden by `OTEL_SERVICE_NAME` env var                          |
| `otel.traces.enabled`  | boolean | `true`    | Enable trace export (when `otel.enabled` is true and endpoint is set)                          |
| `otel.metrics.enabled` | boolean | `true`    | Enable metrics export                                                                          |
| `otel.logs.enabled`    | boolean | `true`    | Enable log export                                                                              |

### Environment Variable Precedence

Standard OTEL environment variables (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, etc.) take precedence over Lopen configuration settings. This follows the OpenTelemetry specification convention and ensures compatibility with any OTEL-aware deployment environment.

---

## Acceptance Criteria

- [ ] `lopen.command` root span is created for every CLI invocation with correct `command.name`, `headless`, and `exit_code` attributes
- [ ] `lopen.workflow.phase` spans are created for each phase with correct `phase.name` and `module` attributes
- [ ] `lopen.sdk.invocation` spans are created for each Copilot SDK call with `model`, `tokens.input`, `tokens.output`, and `is_premium` attributes
- [ ] `lopen.tool.<tool_name>` spans are created for every Lopen-managed tool call with `tool.name` and `success` attributes
- [ ] `lopen.oracle.verification` spans are created for every oracle dispatch with `scope`, `verdict`, and `attempt` attributes
- [ ] `lopen.task.execution` spans are created for each task with `outcome` and `iterations` attributes
- [ ] `lopen.backpressure.event` spans are created when any back-pressure guardrail fires
- [ ] All counter metrics increment correctly: `commands.count`, `tools.count`, `sdk.invocations.count`, `sdk.tokens.consumed`, `premium_requests.count`, `oracle.verdicts.count`, `tasks.completed.count`, `tasks.failed.count`, `backpressure.events.count`, `git.commits.count`
- [ ] All histogram metrics record correct durations: `sdk.invocation.duration`, `tool.duration`, `task.duration`, `command.duration`, `oracle.duration`
- [ ] Structured logs emitted via `ILogger` carry `TraceId` and `SpanId` for correlation with active spans
- [ ] When `OTEL_EXPORTER_OTLP_ENDPOINT` is set, all three signals (traces, metrics, logs) are exported via OTLP
- [ ] When `OTEL_EXPORTER_OTLP_ENDPOINT` is not set, no telemetry leaves the process and no export errors occur
- [ ] `aspire run` starts the Aspire Dashboard and Lopen with OTEL telemetry pre-configured and visible in the Dashboard
- [ ] Setting `otel.enabled` to `false` disables all instrumentation with no measurable performance overhead
- [ ] Individual signal toggles (`otel.traces.enabled`, `otel.metrics.enabled`, `otel.logs.enabled`) independently control their respective exports
- [ ] OTEL environment variables (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`, `OTEL_EXPORTER_OTLP_PROTOCOL`) take precedence over Lopen config settings
- [ ] CLI command execution time is not measurably degraded by OTEL instrumentation (< 5ms overhead on command startup)

---

## Dependencies

- **[LLM module](../llm/SPECIFICATION.md)** — SDK invocation metadata (model, token counts, premium status) for trace attributes and metrics
- **[Core module](../core/SPECIFICATION.md)** — Workflow phase, task, and back-pressure events that generate spans and metrics
- **[Configuration module](../configuration/SPECIFICATION.md)** — OTEL settings resolution within the configuration hierarchy
- **[CLI module](../cli/SPECIFICATION.md)** — Command invocation context for the root span
- **[Storage module](../storage/SPECIFICATION.md)** — Session lifecycle events for tracing
- **OpenTelemetry .NET SDK** — `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`
- **System.Diagnostics.Activity** — .NET distributed tracing API (trace spans)
- **System.Diagnostics.Metrics** — .NET metrics API (counters, histograms, gauges)
- **Aspire CLI** — `aspire run` for local development orchestration (primary approach)

---

## Skills & Hooks

- **verify-otel-spans**: Validate that a test CLI invocation produces the expected root span and child span hierarchy
- **verify-otel-metrics**: Validate that counters and histograms are registered and increment on tool/command invocations
- **verify-otel-export**: Validate that OTLP export activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set and is silent when unset

---

## Notes

This specification defines **what telemetry Lopen emits and how it is exported**. It does not define how telemetry is displayed in the TUI — that remains the concern of the [TUI module](../tui/SPECIFICATION.md). The existing token and cost tracking in the [LLM module](../llm/SPECIFICATION.md) continues to own the *business logic* of budget enforcement; this module provides the *transport* for those metrics to external observability systems.

The Aspire Dashboard is a **development tool**, not a production monitoring solution. For production deployments, the same OTLP export should be routed to a durable collector. This specification intentionally avoids prescribing a production backend.

Span names and metric names follow OpenTelemetry semantic conventions where applicable. Custom attributes use the `lopen.` namespace to avoid collisions.

---

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow phases, task management, back-pressure
- [LLM Specification](../llm/SPECIFICATION.md) — SDK invocation, token tracking, oracle verification
- [Configuration Specification](../configuration/SPECIFICATION.md) — Settings hierarchy
- [CLI Specification](../cli/SPECIFICATION.md) — Command structure and exit codes
- [Storage Specification](../storage/SPECIFICATION.md) — Session persistence
- [Aspire Research](./RESEARCH-aspire.md) — .NET Aspire dashboard and OTEL integration research
- [OpenTelemetry .NET SDK](https://github.com/open-telemetry/opentelemetry-dotnet) — Tracing, metrics, and logging APIs
