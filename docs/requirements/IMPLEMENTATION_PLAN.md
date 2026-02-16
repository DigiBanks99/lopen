# Implementation Plan

## Current Job: JOB-076 — Root Command

**Module**: cli/tui  
**Priority**: P4  
**Status**: ✅ Complete  
**Description**: Root command (`lopen`) launches TUI with full workflow, offers session resume.

### Tasks

- [x] **1. Add Lopen.Tui reference** — ProjectReference in Lopen.csproj
- [x] **2. Register AddLopenTui()** — in Program.cs DI
- [x] **3. Create RootCommandHandler** — Configure method with headless validation, session resume, TUI launch
- [x] **4. Fix ValidateHeadlessPromptAsync** — Use GetService instead of GetRequiredService for ISessionManager
- [x] **5. Write tests** — 10 RootCommandTests (TUI launch, headless, session resume, error handling)
- [x] **6. Update integration tests** — Add AddLopenTui(), use RootCommandHandler
- [x] **7. Validate** — 1198 tests pass

### Recently Completed Jobs

| Job | Module | Description |
|-----|--------|-------------|
| JOB-076 | cli/tui | Root command launches TUI |
| JOB-084 | tui | Split-screen layout calculator |
| JOB-077 | core/cli | --headless mode with IOutputRenderer |
| JOB-037 | storage/cli | Session resume (--resume/--no-resume) |
| JOB-083 | cli | CLI integration tests |