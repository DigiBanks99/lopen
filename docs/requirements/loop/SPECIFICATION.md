# Loop - Specification

> Autonomous iterative development workflow with human-on-the-loop oversight

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-030 | Loop Command | High | 🟢 Complete |
| REQ-031 | Loop Configuration | High | 🟢 Complete |
| REQ-032 | Plan Phase | High | 🟢 Complete |
| REQ-033 | Build Phase | High | 🟢 Complete |
| REQ-034 | State Management | High | 🟢 Complete |
| REQ-035 | Output Streaming | Medium | 🟢 Complete |
| REQ-036 | Verification Agent | Medium | 🟢 Complete |

---

## REQ-030: Loop Command

### Description
The Lopen Loop - an autonomous development workflow that plans and builds features iteratively with minimal human intervention. Named after the Stormlight Archive character who never gives up.

### Command Signature
```bash
lopen loop                    # Start loop with interactive setup
lopen loop --auto             # Skip interactive setup, use defaults
lopen loop --config <path>    # Use custom config file
```

### Acceptance Criteria
- [ ] Starts with interactive prompt: "Do you need to add specifications, or shall we commence planning and building?"
- [ ] Provides clear options for proceeding to plan phase or build phase
- [ ] Exits only on user interrupt (Ctrl+C / Cmd+C) or when `lopen.loop.done` file is created by agent
- [ ] Displays iteration count after each cycle
- [ ] Real-time streaming output from Copilot SDK
- [ ] Respects configuration from `loop configure` or `--config` flag

### Implementation Notes
- Uses Copilot SDK via `CopilotClient` and `CopilotSession`
- Human-on-the-loop, not in-the-loop: provides visibility without blocking
- Agent autonomy: decides priorities, work order, and when tasks are complete
- Integration with existing `jobs-to-be-done.json` for state tracking

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-030-01 | `lopen loop` | Prompts for specifications/planning choice | ❌ |
| TC-030-02 | `lopen loop --auto` | Skips prompts, starts immediately | ❌ |
| TC-030-03 | Loop with Ctrl+C | Gracefully exits loop | ❌ |
| TC-030-04 | Loop iteration counter | Displays `Completed iteration N` | ❌ |
| TC-030-05 | Loop completion file | Exits when `lopen.loop.done` exists | ❌ |

---

## REQ-031: Loop Configuration

### Description
Configure loop behavior, prompts, model, and iteration parameters.

### Command Signature
```bash
lopen loop configure          # Interactive configuration in REPL
lopen loop configure --model <model>
lopen loop configure --plan-prompt <path>
lopen loop configure --build-prompt <path>
lopen loop configure --allow-all
lopen loop configure --reset  # Reset to defaults
```

### Acceptance Criteria
- [ ] Interactive mode asks for all configuration options
- [ ] Supports model selection (default: `claude-opus-4.5`)
- [ ] Custom plan prompt template (default: `PLAN.PROMPT.md`)
- [ ] Custom build prompt template (default: `BUILD.PROMPT.md`)
- [ ] Allow all Copilot SDK operations flag (default: true)
- [ ] Stream output flag (default: true)
- [ ] Configuration persists in `~/.lopen/loop-config.json`
- [ ] Supports project-level config (`.lopen/loop-config.json` in repo root)
- [ ] Project config overrides user config

### Configuration Schema
```json
{
  "model": "claude-opus-4.5",
  "planPromptPath": "PLAN.PROMPT.md",
  "buildPromptPath": "BUILD.PROMPT.md",
  "allowAll": true,
  "stream": true,
  "autoCommit": false,
  "logLevel": "all"
}
```

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-031-01 | Configure interactively | Prompts for all options | ❌ |
| TC-031-02 | Configure with flags | Sets specified options | ❌ |
| TC-031-03 | Reset to defaults | Restores default configuration | ❌ |
| TC-031-04 | Project config override | Project settings override user settings | ❌ |

---

## REQ-032: Plan Phase

### Description
Agent analyzes specifications and creates/updates jobs-to-be-done list.

### Behavior
Executes `PLAN.PROMPT.md` (or configured alternative) once:
1. Study all `SPECIFICATION.md` files in `docs/requirements/`
2. Review existing `jobs-to-be-done.json`
3. Identify incomplete/partially complete jobs
4. Use sub-agents to find TODOs, FIXMEs, incomplete work
5. Verify jobs not already complete
6. Update `jobs-to-be-done.json` with prioritized tasks (max 100)
7. Research implementation approaches and update `RESEARCH.md` files
8. Clean `lopen.loop.done` file if present

