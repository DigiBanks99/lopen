# Authentication Research

> Research for REQ-003: GitHub OAuth2 Authentication
> Last validated: 2026-01-24

## Key Finding

**No official GitHub Copilot SDK for .NET exists on NuGet.** Authentication must use GitHub's OAuth2 device flow directly.

## GitHub OAuth2 Device Flow

Device flow is ideal for CLI apps - no browser redirect needed.

### Flow Diagram

```
1. Request device code → GitHub returns device_code, user_code, verification_uri
2. Display user_code and URL to user
3. Poll for token → User authorizes in browser
4. Receive access_token → Store securely
```

### API Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `https://github.com/login/device/code` | POST | Request device code |
| `https://github.com/login/oauth/access_token` | POST | Poll for/exchange token |

### Device Code Request

```csharp
var response = await httpClient.PostAsync(
    "https://github.com/login/device/code",
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = clientId,
        ["scope"] = "copilot read:user"
    }));

// Response: device_code, user_code, verification_uri, expires_in, interval
```

### Token Polling

```csharp
// Poll every `interval` seconds until authorized or expired
var tokenResponse = await httpClient.PostAsync(
    "https://github.com/login/oauth/access_token",
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["client_id"] = clientId,
        ["device_code"] = deviceCode,
        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
    }));
```

### Response Codes

| Response | Action |
|----------|--------|
| `access_token` present | Success - store token |
| `error: authorization_pending` | Continue polling |
| `error: slow_down` | Increase interval by 5s |
| `error: expired_token` | Restart flow |
| `error: access_denied` | User denied - show message |

## Secure Token Storage

### Cross-Platform Options

| Platform | Storage | NuGet Package |
|----------|---------|---------------|
| Windows | DPAPI / Credential Manager | Built-in |
| macOS | Keychain | `Keychain.Net` or P/Invoke |
| Linux | libsecret / encrypted file | `LibSecret` or custom |

### Simple Approach (Initial)

Encrypted file in `~/.lopen/credentials.json`:

```csharp
// Use DPAPI on Windows, file-based encryption elsewhere
public interface ICredentialStore
{
    Task<string?> GetTokenAsync();
    Task StoreTokenAsync(string token);
    Task ClearAsync();
}
```

## GitHub OAuth App Setup

Register app at https://github.com/settings/developers:

- **Application name**: Lopen CLI
- **Homepage URL**: (repo URL)
- **Enable Device Flow**: ✅ Yes
- **Scopes needed**: `copilot`, `read:user`

## Implementation Order

1. Create `IAuthService` interface in Lopen.Core
2. Implement device flow in `GitHubDeviceFlowAuth`
3. Add simple file-based credential storage
4. Create `auth login`, `auth status`, `auth logout` commands
5. Support `GITHUB_TOKEN` environment variable override

## References

- [GitHub Device Flow](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow)
- [OAuth 2.0 Device Authorization Grant](https://datatracker.ietf.org/doc/html/rfc8628)
