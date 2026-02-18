---
name: requirements-readme
description: Contains the index lookup of the requirements for Lopen. It has a brief summary of each requirement (module), where to find it, plus the latest known state of said requirement
---

# Lopen Requirements

This document describes the known modules (requirements) for Lopen.

| Requirement   | Location                          | Summary                                                          | State          |
| ------------- | --------------------------------- | ---------------------------------------------------------------- | -------------- |
| Core          | `docs/requirements/core`          | Workflow orchestration, task management, failure handling (25 reqs) | Complete     |
| LLM           | `docs/requirements/llm`           | Copilot SDK integration, model selection, tool strategy (14 reqs)  | Complete     |
| Storage       | `docs/requirements/storage`       | Session persistence, `.lopen/` structure, document formats (22 reqs) | Complete     |
| Configuration | `docs/requirements/configuration` | Settings hierarchy, CLI flags, model assignments, defaults (16 reqs) | Complete     |
| CLI           | `docs/requirements/cli`           | Command structure, global flags, headless mode, prompt injection (28 reqs) | Complete     |
| Auth          | `docs/requirements/auth`          | GitHub Copilot authentication and credential management (15 reqs)  | Complete     |
| TUI           | `docs/requirements/tui`           | Terminal UI layout, progressive disclosure, visual design (52 reqs) | Complete     |
| OTEL          | `docs/requirements/otel`          | OpenTelemetry observability, tracing, metrics, Aspire Dashboard (17 reqs) | Complete     |
