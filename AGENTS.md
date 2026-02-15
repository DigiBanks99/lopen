# Agents instructions

IMPORTANT:

- Update this document with learnings that assist in gaining context about the project.
- Only add learnings that are not referenceable from other documentation in the @docs folder.
- AGENTS.md is not meant for state tracking. Store state tracking in @.lopen/state/<module>-<YYYYMMDD>-<number>.json session files.
- AGENTS.md is not an architecture document.
- AGENTS.md is not a reporting document.
- Keep AGENTS.md concise and only with information to help solve problems quickly.
- Never make additional files for research. Keep all isolated to @docs/requirements/<module>/RESEARCH.md
- Keep SPECIFICATION.md files clean of research or implementation advice

**Lopen** is a .NET 10.0 CLI application with REPL capabilities for GitHub Copilot integration.

## Structure

| Folder | Intent                                                                                                      |
| ------ | ----------------------------------------------------------------------------------------------------------- |
| docs   | Contains requirements with its specification and research, explanations, how-to docs, guides and references |

## Key Files

| File                                             | Purpose                                                                                          |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| `.lopen/jobs-to-be-done.json`                    | Prioritized backlog of 100 jobs, each with id, requirement module, description, status, priority |
| `docs/requirements/README.md`                    | Index of all requirement modules with state                                                      |
| `docs/requirements/RESEARCH.md`                  | Index of all research documents across modules                                                   |
| `.lopen/state/<module>-<YYYYMMDD>-<number>.json` | Session tracking and state tracking files                                                        |
| `scripts/lopen.sh`                               | Bash loop that drives Copilot CLI for plan/build modes                                           |
| `BUILD.PROMPT.md`                                | Prompt for build mode iterations                                                                 |
| `PLAN.PROMPT.md`                                 | Prompt for planning mode                                                                         |

## Modules (8)

core, llm, storage, configuration, cli, auth, tui, otel — each under `docs/requirements/{module}/`

## Implementation Guidelines

1. Refer to domain-specific SPECIFICATION.md files in `docs/requirements/` subdirectories
2. Avoid adding documentation that consumes context unnecessarily
3. Project uses .NET 10.0 SDK
4. The Copilot SDK package is `GitHub.Copilot.SDK` (NuGet, technical preview)
5. CLI parsing uses `System.CommandLine`
6. TUI uses Spectre.Tui (cell-based rendering, Ratatui-inspired) with Spectre.Console for non-TUI output
