# Agents instructions

IMPORTANT:

- Track state using `lopen-memory`.
- Track research using `lopen-memory`.
- Modules and Specifications are the same thing in `lopen-memory`.
- AGENTS.md is not meant for state tracking.
- AGENTS.md is not an architecture document.
- AGENTS.md is not a reporting document.
- Keep AGENTS.md concise and only with information to help solve problems quickly.
- Keep modules clean of research or implementation advice
- Functionality is driven by other Lopen modules in `src`
- Use subagents as much as possible as you've got a limited context window

## Implementation Guidelines

1. Find modules in `lopen-memory` for project `lopen`. If not found only then consider `docs/requirements/{module}`
2. The Copilot SDK package is `GitHub.Copilot.SDK` (NuGet, technical preview)
3. CLI parsing uses `System.CommandLine`
4. Run a sub-agent with model gpt-5-mini to verify that all the acceptance criteria have been met before marking a task as done. Be clear on what task was done and where to find the acceptance criteria
5. Remember the state using the appropriate `lopen-memory` struct
