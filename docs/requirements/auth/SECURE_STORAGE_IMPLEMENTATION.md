# Secure Token Storage Implementation Guide

> Implementation guide for JTBD-039: Secure Token Storage
> Date: 2026-01-29

## Quick Start

### Step 1: Add NuGet Package

```bash
cd src/Lopen.Core
dotnet add package Devlooped.CredentialManager
```

### Step 2: Create SecureCredentialStore

Create `src/Lopen.Core/SecureCredentialStore.cs`:

```csharp
using GitCredentialManager;

namespace Lopen.Core;

/// <summary>
/// Secure credential storage using platform-specific secure storage.
/// Windows: Windows Credential Manager (DPAPI)
/// macOS: Keychain
/// Linux: libsecret (Secret Service API)
/// </summary>
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
    
    /// <summary>
    /// Constructor for testing with a custom store.
    /// </summary>
    internal SecureCredentialStore(GitCredentialManager.ICredentialStore store)
    {
        _store = store;
    }
    
    public Task<string?> GetTokenAsync()
    {
        try
        {
            var credential = _store.Get(Service, Account);
            return Task.FromResult(credential?.Password);
        }
        catch (Exception ex)
        {
            // Log but don't throw - treat as no credential found
            Console.Error.WriteLine($"Warning: Failed to retrieve credential: {ex.Message}");
            return Task.FromResult<string?>(null);
        }
    }
    
    public Task StoreTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be empty", nameof(token));
            
        try
        {
            _store.AddOrUpdate(Service, Account, token);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to store credential securely: {ex.Message}", ex);
        }
    }
    
    public Task ClearAsync()
    {
        try
        {
            _store.Remove(Service, Account);
        }
        catch
        {
            // Ignore if credential doesn't exist
        }
        return Task.CompletedTask;
    }
}
```

### Step 3: Update Program.cs

Replace the `FileCredentialStore` registration:

```csharp
// Before:
services.AddSingleton<ICredentialStore, FileCredentialStore>();

// After:
services.AddSingleton<ICredentialStore, SecureCredentialStore>();
```

### Step 4: Add Migration Logic (Optional)

If you want to migrate existing tokens from `FileCredentialStore` to `SecureCredentialStore`:

```csharp
public class CredentialMigration
{
    public static async Task MigrateIfNeededAsync(
        ICredentialStore newStore, 
        FileCredentialStore oldStore)
    {
        // Check if there's a token in the new store already
        var existingToken = await newStore.GetTokenAsync();
        if (existingToken != null)
            return; // Already migrated or has credentials
            
        // Check if there's a token in the old store
        var oldToken = await oldStore.GetTokenAsync();
        if (oldToken == null)
            return; // Nothing to migrate
            
        // Migrate
        await newStore.StoreTokenAsync(oldToken);
        
        // Optionally clear old store
        await oldStore.ClearAsync();
        
        Console.WriteLine("Migrated credentials to secure storage.");
    }
}
```

Call during app startup:

```csharp
// In Program.cs after building the service provider
var credentialStore = serviceProvider.GetRequiredService<ICredentialStore>();
if (credentialStore is SecureCredentialStore secureStore)
{
    var oldStore = new FileCredentialStore();
    await CredentialMigration.MigrateIfNeededAsync(secureStore, oldStore);
}
```

### Step 5: Add Tests

Create `tests/Lopen.Core.Tests/SecureCredentialStoreTests.cs`:

