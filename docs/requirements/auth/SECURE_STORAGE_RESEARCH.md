# Secure Token Storage Research for .NET 10 CLI Application

> Research for JTBD-039: Secure Token Storage
> Date: 2026-01-29

## Executive Summary

For cross-platform secure credential storage in .NET 10, **I recommend using `Devlooped.CredentialManager`** (NuGet package) as the primary solution. This package wraps the official Git Credential Manager (GCM) and provides a unified API for Windows, macOS, and Linux with platform-specific secure storage implementations.

## Recommended Package: Devlooped.CredentialManager

### Overview
- **Package**: `Devlooped.CredentialManager` v2.6.1.1 (latest)
- **NuGet**: https://www.nuget.org/packages/Devlooped.CredentialManager
- **Source**: https://github.com/devlooped/CredentialManager
- **Target**: .NET Standard 2.0 (compatible with .NET 10)
- **Cross-platform**: ✅ Windows, macOS, Linux

### Key Features
- Wraps the official Git Credential Manager implementation
- No external dependencies or UI requirements
- Platform-specific secure storage automatically selected:
  - **Windows**: Windows Credential Manager (via DPAPI)
  - **macOS**: Keychain
  - **Linux**: libsecret (Secret Service API)
- Single unified API across all platforms
- Battle-tested (used by Git ecosystem with 80K+ downloads)

### Usage Example
```csharp
using GitCredentialManager;

ICredentialStore store = CredentialManager.Create("lopen");

// Store a credential
store.AddOrUpdate("github.com", "lopen-user", token);

// Retrieve a credential
ICredential cred = store.Get("github.com", "lopen-user");
string token = cred.Password;

// Delete credentials
store.Remove("github.com", "lopen-user");
```

### Dependencies
The package only requires:
- `DotNetConfig` (>= 1.2.0)
- `System.Security.Cryptography.ProtectedData` (>= 8.0.0)
- `System.Text.Json` (>= 8.0.5)

## Alternative Approach: Manual Platform-Specific Implementation

If you prefer more control or want to avoid external dependencies, you can implement platform-specific stores:

### 1. Windows: DPAPI (ProtectedData)

**Package**: `System.Security.Cryptography.ProtectedData` (built-in for .NET 10)

```csharp
using System.Security.Cryptography;

public class WindowsCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    
    public WindowsCredentialStore()
    {
        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        _filePath = Path.Combine(appData, "lopen", "credentials.dat");
    }
    
    public async Task StoreTokenAsync(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var entropy = GetOrCreateEntropy();
        
        // Encrypt using DPAPI - tied to current user account
        var encryptedBytes = ProtectedData.Protect(
            tokenBytes, 
            entropy, 
            DataProtectionScope.CurrentUser);
        
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllBytesAsync(_filePath, encryptedBytes);
    }
    
    public async Task<string?> GetTokenAsync()
    {
        if (!File.Exists(_filePath))
            return null;
            
        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(_filePath);
            var entropy = GetOrCreateEntropy();
            
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes, 
                entropy, 
                DataProtectionScope.CurrentUser);
                
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return null;
        }
    }
    
    public Task ClearAsync()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
        return Task.CompletedTask;
    }
    
    private byte[] GetOrCreateEntropy()
    {
        // Store entropy separately or use a fixed app-specific value
        // For simplicity, using a fixed value here
        return Encoding.UTF8.GetBytes("lopen-entropy-v1");
    }
}
```

**Pros**:
- Built-in, no external dependencies
- Very secure - uses Windows DPAPI
- Credentials tied to user account
- Simple implementation

**Cons**:
- Windows only
- Requires separate implementations for other platforms

### 2. Windows: Windows Credential Manager API

**Package**: `CredentialManagement` v1.0.2

```csharp
using CredentialManagement;

public class WindowsCredentialManagerStore : ICredentialStore
{
    private const string Target = "lopen-github-token";
    
    public Task StoreTokenAsync(string token)
    {
        using var cred = new Credential
        {
            Target = Target,
            Username = "lopen-user",
            Password = token,
            Type = CredentialType.Generic,
            PersistanceType = PersistanceType.LocalComputer
        };
        
        if (!cred.Save())
            throw new InvalidOperationException("Failed to save credential");
            
        return Task.CompletedTask;
    }
    
    public Task<string?> GetTokenAsync()
    {
        using var cred = new Credential { Target = Target };
        if (!cred.Load())
            return Task.FromResult<string?>(null);
            
        return Task.FromResult<string?>(cred.Password);
    }
    
    public Task ClearAsync()
    {
        using var cred = new Credential { Target = Target };
        cred.Delete();
        return Task.CompletedTask;
    }
}
```

**Pros**:
- Uses Windows Credential Manager UI
- Well-integrated with Windows
- Simple API

**Cons**:
- Windows only (2.2M downloads)
- Package is old (last updated 2014)
- Not actively maintained
- .NET Framework only (net35)

### 3. macOS: Keychain

For macOS, you would need to use P/Invoke to call native APIs or use the GCM implementation (which Devlooped.CredentialManager provides).

**Native approach** (complex, not recommended):
```csharp
// Requires P/Invoke to Security.framework
[DllImport("/System/Library/Frameworks/Security.framework/Security")]
private static extern int SecKeychainAddGenericPassword(
    IntPtr keychain, 
    uint serviceNameLength, 
    string serviceName,
    uint accountNameLength, 
    string accountName,
    uint passwordLength, 
    byte[] passwordData,
    IntPtr itemRef);
```

