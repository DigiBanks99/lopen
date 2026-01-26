# Secure Token Storage Architecture

> Architecture design for JTBD-039: Secure Token Storage
> Date: 2026-01-29

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Lopen CLI Application                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────┐      ┌──────────────┐      ┌────────────────┐ │
│  │ AuthService │─────▶│ICredentialStore├────▶│SecureCredential│ │
│  │             │      │   Interface    │      │     Store      │ │
│  └─────────────┘      └──────────────┘      └────────┬─────────┘ │
│                                                       │           │
│                                              ┌────────▼────────┐  │
│                                              │ Devlooped.      │  │
│                                              │CredentialManager│  │
│                                              └────────┬────────┘  │
└───────────────────────────────────────────────────────┼───────────┘
                                                        │
                   ┌────────────────────────────────────┼────────────────────┐
                   │                                    │                    │
                   ▼                                    ▼                    ▼
         ┌─────────────────┐              ┌────────────────────┐  ┌──────────────────┐
         │     Windows      │              │       macOS        │  │      Linux       │
         ├─────────────────┤              ├────────────────────┤  ├──────────────────┤
         │ Windows          │              │ Keychain           │  │ libsecret        │
         │ Credential       │              │ (Security.         │  │ (Secret Service  │
         │ Manager          │              │  framework)        │  │  API)            │
         │                  │              │                    │  │                  │
         │ Uses DPAPI       │              │ System Keychain    │  │ GNOME Keyring    │
         │ encryption       │              │ Access.app         │  │ KWallet          │
         └─────────────────┘              └────────────────────┘  └──────────────────┘
```

## Component Responsibilities

### 1. ICredentialStore Interface
```csharp
public interface ICredentialStore
{
    Task<string?> GetTokenAsync();     // Retrieve stored token
    Task StoreTokenAsync(string token); // Store token securely
    Task ClearAsync();                  // Remove stored token
}
```

**Responsibilities:**
- Define contract for credential storage
- Abstraction for testing and multiple implementations

### 2. SecureCredentialStore (New)
```csharp
public class SecureCredentialStore : ICredentialStore
{
    private readonly GitCredentialManager.ICredentialStore _store;
}
```

**Responsibilities:**
- Wrap Git Credential Manager
- Implement ICredentialStore interface
- Handle platform-specific storage automatically
- Provide error handling and logging

**Dependencies:**
- `Devlooped.CredentialManager` NuGet package

### 3. Devlooped.CredentialManager Package
**Responsibilities:**
- Platform detection
- Route to appropriate backend (DPAPI/Keychain/libsecret)
- Fallback handling
- Error management

**Key Classes:**
- `CredentialManager.Create(namespace)` - Factory method
- `ICredentialStore` - Git Credential Manager interface
- `ICredential` - Credential data structure

### 4. Platform-Specific Backends

#### Windows: DPAPI + Credential Manager
- **Storage**: `%USERPROFILE%\AppData\Local\Microsoft\Credentials\`
- **API**: `CredRead`, `CredWrite`, `CredDelete` Win32 APIs
- **Encryption**: DPAPI (Data Protection API)
- **Scope**: Current user

#### macOS: Keychain
- **Storage**: `~/Library/Keychains/login.keychain-db`
- **API**: Security.framework (`SecKeychainAddGenericPassword`, etc.)
- **Encryption**: Keychain encryption
- **Scope**: Current user

#### Linux: libsecret (Secret Service API)
- **Storage**: `~/.local/share/keyrings/` (GNOME Keyring)
- **API**: D-Bus Secret Service API
- **Encryption**: Provider-specific (AES, etc.)
- **Scope**: Current user session

## Data Flow

### Store Token Flow
```
User runs: lopen auth login
    │
    ▼
DeviceFlowAuth authenticates with GitHub
    │
    ▼
AuthService.StoreTokenAsync(token)
    │
    ▼
ICredentialStore.StoreTokenAsync(token)
    │
    ▼
SecureCredentialStore.StoreTokenAsync(token)
    │
    ▼
GitCredentialManager.ICredentialStore.AddOrUpdate(service, account, token)
    │
    ▼
Platform-specific storage
    │
    ├─ Windows: credwrite() → Windows Credential Manager
    ├─ macOS: SecKeychainAddGenericPassword() → Keychain
    └─ Linux: D-Bus Secret Service API → libsecret
