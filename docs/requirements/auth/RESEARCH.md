---
date: 2026-02-15
sources:
  - https://www.nuget.org/packages/GitHub.Copilot.SDK
  - https://github.com/github/copilot-sdk
  - https://raw.githubusercontent.com/github/copilot-sdk/main/docs/auth/index.md
  - https://raw.githubusercontent.com/github/copilot-sdk/main/docs/auth/byok.md
  - https://raw.githubusercontent.com/github/copilot-sdk/main/docs/getting-started.md
  - https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow
---

# Auth Module Research

## 1. GitHub Copilot SDK Authentication in .NET

The official **`GitHub.Copilot.SDK`** NuGet package (latest: `0.1.24-preview.0`, as of Feb 2026) is the .NET SDK for programmatic control of GitHub Copilot CLI. It is in **technical preview** and may change in breaking ways.

The SDK does not implement authentication directly. Instead, it wraps the **Copilot CLI** (`copilot` binary), which must be installed separately and available in `PATH`. The SDK communicates with the CLI over JSON-RPC (stdio transport by default). Authentication is handled entirely by the CLI process — the SDK passes configuration that tells the CLI which auth method to use.

### Architecture

```
Lopen (.NET Application)
       ↓
  GitHub.Copilot.SDK (NuGet)
       ↓ JSON-RPC (stdio)
  Copilot CLI (server mode)
       ↓ HTTPS
  GitHub Copilot API
```

### SDK Authentication Options

The `CopilotClientOptions` class exposes two auth-relevant properties:

| Property | Type | Default | Description |
|---|---|---|---|
| `GithubToken` | `string?` | `null` | GitHub token for authentication. When provided, takes priority over all other auth methods. |
| `UseLoggedInUser` | `bool` | `true` (`false` when `GithubToken` is set) | Whether to use the stored OAuth credentials from a prior `copilot` CLI login. Cannot be used with `CliUrl`. |

### Authentication Priority (SDK/CLI combined)

The SDK documentation specifies this priority order:

1. **Explicit `GithubToken`** — token passed directly to `CopilotClientOptions`
2. **HMAC key** — `CAPI_HMAC_KEY` or `COPILOT_HMAC_KEY` environment variables
3. **Direct API token** — `GITHUB_COPILOT_API_TOKEN` with `COPILOT_API_URL`
4. **Environment variable tokens** — `COPILOT_GITHUB_TOKEN` → `GH_TOKEN` → `GITHUB_TOKEN`
5. **Stored OAuth credentials** — from previous `copilot` CLI login (device flow)
6. **GitHub CLI** — `gh auth` credentials

## 2. Device Flow Implementation

### How the GitHub OAuth Device Flow Works

The device flow (RFC 8628) is designed for headless apps like CLI tools. The flow is:

1. **Request device code** — `POST https://github.com/login/device/code` with `client_id` and optional `scope`. Returns:
   - `device_code` — 40-char verification code (app keeps this secret)
   - `user_code` — 8-char code with hyphen (e.g., `WDJB-MJHT`) shown to the user
   - `verification_uri` — `https://github.com/login/device`
   - `expires_in` — 900 seconds (15 minutes)
   - `interval` — minimum polling interval in seconds (typically 5)

2. **Display to user** — show `user_code` and `verification_uri`, optionally open browser

3. **Poll for completion** — `POST https://github.com/login/oauth/access_token` with `client_id`, `device_code`, and `grant_type=urn:ietf:params:oauth:grant-type:device_code`. Poll at the specified `interval`.

4. **Receive token** — on success, receive `access_token`, `token_type`, and `scope`

### Polling Error Codes

| Error | Meaning |
|---|---|
| `authorization_pending` | User hasn't entered the code yet — keep polling |
| `slow_down` | Polling too fast — add 5 seconds to interval |
| `expired_token` | Device code expired (15 min timeout) — restart flow |
| `access_denied` | User cancelled authorization |

### How the Copilot SDK Handles It

