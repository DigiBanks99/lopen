# Agents instructions

IMPORTANT:

- Update this document with learnings that assist in gaining context about the project.
- Keep AGENTS.md concise and only with information to help solve problems quickly.
- Never make additional files for research. Keep all isolated to @docs/requirements/<module>/RESEARCH.md
- Keep SPECIFICATION.md files clean of research or implementation advice

**Lopen** is a .NET CLI application with REPL capabilities for GitHub Copilot integration.

## Project Status

No implementation exists yet. All specifications and requirements are in `@docs/requirements/<requirement>/SPECIFICATION.md`.

## Structure

| Folder | Intent                                                                                                      |
| ------ | ----------------------------------------------------------------------------------------------------------- |
| docs   | Contains requirements with its specification and research, explanations, how-to docs, guides and references |

## Implementation Guidelines

1. Refer to domain-specific SPECIFICATION.md files in `docs/requirements/` subdirectories
2. Avoid adding documentation that consumes context unnecessarily
