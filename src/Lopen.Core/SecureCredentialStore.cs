using System.Text.Json;
using GitCredentialManager;

namespace Lopen.Core;

/// <summary>
/// Secure credential storage using platform-specific secure storage.
/// Windows: Windows Credential Manager (DPAPI)
/// macOS: Keychain
/// Linux: libsecret (Secret Service API)
/// </summary>
public class SecureCredentialStore : ICredentialStore, ITokenInfoStore
{
    private readonly GitCredentialManager.ICredentialStore _store;
    private const string Service = "github.com";
    private const string Account = "lopen-oauth-token";
    private const string TokenInfoAccount = "lopen-oauth-token-info";

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

    /// <summary>
    /// Constructor for testing with a custom store.
    /// </summary>
    internal SecureCredentialStore(GitCredentialManager.ICredentialStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<string?> GetTokenAsync()
    {
        try
        {
            var credential = _store.Get(Service, Account);
            return Task.FromResult(credential?.Password);
        }
        catch (Exception)
        {
            // Treat store access failure as no credential found
            return Task.FromResult<string?>(null);
        }
    }

    public Task<TokenInfo?> GetTokenInfoAsync()
    {
        try
        {
            var credential = _store.Get(Service, TokenInfoAccount);
            if (credential?.Password is null)
                return Task.FromResult<TokenInfo?>(null);

            var tokenInfo = JsonSerializer.Deserialize<TokenInfo>(credential.Password);
            return Task.FromResult(tokenInfo);
        }
        catch
        {
            return Task.FromResult<TokenInfo?>(null);
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

    public Task StoreTokenInfoAsync(TokenInfo tokenInfo)
    {
        ArgumentNullException.ThrowIfNull(tokenInfo);

        try
        {
            var json = JsonSerializer.Serialize(tokenInfo);
            _store.AddOrUpdate(Service, TokenInfoAccount, json);
            // Also store just the access token for backward compatibility
            _store.AddOrUpdate(Service, Account, tokenInfo.AccessToken);
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
        try
        {
            _store.Remove(Service, TokenInfoAccount);
        }
        catch
        {
            // Ignore if credential doesn't exist
        }
        return Task.CompletedTask;
    }

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
                _ = store.Get("lopen-test-service", "lopen-test-account");
            }
            catch (Exception ex)
            {
                // Check if this is a "not configured" error vs "not found" (expected)
                if (ex.Message.Contains("credential store") ||
                    ex.Message.Contains("GCM_CREDENTIAL_STORE"))
                {
                    return false;
                }
                // "Not found" or similar is expected - store is working
            }

            return true;
        }
        catch (Exception ex)
        {
            // Check if this is the "no credential store configured" error
            if (ex.Message.Contains("No credential store has been selected") ||
                ex.Message.Contains("credential store") ||
                ex.Message.Contains("GCM_CREDENTIAL_STORE"))
            {
                // GCM is installed but not configured - not available
                return false;
            }

            // Other errors (e.g., GCM not installed) also mean not available
            return false;
        }
    }
}

/// <summary>
/// Factory for creating the appropriate credential store based on platform/environment.
/// </summary>
public interface ICredentialStoreFactory
{
    /// <summary>
    /// Creates the most appropriate credential store for the current environment.
    /// </summary>
    ICredentialStore Create();
}

/// <summary>
/// Default credential store factory that prefers secure storage with file-based fallback.
/// </summary>
public class CredentialStoreFactory : ICredentialStoreFactory
{
    public ICredentialStore Create()
    {
        if (SecureCredentialStore.IsAvailable())
        {
            return new SecureCredentialStore();
        }

        // Fall back to file-based storage
        return new FileCredentialStore();
    }
}

/// <summary>
/// Migrates credentials from file-based storage to secure storage.
/// </summary>
public static class CredentialMigration
{
    /// <summary>
    /// Migrates existing credentials from file storage to secure storage if needed.
    /// </summary>
    /// <param name="secureStore">The secure credential store to migrate to.</param>
    /// <param name="fileStore">The file-based store to migrate from.</param>
    /// <returns>True if migration was performed, false if not needed.</returns>
    public static async Task<bool> MigrateIfNeededAsync(
        ICredentialStore secureStore,
        FileCredentialStore fileStore)
    {
        // Check if there's a token in the new store already
        var existingToken = await secureStore.GetTokenAsync();
        if (existingToken is not null)
            return false; // Already has credentials

        // Check if there's a token in the old store
        var oldToken = await fileStore.GetTokenAsync();
        if (oldToken is null)
            return false; // Nothing to migrate

        // Migrate
        await secureStore.StoreTokenAsync(oldToken);

        // Clear old store after successful migration
        await fileStore.ClearAsync();

        return true;
    }
}
