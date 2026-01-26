# Secure Token Storage - Quick Summary

> Quick reference for JTBD-039: Secure Token Storage
> Date: 2026-01-29

## TL;DR - Recommendation

**Use `Devlooped.CredentialManager` package** - It provides cross-platform secure storage with a single unified API.

```bash
dotnet add package Devlooped.CredentialManager
```

```csharp
using GitCredentialManager;

var store = CredentialManager.Create("lopen");
store.AddOrUpdate("github.com", "user", token);
var cred = store.Get("github.com", "user");
```

## Package Comparison

| Package | Platforms | Active? | Downloads | Rating |
|---------|-----------|---------|-----------|--------|
| **Devlooped.CredentialManager** ⭐ | Win/Mac/Linux | ✅ 2025 | 81K | ⭐⭐⭐⭐⭐ |
| CredentialManagement | Windows only | ❌ 2014 | 2.2M | ⭐⭐⭐ |
| AdysTech.CredentialManager | Windows only | ❌ 2022 | 927K | ⭐⭐⭐ |
| System.Security.Cryptography.ProtectedData | Windows only | ✅ Built-in | N/A | ⭐⭐⭐⭐ |

## Security Comparison

| Storage Method | Encryption | User-Scoped | Cross-Platform | Battle-Tested |
|----------------|------------|-------------|----------------|---------------|
| **Devlooped.CredentialManager** | ✅ OS-native | ✅ Yes | ✅ Yes | ✅ Git ecosystem |
| Windows DPAPI | ✅ DPAPI | ✅ Yes | ❌ Windows only | ✅ Microsoft |
| Windows Credential Manager | ✅ DPAPI | ✅ Yes | ❌ Windows only | ✅ Microsoft |
| macOS Keychain | ✅ Keychain | ✅ Yes | ❌ macOS only | ✅ Apple |
| Linux libsecret | ✅ libsecret | ✅ Yes | ❌ Linux only | ✅ GNOME/KDE |
| File + Base64 (current) | ❌ Obfuscated | ❌ File perms | ✅ Yes | ❌ Not secure |

## Platform-Specific Storage

### What Devlooped.CredentialManager Uses:

| Platform | Backend | Location |
|----------|---------|----------|
| **Windows** | Windows Credential Manager (DPAPI) | `Control Panel → Credential Manager` |
| **macOS** | Keychain | `Keychain Access.app` or `security` command |
| **Linux** | libsecret (Secret Service API) | GNOME Keyring, KWallet, etc. |

## Quick Implementation Checklist

- [ ] Add NuGet: `dotnet add package Devlooped.CredentialManager`
- [ ] Create `SecureCredentialStore` class
- [ ] Update DI: Replace `FileCredentialStore` with `SecureCredentialStore`
- [ ] Add tests with mocked `ICredentialStore`
- [ ] Test on Windows, macOS, Linux
- [ ] Migrate existing tokens from file storage
- [ ] Update documentation

## Code Template

```csharp
using GitCredentialManager;

namespace Lopen.Core;

public class SecureCredentialStore : ICredentialStore
{
    private readonly GitCredentialManager.ICredentialStore _store;
    private const string Service = "github.com";
    private const string Account = "lopen-oauth-token";
    
    public SecureCredentialStore()
    {
        _store = CredentialManager.Create("lopen");
    }
    
    public Task<string?> GetTokenAsync()
    {
        try
        {
            var credential = _store.Get(Service, Account);
            return Task.FromResult(credential?.Password);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }
    
    public Task StoreTokenAsync(string token)
    {
        _store.AddOrUpdate(Service, Account, token);
        return Task.CompletedTask;
    }
    
    public Task ClearAsync()
    {
        try { _store.Remove(Service, Account); } catch { }
        return Task.CompletedTask;
    }
}
```

## Testing Commands

### Windows
```powershell
# Store
lopen auth login

# View
cmdkey /list | findstr lopen
```

### macOS
```bash
# Store
lopen auth login

# View
security find-generic-password -s "lopen"
```

### Linux
```bash
# Store
lopen auth login

# View (requires secret-tool)
secret-tool search service lopen
```

## Pros vs Cons

### Devlooped.CredentialManager (Recommended)

**Pros:**
- ✅ Cross-platform (Windows, macOS, Linux)
- ✅ Single unified API
- ✅ Uses OS-native secure storage
- ✅ Battle-tested (Git ecosystem)
- ✅ Actively maintained (2025)
- ✅ .NET Standard 2.0 (compatible with .NET 10)
- ✅ No UI dependencies
- ✅ Automatic fallback handling

**Cons:**
- ❌ Additional dependency (80KB)
- ❌ Requires Git Credential Manager on system (auto-installed)
- ❌ Slightly more complex than DPAPI-only

### Manual DPAPI Implementation

**Pros:**
- ✅ No external dependencies
- ✅ Built into .NET
- ✅ Very secure on Windows

**Cons:**
- ❌ Windows only
- ❌ Need separate macOS/Linux implementations
- ❌ More code to maintain
- ❌ Complex P/Invoke for Keychain/libsecret

### Current FileCredentialStore

**Pros:**
- ✅ Cross-platform
- ✅ Simple
- ✅ No dependencies

**Cons:**
- ❌ Not secure (Base64 obfuscation only)
- ❌ Token in plaintext on disk
- ❌ File permissions can be changed
- ❌ Not suitable for production

## Migration Strategy

1. **Phase 1**: Add `SecureCredentialStore` alongside `FileCredentialStore`
2. **Phase 2**: Auto-migrate tokens on first run
3. **Phase 3**: Default to `SecureCredentialStore`
4. **Phase 4**: Deprecate `FileCredentialStore` (keep for tests)

## Environment Variables

The credential store respects these environment variables:

```bash
# Override credential store type
GCM_CREDENTIAL_STORE=dpapi          # Windows Credential Manager
GCM_CREDENTIAL_STORE=keychain       # macOS Keychain
GCM_CREDENTIAL_STORE=secretservice  # Linux libsecret
GCM_CREDENTIAL_STORE=cache          # In-memory (temporary)
GCM_CREDENTIAL_STORE=plaintext      # File-based (insecure)
```

## Common Issues

| Issue | Platform | Solution |
|-------|----------|----------|
| "Secret Service not available" | Linux | Install `libsecret-1-0` package |
| "Keychain access denied" | macOS | Grant Terminal permissions in System Preferences |
| "Failed to store credential" | Windows | Run as admin once to initialize |
| "Credential not found" | All | Check namespace ("lopen") is correct |

## Next Steps

1. Read `SECURE_STORAGE_RESEARCH.md` for detailed analysis
2. Read `SECURE_STORAGE_IMPLEMENTATION.md` for step-by-step guide
3. Implement `SecureCredentialStore`
4. Add tests
5. Test on all platforms
6. Update documentation

## References

- **Research**: `SECURE_STORAGE_RESEARCH.md`
- **Implementation**: `SECURE_STORAGE_IMPLEMENTATION.md`
- **NuGet**: https://www.nuget.org/packages/Devlooped.CredentialManager
- **Source**: https://github.com/devlooped/CredentialManager
- **GCM Docs**: https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md
