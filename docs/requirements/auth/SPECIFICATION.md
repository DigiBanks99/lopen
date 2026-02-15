---
name: auth
description: The authentication requirements of Lopen for GitHub Copilot SDK access
---

# Auth Specification

## Overview

Lopen requires authenticated access to the GitHub Copilot SDK. This module defines how Lopen authenticates users, detects authentication state, and handles credential lifecycle — all by delegating to the Copilot SDK rather than implementing its own OAuth or token management.

### Design Principles

1. **SDK-Delegated** — Lopen does not manage tokens, OAuth flows, or credential storage directly; the Copilot SDK owns the auth lifecycle
2. **Transparent Renewal** — Token expiry during active sessions is handled automatically without user intervention
3. **Single Identity** — One GitHub identity at a time, matching the Copilot CLI's model
4. **Environment-Aware** — Headless/CI environments authenticate via environment variables, interactive environments use the device flow

---

## Authentication Flow

### Interactive Authentication (Device Flow)

When a user runs `lopen auth login`, Lopen delegates to the Copilot SDK's built-in device flow:

1. Lopen calls the SDK's authentication API
2. The SDK initiates the GitHub Device Flow — generates a user code and verification URL
3. Lopen displays the user code and URL, and opens the browser if possible
4. The user authorizes in the browser
5. The SDK receives and stores credentials
6. Lopen confirms success and displays the authenticated GitHub username

If the user cancels the browser authorization or the device code expires, Lopen reports the failure and remains unauthenticated.

### Environment Variable Authentication (Headless/CI)

For non-interactive environments (CI pipelines, `--headless` mode), authentication is via environment variables:

- **`GH_TOKEN`** — Checked first (highest precedence)
- **`GITHUB_TOKEN`** — Checked second

The token must be a fine-grained Personal Access Token (PAT) with the **"Copilot Requests"** permission enabled. If a token is present in the environment, Lopen passes it to the SDK and skips the device flow entirely.

If the token is invalid or lacks required permissions, Lopen surfaces the SDK's error with guidance on creating a correctly-scoped PAT.

### Authentication Precedence

When determining credentials, Lopen follows this order:

1. **`GH_TOKEN` environment variable** — if set, used immediately
2. **`GITHUB_TOKEN` environment variable** — if set and `GH_TOKEN` is not
3. **SDK-managed credentials** — stored by a prior `lopen auth login` device flow

The first valid credential source wins. Environment variables always take precedence over stored SDK credentials.

---

## Commands

### `lopen auth login`

Initiates interactive authentication via the Copilot SDK's device flow.

- Displays user code and verification URL
- Opens the browser automatically where supported
- Blocks until authorization completes or times out
- On success: displays authenticated username
- On failure: displays error with guidance
- If already authenticated: informs the user and offers to re-authenticate

Not applicable in headless mode — environment variables are used instead. Running `lopen auth login --headless` errors with a message directing the user to set `GH_TOKEN`.

### `lopen auth status`

Checks and displays the current authentication state.

| State | Output |
| --- | --- |
| Authenticated (SDK credentials) | `✓ Authenticated as <username> via device flow` |
| Authenticated (env var) | `✓ Authenticated via GH_TOKEN` or `✓ Authenticated via GITHUB_TOKEN` |
| Not authenticated | `✗ Not authenticated. Run 'lopen auth login' or set GH_TOKEN.` |
| Invalid/expired credentials | `✗ Credentials expired or invalid. Run 'lopen auth login' to re-authenticate.` |

Status is determined by querying the SDK — Lopen does not cache auth state.

### `lopen auth logout`

Clears stored SDK credentials.

- Calls the SDK's logout/credential-clearing API
- Confirms credentials were removed
- Does **not** unset environment variables — if `GH_TOKEN` is set, the user remains effectively authenticated. Lopen warns about this

---

## Automatic Token Renewal

During active sessions (e.g., a long-running `lopen build`), tokens may expire. Lopen handles this transparently:

1. The SDK returns an authentication error (401/403) during an API call
2. Lopen intercepts the error before surfacing it to the workflow
3. Lopen calls the SDK's token refresh mechanism
4. If refresh succeeds, the failed API call is retried transparently
5. If refresh fails (e.g., token was revoked, not just expired), Lopen treats this as a **critical system error** per [Core § Failure Handling](../core/SPECIFICATION.md#failure-handling--self-correction) — the workflow pauses, session state is saved, and the user is informed

Renewal is invisible to the user on success. No notification, no interruption.

---

## Pre-Flight Authentication Check

Before entering any workflow phase, Lopen performs a pre-flight auth check:

1. Determine credential source (env var or SDK-stored)
2. Validate credentials by making a lightweight SDK call
3. If valid: proceed to the workflow
4. If invalid or missing: block with a clear error message and guidance

This prevents wasting time on workflow setup only to fail on the first SDK invocation.

---

## Error Handling

### Error Categories

| Error | Cause | Behavior |
| --- | --- | --- |
| **Not authenticated** | No credentials found | Block workflow start; direct user to `lopen auth login` or `GH_TOKEN` |
| **Invalid token** | PAT lacks permissions, revoked, or malformed | Block with specific guidance (e.g., "PAT requires Copilot Requests permission") |
| **Token expired (renewable)** | SDK credential expired but refreshable | Auto-renew transparently (see [Automatic Token Renewal](#automatic-token-renewal)) |
| **Token revoked mid-session** | Credential revoked externally during workflow | Critical error — save session, inform user, block |
| **Rate limited (429)** | Copilot API rate limit hit | Not an auth error — handled by [Core § Back-Pressure](../core/SPECIFICATION.md#category-1-resource-limits) via exponential backoff |
| **Network error** | Cannot reach GitHub | Retry with backoff; if persistent, surface as critical error |

### Error Messages

All auth errors must include:

- **What failed** — clear description of the problem
- **Why** — the underlying cause if known
- **How to fix** — actionable next step (command to run, URL to visit, env var to set)

---

## Acceptance Criteria

- [ ] `lopen auth login` initiates the Copilot SDK device flow and completes authentication successfully
- [ ] `lopen auth status` accurately reports authenticated, unauthenticated, and invalid credential states
- [ ] `lopen auth logout` clears SDK-managed credentials and confirms removal
- [ ] `lopen auth logout` warns when `GH_TOKEN`/`GITHUB_TOKEN` environment variable is still set
- [ ] `lopen auth login --headless` returns an error directing the user to set `GH_TOKEN`
- [ ] Authentication via `GH_TOKEN` environment variable works without interactive login
- [ ] Authentication via `GITHUB_TOKEN` environment variable works when `GH_TOKEN` is not set
- [ ] `GH_TOKEN` takes precedence over `GITHUB_TOKEN` when both are set
- [ ] Environment variables take precedence over SDK-stored credentials
- [ ] Pre-flight auth check blocks workflow start when credentials are missing or invalid
- [ ] Automatic token renewal transparently refreshes expired credentials during active sessions
- [ ] Failed automatic renewal (revoked token) triggers a critical error, saves session state, and informs the user
- [ ] All auth error messages include what failed, why, and how to fix
- [ ] Invalid PAT errors include guidance about the "Copilot Requests" permission requirement
- [ ] No auth credentials or tokens are stored by Lopen — all credential storage is delegated to the SDK

---

## Dependencies

- **Copilot SDK** — Authentication API, device flow, token refresh, credential storage (the SDK is the auth backend)
- **[Core module](../core/SPECIFICATION.md)** — Failure handling classification (critical system error on auth failure)
- **[CLI module](../cli/SPECIFICATION.md)** — Command structure for `lopen auth` subcommands

---

## Skills & Hooks

- **verify-auth**: `lopen auth status` — Check that valid credentials exist before workflow execution
- **pre-workflow**: Run verify-auth before entering any workflow phase

---

## Notes

- **Dedicated Lopen GitHub App**: A future consideration if Lopen gains features requiring independent GitHub API access beyond what the Copilot SDK provides (e.g., direct repository management, issue creation outside of SDK tool calls). This would give users separate audit trail visibility and permission scoping for Lopen operations. Not needed for the current SDK-delegated model.
- **Multi-account support**: Not supported in v1. Lopen follows the Copilot CLI's single-identity model. If needed later, it could be implemented as profile switching (e.g., `lopen auth login --profile work`).
- **`lopen auth renew` command**: Deliberately omitted. Automatic token renewal makes an explicit renew command unnecessary. If a user's credentials are truly broken, `lopen auth logout && lopen auth login` is the recovery path.
- **Relationship with `gh` CLI**: Lopen does not reuse `gh auth` tokens. The Copilot SDK manages its own credential store independently. Users may be authenticated with `gh` and not with Lopen, or vice versa.

---

## References

- [CLI Specification](../cli/SPECIFICATION.md) — Auth command structure (`lopen auth` subcommands)
- [LLM Specification](../llm/SPECIFICATION.md) — How authentication feeds into SDK invocation
- [Core Specification](../core/SPECIFICATION.md) — Failure handling classification for auth errors
- [Configuration Specification](../configuration/SPECIFICATION.md) — Settings hierarchy (no auth-specific settings currently)
- [GitHub Copilot CLI — PAT Authentication](https://github.com/settings/personal-access-tokens/new) — Fine-grained PAT creation with Copilot Requests permission