This is complex and error-prone. **Better to use Devlooped.CredentialManager**.

### 4. Linux: libsecret

For Linux, you would need to interact with the Secret Service API via D-Bus or use libsecret bindings.

**Challenges**:
- Requires libsecret installed on system
- Complex D-Bus interaction
- Multiple backend support (GNOME Keyring, KWallet)

Again, **Devlooped.CredentialManager handles this automatically**.

## Fallback Strategy

For systems where secure storage isn't available or for testing, provide encrypted file storage:

```csharp
public class EncryptedFileCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    
    public async Task StoreTokenAsync(string token)
    {
        // Use AES with a key derived from machine/user info
        var key = DeriveKey();
        var encrypted = AesEncrypt(token, key);
        await File.WriteAllTextAsync(_filePath, encrypted);
        
        // Set restrictive permissions
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(_filePath, 
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
    
    private byte[] DeriveKey()
    {
        // Derive key from machine ID + username
        var machineId = Environment.MachineName;
        var username = Environment.UserName;
        var combined = $"{machineId}:{username}:lopen-v1";
        
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
    }
}
```

## Implementation Recommendation

### Best Approach (Recommended) ⭐

**Use Devlooped.CredentialManager as the primary implementation:**

1. **Add NuGet package**: `Devlooped.CredentialManager`

2. **Create a unified credential store implementation**:

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
        // Create platform-specific store with "lopen" namespace
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
        try
        {
            _store.Remove(Service, Account);
        }
        catch
        {
            // Ignore if doesn't exist
        }
        return Task.CompletedTask;
    }
}
```

3. **Update DI registration** in `Program.cs`:

```csharp
// Replace FileCredentialStore with SecureCredentialStore
services.AddSingleton<ICredentialStore, SecureCredentialStore>();
```

### Benefits
- ✅ Single implementation works across all platforms
- ✅ Battle-tested code from Git ecosystem
- ✅ Automatic platform detection and secure storage selection
- ✅ Handles edge cases and error scenarios
- ✅ No need to maintain platform-specific code
- ✅ Uses OS-native secure storage (DPAPI, Keychain, libsecret)

### Migration Path
1. Implement `SecureCredentialStore` using Devlooped.CredentialManager
2. Add migration logic to move tokens from old `FileCredentialStore` to new store
3. Keep `FileCredentialStore` as a fallback for testing or environments where GCM isn't available

## Security Comparison

| Solution | Windows | macOS | Linux | Security Level | Maintenance | Status |
|----------|---------|-------|-------|----------------|-------------|--------|
| **Devlooped.CredentialManager** ⭐ | Credential Manager | Keychain | libsecret | ⭐⭐⭐⭐⭐ | Low | Active (2025) |
| Manual DPAPI + P/Invoke | DPAPI | Custom | Custom | ⭐⭐⭐⭐ | High | N/A |
| CredentialManagement pkg | Credential Manager | ❌ | ❌ | ⭐⭐⭐ | High | Unmaintained (2014) |
| Current FileCredentialStore | File + Base64 | File + Base64 | File + Base64 | ⭐ | Low | Current |

## Testing Strategy

```csharp
// Mock for testing
public class MockCredentialStore : ICredentialStore
{
    private string? _token;
    
    public Task<string?> GetTokenAsync() => Task.FromResult(_token);
    
    public Task StoreTokenAsync(string token) 
    {
        _token = token;
        return Task.CompletedTask;
    }
    
    public Task ClearAsync() 
    {
        _token = null;
        return Task.CompletedTask;
    }
}
```

## Additional Best Practices

1. **Token Expiry**: Store token expiry time alongside the token
2. **Encryption at Rest**: All solutions encrypt tokens at rest
3. **User Permissions**: Tokens only accessible by the creating user
4. **Audit Logging**: Log credential access (but not values)
5. **Secure Deletion**: Overwrite memory after use for sensitive data
6. **Fallback Chain**: Environment variable → Secure storage → Prompt user

## Platform-Specific Details

### Windows DPAPI
- Uses `ProtectedData.Protect()` and `Unprotect()`
- Data encrypted with user's Windows credentials
- Survives password changes
- Cannot be decrypted by other users
- Scope: `DataProtectionScope.CurrentUser`

### macOS Keychain
- Uses Security.framework APIs
- Integrates with system Keychain
- Can sync across devices with iCloud Keychain
- Accessible via Keychain Access.app
- Supports TouchID authentication

### Linux libsecret
- Uses D-Bus Secret Service API
- Backends: GNOME Keyring, KWallet, etc.
- Requires `libsecret-1-0` package installed
- Encrypted database per user
- Integrates with desktop session

## Implementation Checklist

- [ ] Add `Devlooped.CredentialManager` NuGet package
- [ ] Create `SecureCredentialStore` class
- [ ] Update DI registration in `Program.cs`
- [ ] Add migration logic from `FileCredentialStore`
- [ ] Add tests for `SecureCredentialStore`
- [ ] Keep `FileCredentialStore` for fallback/testing
- [ ] Update documentation
- [ ] Test on all three platforms (Windows, macOS, Linux)

## References

- [Devlooped.CredentialManager GitHub](https://github.com/devlooped/CredentialManager)
- [Devlooped.CredentialManager NuGet](https://www.nuget.org/packages/Devlooped.CredentialManager)
- [Git Credential Manager Docs](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md)
- [DPAPI Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)
- [How to Use Data Protection](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection)
- [CredentialManagement Package](https://www.nuget.org/packages/CredentialManagement)
