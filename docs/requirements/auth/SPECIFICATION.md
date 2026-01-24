# Authentication - Specification

> GitHub OAuth2 authentication via device flow

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-003 | GitHub OAuth2 Authentication | High | 🔴 Not Started |

---

## REQ-003: GitHub OAuth2 Authentication

### Description
Authenticate with GitHub using OAuth2 device flow to obtain access tokens for Copilot API access.

### Prerequisites
- GitHub account with Copilot access
- Internet connectivity for OAuth2 flow

### Command Signature
```bash
lopen auth login              # Device flow authentication
lopen auth status             # Check authentication status
lopen auth logout             # Clear stored credentials
lopen auth token              # Display current token (masked)
```

### Acceptance Criteria
- [ ] OAuth2 device code flow for CLI authentication
- [ ] Secure token storage (OS credential manager where available)
- [ ] Token refresh handling (when supported)
- [ ] Clear error messages for auth failures
- [ ] Support for environment variable token override (`GITHUB_TOKEN`)

### Authentication Methods (Priority Order)
1. Environment variable (`GITHUB_TOKEN`)
2. Cached token from secure storage
3. Device code flow (interactive)

### GitHub OAuth App Requirements
Register at https://github.com/settings/developers:
- **Application name**: Lopen CLI
- **Device Flow**: Enabled
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
