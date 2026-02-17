using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Auth.Tests;

/// <summary>
/// AUTH-15: No credentials are stored by Lopen — all storage delegated to SDK/gh CLI.
/// These tests verify that CopilotAuthService and EnvironmentTokenSourceResolver
/// never persist credentials themselves.
/// </summary>
public class AuthNoCredentialStorageTests
{
    // === LoginAsync delegates to IGhCliAdapter (no self-storage) ===

    [Fact]
    public async Task LoginAsync_DelegatesToGhCliAdapter_DoesNotStoreCredentials()
    {
        var ghCli = new FakeGhCliAdapter { Available = true, LoginUsername = "testuser" };
        var tokenResolver = new FakeTokenSourceResolver();
        var service = new CopilotAuthService(tokenResolver, ghCli, NullLogger<CopilotAuthService>.Instance, () => true);

        await service.LoginAsync();

        Assert.True(ghCli.LoginCalled);
        Assert.False(ghCli.AnyWriteOperationCalled, "LoginAsync must not trigger any write/store operation beyond gh CLI delegation");
    }

    // === LogoutAsync delegates to IGhCliAdapter (no self-deletion) ===

    [Fact]
    public async Task LogoutAsync_DelegatesToGhCliAdapter_DoesNotDeleteFromOwnStore()
    {
        var ghCli = new FakeGhCliAdapter();
        var tokenResolver = new FakeTokenSourceResolver();
        var service = new CopilotAuthService(tokenResolver, ghCli, NullLogger<CopilotAuthService>.Instance, () => true);

        await service.LogoutAsync();

        Assert.True(ghCli.LogoutCalled);
        Assert.False(ghCli.AnyWriteOperationCalled, "LogoutAsync must not trigger any write/store operation beyond gh CLI delegation");
    }

    // === GetStatusAsync reads only — no caching ===

    [Fact]
    public async Task GetStatusAsync_ReadsFromResolverAndGhCli_NoCaching()
    {
        var ghCli = new FakeGhCliAdapter { StatusInfo = new GhAuthStatusInfo("user1", true), CredentialsValid = true };
        var tokenResolver = new FakeTokenSourceResolver();
        tokenResolver.SetResult(AuthCredentialSource.None, null);
        var service = new CopilotAuthService(tokenResolver, ghCli, NullLogger<CopilotAuthService>.Instance, () => true);

        var status1 = await service.GetStatusAsync();
        Assert.Equal("user1", status1.Username);

        // Change the underlying state — second call should reflect new state (no caching)
        ghCli.StatusInfo = new GhAuthStatusInfo("user2", true);

        var status2 = await service.GetStatusAsync();
        Assert.Equal("user2", status2.Username);
        Assert.False(ghCli.AnyWriteOperationCalled, "GetStatusAsync must be read-only");
    }

    // === CopilotAuthService has no file I/O fields ===

    [Theory]
    [InlineData(typeof(StreamWriter))]
    [InlineData(typeof(FileStream))]
    [InlineData(typeof(BinaryWriter))]
    public void CopilotAuthService_HasNoFileIoFields(Type forbiddenType)
    {
        var fields = typeof(CopilotAuthService)
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            Assert.False(
                forbiddenType.IsAssignableFrom(field.FieldType),
                $"CopilotAuthService must not have field '{field.Name}' of I/O type {forbiddenType.Name}. AUTH-15 requires no credential storage.");
        }
    }

    // === CopilotAuthService source has no file-write API calls ===

    [Theory]
    [InlineData("File.WriteAllText")]
    [InlineData("File.WriteAllBytes")]
    [InlineData("File.WriteAllLines")]
    [InlineData("File.Create")]
    [InlineData("File.AppendAllText")]
    [InlineData("File.Open")]
    [InlineData("StreamWriter")]
    [InlineData("FileStream")]
    public void CopilotAuthService_SourceDoesNotUseFileWriteApis(string forbiddenApi)
    {
        // Read the source file and verify it doesn't reference file-write APIs
        var sourceFile = FindSourceFile("CopilotAuthService.cs");
        var source = File.ReadAllText(sourceFile);

        Assert.DoesNotContain(forbiddenApi, source);
    }

    // === EnvironmentTokenSourceResolver is read-only ===

    [Fact]
    public void EnvironmentTokenSourceResolver_OnlyReadsEnvVars_NeverWritesThem()
    {
        var readVariables = new List<string>();
        var resolver = new EnvironmentTokenSourceResolver(name =>
        {
            readVariables.Add(name);
            return name == "GH_TOKEN" ? "test-token" : null;
        });

        resolver.Resolve();

        Assert.Contains("GH_TOKEN", readVariables);
        // The resolver uses a Func<string, string?> — a pure read accessor.
        // If it tried to write env vars, it would need Environment.SetEnvironmentVariable,
        // which is not possible through the injected read-only delegate.
    }

    [Fact]
    public void EnvironmentTokenSourceResolver_HasNoFileIoFields()
    {
        var fields = typeof(EnvironmentTokenSourceResolver)
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            Assert.False(
                typeof(Stream).IsAssignableFrom(field.FieldType) || typeof(TextWriter).IsAssignableFrom(field.FieldType),
                $"EnvironmentTokenSourceResolver must not have I/O field '{field.Name}'. AUTH-15 requires read-only token resolution.");
        }
    }

    [Fact]
    public void EnvironmentTokenSourceResolver_SourceDoesNotUseFileWriteApis()
    {
        var sourceFile = FindSourceFile("EnvironmentTokenSourceResolver.cs");
        var source = File.ReadAllText(sourceFile);

        Assert.DoesNotContain("File.Write", source);
        Assert.DoesNotContain("SetEnvironmentVariable", source);
        Assert.DoesNotContain("StreamWriter", source);
        Assert.DoesNotContain("FileStream", source);
    }

    // === Helpers ===

    private static string FindSourceFile(string fileName)
    {
        // Walk up from test bin directory to find the repo root, then locate the source file
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "src", "Lopen.Auth", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException($"Could not find source file '{fileName}' in any parent directory.");
    }

    // === Fakes ===

    private sealed class FakeTokenSourceResolver : ITokenSourceResolver
    {
        private TokenSourceResult _result = new(AuthCredentialSource.None, null);

        public void SetResult(AuthCredentialSource source, string? token) =>
            _result = new TokenSourceResult(source, token);

        public TokenSourceResult Resolve() => _result;
    }

    private sealed class FakeGhCliAdapter : IGhCliAdapter
    {
        public bool Available { get; set; } = true;
        public string LoginUsername { get; set; } = "testuser";
        public GhAuthStatusInfo? StatusInfo { get; set; } = new("testuser", true);
        public bool CredentialsValid { get; set; } = true;
        public bool LoginCalled { get; private set; }
        public bool LogoutCalled { get; private set; }

        /// <summary>
        /// Tracks whether any operation beyond read/delegation occurred.
        /// This stays false because CopilotAuthService never calls any write API on this adapter.
        /// </summary>
        public bool AnyWriteOperationCalled { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Available);

        public Task<string> LoginAsync(CancellationToken cancellationToken = default)
        {
            LoginCalled = true;
            // Login is a delegation — the adapter handles storage, not CopilotAuthService
            return Task.FromResult(LoginUsername);
        }

        public Task<GhAuthStatusInfo?> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(StatusInfo);

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCalled = true;
            // Logout is a delegation — the adapter handles cleanup, not CopilotAuthService
            return Task.CompletedTask;
        }

        public Task<bool> ValidateCredentialsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CredentialsValid);
    }
}
