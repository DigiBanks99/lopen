using System.Text.Json;

namespace Lopen.Core;

/// <summary>
/// Interface for secure credential storage.
/// </summary>
public interface ICredentialStore
{
    Task<string?> GetTokenAsync();
    Task StoreTokenAsync(string token);
    Task ClearAsync();
}

/// <summary>
/// File-based credential storage with basic obfuscation.
/// For production, use OS credential managers (Windows Credential Manager, macOS Keychain, libsecret).
/// </summary>
public class FileCredentialStore : ICredentialStore
{
    private readonly string _credentialPath;

    public FileCredentialStore() : this(GetDefaultCredentialPath())
    {
    }

    public FileCredentialStore(string credentialPath)
    {
        _credentialPath = credentialPath ?? throw new ArgumentNullException(nameof(credentialPath));
    }

    private static string GetDefaultCredentialPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".lopen", "credentials.json");
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!File.Exists(_credentialPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_credentialPath);
            var credentials = JsonSerializer.Deserialize<CredentialData>(json);
            return credentials?.Token is not null ? Deobfuscate(credentials.Token) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task StoreTokenAsync(string token)
    {
        var directory = Path.GetDirectoryName(_credentialPath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var credentials = new CredentialData { Token = Obfuscate(token) };
        var json = JsonSerializer.Serialize(credentials);
        await File.WriteAllTextAsync(_credentialPath, json);

        // Set restrictive permissions on Unix
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_credentialPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public Task ClearAsync()
    {
        if (File.Exists(_credentialPath))
        {
            File.Delete(_credentialPath);
        }
        return Task.CompletedTask;
    }

    // Basic obfuscation (not encryption - for that, use DPAPI/Keychain)
    private static string Obfuscate(string value)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
    }

    private static string Deobfuscate(string value)
    {
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }

    private record CredentialData
    {
        public string? Token { get; init; }
    }
}
