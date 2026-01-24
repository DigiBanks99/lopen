# Authentication - Specification

> GitHub Copilot SDK authentication via OAuth2 and device flow

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-003 | Copilot SDK Authentication | High | 🔴 Not Started |

---

## REQ-003: Copilot SDK Authentication

### Description
Initialize and authenticate with the GitHub Copilot SDK. Requires Copilot CLI to be installed.

### Prerequisites
- GitHub Copilot CLI installed ([Installation guide](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli))
- GitHub Copilot subscription (free tier available with limited usage)

### Command Signature
```bash
lopen auth login              # Interactive OAuth2 flow
lopen auth login --device     # Device flow (non-interactive)
lopen auth status             # Check authentication status
lopen auth logout             # Clear stored credentials
lopen auth token              # Display current token (masked)
```

### Acceptance Criteria
- [ ] OAuth2 authorization code flow for interactive sessions
- [ ] Device code flow for non-interactive/headless environments
- [ ] Secure token storage (OS credential manager where available)
- [ ] Token refresh handling
- [ ] Clear error messages for auth failures
- [ ] Support for environment variable token override
- [ ] Validate Copilot CLI is installed before auth operations

### Authentication Methods (Priority Order)
1. Environment variable (`GITHUB_TOKEN` or `COPILOT_TOKEN`)
2. Cached token from secure storage
3. Interactive OAuth2 flow
4. Device code flow (when `--device` specified)

### GitHub OAuth App Setup
Create a GitHub OAuth App at https://github.com/settings/developers:
- **Application name**: Lopen CLI
- **Homepage URL**: TBD
- **Authorization callback URL**: `http://localhost:8080/callback`
- **Scopes required**: `copilot`, `read:user`

### Secure Storage Locations
| Platform | Storage |
|----------|---------|
| Windows | Windows Credential Manager |
| macOS | Keychain |
| Linux | libsecret / encrypted file |

### Test Cases
| ID | Description | Expected |
|----|-------------|----------|
| TC-003-01 | `lopen auth login` | Initiates OAuth2 flow |
| TC-003-02 | `lopen auth login --device` | Initiates device flow |
| TC-003-03 | `lopen auth status` (authenticated) | Shows "authenticated" |
| TC-003-04 | `lopen auth status` (no token) | Shows "not authenticated" |
| TC-003-05 | `lopen auth logout` | Clears stored credentials |
| TC-003-06 | `GITHUB_TOKEN` set | Uses env var token |

---

## Implementation Notes

### OAuth2 Flow
1. Start local HTTP server on `localhost:8080`
2. Open browser to GitHub authorization URL
3. Receive callback with authorization code
4. Exchange code for access token
5. Store token securely

### Device Flow
1. Request device code from GitHub
2. Display user code and verification URL
3. Poll for authorization completion
4. Store token securely