### Acceptance Criteria
- [ ] Runs Copilot SDK with plan prompt
- [ ] Outputs real-time streaming to console
- [ ] Creates/updates `docs/requirements/jobs-to-be-done.json`
- [ ] Updates/creates `docs/requirements/<module>/RESEARCH.md` as needed
- [ ] Removes `lopen.loop.done` if it exists
- [ ] Displays clear indication of plan phase completion
- [ ] Uses sub-agents (especially `research` agent) for analysis

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-032-01 | Plan phase execution | Runs copilot with plan prompt | ❌ |
| TC-032-02 | Jobs file updated | `jobs-to-be-done.json` reflects new tasks | ❌ |
| TC-032-03 | Research files created | RESEARCH.md files updated/created | ❌ |
| TC-032-04 | Loop done removed | `lopen.loop.done` deleted at start | ❌ |

---

## REQ-033: Build Phase

### Description
Agent iteratively executes highest priority job until all complete or interrupted.

### Behavior
Loop executes `BUILD.PROMPT.md` (or configured alternative) until `lopen.loop.done` created or user interrupts:

**Each iteration:**
1. Verify not on `main` branch
2. Study `jobs-to-be-done.json`
3. Identify highest priority incomplete task
4. Study relevant `SPECIFICATION.md` and `RESEARCH.md`
5. Verify feature not already complete
6. Update `jobs-to-be-done.json` with progress
7. Study/update `IMPLEMENTATION_PLAN.md` in `docs/requirements/`
8. Implement tasks from plan using sub-agents
9. Add tests (required for completion)
10. Document using Divio model
11. Commit with conventional commit messages
12. Update `AGENTS.md` with learnings
13. Display `Completed iteration N`
14. If all complete, create `lopen.loop.done` and exit

### Acceptance Criteria
- [ ] Loops until `lopen.loop.done` exists or Ctrl+C
- [ ] Each iteration runs Copilot SDK with build prompt
- [ ] Real-time streaming output to console
- [ ] Iteration counter increments and displays
- [ ] Agent autonomously decides priorities and ordering
- [ ] Agent adds tasks to handle build/test failures
- [ ] Requires tests for feature completion
- [ ] Requires documentation (Divio model)
- [ ] Commits changes with conventional commit messages
- [ ] Creates `lopen.loop.done` when all jobs complete

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-033-01 | Build loop execution | Runs copilot repeatedly | ❌ |
| TC-033-02 | Iteration counter | Shows "Completed iteration N" | ❌ |
| TC-033-03 | Jobs completion | Creates `lopen.loop.done` when done | ❌ |
| TC-033-04 | Failure handling | Adds failed builds/tests as tasks | ❌ |
| TC-033-05 | Test requirement | Features must have tests | ❌ |
| TC-033-06 | Documentation requirement | Features documented (Divio) | ❌ |

---

## REQ-034: State Management

### Description
Track loop state, progress, and maintain context across iterations.

### State Tracking
- **Primary**: `docs/requirements/jobs-to-be-done.json` (task list, status, priority)
- **Secondary**: `docs/requirements/IMPLEMENTATION_PLAN.md` (current work plan)
- **Tertiary**: `docs/requirements/<module>/RESEARCH.md` (implementation research)
- **Control**: `lopen.loop.done` (completion signal file)
- **Memory**: Future - see dedicated memory module specification

### Acceptance Criteria
- [ ] State persists between iterations via files
- [ ] Jobs-to-be-done tracks: id, requirement code, description, status, partial implementation
- [ ] Implementation plan tracks: current tasks, completion status
- [ ] Agent can read previous iteration state
- [ ] Agent updates state files each iteration
- [ ] Loop respects `lopen.loop.done` file presence
- [ ] State survives application restart (file-based)

### Jobs-to-be-done Schema
```json
{
  "jobs": [
    {
      "id": "JTBD-001",
      "requirementCode": "REQ-030",
      "description": "Implement loop command",
      "status": "in-progress",
      "partialImplementation": "Command structure created, iteration loop pending",
      "priority": 1
    }
  ]
}
```

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-034-01 | State persistence | State files updated each iteration | ❌ |
| TC-034-02 | Jobs tracking | Jobs status reflects completion | ❌ |
| TC-034-03 | Loop completion | Respects `lopen.loop.done` | ❌ |
| TC-034-04 | Restart resilience | Can resume after restart | ❌ |

---

## REQ-035: Output Streaming

### Description
Real-time visibility into agent actions without blocking progress.