The SDK **does not expose the device flow directly** to the embedding application. The device flow is handled by the Copilot CLI itself when a user runs `copilot` and signs in interactively. The CLI stores credentials in the **system keychain**.

When the SDK is created with `UseLoggedInUser = true` (the default), it tells the CLI process to use these stored credentials. The SDK does not provide an API to programmatically initiate the device flow from .NET code.

**Implication for Lopen**: `lopen auth login` will likely need to either:
- (a) Shell out to `copilot auth login` (or equivalent CLI command) to trigger the device flow, or
- (b) Use the SDK's CLI process with a configuration that triggers interactive auth

This needs verification against the actual CLI command structure. The `copilot` CLI likely has an auth subcommand similar to `gh auth login`.

## 3. Environment Variable Authentication

### How GH_TOKEN / GITHUB_TOKEN Work

When environment variables are set, the Copilot CLI (and by extension the SDK) automatically detects and uses them — **no code changes needed**.

```csharp
// Environment variables are detected automatically by the CLI process
// No explicit configuration required in the SDK
await using var client = new CopilotClient();
```

The SDK also allows passing a token explicitly via `CopilotClientOptions.GithubToken`, which takes the **highest** priority:

```csharp
// Explicit token — overrides all environment variables and stored credentials
await using var client = new CopilotClient(new CopilotClientOptions
{
    GithubToken = Environment.GetEnvironmentVariable("GH_TOKEN")
                  ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN"),
});
```

### Environment Variable Priority

1. `COPILOT_GITHUB_TOKEN` — Copilot-specific (not mentioned in Lopen spec, but supported by SDK)
2. `GH_TOKEN` — GitHub CLI compatible
3. `GITHUB_TOKEN` — GitHub Actions compatible

### Supported Token Types

| Prefix | Type | Supported |
|---|---|---|
| `gho_` | OAuth user access tokens | ✅ Yes |
| `ghu_` | GitHub App user access tokens | ✅ Yes |
| `github_pat_` | Fine-grained personal access tokens | ✅ Yes |
| `ghp_` | Classic personal access tokens | ❌ Deprecated / not supported |

### Token Requirements

Per the Lopen specification, PATs must have the **"Copilot Requests"** permission enabled. This is a fine-grained PAT permission (not a classic scope).

## 4. Token Renewal

### How Automatic Token Refresh Works

The Copilot CLI manages token refresh internally for OAuth-based credentials (stored device flow tokens). This is transparent to the SDK consumer.

**For stored OAuth credentials** (`UseLoggedInUser = true`):
- The CLI handles token refresh automatically using its stored refresh token
- The SDK does not expose any refresh API because it delegates to the CLI
- If the refresh token itself is revoked or expired, the CLI returns an error

**For explicit tokens** (`GithubToken` or environment variables):
- There is **no automatic refresh**. PATs and explicit tokens are static.
- If a PAT expires mid-session, the request fails and the application must handle it
- The BYOK docs confirm: "The SDK does not refresh this token automatically"

### Error Handling for Token Expiry

The SDK emits `SessionErrorEvent` when authentication fails during a session. The `OnErrorOccurred` hook can intercept errors with retry/skip/abort strategies:

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = "gpt-5",
    Hooks = new SessionHooks
    {
        OnErrorOccurred = async (input, invocation) =>
        {
            // input.Error contains the error details
            // input.ErrorContext describes where the error occurred
            return new ErrorOccurredHookOutput
            {
                ErrorHandling = "retry" // "retry", "skip", or "abort"
            };
        }
    }
});
```

**Implication for Lopen**: For the automatic token renewal described in the spec, Lopen should:
1. Use the `OnErrorOccurred` hook to detect 401/403 errors
2. For stored OAuth credentials — the CLI should auto-refresh transparently
3. For explicit tokens (PATs) — treat as unrecoverable, trigger the critical error path
4. Use `ErrorHandling = "retry"` after confirming refresh succeeded (for OAuth), or `"abort"` for revoked tokens

**To be verified**: Whether the CLI's internal token refresh is fully transparent or whether the SDK needs to restart the CLI process. The NuGet package's `AutoRestart` option (default: `true`) suggests the SDK can recover from CLI crashes, which may cover some auth failure scenarios.

## 5. Recommended NuGet Packages

### Required

| Package | Version | Purpose |
|---|---|---|
| `GitHub.Copilot.SDK` | `0.1.24-preview.0` | Core SDK — CopilotClient, sessions, events, auth options |

### Likely Required (Transitive or Companion)

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.Extensions.AI` | latest | Provides `AIFunctionFactory.Create` for custom tool definitions. Used by the SDK for tool registration. |
| `Microsoft.Extensions.Logging` | latest | The SDK accepts an `ILogger` instance via `CopilotClientOptions.Logger` |