```

### Retrieve Token Flow
```
User runs: lopen chat "hello"
    │
    ▼
CopilotClient needs authentication
    │
    ▼
AuthService.GetTokenAsync()
    │
    ▼
ICredentialStore.GetTokenAsync()
    │
    ▼
SecureCredentialStore.GetTokenAsync()
    │
    ▼
GitCredentialManager.ICredentialStore.Get(service, account)
    │
    ▼
Platform-specific retrieval
    │
    ├─ Windows: credread() ← Windows Credential Manager
    ├─ macOS: SecKeychainFindGenericPassword() ← Keychain
    └─ Linux: D-Bus Secret Service API ← libsecret
    │
    ▼
Return ICredential.Password
```

## Implementation Layers

### Layer 1: Application Layer
```csharp
// CLI commands
lopen auth login   → Stores token
lopen auth status  → Checks token existence
lopen auth logout  → Clears token
lopen chat "..."   → Uses token
```

### Layer 2: Service Layer
```csharp
public class AuthService : IAuthService
{
    private readonly ICredentialStore _credentialStore;
    
    public async Task<string?> GetTokenAsync()
    {
        // 1. Check environment variable first
        var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
            return envToken;
            
        // 2. Check secure storage
        return await _credentialStore.GetTokenAsync();
    }
}
```

### Layer 3: Storage Abstraction Layer
```csharp
public interface ICredentialStore
{
    Task<string?> GetTokenAsync();
    Task StoreTokenAsync(string token);
    Task ClearAsync();
}
```

### Layer 4: Platform Adapter Layer
```csharp
public class SecureCredentialStore : ICredentialStore
{
    // Adapts ICredentialStore to GitCredentialManager.ICredentialStore
}
```

### Layer 5: Cross-Platform Library Layer
```csharp
// Devlooped.CredentialManager
// Git Credential Manager (embedded)
```

### Layer 6: OS Native Layer
```
Windows: advapi32.dll (DPAPI)
macOS: Security.framework
Linux: libsecret.so (via D-Bus)
```

## Dependency Injection Setup

```csharp
// Program.cs
services.AddSingleton<ICredentialStore, SecureCredentialStore>();
services.AddSingleton<IAuthService, AuthService>();
```

## Testing Architecture

```
┌────────────────────────────────────────┐
│         Unit Tests                     │
├────────────────────────────────────────┤
│                                        │
│  ┌─────────────┐    ┌───────────────┐ │
│  │ AuthService │───▶│ Mock          │ │
│  │   Tests     │    │ ICredential   │ │
│  │             │    │ Store         │ │
│  └─────────────┘    └───────────────┘ │
│                                        │
│  ┌─────────────┐    ┌───────────────┐ │
│  │SecureCredent│───▶│ Mock GCM      │ │
│  │ialStore     │    │ ICredential   │ │
│  │   Tests     │    │ Store         │ │
│  └─────────────┘    └───────────────┘ │
└────────────────────────────────────────┘

┌────────────────────────────────────────┐
│    Integration Tests                   │
├────────────────────────────────────────┤
│                                        │
│  ┌─────────────┐    ┌───────────────┐ │
│  │ E2E Auth    │───▶│ Real Secure   │ │
│  │   Tests     │    │ Credential    │ │
│  │             │    │ Store         │ │
│  └─────────────┘    └───────────────┘ │
│                           │            │
│                           ▼            │
│                  Platform-specific     │
│                     storage            │
└────────────────────────────────────────┘
```

## Migration Architecture

```
Old Architecture:                 New Architecture:
┌───────────────┐                ┌────────────────────┐
│ AuthService   │                │ AuthService        │
└───────┬───────┘                └─────────┬──────────┘
        │                                  │
        ▼                                  ▼
┌────────────────┐        ┌───────────────────────────┐
│ FileCredential │  ────▶ │ SecureCredentialStore     │
│ Store          │ migrate│                           │
│                │        │ + Migration logic:        │
│ Base64 encoding│        │   1. Check new store      │
│ ~/.lopen/      │        │   2. Check old store      │
│ credentials    │        │   3. Copy if needed       │
│ .json          │        │   4. Clear old store      │
└────────────────┘        └───────────┬───────────────┘
                                      │
                                      ▼
                          ┌────────────────────────────┐
                          │ Git Credential Manager     │
                          │ Platform-specific backends │
                          └────────────────────────────┘
