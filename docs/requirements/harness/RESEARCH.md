# Harness Research

## Claude Agent SDK (TypeScript) capabilities

Question (issue #25): what does the official TypeScript Claude Agent SDK expose for streaming messages, hooks, interrupting and resuming sessions, sub-agents, skills, permissions, and usage/cost/model reporting?

Researched 2026-11-10 against `@anthropic-ai/claude-agent-sdk` **0.3.322** (npm, published 2026-11-10). Field names below are exact. The SDK moves fast (many fields carry "requires v0.3.2xx+" notes), so pin a version.

### Sources

- [S1] TypeScript SDK reference: <https://code.claude.com/docs/en/agent-sdk/typescript>
- [S2] Sessions: <https://code.claude.com/docs/en/agent-sdk/sessions>
- [S3] Session storage: <https://code.claude.com/docs/en/agent-sdk/session-storage>
- [S4] Cost tracking: <https://code.claude.com/docs/en/agent-sdk/cost-tracking>
- [S5] Streaming vs single mode: <https://code.claude.com/docs/en/agent-sdk/streaming-vs-single-mode>
- [S6] Subagents: <https://code.claude.com/docs/en/agent-sdk/subagents>
- [S7] Skills: <https://code.claude.com/docs/en/agent-sdk/skills>
- [S8] Permissions: <https://code.claude.com/docs/en/agent-sdk/permissions>
- [S9] Hosting: <https://code.claude.com/docs/en/agent-sdk/hosting>
- [S10] Hooks: <https://code.claude.com/docs/en/agent-sdk/hooks>
- [S11] MCP: <https://code.claude.com/docs/en/agent-sdk/mcp>
- [S12] npm metadata: `npm view @anthropic-ai/claude-agent-sdk` (version 0.3.322, `engines.node >=18.14.0`, per-platform optional deps incl. `win32-x64`, `win32-arm64`, `linux-*-musl`)
- Repo/changelog: <https://github.com/anthropics/claude-agent-sdk-typescript> (`CHANGELOG.md`, `examples/session-stores/`)

### Runtime model

- `query()` spawns a `claude` CLI subprocess and talks to it over stdio. One session = one subprocess, which owns the shell, `cwd`, and JSONL transcripts on local disk. [S9]
- The npm package bundles a native Claude Code binary per platform; the CLI version is pinned to the SDK version (updating the SDK updates the CLI). Node 18.14+ required. [S9][S12]
- Sizing guidance: start at 1 GiB RAM, 5 GiB disk, 1 CPU per agent; memory grows with session length. No top-level session timeout (bound with `maxTurns`), no per-subagent wall-clock deadline. [S9]
- Telemetry: OpenTelemetry via env (`CLAUDE_CODE_ENABLE_TELEMETRY=1`, `OTEL_*`; `CLAUDE_CODE_ENHANCED_TELEMETRY_BETA=1` for traces). [S9]
- Auth via subprocess env: `ANTHROPIC_API_KEY`, `ANTHROPIC_BASE_URL` (proxy), `CLAUDE_CODE_OAUTH_TOKEN`, Bedrock/Vertex/Foundry flags. [S3][S9]
- Gotcha: `options.env` **replaces** the subprocess environment in TypeScript; spread `process.env` to keep `PATH` etc. [S1][S6]

### API surface

```ts
function query({ prompt, options }: {
  prompt: string | AsyncIterable<SDKUserMessage>;
  options?: Options;
}): Query; // Query extends AsyncGenerator<SDKMessage, void>
```

`Query` control methods (most require streaming input mode) [S1]: `interrupt()`, `setPermissionMode(mode)`, `setModel(model?)`, `streamInput(stream)`, `applyFlagSettings(settings)`, `rewindFiles(userMessageId, {dryRun?})` (needs `enableFileCheckpointing`), `initializationResult()`, `reinitialize()`, `supportedCommands()`, `supportedModels()`, `supportedAgents()`, `getContextUsage()`, `mcpServerStatus()`, `reconnectMcpServer(name)`, `toggleMcpServer(name, enabled)`, `setMcpServers(servers)`, `reloadPlugins()`, `reloadSkills()`, `stopTask(taskId)`, `backgroundTasks()`, `accountInfo()`, `close()`.

Other exports [S1][S3]: `tool()`, `createSdkMcpServer()`, `listSessions()`, `getSessionMessages()`, `getSessionInfo()`, `forkSession()`, `renameSession()`, `tagSession()`, `deleteSession()`, `listSubagents()`, `getSubagentMessages()`, `startup()` / `prewarm()` (pre-warm subprocesses), `InMemorySessionStore`, `foldSessionSummary`, `AbortError`, `BUILTIN_TOOL_NAMES`, `LEGACY_TOOL_NAME_ALIASES`.

The experimental V2 session API (`createSession()` with `send`/`stream`) was **removed** in 0.3.142; `query()` is the only entry point. [S2]

Key `Options` [S1]: `cwd`, `additionalDirectories`, `resume`, `resumeSessionAt`, `continue`, `forkSession`, `sessionId`, `persistSession`, `sessionStore`, `agent`, `agents`, `mcpServers`, `strictMcpConfig`, `tools`, `allowedTools`, `disallowedTools`, `skills`, `plugins`, `permissionMode`, `allowDangerouslySkipPermissions`, `canUseTool`, `permissionPromptToolName`, `permissionPrompts` (`'host' | 'none'`), `hooks`, `settings`, `settingSources`, `managedSettings`, `sandbox`, `maxTurns`, `maxBudgetUsd`, `taskBudget`, `model`, `fallbackModel`, `effort`, `thinking`, `includePartialMessages`, `includeHookEvents`, `agentProgressSummaries`, `outputFormat`, `systemPrompt`, `abortController`, `env`, `executable`, `pathToClaudeCodeExecutable`, `spawnClaudeCodeProcess`, `stderr`, `enableFileCheckpointing`.

### Streaming messages (`SDKMessage`)

Every message carries `uuid` and `session_id`. Main members [S1]:

| Type | Discriminator | Key fields |
| --- | --- | --- |
| `SDKSystemMessage` | `type:"system", subtype:"init"` | `model`, `tools[]`, `mcp_servers[{name,status}]`, `permissionMode`, `slash_commands[]`, `agents[]`, `skills[]`, `plugins[{name,path}]`, `cwd`, `apiKeySource`, `claude_code_version`, `output_style`, `capabilities` |
| `SDKAssistantMessage` | `type:"assistant"` | `message` (Anthropic `BetaMessage`: `id`, `content[]`, `usage`), `parent_tool_use_id`, `agent_id?`, `error?`, `usage_report?`, `context_usage?` |
| `SDKUserMessage` / `SDKUserMessageReplay` | `type:"user"` | `message` (`MessageParam`), `parent_tool_use_id`, `tool_use_result?`, `priority?` (`now`/`next`/`later`), `isReplay` |
| `SDKPartialAssistantMessage` | `type:"stream_event"` | raw `BetaRawMessageStreamEvent`; only with `includePartialMessages` |
| `SDKResultMessage` | `type:"result"` | see below; one per turn |
| `SDKCompactBoundaryMessage` | `system/compact_boundary` | `compact_metadata{trigger, pre_tokens}` |
| `SDKStatusMessage` | `system/status` | e.g. `permissionMode` change |
| Task events | `system/task_started`, `task_progress`, `task_updated`, `task_notification`, `background_tasks_changed` | `task_id`, `run_id`, `task_type` (`local_agent`, `local_bash`, `mcp_task`, `remote_agent`), `parent_task_id`, `spawn_depth`, `usage{total_tokens,tool_uses,duration_ms}`, `status` |
| `SDKToolProgressMessage` | `type:"tool_progress"` | `tool_use_id`, `tool_name`, `elapsed_time_seconds`, `heartbeat` (every 30s), `subagent_retry` |
| Hook events | `system/hook_started`, `hook_progress`, `hook_response` | `hook_id`, `hook_event`, `stdout`, `stderr`, `outcome` (with `includeHookEvents`) |
| `SDKSessionStateChangedMessage` | `system/session_state_changed` | `state: idle \| running \| requires_action` (needs env `CLAUDE_CODE_EMIT_SESSION_STATE_EVENTS=1`) |
| Others | | `SDKPermissionDeniedMessage`, `SDKRateLimitEvent`, `SDKAPIRetryMessage`, `SDKThinkingTokensMessage`, `SDKInformationalMessage`, `SDKConversationResetMessage` (`/clear`; new `session_id`), `SDKMirrorErrorMessage`, `SDKAuthStatusMessage`, `SDKFilesPersistedEvent`, `SDKWorkerShuttingDownMessage` |

The union has ~40 members and grows; a protocol mapping should pass unknown types through rather than reject them.

### Usage, cost and model reporting

`SDKResultMessage` subtypes: `success` (has `result`) and `error_max_turns`, `error_during_execution`, `error_max_budget_usd`, `error_max_structured_output_retries` (have `errors[]`). All carry [S1][S4]:

- `total_cost_usd`: estimated USD, **includes subagents**
- `usage` (`NonNullableUsage`: `input_tokens`, `output_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`, `cache_creation{ephemeral_5m,1h}`, `server_tool_use`, `service_tier`, `output_tokens_details.thinking_tokens`, `inference_geo`): **main loop only, excludes subagents**
- `modelUsage: Record<model, ModelUsage>` with `inputTokens`, `outputTokens` (includes thinking), `thinkingTokens?`, `cacheReadInputTokens`, `cacheCreationInputTokens`, `webSearchRequests`, `costUSD`, `contextWindow`, `maxOutputTokens`, `canonicalModel?`, `provider?`, `costBasis?` (`list`/`managed`/`unknown`): **includes subagents; use for accounting**
- `num_turns`, `duration_ms`, `duration_api_ms`, `stop_reason`, `permission_denials[]`, `session_id`, `terminal_reason?`, `ttft_ms?`

Semantics to design the store around [S4]:

- Cost fields are **client-side estimates** from a price table bundled in the SDK build. Do not bill from them.
- Per-step usage is on `assistant.message.usage`; parallel tool calls share one `message.id`, so dedupe by id. Per-step `output_tokens` is a placeholder; read output tokens from the result.
- Streaming input mode: one result per turn; `usage` is per turn, while `total_cost_usd`/`modelUsage` are **running totals for the call**. Read the latest; do not sum. `/clear` resets the totals and the `session_id`.
- Resume (Claude Code v2.1.277+): totals are restored from the transcript, so a resumed call's result includes earlier spend. Read the latest; do not sum.
- On a crash, the final `error_during_execution` result may carry zeroed cost fields. Fall back to the previous turn's result.
- Model: `init.model`, `modelUsage` keys per model, and `setModel()` mid-session; `PreModelSwitch`/`PostModelSwitch` hooks.

### Injecting input and interrupting

- **Streaming input mode** (`prompt` is an `AsyncIterable<SDKUserMessage>`) is the recommended mode. It allows queued follow-up messages, images, `interrupt()`, `setPermissionMode()` and `setModel()` mid-session. Single-message mode lacks queueing, real-time interruption and images. [S5]
- Inject more user turns by yielding from the iterable or calling `q.streamInput(stream)`. `SDKUserMessage.priority` (`now`/`next`/`later`) controls queueing. [S1][S9]
- `q.interrupt()` stops the current turn; `abortController` kills the run; `q.close()` terminates the subprocess. `stopTask(taskId)` stops a background subagent or bash task. [S1]
- A single-shot `query()` **throws after yielding an error result**, so wrap iteration in try/catch. [S2][S5]

### Sessions: resume, fork, storage

- Session id: `init.session_id` (early) or `result.session_id`. `sessionId` option lets the host choose the id. [S1][S2]
- `resume: id` continues a session; `continue: true` resumes the newest in `cwd`; `forkSession: true` with `resume` branches to a new id (conversation only, not filesystem); `resumeSessionAt` resumes at a message; `persistSession: false` keeps it in memory. [S1][S2]
- Transcripts: `~/.claude/projects/<encoded-cwd>/<session-id>.jsonl` (or under `CLAUDE_CONFIG_DIR`). Subagent transcripts live in separate files (`subagents/agent-<id>`). [S2][S3]
- **`SessionStore` adapter** mirrors transcripts to your backend for cross-host resume [S3]:

  ```ts
  type SessionKey = { projectKey: string; sessionId: string; subpath?: string };
  type SessionStore = {
    append(key: SessionKey, entries: SessionStoreEntry[]): Promise<void>;
    load(key: SessionKey): Promise<SessionStoreEntry[] | null>;
    listSessions?(projectKey: string): Promise<{ sessionId: string; mtime: number }[]>;
    listSessionSummaries?(projectKey: string): Promise<SessionSummaryEntry[]>;
    delete?(key: SessionKey): Promise<void>;
    listSubkeys?(key: { projectKey: string; sessionId: string }): Promise<string[]>;
  };
  ```

  - Entries are opaque JSON (`{type, ...}`), returned in append order, deep-equal. Dedupe by `entry.uuid`, because retries can redeliver.
  - `projectKey` encodes `cwd`, so **resume must use a matching `cwd`** (or set `CLAUDE_CODE_PROJECT_DIR_NAME`).
  - The mirror is dual-write and best-effort: local disk is written first, up to 3 append attempts, then a `system/mirror_error` message and the batch is dropped.
  - Incompatible with `persistSession: false` and `enableFileCheckpointing`.
  - `forkSession` rewrites ids; it is not a byte copy.
  - Reference adapters (S3, Redis, Postgres) and a conformance suite live in the repo's `examples/session-stores/`.
- Sessions persist conversation only. The working directory, CLAUDE.md and artifacts need their own storage. [S2][S9]

### Hooks

`hooks: Partial<Record<HookEvent, { matcher?, hooks: HookCallback[], timeout?, onFailure?: "continue"|"block" }[]>>`. The callback is `(input, toolUseID, { signal }) => Promise<HookJSONOutput>`. Shell hooks from loaded settings files also fire. [S10]

TypeScript `HookEvent` [S1][S10]: `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `PostToolBatch`, `UserPromptSubmit`, `UserPromptExpansion`, `MessageDisplay`, `Stop`, `StopFailure`, `SubagentStart`, `SubagentStop`, `PreCompact`, `PostCompact`, `PreModelSwitch`, `PostModelSwitch`, `PermissionRequest`, `PermissionDenied`, `SessionStart`, `SessionEnd`, `Notification`, `Setup`, `TeammateIdle`, `TaskCreated`, `TaskCompleted`, `Elicitation`, `ElicitationResult`, `ConfigChange`, `InstructionsLoaded`, `WorktreeCreate`, `WorktreeRemove`, `CwdChanged`, `FileChanged`, `DirectoryAdded`. The TypeScript set is a superset of Python's; `SessionStart`/`SessionEnd` are TypeScript-only as callbacks.

- Inputs share `session_id`, `cwd`, `hook_event_name`, plus `agent_id`/`agent_type` inside subagents. PreToolUse adds `tool_name` and `tool_input`. [S10]
- Outputs: top-level `continue`, `stopReason`, `systemMessage`, `suppressOutput`, `decision`. `hookSpecificOutput` for PreToolUse carries `permissionDecision` (`allow`/`deny`/`ask`/`defer`), `permissionDecisionReason`, `updatedInput`, `additionalContext`. PostToolUse supports `additionalContext` and `updatedToolOutput`. `{async: true}` makes a hook fire-and-forget. [S1][S10]
- Multiple hooks run in parallel; precedence is deny > defer > ask > allow. `defer` ends the turn with `stop_reason: "tool_deferred"`. Default timeout is 600 s for most events. [S10]

### Permissions

- Evaluation order: hooks, then deny rules (`disallowedTools`/settings), then ask rules, then permission mode, then allow rules (`allowedTools`/settings), then the `canUseTool` callback. [S8]
- `PermissionMode`: `default`, `acceptEdits`, `bypassPermissions` (needs `allowDangerouslySkipPermissions`), `plan`, `dontAsk` (deny anything that would prompt; good for headless), `auto` (classifier). Change it with `setPermissionMode()`. [S1][S8]
- `allowedTools` only pre-approves; it does not restrict. To remove a tool, use `disallowedTools` (bare name removes the definition; `Bash(rm *)` scopes it; `"*"` / `mcp__*` globs). `tools` sets the base tool list. Allow globs need a literal `mcp__<server>__` prefix. [S8]
- `canUseTool(toolName, input, {signal, suggestions, toolUseID, agentID, requestId, decisionReason, ...}) => Promise<PermissionResult>`. The result is `{behavior:"allow", updatedInput?, updatedPermissions?}` or `{behavior:"deny", message, interrupt?}`. Auto-approved calls never reach it. Use a PreToolUse hook to see every call. [S1][S8]
- Recommended locked-down headless profile: `allowedTools` + `permissionMode: "dontAsk"`. [S8]
- Subagents inherit `plan`/`acceptEdits`/`auto`/`bypassPermissions` from the parent. An `AgentDefinition.permissionMode` of `bypassPermissions` is never applied. [S8]

### Sub-agents

- Define them with `agents: Record<string, AgentDefinition>` (programmatic definitions take precedence over `.claude/agents/*.md`). There is also a built-in `general-purpose` agent; `CLAUDE_AGENT_SDK_DISABLE_BUILTIN_AGENTS=1` removes it. [S6]
- `AgentDefinition`: `description`*, `prompt`*, `tools`, `disallowedTools`, `model` (`sonnet`/`opus`/`haiku`/`inherit`/id), `mcpServers`, `skills`, `initialPrompt`, `maxTurns`, `autoCompactWindow`, `background`, `omitClaudeMd`, `memory`, `effort`, `permissionMode`. [S1][S6]
- Claude invokes subagents through the `Agent` tool (listed as `Task` in `init.tools`). Subagent messages carry `parent_tool_use_id` and `agent_id` (= `task_id`). Subagents run in the **background by default** (`status: "async_launched"`). They can nest (default depth 3) and can be resumed via the `agentId` in the tool result plus `resume`. [S6]
- Caps: env `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` (default 3), `CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS` (default 20), option `maxBudgetUsd` (includes subagent spend; ends with `error_max_budget_usd`). [S6]
- A forked subagent (`subagent_type: "fork"`) copies the conversation; deny it with `disallowedTools: ["Agent(fork)"]`. [S6]

### Skills and plugins

- Skills are **filesystem-only** (`.claude/skills/<name>/SKILL.md`); there is no programmatic registration API. They are discovered through `settingSources` (`user`, `project`; `project` also covers `additionalDirectories`). [S7]
- `skills?: string[] | 'all'` restricts which skills the model may invoke. Setting it auto-adds `Skill` to `allowedTools`. `init.skills` and `init.slash_commands` list what loaded. `/<name>` in a prompt dispatches a skill or command directly. `reloadSkills()` exists. [S1][S7]
- `plugins: SdkPluginConfig[]`, e.g. `{ type: "local", path: "<abs path>" }`. Plugins can carry skills, agents, hooks and MCP servers; plugin skills are named `plugin:skill`. [S1][S7]
- `settingSources` defaults to all of `user`/`project`/`local`; `[]` loads none. Use that, plus `CLAUDE_CONFIG_DIR` per tenant and `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1`, for isolation. [S1][S9]

### Working directory, MCP, sandbox

- `cwd` (default `process.cwd()`) and `additionalDirectories`. Use a distinct `cwd` per session for filesystem isolation. [S1][S9]
- `mcpServers: Record<string, McpServerConfig>` supports stdio (`command`, `args`, `env`), `type:"http"`, `type:"sse"` (`url`, `headers`), and in-process `createSdkMcpServer({name, tools})` with `tool()`. `.mcp.json` loads through the `project` setting source. Tools are named `mcp__<server>__<tool>`. Status values are `pending`/`connected`/`failed`/`needs-auth`/`disabled`. Servers can be changed at runtime with `setMcpServers()`. No interactive OAuth: pass a token in `headers`. [S11]
- `sandbox: SandboxSettings` sandboxes **Bash commands only** at OS level. Fields: `enabled`, `failIfUnavailable` (default true), `autoAllowBashIfSandboxed`, `excludedCommands`, `allowUnsandboxedCommands` (the model may set `dangerouslyDisableSandbox`, which falls back to `canUseTool`), `network{allowedDomains, deniedDomains, strictAllowlist, allowLocalBinding, allowUnixSockets, httpProxyPort, socksProxyPort}`, `filesystem{allowWrite, denyWrite, denyRead}`. On Linux it needs `bubblewrap` and `socat`. It does not cover WebFetch or in-process tools, and the domain proxy does not inspect TLS. Anthropic recommends container isolation (Docker/gVisor/Firecracker) plus an egress proxy for production. [S1][S9]

### Implications for Lopen (pointers, not decisions)

- The SDK already speaks a stdio JSON control protocol to its own CLI, and Lopen's TypeScript worker sits on top of `query()`. Every SDK concept above maps cleanly onto an SDK-independent protocol, but the message union is large and versioned, so plan for pass-through of unknown events.
- Cost accounting should take the **latest** `result.total_cost_usd`/`modelUsage` per call or session, not sum per-turn results. Store `usage` separately because it excludes subagents.
- Cross-host resume is supported via `SessionStore`, keyed by `cwd`. A .NET-side session store can back an adapter in the worker, so the worker stays stateless.
- Workspace isolation is the host's job (per-session `cwd`, `CLAUDE_CONFIG_DIR`, `settingSources: []`, a container). The built-in `sandbox` covers Bash only.
