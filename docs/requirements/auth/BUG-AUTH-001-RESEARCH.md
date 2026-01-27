# BUG-AUTH-001: GCM Credential Store Not Configured - Research & Analysis

> **Status**: 🔴 Open  
> **Priority**: High  
> **Discovered**: 2026-01-27  
> **Research Date**: 2026-01-29

---

## Executive Summary

Device authentication fails when Git Credential Manager (GCM) is not configured with a credential store on Linux systems. The application attempts to use `SecureCredentialStore` by default, which wraps the GCM library (via `Devlooped.CredentialManager` package). When GCM has no credential store configured (via `GCM_CREDENTIAL_STORE` environment variable or `credential.credentialStore` Git config), the `CredentialManager.Create()` method throws an exception, causing authentication to fail after successful MFA completion.

**Impact**: Users cannot authenticate on Linux systems without pre-configuring GCM, creating a poor first-run experience.

**Recommended Solution**: Implement graceful fallback to `FileCredentialStore` when GCM initialization fails, with appropriate user warnings about security implications.

---

## Bug Description

### Error Details

**Error Location**: `SecureCredentialStore.cs:line 22` (inside constructor)  
**Propagates to**: `Program.cs:line 15` (during startup initialization)  
**User-visible failure**: `AuthService.StoreTokenAsync()` at line 177 during device flow completion

**Exception**:
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

### Steps to Reproduce

1. Use a Linux system without GCM credential store configured
2. Run `lopen auth login`
3. Complete device flow sign-in at GitHub
4. Complete MFA challenge
5. Observer credential storage failure with stack trace

---

## Current Implementation Analysis

### 1. Program.cs - Credential Store Initialization (Lines 11-27)

```csharp
// Use secure credential storage with fallback to file-based storage
ICredentialStore credentialStore;
ITokenInfoStore tokenInfoStore;
if (SecureCredentialStore.IsAvailable())
{
    var secureStore = new SecureCredentialStore();  // ❌ FAILS HERE on line 15
    credentialStore = secureStore;
    tokenInfoStore = secureStore;
    // Migrate credentials from file storage if present
    var fileStore = new FileCredentialStore();
    await CredentialMigration.MigrateIfNeededAsync(credentialStore, fileStore);
}
else
{
    var fileStore = new FileCredentialStore();
    credentialStore = fileStore;
    tokenInfoStore = fileStore;
}
```

**Problem**: The `IsAvailable()` check returns `true` even when GCM is installed but not configured with a credential store. The actual failure occurs during `SecureCredentialStore()` construction.

### 2. SecureCredentialStore.cs - Constructor (Lines 19-23)

```csharp
public SecureCredentialStore()
{
    // Create platform-specific store with "lopen" namespace
    _store = CredentialManager.Create("lopen");  // ❌ THROWS HERE on line 22
}
```

**Problem**: `CredentialManager.Create()` from the `Devlooped.CredentialManager` package (which wraps Git Credential Manager) throws an exception when no credential store is configured.

### 3. SecureCredentialStore.cs - IsAvailable() Method (Lines 124-136)

```csharp
public static bool IsAvailable()
{
    try
    {
        // Try to create the store - this will fail if platform support is missing
        var _ = CredentialManager.Create("lopen-test");  // ❌ FALSE POSITIVE
        return true;
    }
    catch
    {
        return false;
    }
}
```

**Problem**: This method is intended to detect if secure storage is available, but on Linux systems with GCM installed but not configured, `CredentialManager.Create()` **throws an exception during construction** with the error message about selecting a credential store. However, this catch block swallows the exception, making `IsAvailable()` return `false` only when GCM is not installed at all, not when it's installed but unconfigured.

**Actual Behavior on Different Scenarios**:
- **Windows**: GCM defaults to Windows Credential Manager → `IsAvailable()` returns `true` ✅
- **macOS**: GCM defaults to macOS Keychain → `IsAvailable()` returns `true` ✅
- **Linux with GCM configured**: Uses configured store → `IsAvailable()` returns `true` ✅
- **Linux without GCM installed**: Library missing → `IsAvailable()` returns `false` ✅
- **Linux with GCM installed but not configured**: ❌ **THROWS EXCEPTION** - not caught by `IsAvailable()` because it's thrown DURING `Create()`, not after

### 4. AuthService.cs - StoreTokenAsync() (Lines 173-178)

```csharp
public Task StoreTokenAsync(string token)
{
    if (string.IsNullOrEmpty(token))
        throw new ArgumentException("Token cannot be empty", nameof(token));
    return _credentialStore.StoreTokenAsync(token);  // Called after device flow completes
}
```

