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

    /// <summary>
    /// Checks if secure storage is available on this platform.
    /// </summary>
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
