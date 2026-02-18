---
name: configuration
description: The configuration and settings requirements of Lopen
---

# Configuration Specification

## Overview

Lopen's behavior is controlled through a layered configuration system that supports project-level settings, user preferences, and CLI flags. This module defines the configuration hierarchy, available settings, defaults, and how configuration is discovered.

### Design Principles

1. **Convention over Configuration** — Sensible defaults for everything; configuration is optional
2. **Layered** — CLI flags override environment variables, which override project config, which overrides global defaults
3. **Discoverable** — Users can inspect active configuration and its sources
4. **Minimal** — Only settings that meaningfully affect behavior are exposed

---

## Configuration Hierarchy

Settings are resolved in this order (highest priority first):

1. **CLI flags** — Per-invocation overrides (e.g., `--model claude-opus-4.6`)
2. **Environment variables** — `LOPEN_`-prefixed variables (e.g., `LOPEN_Models__Planning=gpt-5`)
3. **Project configuration** — `.lopen/config.json` in the project root
4. **Global configuration** — `~/.config/lopen/config.json` (user-wide defaults)
5. **Built-in defaults** — Hardcoded sensible defaults in Lopen

When a setting is defined at multiple levels, the highest-priority source wins.

---

## Settings

### Model Assignments

Configure which Copilot SDK model is used for each workflow phase (see [LLM § Per-Step Configuration](../llm/SPECIFICATION.md#per-step-configuration)):

```json
{
  "models": {
    "requirement_gathering": "claude-opus-4.6",
    "planning": "claude-opus-4.6",
    "building": "claude-opus-4.6",
    "research": "claude-opus-4.6"
  }
}
```

**Default**: All phases use the premium tier model.

### Budget Settings

| Setting                          | Type    | Default | Description                                                              |
| -------------------------------- | ------- | ------- | ------------------------------------------------------------------------ |
| `budget.token_budget_per_module` | integer | `0`     | Maximum token budget per module (0 = unlimited)                          |
| `budget.premium_request_budget`  | integer | `0`     | Maximum premium API requests per module (0 = unlimited)                  |
| `budget.warning_threshold`       | number  | `0.8`   | Fraction of budget consumed before warning (0.0–1.0)                     |
| `budget.confirmation_threshold`  | number  | `0.9`   | Fraction of budget consumed before requiring user confirmation (0.0–1.0) |

### Oracle Settings

| Setting        | Type   | Default        | Description                                                    |
| -------------- | ------ | -------------- | -------------------------------------------------------------- |
| `oracle.model` | string | `"gpt-5-mini"` | Copilot SDK model used for oracle verification sub-agent calls |

### Workflow Settings

| Setting             | Type    | Default | Description                                                |
| ------------------- | ------- | ------- | ---------------------------------------------------------- |
| `unattended`        | boolean | `false` | Suppress user intervention prompts on repeated failures    |
| `max_iterations`    | integer | `100`   | Maximum loop iterations before Lopen pauses for user input |
| `failure_threshold` | integer | `3`     | Repeated task failures before prompting user intervention  |

### Session Settings

| Setting                  | Type    | Default | Description                                           |
| ------------------------ | ------- | ------- | ----------------------------------------------------- |
| `auto_resume`            | boolean | `true`  | Offer to resume the latest session on startup         |
| `session_retention`      | integer | `10`    | Number of completed sessions to retain before pruning |
| `save_iteration_history` | boolean | `false` | Save per-iteration snapshots (increases storage)      |

### Git Settings

| Setting           | Type    | Default          | Description                                                |
| ----------------- | ------- | ---------------- | ---------------------------------------------------------- |
| `git.enabled`     | boolean | `true`           | Allow the LLM to perform git operations via native tools   |
| `git.auto_commit` | boolean | `true`           | Instruct the LLM to commit after task/component completion |
| `git.convention`  | string  | `"conventional"` | Commit message convention to instruct the LLM to follow    |

### Tool Discipline Settings

| Setting                               | Type    | Default | Description                                                          |
| ------------------------------------- | ------- | ------- | -------------------------------------------------------------------- |
| `tool_discipline.max_file_reads`      | integer | `3`     | Max reads of the same file per iteration before corrective injection |
| `tool_discipline.max_command_retries` | integer | `3`     | Max re-runs of the same failing command per iteration                |

### Display Settings

| Setting              | Type    | Default | Description                                |
| -------------------- | ------- | ------- | ------------------------------------------ |
| `show_token_usage`   | boolean | `true`  | Display token metrics in the TUI           |
| `show_premium_count` | boolean | `true`  | Display premium request counter in the TUI |

---

## CLI Flags

Common CLI flags that override configuration:

| Flag                   | Overrides             | Example                             |
| ---------------------- | --------------------- | ----------------------------------- |
| `--model <name>`       | All model assignments | `lopen --model claude-sonnet-4`     |
| `--unattended`         | `unattended`          | `lopen --unattended`                |
| `--resume <id>`        | `auto_resume`         | `lopen --resume auth-20260214-1357` |
| `--no-resume`          | `auto_resume`         | `lopen --no-resume`                 |
| `--max-iterations <n>` | `max_iterations`      | `lopen --max-iterations 50`         |

---

## Environment Variables

Lopen supports configuration via environment variables prefixed with `LOPEN_`. This is useful for CI/CD pipelines, container deployments, and ephemeral overrides that shouldn't be committed to project config.

### Naming Convention

Environment variable names map to configuration keys using the `__` (double underscore) separator for nested properties:

| Environment Variable                   | Configuration Key              | Example Value     |
| -------------------------------------- | ------------------------------ | ----------------- |
| `LOPEN_Models__Planning`               | `Models:Planning`              | `gpt-5`           |
| `LOPEN_Models__Building`               | `Models:Building`              | `claude-sonnet-4` |
| `LOPEN_Workflow__MaxIterations`        | `Workflow:MaxIterations`       | `50`              |
| `LOPEN_Workflow__Unattended`           | `Workflow:Unattended`          | `true`            |
| `LOPEN_Budget__TokenBudgetPerModule`   | `Budget:TokenBudgetPerModule`  | `100000`          |
| `LOPEN_Session__AutoResume`            | `Session:AutoResume`           | `false`           |

### Precedence

Environment variables override both global and project configuration files, but are themselves overridden by CLI flags. This makes them ideal for deployment-specific defaults that can still be tuned per invocation.

---

## Configuration Discovery

### Project Configuration

- Lopen looks for `.lopen/config.json` in the current working directory (or nearest parent with a `.lopen/` directory)
- If not found, built-in defaults are used
- Project configuration is created when the user first customizes a setting, or can be created manually

### Global Configuration

- Lopen looks for `~/.config/lopen/config.json` for user-wide defaults
- Useful for setting preferred models or display preferences across all projects

### Inspecting Configuration

- `lopen config show` — Display the resolved configuration with sources indicated
- `lopen config show --json` — Machine-readable output

---

## Notes

This specification defines **what can be configured and how settings are resolved**. It does not define the settings' effects — those are documented in the modules they affect ([Core](../core/SPECIFICATION.md), [LLM](../llm/SPECIFICATION.md), [Storage](../storage/SPECIFICATION.md), [TUI](../tui/SPECIFICATION.md)).

---

## Acceptance Criteria

- [x] [CFG-01] Configuration hierarchy resolves in order: CLI flags → environment variables → project config → global config → built-in defaults
- [x] [CFG-02] Higher-priority source wins when a setting is defined at multiple levels
- [x] [CFG-03] Project configuration is discovered at `.lopen/config.json` in the current working directory or nearest parent with `.lopen/`
- [x] [CFG-04] Global configuration is discovered at `~/.config/lopen/config.json`
- [x] [CFG-05] `lopen config show` displays the resolved configuration with sources indicated for each setting
- [x] [CFG-06] `lopen config show --json` outputs machine-readable JSON
- [x] [CFG-07] All settings have sensible built-in defaults — Lopen works without any configuration files
- [x] [CFG-08] `--model <name>` CLI flag overrides all model phase assignments for the invocation
- [x] [CFG-09] `--unattended` CLI flag overrides the `unattended` setting
- [x] [CFG-10] `--resume <id>` and `--no-resume` CLI flags override `auto_resume` behavior
- [x] [CFG-11] `--max-iterations <n>` CLI flag overrides `max_iterations`
- [x] [CFG-12] Budget settings (`token_budget_per_module`, `premium_request_budget`) are respected when non-zero
- [x] [CFG-13] Oracle model setting is passed to the LLM module for verification sub-agent dispatch
- [x] [CFG-14] Tool discipline settings control corrective injection thresholds
- [x] [CFG-15] Invalid configuration values produce clear error messages with guidance
- [x] [CFG-16] `LOPEN_`-prefixed environment variables override file-based config but are overridden by CLI flags

---

## Dependencies

- **[Storage module](../storage/SPECIFICATION.md)** — `.lopen/config.json` location within the `.lopen/` directory structure
- **[CLI module](../cli/SPECIFICATION.md)** — CLI flags that override configuration settings
- **File system** — Reading configuration files from project and global paths

---

## Skills & Hooks

- **verify-config**: Validate that configuration files are well-formed JSON with recognized settings before workflow start

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow settings (unattended, failure threshold)
- [LLM Specification](../llm/SPECIFICATION.md) — Model assignments, token budgets
- [Storage Specification](../storage/SPECIFICATION.md) — Session retention, storage format, `.lopen/` structure
- [TUI Specification](../tui/SPECIFICATION.md) — Display settings