**Context**: This method is called from `Program.cs` line 130 after successful device flow authentication:

```csharp
if (result.Success && result.AccessToken is not null)
{
    await authService.StoreTokenAsync(result.AccessToken);  // Fails here in user's flow
    output.WriteLine();
    output.Success("Successfully authenticated!");
    return ExitCodes.Success;
}
```

---

## Root Cause Analysis

### Primary Issue: Incorrect Platform Detection Logic

The `IsAvailable()` method in `SecureCredentialStore.cs` does not correctly distinguish between:
1. **GCM not installed** (should return `false`)
2. **GCM installed but not configured** (currently throws exception, should be handled)

### Secondary Issue: No Fallback Mechanism at Construction Time

Even if `IsAvailable()` worked correctly, the initialization happens in `Program.cs` at startup (line 15), not during authentication. The exception occurs:
1. At app startup when `SecureCredentialStore()` constructor is called
2. Before any user interaction
3. The exception is not caught, causing the app to crash

### Specific Failure Points

| Line | File | Code | Issue |
|------|------|------|-------|
| `Program.cs:15` | `/src/Lopen.Cli/Program.cs` | `var secureStore = new SecureCredentialStore();` | Constructor throws exception when GCM not configured |
| `SecureCredentialStore.cs:22` | `/src/Lopen.Core/SecureCredentialStore.cs` | `_store = CredentialManager.Create("lopen");` | Underlying GCM library throws when no store configured |
| `SecureCredentialStore.cs:129` | `/src/Lopen.Core/SecureCredentialStore.cs` | `var _ = CredentialManager.Create("lopen-test");` | IsAvailable() catches exception but main code doesn't |

### GCM Behavior on Linux

According to [GCM credential store documentation](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md):

> **GCM comes without a default store on Linux distributions.**

This means:
- On **Windows**: Defaults to Windows Credential Manager
- On **macOS**: Defaults to macOS Keychain  
- On **Linux**: **NO DEFAULT** - must be explicitly configured via:
  - `GCM_CREDENTIAL_STORE` environment variable, OR
  - `credential.credentialStore` Git configuration

Available Linux options:
- `secretservice` - requires GUI session
- `gpg` - requires GPG and `pass` setup
- `cache` - ephemeral in-memory cache
- `plaintext` - insecure file storage
- `none` - no storage (not useful for our case)

---

## Proposed Solution

### High-Level Approach

Implement **graceful degradation** with the following fallback chain:

1. **Try secure storage** (GCM-based)
2. **On failure, fall back to file-based storage** with warning
3. **Inform user** about security implications and provide setup instructions

### Specific Code Changes

#### Change 1: Enhance `SecureCredentialStore.IsAvailable()` to Detect Configuration Issues

**File**: `src/Lopen.Core/SecureCredentialStore.cs` (lines 124-136)

**Current Code**:
```csharp
public static bool IsAvailable()
{
    try
    {
        // Try to create the store - this will fail if platform support is missing
        var _ = CredentialManager.Create("lopen-test");
        return true;
    }
    catch
    {
        return false;
    }
}
```

**Proposed Change**:
```csharp
/// <summary>
/// Checks if secure storage is available and properly configured on this platform.
/// Returns false if GCM is not installed OR if it's installed but not configured.
/// </summary>
public static bool IsAvailable()
{
    try
    {
        // Try to create the store - this will fail if platform support is missing
        // or if GCM is not configured on Linux
        var store = CredentialManager.Create("lopen-test");
        
        // Try a test operation to ensure the store is actually usable
        // This helps catch configuration issues that might not surface during Create()
        try
        {
            // Attempt to get a non-existent credential to verify store access
            _ = store.Get("test-service", "test-account");
        }
        catch
        {
            // If the test operation fails, the store is not usable
            return false;
        }
        
        return true;
    }
    catch (Exception ex)
    {
        // Check if this is the "no credential store configured" error
        if (ex.Message.Contains("No credential store has been selected") ||
            ex.Message.Contains("credential store"))
        {
            // GCM is installed but not configured - not available
            return false;
        }
        
        // Other errors (e.g., GCM not installed) also mean not available
        return false;
    }
}
```

**Rationale**: The enhanced method:
- Explicitly checks if the store is usable with a test operation
- Detects the specific "not configured" error message
- Returns `false` for both "not installed" and "not configured" scenarios
- Allows the fallback logic in `Program.cs` to work correctly