### Optional

| Package | Version | Purpose |
|---|---|---|
| `GitHub.Copilot.SDK.Supercharged` | `1.0.15` | Community package with additional helpers (21 language support). Not official. |

### Not Needed

- **No separate auth library** — authentication is handled entirely by the SDK/CLI
- **No Octokit** — Lopen does not need direct GitHub API access for auth
- **No custom OAuth library** — the device flow is owned by the CLI

## 6. Implementation Approach

### Recommended Architecture

```
Lopen.Auth (module)
├── IAuthService                 # Interface for auth operations
├── CopilotAuthService           # Implementation delegating to SDK/CLI
├── AuthCommands                 # CLI command handlers (login, status, logout)
├── AuthPreflightCheck           # Pre-workflow validation
└── TokenSourceResolver          # Determines credential source and precedence
```

### lopen auth login

Since the SDK does not expose a device flow API, `lopen auth login` should:

```csharp
// Option A: Use the SDK's CLI process management to trigger auth
// The copilot CLI has auth commands — invoke them through the SDK's CLI path
var cliPath = options.CliPath ?? "copilot"; // From CopilotClientOptions
var process = Process.Start(new ProcessStartInfo
{
    FileName = cliPath,
    Arguments = "auth login",
    RedirectStandardOutput = true,
    RedirectStandardError = true,
});
// Parse output for user_code and verification_uri
// Display to user, wait for completion
```

```csharp
// Option B: Create a CopilotClient and let it handle auth on first use
// If no stored credentials exist, the CLI may prompt for login
// This depends on CLI behavior — to be verified
await using var client = new CopilotClient(new CopilotClientOptions
{
    UseLoggedInUser = true,
});
await client.StartAsync(); // May trigger auth if not logged in
```

**Recommendation**: Option A gives Lopen more control over the UX (displaying the code, opening browser, showing progress). Option B is simpler but may not provide the interactive feedback the spec requires.

### lopen auth status

```csharp
public async Task<AuthStatus> CheckStatusAsync()
{
    // 1. Check environment variables first
    var ghToken = Environment.GetEnvironmentVariable("GH_TOKEN");
    var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    if (!string.IsNullOrEmpty(ghToken))
        return AuthStatus.AuthenticatedViaEnvVar("GH_TOKEN");
    if (!string.IsNullOrEmpty(githubToken))
        return AuthStatus.AuthenticatedViaEnvVar("GITHUB_TOKEN");

    // 2. Try to create a client and ping to validate stored credentials
    try
    {
        await using var client = new CopilotClient();
        await client.StartAsync();
        await client.PingAsync();
        return AuthStatus.AuthenticatedViaDeviceFlow(username);
    }
    catch
    {
        return AuthStatus.NotAuthenticated;
    }
}
```

### lopen auth logout

```csharp
// Shell out to copilot CLI to clear stored credentials
var process = Process.Start("copilot", "auth logout");
await process.WaitForExitAsync();

// Warn if env vars are still set
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GH_TOKEN")))
    Console.WriteLine("Warning: GH_TOKEN is still set in your environment.");
```

### Pre-flight Auth Check