```

## Fallback Strategy

```
Token Retrieval Priority:

1. Environment Variable (GITHUB_TOKEN)
   │
   ├─ Found? → Use it
   │
2. SecureCredentialStore (OS-native storage)
   │
   ├─ Found? → Use it
   ├─ Error? → Try fallback
   │
3. FileCredentialStore (Fallback, testing only)
   │
   ├─ Found? → Use it (with warning)
   │
4. None found → Prompt user to login
```

## Security Considerations

### Token Storage Security

| Layer | Security Measure |
|-------|-----------------|
| **Application** | - No token in code<br>- No token in logs<br>- Environment variable override |
| **Storage** | - OS-native encryption<br>- User-scoped access<br>- No world-readable files |
| **Transport** | - HTTPS only<br>- TLS 1.2+ |
| **Access** | - Per-user credentials<br>- No sharing between users<br>- Secure deletion on clear |

### Threat Model

| Threat | Mitigation |
|--------|-----------|
| **File access by other users** | OS-native storage with user-scoped permissions |
| **Memory dump attacks** | Token only in memory briefly, cleared after use |
| **Plaintext storage** | OS-native encryption (DPAPI, Keychain, libsecret) |
| **Token theft from backups** | Encrypted storage not portable to other machines |
| **Malware on same user** | Limited mitigation - malware with user privileges can access |

### Best Practices Implemented

1. ✅ Use OS-native secure storage
2. ✅ User-scoped credentials
3. ✅ Automatic encryption at rest
4. ✅ No plaintext storage
5. ✅ Secure deletion
6. ✅ Environment variable override for CI/CD
7. ✅ Minimal token lifetime in memory
8. ✅ Clear on logout

## Configuration

### Application Configuration
```json
// No configuration needed - platform auto-detected
```

### Environment Variables
```bash
# Override credential store backend (optional)
GCM_CREDENTIAL_STORE=dpapi          # Windows
GCM_CREDENTIAL_STORE=keychain       # macOS
GCM_CREDENTIAL_STORE=secretservice  # Linux

# Direct token override (CI/CD)
GITHUB_TOKEN=gho_xxxxxxxxxxxx
```

### Platform Requirements

| Platform | Requirements |
|----------|--------------|
| Windows | - Windows 7+ (DPAPI built-in)<br>- No additional packages |
| macOS | - macOS 10.9+ (Keychain built-in)<br>- No additional packages |
| Linux | - libsecret-1-0 package<br>- D-Bus session<br>- Keyring daemon (gnome-keyring, kwallet) |

## Performance Characteristics

| Operation | Windows | macOS | Linux | Notes |
|-----------|---------|-------|-------|-------|
| **Store** | ~5ms | ~10ms | ~20ms | One-time per auth |
| **Retrieve** | ~2ms | ~5ms | ~10ms | Per CLI invocation |
| **Clear** | ~2ms | ~5ms | ~10ms | One-time per logout |

All operations are synchronous and blocking, but fast enough for CLI usage.

## Monitoring & Observability

### Logging Strategy
```csharp
// Log successful operations (info level)
logger.LogInformation("Token stored securely using {Platform}", 
    OperatingSystem.IsWindows() ? "Windows Credential Manager" :
    OperatingSystem.IsMacOS() ? "macOS Keychain" : "libsecret");

// Log retrieval (without token value)
logger.LogInformation("Token retrieved from secure storage");

// Log errors (without token value)
logger.LogError("Failed to store token: {Error}", ex.Message);

// Never log token values
// ❌ logger.LogDebug("Token: {Token}", token);
```

### Metrics to Track
- Token storage success rate
- Token retrieval success rate
- Average retrieval time
- Platform distribution
- Fallback usage rate

## References

- **Research Document**: `SECURE_STORAGE_RESEARCH.md`
- **Implementation Guide**: `SECURE_STORAGE_IMPLEMENTATION.md`
- **Quick Summary**: `SECURE_STORAGE_SUMMARY.md`
- **Current Interface**: `src/Lopen.Core/CredentialStore.cs`