#### Change 2: Add User Warning in Program.cs

**File**: `src/Lopen.Cli/Program.cs` (lines 11-27)

**Current Code**:
```csharp
// Use secure credential storage with fallback to file-based storage
ICredentialStore credentialStore;
ITokenInfoStore tokenInfoStore;
if (SecureCredentialStore.IsAvailable())
{
    var secureStore = new SecureCredentialStore();
    credentialStore = secureStore;
    tokenInfoStore = secureStore;
    // Migrate credentials from file storage if present
    var fileStore = new FileCredentialStore();
    await CredentialMigration.MigrateIfNeededAsync(credentialStore, fileStore);
}
else
{
    var fileStore = new FileCredentialStore();
    credentialStore = fileStore;
    tokenInfoStore = fileStore;
}
```

**Proposed Change**:
```csharp
// Use secure credential storage with fallback to file-based storage
ICredentialStore credentialStore;
ITokenInfoStore tokenInfoStore;
bool usingSecureStorage = false;

if (SecureCredentialStore.IsAvailable())
{
    var secureStore = new SecureCredentialStore();
    credentialStore = secureStore;
    tokenInfoStore = secureStore;
    usingSecureStorage = true;
    
    // Migrate credentials from file storage if present
    var fileStore = new FileCredentialStore();
    await CredentialMigration.MigrateIfNeededAsync(credentialStore, fileStore);
}
else
{
    var fileStore = new FileCredentialStore();
    credentialStore = fileStore;
    tokenInfoStore = fileStore;
    usingSecureStorage = false;
}

// Service initialization
var deviceFlowAuth = new DeviceFlowAuth();
var authService = new AuthService(credentialStore, tokenInfoStore, deviceFlowAuth);
var sessionStore = new FileSessionStore();
var output = new ConsoleOutput();

// Show security warning if not using secure storage
// Only show once per session and only during auth operations
var showSecurityWarning = !usingSecureStorage;
```

**Then, modify the `auth login` command** (around line 128-133):

**Current Code**:
```csharp
if (result.Success && result.AccessToken is not null)
{
    await authService.StoreTokenAsync(result.AccessToken);
    output.WriteLine();
    output.Success("Successfully authenticated!");
    return ExitCodes.Success;
}
```

**Proposed Change**:
```csharp
if (result.Success && result.AccessToken is not null)
{
    // Show security warning before storing credentials if using insecure storage
    if (showSecurityWarning)
    {
        output.WriteLine();
        output.Warning("⚠️  Secure credential storage not available.");
        output.Muted("Credentials will be stored with basic encryption in:");
        output.Muted($"  ~/.lopen/credentials.json");
        output.WriteLine();
        output.Muted("To configure secure storage on Linux, set GCM_CREDENTIAL_STORE:");
        output.Muted("  export GCM_CREDENTIAL_STORE=cache         # in-memory (temporary)");
        output.Muted("  export GCM_CREDENTIAL_STORE=secretservice # GUI required");
        output.Muted("  export GCM_CREDENTIAL_STORE=gpg           # requires GPG/pass");
        output.Muted("See: https://aka.ms/gcm/credstores");
        output.WriteLine();
        
        // Only show once per session
        showSecurityWarning = false;
    }
    
    await authService.StoreTokenAsync(result.AccessToken);
    output.WriteLine();
    output.Success("Successfully authenticated!");
    return ExitCodes.Success;
}
```

**Rationale**: 
- Informs users about the security implications
- Provides actionable steps to configure secure storage
- Doesn't block authentication flow
- Only shows once to avoid annoyance

#### Change 3: Add Try-Catch in Constructor (Defense in Depth)

**File**: `src/Lopen.Core/SecureCredentialStore.cs` (lines 19-23)

While the improved `IsAvailable()` should prevent this from being called, add a fallback for safety:

**Current Code**:
```csharp
public SecureCredentialStore()
{
    // Create platform-specific store with "lopen" namespace
    _store = CredentialManager.Create("lopen");
}
```

**Proposed Change**:
```csharp
public SecureCredentialStore()
{
    try
    {
        // Create platform-specific store with "lopen" namespace
        _store = CredentialManager.Create("lopen");
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Failed to initialize secure credential store. " +
            "On Linux, ensure GCM_CREDENTIAL_STORE is configured. " +
            "See https://aka.ms/gcm/credstores for details.", 
            ex);
    }
}
```

**Rationale**: Provides a more helpful error message if the store is somehow instantiated despite `IsAvailable()` returning false.

---

## Test Coverage Recommendations

