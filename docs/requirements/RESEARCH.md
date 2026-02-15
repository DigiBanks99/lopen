# Research

Research documents for Lopen modules.

## Auth Module

- [RESEARCH.md](auth/RESEARCH.md) — GitHub Copilot SDK authentication, device flow, environment variable auth, token renewal

## CLI Module

- [RESEARCH.md](cli/RESEARCH.md) — System.CommandLine, project structure, subcommands, global flags, exit codes, headless/TUI architecture

## Configuration Module

- [RESEARCH.md](configuration/RESEARCH.md) — Layered .NET configuration for CLI tools (M.E.Configuration, JSON files, binding, validation)

## Core Module

- [RESEARCH.md](core/RESEARCH.md) — Core orchestration engine implementation patterns (state machine, task hierarchy, back-pressure, git, oracle, markdown parsing)
- [RESEARCH-state-machine.md](core/RESEARCH-state-machine.md) — Re-entrant state machine patterns (Stateless, enum/switch, workflow-as-data)
- [RESEARCH-hierarchical-task-data-structures.md](core/RESEARCH-hierarchical-task-data-structures.md) — Module → Component → Task → Subtask hierarchy (composite, state tracking, JSON serialization)
- [RESEARCH-backpressure.md](core/RESEARCH-backpressure.md) — Guardrail pipeline, budget tracker, churn detection, tool discipline
- [RESEARCH-markdown-parsing.md](core/RESEARCH-markdown-parsing.md) — Markdig section extraction, XxHash128 drift detection
- [RESEARCH-git-integration.md](core/RESEARCH-git-integration.md) — LibGit2Sharp vs Process-based git CLI

## LLM Module

- [RESEARCH.md](llm/RESEARCH.md) — GitHub Copilot SDK for .NET and LLM integration patterns
- [RESEARCH-oracle-verification.md](llm/RESEARCH-oracle-verification.md) — Oracle verification in Copilot SDK tool-calling loop

## OTEL Module

- [RESEARCH.md](otel/RESEARCH.md) — OpenTelemetry .NET SDK setup, custom spans/metrics, OTLP exporter, ILogger integration, configuration, performance
- [RESEARCH-aspire.md](otel/RESEARCH-aspire.md) — .NET Aspire dashboard and OpenTelemetry integration research

## Storage Module

- [RESEARCH.md](storage/RESEARCH.md) — .NET storage implementation patterns (file I/O, JSON serialization, caching, markdown parsing)

## TUI Module

- [RESEARCH.md](tui/RESEARCH.md) — Spectre.Console, Spectre.Tui, split-screen layout, component architecture, syntax highlighting
