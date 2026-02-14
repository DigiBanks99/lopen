---
name: configuration
description: The configuration and settings requirements of Lopen
---

# Configuration Specification

## Overview

Lopen's behavior is controlled through a layered configuration system that supports project-level settings, user preferences, and CLI flags. This module defines the configuration hierarchy, available settings, defaults, and how configuration is discovered.

### Design Principles

1. **Convention over Configuration** — Sensible defaults for everything; configuration is optional
2. **Layered** — CLI flags override project config, which overrides global defaults
3. **Discoverable** — Users can inspect active configuration and its sources
4. **Minimal** — Only settings that meaningfully affect behavior are exposed

---

## Configuration Hierarchy

Settings are resolved in this order (highest priority first):

1. **CLI flags** — Per-invocation overrides (e.g., `--model claude-opus-4.5`)
2. **Project configuration** — `.lopen/config.json` in the project root
3. **Global configuration** — `~/.config/lopen/config.json` (user-wide defaults)
4. **Built-in defaults** — Hardcoded sensible defaults in Lopen

When a setting is defined at multiple levels, the highest-priority source wins.

---

## Settings

### Model Assignments

Configure which Copilot SDK model is used for each workflow phase (see [LLM § Per-Step Configuration](../llm/SPECIFICATION.md#per-step-configuration)):

```json
{
  "models": {
    "requirement_gathering": "claude-opus-4.5",
    "planning": "claude-opus-4.5",
    "building": "claude-opus-4.5",
    "research": "claude-sonnet-4"
  }
}
```

**Default**: All phases use the premium tier model.

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

### Display Settings

| Setting              | Type    | Default | Description                                |
| -------------------- | ------- | ------- | ------------------------------------------ |
| `show_token_usage`   | boolean | `true`  | Display token metrics in the TUI           |
| `show_premium_count` | boolean | `true`  | Display premium request counter in the TUI |

---

## CLI Flags

Common CLI flags that override configuration:

| Flag                   | Overrides             | Example                         |
| ---------------------- | --------------------- | ------------------------------- |
| `--model <name>`       | All model assignments | `lopen --model claude-sonnet-4` |
| `--unattended`         | `unattended`          | `lopen --unattended`            |
| `--resume <id>`        | `auto_resume`         | `lopen --resume 20260214-1357`  |
| `--no-resume`          | `auto_resume`         | `lopen --no-resume`             |
| `--max-iterations <n>` | `max_iterations`      | `lopen --max-iterations 50`     |

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

## References

- [Core Specification](../core/SPECIFICATION.md) — Workflow settings (unattended, failure threshold)
- [LLM Specification](../llm/SPECIFICATION.md) — Model assignments, token budgets
- [Storage Specification](../storage/SPECIFICATION.md) — Session retention, storage format, `.lopen/` structure
- [TUI Specification](../tui/SPECIFICATION.md) — Display settings