Use the SDK's `PingAsync()` method to validate connectivity and authentication before starting a workflow:

```csharp
public async Task ValidateAuthAsync()
{
    await using var client = new CopilotClient(BuildClientOptions());
    await client.StartAsync();

    try
    {
        var response = await client.PingAsync();
        // Ping succeeded — credentials are valid
    }
    catch (Exception ex)
    {
        throw new AuthenticationException(
            "Authentication failed. Run 'lopen auth login' or set GH_TOKEN.",
            ex);
    }
}
```

### Token Source Resolution

```csharp
public CopilotClientOptions BuildClientOptions()
{
    var ghToken = Environment.GetEnvironmentVariable("GH_TOKEN");
    var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
    var explicitToken = ghToken ?? githubToken;

    if (explicitToken != null)
    {
        return new CopilotClientOptions
        {
            GithubToken = explicitToken,
            UseLoggedInUser = false,
        };
    }

    // Fall back to stored credentials
    return new CopilotClientOptions
    {
        UseLoggedInUser = true,
    };
}
```

### Automatic Token Renewal (Mid-Session)

```csharp
var session = await client.CreateSessionAsync(new SessionConfig
{
    Model = model,
    Hooks = new SessionHooks
    {
        OnErrorOccurred = async (input, invocation) =>
        {
            if (IsAuthError(input.Error))
            {
                // For stored OAuth: CLI should auto-refresh, retry
                if (usingStoredCredentials)
                    return new ErrorOccurredHookOutput { ErrorHandling = "retry" };

                // For PATs: unrecoverable — abort and save session
                await SaveSessionStateAsync();
                return new ErrorOccurredHookOutput { ErrorHandling = "abort" };
            }
            return null;
        }
    }
});
```

---

## Relevance to Lopen

### Key Findings

1. **The SDK fully supports Lopen's auth model.** `CopilotClientOptions.GithubToken` maps to the spec's environment variable auth, and `UseLoggedInUser` maps to the device flow credential path. No custom OAuth implementation is needed.

2. **Device flow is CLI-owned, not SDK-owned.** `lopen auth login` must delegate to the `copilot` CLI binary (e.g., `copilot auth login`) rather than calling an SDK method. The SDK has no `LoginAsync()` or `AuthenticateAsync()` method.

3. **Environment variable precedence matches the spec.** The SDK checks `GH_TOKEN` before `GITHUB_TOKEN`. The SDK also supports `COPILOT_GITHUB_TOKEN` (highest env var priority) which is not in the Lopen spec — Lopen can optionally support this for forward-compatibility.

4. **Token renewal is partially transparent.** For stored OAuth credentials, the CLI handles refresh internally. For PATs (environment variable auth), there is no auto-refresh — Lopen must detect failures and surface them as critical errors per the spec.

5. **`PingAsync()` is ideal for pre-flight checks.** It validates both connectivity and authentication in one call.

6. **Session hooks enable error interception.** The `OnErrorOccurred` hook provides the retry/abort mechanism needed for the spec's automatic token renewal and critical error handling.

7. **The SDK is in technical preview.** Breaking changes are expected. Lopen should pin to a specific version and plan for SDK updates.

### Open Questions (To Be Verified)

- **Exact CLI auth commands**: Does `copilot auth login` exist as a subcommand? Or is it `copilot login`? Verify against the installed CLI.
- **Username retrieval**: How to get the authenticated GitHub username for `lopen auth status` display. The SDK may expose this through session metadata or require a separate API call.
- **Credential storage location**: The docs say "system keychain" — confirm this works across Linux, macOS, and Windows for the Lopen Dockerfile environment.
- **CLI process lifetime**: Does each `CopilotClient` instance spawn a new CLI process? If so, what's the cost of creating a client just for auth status checks?
- **`COPILOT_GITHUB_TOKEN` support**: Should Lopen support this in addition to `GH_TOKEN` / `GITHUB_TOKEN` for alignment with the SDK's full priority chain?
