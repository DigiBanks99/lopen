---
name: test-tui-gallery
description: Launch lopen test tui and confirm every TUI component in the gallery renders, previews, and responds with mock data so the UI surfaces match the specification.
---

# Prepare the gallery
1. Ensure the host application registers the TUI services (`AddLopenTui()` in the DI graph) so `IComponentGallery` exposes all components and the CLI command can resolve it.
2. Run `lopen test tui --list` once to confirm the command can start, the gallery reports a non-empty component list, and the command exits with code 0. This also verifies the `test` command group and `tui` subcommand wiring described in the CLI research notes.

# Validate every component
1. Execute `lopen test tui` in a writable terminal. The gallery should render a two-pane fullscreen TUI with the left pane showing the selectable list from `GalleryListComponent` and the right pane displaying previews rendered via `IPreviewableComponent.RenderPreview()`.
2. Walk through the list so every component is highlighted at least once. The expected baseline components are: `Top Panel`, `Main Activity Area`, `Context Panel`, `Prompt Area`, `Landing Page`, `Session Resume Modal`, `Confirmation Modal`, `Error Display`, `Diff Viewer`, `File Picker`, `Module Selection Modal`, `Progress & Spinners`, `Tool Call Display`, and `Component Selection UI`. (New additions should register automatically.)
3. For each component:
   - Press Enter to open its preview, verify the stub data exercises multiple states (empty, populated, loading, error) as described in TUI-48, and confirm no real network/LLM/filesystem operations occur.
   - Interact with the preview using the same keyboard shortcuts/spec actions that exist in the live app (scroll, expand/collapse, `Tab`, etc.) to ensure the component stays responsive.
   - Press `Esc`/`q` to return to the gallery list without crashing.
4. Use `lopen test tui <component-name>` to jump straight to a specific component when iterating on fixes. Verify the command succeeds, renders the preview, and returns to the terminal once you exit the preview.

# Non-interactive or CI-friendly checks
1. In automation or headless environments, rely on `lopen test tui --list` to assert the gallery still registers all components (look for the `Component Gallery:` header and each component name in the output). This also catches scenarios where the registry is empty and the command returns exit code 1 with the "Component gallery not available" or "No components registered" messages.
2. Consider piping the output to a log file so you can refer back to the component list when analyzing regressions.

# Troubleshooting hints
- If the interactive run immediately exits, check `Console.IsInputRedirected`; the command falls back to list mode when stdin is unavailable.
- If a component fails to render, make sure it implements the gallery interfaces (`ITuiComponent`, optionally `IPreviewableComponent`, and `IStatefulTuiComponent<T>`) and can accept stub state instead of reaching for live services.
- Use the CLI research file references (TUI-42 through TUI-49) to cross-check the acceptance criteria the gallery is meant to cover.
