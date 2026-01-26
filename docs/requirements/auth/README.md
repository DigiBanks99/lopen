# Authentication & Secure Token Storage Documentation

> Documentation for OAuth2 Device Flow and Secure Token Storage
> Last updated: 2026-01-29

## Overview

This directory contains research, specifications, and implementation guides for authentication and secure credential storage in the Lopen CLI application.

## Quick Links

### 🎯 Start Here
- **[SECURE_STORAGE_SUMMARY.md](SECURE_STORAGE_SUMMARY.md)** - Quick reference and TL;DR for secure token storage

### 📚 Detailed Documentation

#### Secure Token Storage (JTBD-039)
1. **[SECURE_STORAGE_RESEARCH.md](SECURE_STORAGE_RESEARCH.md)** - Comprehensive research on credential storage packages and approaches
2. **[SECURE_STORAGE_IMPLEMENTATION.md](SECURE_STORAGE_IMPLEMENTATION.md)** - Step-by-step implementation guide
3. **[SECURE_STORAGE_ARCHITECTURE.md](SECURE_STORAGE_ARCHITECTURE.md)** - Architecture diagrams and design decisions
4. **[SECURE_STORAGE_SUMMARY.md](SECURE_STORAGE_SUMMARY.md)** - Quick reference and comparison tables

#### OAuth2 Device Flow
1. **[RESEARCH.md](RESEARCH.md)** - OAuth2 Device Flow research and GitHub API documentation
2. **[SPECIFICATION.md](SPECIFICATION.md)** - Technical specification for authentication

## Document Purpose

| Document | Purpose | Audience |
|----------|---------|----------|
| **SECURE_STORAGE_SUMMARY** | Quick reference, TL;DR, decision summary | Developers (quick start) |
| **SECURE_STORAGE_RESEARCH** | Package comparison, security analysis | Architects, decision makers |
| **SECURE_STORAGE_IMPLEMENTATION** | Step-by-step implementation guide | Developers (implementation) |
| **SECURE_STORAGE_ARCHITECTURE** | System design, data flow, components | Architects, maintainers |
| **RESEARCH** | OAuth2 device flow, GitHub API | All team members |
| **SPECIFICATION** | Technical requirements | All team members |

## Key Recommendations

### ✅ Secure Token Storage (JTBD-039)

**Recommendation**: Use `Devlooped.CredentialManager` NuGet package

**Why?**
- ✅ Cross-platform (Windows, macOS, Linux)
- ✅ Single unified API
- ✅ OS-native secure storage (DPAPI, Keychain, libsecret)
- ✅ Battle-tested (Git ecosystem)
- ✅ Actively maintained (2025)

**Platform-Specific Storage:**
- **Windows**: Windows Credential Manager (DPAPI)
- **macOS**: Keychain
- **Linux**: libsecret (Secret Service API)

**Quick Start:**
```bash
dotnet add package Devlooped.CredentialManager
```

See [SECURE_STORAGE_IMPLEMENTATION.md](SECURE_STORAGE_IMPLEMENTATION.md) for full implementation.

### ✅ OAuth2 Device Flow (JTBD-038) - Completed

**Status**: ✅ Implemented

**Implementation:**
- `IDeviceFlowAuth` interface
- `DeviceFlowAuth` service
- OAuth app configuration from `~/.config/lopen/oauth.json`
- `auth login` command with device flow

See [RESEARCH.md](RESEARCH.md) for details.

## Current Implementation Status

### Completed ✅
- OAuth2 Device Flow (JTBD-038)
- Basic file-based credential storage
- Environment variable token support (`GITHUB_TOKEN`)
- `ICredentialStore` abstraction

### Next Steps 🚀
- [ ] Implement `SecureCredentialStore` using Devlooped.CredentialManager
- [ ] Add migration from `FileCredentialStore` to `SecureCredentialStore`
- [ ] Test on all platforms (Windows, macOS, Linux)
- [ ] Update documentation
- [ ] Add token refresh handling (JTBD-043)

## Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                  Lopen CLI                          │
│                                                     │
│  ┌──────────────┐         ┌──────────────────────┐ │
│  │ AuthService  │────────▶│ ICredentialStore     │ │
│  └──────────────┘         └──────────┬───────────┘ │
│                                      │             │
│                    ┌─────────────────┴──────┐      │
│                    │                        │      │
│           ┌────────▼────────┐    ┌─────────▼─────┐│
│           │ SecureCredential│    │ FileCredential││
│           │ Store (new)     │    │ Store (old)   ││
│           └────────┬────────┘    └───────────────┘│
│                    │                               │
└────────────────────┼───────────────────────────────┘
                     │
         ┌───────────┴──────────┐
         │ Devlooped.           │
         │ CredentialManager    │
         └───────────┬──────────┘
                     │
      ┌──────────────┼──────────────┐
      │              │              │
      ▼              ▼              ▼
