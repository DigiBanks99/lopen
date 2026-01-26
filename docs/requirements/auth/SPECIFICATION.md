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
- [ ] Token refresh handling (future)

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
- `DeviceFlowAuth` service with `StartDeviceFlowAsync()` and `PollForTokenAsync()`
- `OAuthAppConfig` record for OAuth app configuration
- `DeviceCodeResponse` and `TokenResponse` for GitHub API responses
- `DeviceFlowResult` for polling result
- `MockDeviceFlowAuth` for testing
- 13 unit tests covering device flow functionality

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

## Implementation Notes

See [RESEARCH.md](RESEARCH.md) for detailed implementation guidance.

### Device Flow (Primary)
1. Request device code from GitHub
2. Display user code and verification URL
3. Poll for authorization completion
4. Store token securely
