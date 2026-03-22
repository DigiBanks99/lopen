using GitHub.Copilot.SDK;
using Lopen.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;

namespace Lopen.Llm.Tests;

public class CopilotClientProviderTests : IAsyncDisposable
{
    private CopilotClientProvider? _provider;

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }
    }

    [Fact]
    public void Constructor_NullTokenProvider_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotClientProvider(null!, NullLogger<CopilotClientProvider>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotClientProvider(new TestTokenProvider(null), null!));
    }

    [Fact]
    public async Task CreateClient_NoToken_CreatesClientWithoutExplicitToken()
    {
        string? originalCli = Environment.GetEnvironmentVariable("COPILOT_CLI");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"lopen-copilot-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "copilot.exe" : "copilot";
        string fakeCopilotPath = Path.Combine(tempDirectory, executableName);
        File.WriteAllText(fakeCopilotPath, "placeholder");

        Environment.SetEnvironmentVariable("PATH", tempDirectory);
        Environment.SetEnvironmentVariable("COPILOT_CLI", null);

        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);
        try
        {
            CopilotClient client = await _provider.CreateClientAsync();

            Assert.NotNull(client);
            client.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_CLI", originalCli);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateClient_WithToken_CreatesClientWithToken()
    {
        string? originalCli = Environment.GetEnvironmentVariable("COPILOT_CLI");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"lopen-copilot-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "copilot.exe" : "copilot";
        string fakeCopilotPath = Path.Combine(tempDirectory, executableName);
        File.WriteAllText(fakeCopilotPath, "placeholder");

        Environment.SetEnvironmentVariable("PATH", tempDirectory);
        Environment.SetEnvironmentVariable("COPILOT_CLI", null);

        var tokenProvider = new TestTokenProvider("test-github-token");
        _provider = new CopilotClientProvider(
            tokenProvider,
            NullLogger<CopilotClientProvider>.Instance);
        try
        {
            CopilotClient client = await _provider.CreateClientAsync();

            Assert.NotNull(client);
            client.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_CLI", originalCli);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void EnsureCopilotCliPathConfigured_PrefersExplicitEnvironmentVariable()
    {
        string? originalCli = Environment.GetEnvironmentVariable("COPILOT_CLI");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        string tempDirectory = Path.Combine(Path.GetTempPath(), $"lopen-copilot-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "copilot.exe" : "copilot";
        string explicitCopilotPath = Path.Combine(tempDirectory, executableName);
        File.WriteAllText(explicitCopilotPath, "placeholder");

        Environment.SetEnvironmentVariable("COPILOT_CLI", explicitCopilotPath);
        Environment.SetEnvironmentVariable("PATH", string.Empty);

        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);

        try
        {
            _provider.EnsureCopilotCliPathConfigured();

            Assert.Equal(explicitCopilotPath, Environment.GetEnvironmentVariable("COPILOT_CLI"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_CLI", originalCli);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void EnsureCopilotCliPathConfigured_SetsEnvironmentVariableFromPath()
    {
        string? originalCli = Environment.GetEnvironmentVariable("COPILOT_CLI");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        string tempDirectory = Path.Combine(Path.GetTempPath(), $"lopen-copilot-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "copilot.exe" : "copilot";
        string discoveredCopilotPath = Path.Combine(tempDirectory, executableName);
        File.WriteAllText(discoveredCopilotPath, "placeholder");

        Environment.SetEnvironmentVariable("COPILOT_CLI", null);
        Environment.SetEnvironmentVariable("PATH", tempDirectory);

        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);

        try
        {
            _provider.EnsureCopilotCliPathConfigured();

            Assert.Equal(discoveredCopilotPath, Environment.GetEnvironmentVariable("COPILOT_CLI"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_CLI", originalCli);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void EnsureCopilotCliPathConfigured_ThrowsWhenCliNotDiscoverable()
    {
        string? originalCli = Environment.GetEnvironmentVariable("COPILOT_CLI");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        Environment.SetEnvironmentVariable("COPILOT_CLI", null);
        Environment.SetEnvironmentVariable("PATH", string.Empty);

        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _provider.EnsureCopilotCliPathConfigured());

            Assert.Contains("Copilot CLI executable was not found on PATH", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_CLI", originalCli);
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public async Task GetClientAsync_AfterDispose_ThrowsObjectDisposed()
    {
        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);

        await _provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.GetClientAsync());
    }

    [Fact]
    public async Task IsAuthenticatedAsync_AfterDispose_ThrowsObjectDisposed()
    {
        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);

        await _provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _provider.IsAuthenticatedAsync());
    }

    [Fact]
    public async Task DisposeAsync_MultipleDispose_DoesNotThrow()
    {
        _provider = new CopilotClientProvider(
            new TestTokenProvider(null),
            NullLogger<CopilotClientProvider>.Instance);

        await _provider.DisposeAsync();
        await _provider.DisposeAsync(); // Should not throw
    }

    private sealed class TestTokenProvider(string? token) : IAuthTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
    }
}
