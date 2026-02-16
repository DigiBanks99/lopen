# Implementation Plan — Current Batch

## Completed This Session

### JOB-055 (TUI-26): Consecutive Failure Threshold ✅
- [x] ConsecutiveFailureCount tracks on IActivityPanelDataProvider
- [x] 4 new tests (initial zero, increment, reset, via AddEntry)

### JOB-057 (TUI-28): Spinner Wiring ✅
- [x] PromptAreaData.Spinner renders spinner instead of input
- [x] 3 new tests (with spinner, with frame, null spinner)

### JOB-062 (TUI-39): Unknown Slash Commands ✅
- [x] Already implemented — SlashCommandExecutor.UnknownCommand + activity panel error display

### JOB-064 (TUI-43): Gallery Selection Navigation ✅
- [x] Already implemented — GalleryListComponent + TestCommand interactive mode

### JOB-065 (TUI-44): Gallery Preview with Stub Data ✅
- [x] All 15 components implement IPreviewableComponent
- [x] RenderPreview creates realistic stub data
- [x] 31 new tests (15 non-empty + 15 type + 1 gallery-wide)

### JOB-090 (TUI-38): SlashCommandRegistry Tests ✅
- [x] Already had 8 registry tests + 30 executor tests

### JOB-091 (TUI-28): SpinnerComponent Tests ✅
- [x] Already had 5 spinner tests + 3 integration tests

## Next Up
- JOB-066 (TUI-48): Gallery stub data multiple visual states
- JOB-052 (TUI-22): Guided conversation UI
- JOB-063 (TUI-40): Queued user messages in next SDK invocation
- Integration tests: JOB-080, JOB-081, JOB-085-089