### Unit Tests to Add

#### Test File: `tests/Lopen.Core.Tests/SecureCredentialStoreTests.cs`

Add these test cases:

```csharp
[Fact]
public void IsAvailable_ReturnsFalse_WhenGcmNotConfigured()
{
    // This test should run on Linux in CI without GCM configured
    // and verify IsAvailable() returns false rather than throwing
    
    // Skip on Windows/macOS where there are defaults
    if (!OperatingSystem.IsLinux())
    {
        return;
    }
    
    // Ensure GCM_CREDENTIAL_STORE is not set
    var oldValue = Environment.GetEnvironmentVariable("GCM_CREDENTIAL_STORE");
    try
    {
        Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", null);
        
        // This should not throw and should return false
        var result = SecureCredentialStore.IsAvailable();
        
        result.ShouldBeFalse();
    }
    finally
    {
        Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", oldValue);
    }
}

[Fact]
public void Constructor_ThrowsHelpfulException_WhenGcmNotConfigured()
{
    // Skip on Windows/macOS where there are defaults
    if (!OperatingSystem.IsLinux())
    {
        return;
    }
    
    var oldValue = Environment.GetEnvironmentVariable("GCM_CREDENTIAL_STORE");
    try
    {
        Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", null);
        
        var ex = Should.Throw<InvalidOperationException>(() => 
            new SecureCredentialStore());
        
        ex.Message.ShouldContain("GCM_CREDENTIAL_STORE");
        ex.Message.ShouldContain("https://aka.ms/gcm/credstores");
    }
    finally
    {
        Environment.SetEnvironmentVariable("GCM_CREDENTIAL_STORE", oldValue);
    }
}
```

#### Test File: `tests/Lopen.Core.Tests/CredentialStoreFactoryTests.cs`

```csharp
[Fact]
public void Create_ReturnsFileStore_WhenSecureStoreNotAvailable()
{
    var factory = new CredentialStoreFactory();
    
    var store = factory.Create();
    
    store.ShouldNotBeNull();
    
    // If secure storage is not available, should get FileCredentialStore
    if (!SecureCredentialStore.IsAvailable())
    {
        store.ShouldBeOfType<FileCredentialStore>();
    }
}
```

### Integration Tests to Add

#### Test File: `tests/Lopen.Integration.Tests/AuthenticationFlowTests.cs` (new file)

```csharp
public class AuthenticationFlowTests
{
    [Fact]
    public async Task AuthLogin_SucceedsWithFileStorage_WhenSecureStorageUnavailable()
    {
        // This test verifies the complete authentication flow works
        // even when secure storage is not available
        
        // Arrange: Create services with file-based storage
        var fileStore = new FileCredentialStore();
        var authService = new AuthService(fileStore, fileStore, null);
        
        // Act: Store a test token
        await authService.StoreTokenAsync("test-token-12345");
        
        // Assert: Token should be retrievable
        var token = await authService.GetTokenAsync();
        token.ShouldBe("test-token-12345");
        
        // Cleanup
        await authService.ClearAsync();
    }
    
    [Fact]
    public async Task AuthLogin_MigratesFromFileToSecure_WhenSecureBecomesAvailable()
    {
        // Tests the migration scenario
        // (Detailed implementation omitted for brevity)
    }
}
```

### Manual Testing

Create a test matrix for different Linux configurations:

| Configuration | Expected Behavior | Test Command |
|--------------|-------------------|--------------|
| No GCM installed | Uses FileCredentialStore | `lopen auth login --token test` |
| GCM installed, not configured | Uses FileCredentialStore + warning | `lopen auth login --token test` |
| GCM with cache store | Uses SecureCredentialStore | `GCM_CREDENTIAL_STORE=cache lopen auth login --token test` |
| GCM with plaintext | Uses SecureCredentialStore | `GCM_CREDENTIAL_STORE=plaintext lopen auth login --token test` |

---

## Alternative Approaches Considered

### Alternative 1: Require GCM Configuration (Rejected)

**Approach**: Document that users must configure GCM before using lopen on Linux.

**Pros**:
- Simplest implementation (no code changes)
- Encourages secure practices

**Cons**:
- Poor user experience (authentication fails with cryptic error)
- High barrier to entry for new users
- Not aligned with "it just works" philosophy

**Decision**: ❌ Rejected - creates too much friction

### Alternative 2: Auto-Configure GCM (Rejected)

**Approach**: Automatically set `GCM_CREDENTIAL_STORE=cache` or `plaintext` if not configured.