┌─────────┐    ┌─────────┐    ┌─────────┐
│ Windows │    │  macOS  │    │  Linux  │
│ Cred.   │    │ Keychain│    │libsecret│
│ Manager │    │         │    │         │
└─────────┘    └─────────┘    └─────────┘
```

## Security Comparison

| Storage Method | Encryption | User-Scoped | Cross-Platform | Secure? |
|----------------|------------|-------------|----------------|---------|
| **SecureCredentialStore** (new) | ✅ OS-native | ✅ Yes | ✅ Yes | ⭐⭐⭐⭐⭐ |
| FileCredentialStore (current) | ❌ Base64 only | ⚠️ File perms | ✅ Yes | ⭐ |
| Environment variable | ❌ Plaintext | ❌ No | ✅ Yes | ⚠️ CI/CD only |

## Testing Strategy

### Unit Tests
- Mock `ICredentialStore` interface
- Test `AuthService` token retrieval priority
- Test `SecureCredentialStore` error handling

### Integration Tests
- Test on real Windows Credential Manager (Windows)
- Test on real Keychain (macOS)
- Test on real libsecret (Linux)

### Platform Testing
```bash
# Windows
cmdkey /list | findstr lopen

# macOS
security find-generic-password -s "lopen"

# Linux
secret-tool search service lopen
```

## Migration Path

1. **Phase 1**: Research and document (✅ Done)
2. **Phase 2**: Implement `SecureCredentialStore`
3. **Phase 3**: Add migration logic
4. **Phase 4**: Update DI registration
5. **Phase 5**: Test on all platforms
6. **Phase 6**: Deploy with migration
7. **Phase 7**: Deprecate `FileCredentialStore` (keep for tests)

## Common Issues & Solutions

| Issue | Platform | Solution |
|-------|----------|----------|
| "Secret Service not available" | Linux | Install `libsecret-1-0` |
| "Keychain access denied" | macOS | Grant Terminal permissions |
| "Failed to store credential" | Windows | Run as admin once |

See [SECURE_STORAGE_SUMMARY.md](SECURE_STORAGE_SUMMARY.md#common-issues) for more.

## Related Files

### Source Code
- `src/Lopen.Core/CredentialStore.cs` - Current implementation
- `src/Lopen.Core/AuthService.cs` - Authentication service
- `src/Lopen.Core/DeviceFlowAuth.cs` - OAuth2 device flow

### Tests
- `tests/Lopen.Core.Tests/FileCredentialStoreTests.cs`
- `tests/Lopen.Core.Tests/AuthServiceTests.cs`

### Configuration
- `~/.config/lopen/oauth.json` - OAuth app configuration
- `~/.lopen/credentials.json` - Current file-based storage (deprecated)

## References

### External Documentation
- [Devlooped.CredentialManager](https://github.com/devlooped/CredentialManager)
- [Git Credential Manager](https://github.com/git-ecosystem/git-credential-manager)
- [GitHub OAuth Device Flow](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow)
- [DPAPI Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)

### Internal Documentation
- [Implementation Plan](../../IMPLEMENTATION_PLAN.md)
- [Requirements](../../../README.md)

## Contributing

When making changes to authentication or credential storage:

1. Update relevant documentation files
2. Update this README if adding new documents
3. Run tests on all platforms
4. Update the implementation plan
5. Consider security implications

## Questions?

For questions about:
- **OAuth2 Device Flow**: See [RESEARCH.md](RESEARCH.md)
- **Secure Storage**: See [SECURE_STORAGE_SUMMARY.md](SECURE_STORAGE_SUMMARY.md) (quick) or [SECURE_STORAGE_RESEARCH.md](SECURE_STORAGE_RESEARCH.md) (detailed)
- **Implementation**: See [SECURE_STORAGE_IMPLEMENTATION.md](SECURE_STORAGE_IMPLEMENTATION.md)
- **Architecture**: See [SECURE_STORAGE_ARCHITECTURE.md](SECURE_STORAGE_ARCHITECTURE.md)