### Acceptance Criteria
- [ ] Streams Copilot SDK responses to console in real-time
- [ ] Uses Spectre.Console for formatted output
- [ ] Displays phase indicators (PLAN / BUILD)
- [ ] Shows iteration counter clearly
- [ ] Indicates when agent is using sub-agents
- [ ] Displays file changes, commits as they happen
- [ ] Supports `NO_COLOR` environment variable
- [ ] Output allows user to see progress and decide when to interrupt

### Output Format
```
------------
Mode: PLAN
------------
[Streaming Copilot SDK output...]

------------------------------
Completed iteration 1
------------------------------

------------
Mode: BUILD
------------
[Streaming Copilot SDK output...]

------------------------------
Completed iteration 2
------------------------------
```

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-035-01 | Real-time streaming | Output appears as generated | ❌ |
| TC-035-02 | Phase indicators | Shows PLAN/BUILD mode clearly | ❌ |
| TC-035-03 | Iteration display | Shows iteration count after each | ❌ |
| TC-035-04 | NO_COLOR support | Respects NO_COLOR env var | ❌ |

---

## REQ-036: Verification Agent

### Description
Single sub-agent dedicated to verifying completion and quality.

### Responsibilities
- Verify features are truly complete (not just claimed)
- Run tests and validate they pass
- Check documentation exists and follows Divio model
- Validate conventional commit message format
- Ensure requirement codes exist in SPECIFICATION.md files
- Confirm no invented/made-up requirement codes
- Verify build succeeds after changes

### Acceptance Criteria
- [x] Dedicated verification sub-agent invoked
- [x] Checks test coverage and passage
- [x] Validates documentation presence and format
- [x] Verifies requirement code validity
- [x] Confirms build success
- [x] Reports verification status to loop
- [x] Prevents jobs from being marked complete without verification

### Test Cases
| ID | Description | Expected | Status |
|----|-------------|----------|--------|
| TC-036-01 | Test verification | Ensures tests exist and pass | ✅ |
| TC-036-02 | Documentation check | Validates Divio documentation | ✅ |
| TC-036-03 | Requirement validation | Confirms requirement codes valid | ✅ |
| TC-036-04 | Build verification | Ensures build succeeds | ✅ |

---

## Implementation Notes

### Architecture
- **Loop Controller**: Main orchestration of plan/build phases
- **Configuration Service**: Load/save/merge user/project configs
- **State Manager**: Track jobs, plans, completion via files
- **Streaming Service**: Real-time Copilot SDK output to console
- **Verification Service**: Dedicated sub-agent for quality checks

### Copilot SDK Integration
- Use `CopilotClient` with configured model
- Stream responses via `session.On(AssistantMessageDeltaEvent)`
- Allow all operations (`--allow-all` flag) for autonomy
- Pass prompt file contents as initial message

### Philosophy: The Ralph Wiggum Technique
Named after the persistent character, this technique embraces:
- **Minimal guardrails**: Agent autonomy in decision-making
- **Persistent iteration**: Never give up until complete or interrupted
- **Human oversight, not intervention**: Visibility without blocking
- **Self-correction**: Agent adjusts own plan and priorities
- **Trust with verification**: Let agent work, but verify completion

### Testing Strategy
- **Unit tests**: Command parsing, configuration, state management
- **Integration tests**: Loop with mocked Copilot SDK
- **Self-test**: `testing` module integration - loop generates "5 bread recipes" in tmp folder
- **Manual testing**: Real loop on small feature to validate end-to-end

---

## Dependencies

| Requirement | Depends On |
|-------------|------------|
| REQ-030 | REQ-020 (Copilot SDK Integration) |
| REQ-031 | REQ-011 (Session State Management) |
| REQ-032 | REQ-020, REQ-023 (Custom Tools) |
| REQ-033 | REQ-032, REQ-020, REQ-023 |
| REQ-034 | REQ-033 |
| REQ-035 | REQ-014 (Modern TUI Patterns) |
| REQ-036 | REQ-033 |

---

## Future Enhancements

- **Memory Module**: Persistent context beyond file-based state
- **Parallel Jobs**: Multiple agents working on independent tasks
- **Custom Verification Rules**: User-defined quality gates
- **Loop Analytics**: Success rates, iteration metrics, time tracking
- **Resume from Checkpoint**: Restore mid-iteration state
- **Multi-repo Loops**: Coordinate changes across repository boundaries

---

## References

- `scripts/lopen.sh` - Original bash implementation
- `PLAN.PROMPT.md` - Default plan phase prompt
- `BUILD.PROMPT.md` - Default build phase prompt
- `docs/requirements/jobs-to-be-done.json` - Task tracking format
- Divio Documentation System: https://documentation.divio.com/
- Conventional Commits: https://www.conventionalcommits.org/