```csharp
using Xunit;
using Moq;
using GitCredentialManager;

namespace Lopen.Core.Tests;

public class SecureCredentialStoreTests
{
    [Fact]
    public async Task StoreTokenAsync_StoresToken()
    {
        // Arrange
        var mockStore = new Mock<GitCredentialManager.ICredentialStore>();
        var store = new SecureCredentialStore(mockStore.Object);
        var token = "test-token-123";
        
        // Act
        await store.StoreTokenAsync(token);
        
        // Assert
        mockStore.Verify(s => s.AddOrUpdate(
            "github.com", 
            "lopen-oauth-token", 
            token), Times.Once);
    }
    
    [Fact]
    public async Task GetTokenAsync_ReturnsStoredToken()
    {
        // Arrange
        var mockStore = new Mock<GitCredentialManager.ICredentialStore>();
        var expectedToken = "test-token-123";
        
        var mockCredential = new Mock<ICredential>();
        mockCredential.Setup(c => c.Password).Returns(expectedToken);
        
        mockStore.Setup(s => s.Get("github.com", "lopen-oauth-token"))
            .Returns(mockCredential.Object);
            
        var store = new SecureCredentialStore(mockStore.Object);
        
        // Act
        var token = await store.GetTokenAsync();
        
        // Assert
        Assert.Equal(expectedToken, token);
    }
    
    [Fact]
    public async Task GetTokenAsync_ReturnsNull_WhenNoCredential()
    {
        // Arrange
        var mockStore = new Mock<GitCredentialManager.ICredentialStore>();
        mockStore.Setup(s => s.Get("github.com", "lopen-oauth-token"))
            .Returns((ICredential?)null);
            
        var store = new SecureCredentialStore(mockStore.Object);
        
        // Act
        var token = await store.GetTokenAsync();
        
        // Assert
        Assert.Null(token);
    }
    
    [Fact]
    public async Task ClearAsync_RemovesCredential()
    {
        // Arrange
        var mockStore = new Mock<GitCredentialManager.ICredentialStore>();
        var store = new SecureCredentialStore(mockStore.Object);
        
        // Act
        await store.ClearAsync();
        
        // Assert
        mockStore.Verify(s => s.Remove(
            "github.com", 
            "lopen-oauth-token"), Times.Once);
    }
    
    [Fact]
    public async Task StoreTokenAsync_ThrowsException_WhenTokenIsEmpty()
    {
        // Arrange
        var mockStore = new Mock<GitCredentialManager.ICredentialStore>();
        var store = new SecureCredentialStore(mockStore.Object);
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            store.StoreTokenAsync(""));
    }
}
```

## Platform-Specific Testing

### Windows
```bash
# Store a token
lopen auth login

# Verify it's in Windows Credential Manager
# Open: Control Panel → User Accounts → Credential Manager
# Look for: "git:https://lopen@github.com" or similar
```

### macOS
```bash
# Store a token
lopen auth login

# Verify it's in Keychain
security find-generic-password -s "lopen" -a "lopen-oauth-token"

# Or use Keychain Access.app
open -a "Keychain Access"
# Search for "lopen"
```

### Linux
```bash
# Store a token
lopen auth login

# Verify it's in Secret Service
secret-tool search service lopen

# Or use GNOME Keyring viewer
seahorse
```

## Troubleshooting

### Windows
- **Error**: "Failed to store credential securely"
  - Check that Windows Credential Manager service is running
  - Try running as administrator once to initialize

### macOS
- **Error**: "Keychain access denied"
  - Check that Terminal has Keychain access permissions
  - System Preferences → Security & Privacy → Privacy → Automation

### Linux
- **Error**: "Secret Service not available"
  - Install libsecret: `sudo apt install libsecret-1-0` (Ubuntu/Debian)
  - Or: `sudo dnf install libsecret` (Fedora/RHEL)
  - Ensure a keyring daemon is running (gnome-keyring, kwallet)

## Fallback Behavior

If secure storage is unavailable on a platform, the Git Credential Manager will automatically fall back to:
1. Git's built-in credential cache (in-memory, temporary)
2. Plaintext file storage (as a last resort, with warnings)

You can also keep `FileCredentialStore` as a manual fallback for specific scenarios.

## Configuration

The credential store can be configured via environment variables:

```bash
# Force a specific credential store
export GCM_CREDENTIAL_STORE=dpapi          # Windows only
export GCM_CREDENTIAL_STORE=keychain       # macOS only
export GCM_CREDENTIAL_STORE=secretservice  # Linux only
export GCM_CREDENTIAL_STORE=cache          # In-memory (all platforms)
export GCM_CREDENTIAL_STORE=plaintext      # File-based (all platforms)
```

## Security Considerations

1. **Namespace Isolation**: Using "lopen" namespace prevents conflicts with other apps
2. **User Scope**: Credentials are user-specific, not machine-wide
3. **No Root Required**: Works without elevated privileges
4. **Automatic Cleanup**: Credentials persist until explicitly cleared
5. **Platform Native**: Uses OS security features (DPAPI, Keychain, libsecret)

## References

- See `SECURE_STORAGE_RESEARCH.md` for detailed research and alternatives
- [Git Credential Manager Documentation](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md)
- [Devlooped.CredentialManager Source](https://github.com/devlooped/CredentialManager)
