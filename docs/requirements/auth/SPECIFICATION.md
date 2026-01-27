# Authentication - Specification

> GitHub OAuth2 authentication via device flow

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-003 | GitHub OAuth2 Authentication | High | 🟢 Complete |

---

## REQ-003: GitHub OAuth2 Authentication

### Description
Authenticate with GitHub using OAuth2 device flow to obtain access tokens for Copilot API access.

### Prerequisites
- GitHub account with Copilot access
- Internet connectivity for OAuth2 flow

### Command Signature
```bash
lopen auth login              # Show login instructions
lopen auth login --token <t>  # Store provided token
lopen auth status             # Check authentication status
lopen auth logout             # Clear stored credentials
```

### Acceptance Criteria
- [x] Secure token storage (file-based with obfuscation)
- [x] Clear error messages for auth failures
- [x] Support for environment variable token override (`GITHUB_TOKEN`)
- [x] OAuth2 device code flow
- [x] Platform-specific secure storage (DPAPI/Keychain/libsecret)
- [x] Token refresh handling (automatic refresh before expiry)

### Authentication Methods (Priority Order)
1. Environment variable (`GITHUB_TOKEN`) ✅
2. Cached token from file storage ✅
3. Device code flow ✅

### GitHub OAuth App Configuration
OAuth app credentials stored in `~/.config/lopen/oauth.json`:
```json
{
    "client-id": "<your-client-id>",
    "client-secret": "<optional>",
    "redirect-uris": ["http://127.0.0.1"]
}
```

### Implementation
- `IDeviceFlowAuth` interface for device flow authentication
- `DeviceFlowAuth` service with `StartDeviceFlowAsync()`, `PollForTokenAsync()`, and `RefreshTokenAsync()`
- `OAuthAppConfig` record for OAuth app configuration
- `DeviceCodeResponse` and `TokenResponse` for GitHub API responses
- `TokenInfo` record for storing access token, refresh token, and expiry times
- `DeviceFlowResult` and `RefreshTokenResult` for operation results
- `ITokenInfoStore` interface for token info storage with refresh support
- `MockDeviceFlowAuth` for testing
- 24 unit tests covering device flow and token refresh functionality

### Token Refresh Behavior
- GitHub OAuth tokens expire after 8 hours (if token expiration is enabled)
- Refresh tokens expire after 6 months
- `AuthService` automatically refreshes tokens 5 minutes before expiry
- If refresh fails and token is not fully expired, returns existing token
- If both access and refresh tokens are expired, user must re-authenticate

### Secure Storage Locations
| Platform | Storage |
|----------|---------|
| Windows | Windows Credential Manager |
| macOS | Keychain |
| Linux | libsecret / encrypted file |

### Test Cases
| ID | Description | Expected |
|----|-------------|----------|
| TC-003-01 | `lopen auth login` | Initiates device flow, shows code |
| TC-003-02 | `lopen auth status` (authenticated) | Shows "authenticated" |
| TC-003-03 | `lopen auth status` (no token) | Shows "not authenticated" |
| TC-003-04 | `lopen auth logout` | Clears stored credentials |
| TC-003-05 | `GITHUB_TOKEN` set | Uses env var token |

---

## Known Issues

### HIGH PRIORITY

#### BUG-AUTH-001: GCM Credential Store Not Configured
**Status**: 🔴 Open  
**Priority**: High  
**Discovered**: 2026-01-27

**Description**:
Device authentication fails after completing MFA when Git Credential Manager (GCM) is not configured with a credential store. The error occurs at `AuthService.cs:line 177` when attempting to store credentials.

**Error Message**:
```
System.Exception: No credential store has been selected.
Set the GCM_CREDENTIAL_STORE environment variable or the credential.credentialStore 
Git configuration setting to one of the following options:
  secretservice : freedesktop.org Secret Service (requires graphical interface)
  gpg           : GNU `pass` compatible credential storage (requires GPG and `pass`)
  cache         : Git's in-memory credential cache
  plaintext     : store credentials in plain-text files (UNSECURE)
  none          : disable internal credential storage
See https://aka.ms/gcm/credstores for more information.
```

**Steps to Reproduce**:
1. Run `lopen auth login` on a system without GCM credential store configured
2. Complete device flow sign-in
3. Complete MFA challenge
4. Observe credential storage failure

**Expected Behavior**:
Authentication should gracefully fallback to plaintext storage with a warning message when GCM is not configured. User should be informed about the security implications and given instructions to configure secure storage.

**Proposed Fix**:
- Detect when GCM credential store is not configured
- Display warning: "⚠️  No secure credential store configured. Storing token in plaintext. See https://aka.ms/gcm/credstores to configure secure storage."
- Fallback to plaintext storage in `~/.config/lopen/` or `~/.copilot/`
- Continue authentication flow successfully

**Testing**:
Add `--interactive` or `-i` flag to `lopen test self` command to enable interactive testing of full device auth flow including MFA and credential storage.

---

## Implementation Notes

See [RESEARCH.md](RESEARCH.md) for detailed implementation guidance.

### Device Flow (Primary)
1. Request device code from GitHub
2. Display user code and verification URL
3. Poll for authorization completion
4. Store token securely