**Pros**:
- Seamless user experience
- Always uses GCM infrastructure

**Cons**:
- Modifying global environment/git config is invasive
- `cache` store is ephemeral (credentials lost on reboot)
- `plaintext` has same security profile as FileCredentialStore
- User might not expect application to modify git configuration

**Decision**: ❌ Rejected - too invasive, limited benefit

### Alternative 3: Hybrid Approach with User Prompt (Deferred)

**Approach**: On first run, detect unconfigured GCM and prompt user to choose:
1. Configure GCM now (interactive setup)
2. Use file-based storage (temporary)
3. Exit and configure manually

**Pros**:
- Educates users about security options
- Gives user control
- Can lead to better security posture

**Cons**:
- Adds complexity to first-run experience
- Requires interactive prompts (problematic for scripts/CI)
- More code to maintain

**Decision**: ⏭️ Deferred - consider for future enhancement if user feedback indicates need

---

## Implementation Priority & Effort

### Priority: **HIGH** 🔴

**Justification**:
- Blocks core authentication functionality on Linux
- Affects developer onboarding experience
- Current error message is cryptic and doesn't guide users to solution
- Fix enables graceful degradation that's already designed in the architecture

### Effort Estimate: **MEDIUM** 🟡

**Breakdown**:
- Code changes: **3 hours**
  - Enhance `IsAvailable()`: 1 hour
  - Update `Program.cs` with warnings: 1 hour  
  - Add defensive constructor: 0.5 hour
  - Code review and refinement: 0.5 hour

- Testing: **2 hours**
  - Unit tests for new behavior: 1 hour
  - Integration testing on Linux: 0.5 hour
  - Manual testing of different scenarios: 0.5 hour

- Documentation: **1 hour**
  - Update SPECIFICATION.md: 0.5 hour
  - Update README with Linux setup notes: 0.5 hour

**Total**: ~6 hours

### Risk Assessment: **LOW** 🟢

- Changes are localized to credential storage layer
- Existing fallback architecture already in place
- No breaking changes to public APIs
- Well-tested FileCredentialStore available as fallback

---

## Success Criteria

### Functional Requirements

✅ Authentication succeeds on Linux without GCM configured  
✅ User receives clear warning about security implications  
✅ Instructions provided for configuring secure storage  
✅ No breaking changes to existing functionality  
✅ Secure storage still used when available

### Non-Functional Requirements

✅ No unhandled exceptions during authentication flow  
✅ Error messages are actionable (tell user what to do)  
✅ Fallback is transparent (user can complete authentication)  
✅ Security warning shown only once per session

### Testing Requirements

✅ Unit tests pass on all platforms (Windows, macOS, Linux)  
✅ Integration tests verify fallback behavior  
✅ Manual testing confirms expected behavior in all configurations  
✅ No regressions in existing secure storage functionality

---

## Related Documentation

- [BUG-AUTH-001 in SPECIFICATION.md](./SPECIFICATION.md#bug-auth-001-gcm-credential-store-not-configured)
- [GCM Credential Stores Documentation](https://github.com/git-ecosystem/git-credential-manager/blob/main/docs/credstores.md)
- [Devlooped.CredentialManager on NuGet](https://www.nuget.org/packages/Devlooped.CredentialManager/)
- [SECURE_STORAGE_ARCHITECTURE.md](./SECURE_STORAGE_ARCHITECTURE.md)

---

## Appendix: Code References

### Complete File Paths

- `src/Lopen.Cli/Program.cs` - Main application entry point
- `src/Lopen.Core/AuthService.cs` - Authentication service
- `src/Lopen.Core/SecureCredentialStore.cs` - GCM-based secure storage
- `src/Lopen.Core/CredentialStore.cs` - File-based credential storage
- `tests/Lopen.Core.Tests/SecureCredentialStoreTests.cs` - Unit tests

### Key Method Signatures

```csharp
// SecureCredentialStore.cs
public SecureCredentialStore()
public static bool IsAvailable()
public Task<string?> GetTokenAsync()
public Task StoreTokenAsync(string token)

// AuthService.cs
public async Task<string?> GetTokenAsync()
public Task StoreTokenAsync(string token)
public Task StoreTokenInfoAsync(TokenInfo tokenInfo)

// Program.cs (initialization)
ICredentialStore credentialStore;
ITokenInfoStore tokenInfoStore;
```

---

## Change History

| Date | Author | Changes |
|------|--------|---------|
| 2026-01-29 | Research Agent | Initial comprehensive research and analysis |

---

**End of Research Document**
