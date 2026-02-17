using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Auth.Tests;

public class CopilotAuthServiceTests
{
    private readonly FakeTokenSourceResolver _tokenResolver = new();
    private readonly FakeGhCliAdapter _ghCli = new();
    private readonly NullLogger<CopilotAuthService> _logger = NullLogger<CopilotAuthService>.Instance;

    private CopilotAuthService CreateService(bool isInteractive = true) =>
        new(_tokenResolver, _ghCli, _logger, () => isInteractive);

    // === LoginAsync ===

    [Fact]
    public async Task LoginAsync_Interactive_GhAvailable_DelegatesDeviceFlow()
    {
        _ghCli.Available = true;
        _ghCli.LoginUsername = "testuser";

        var service = CreateService(isInteractive: true);
        await service.LoginAsync();

        Assert.True(_ghCli.LoginCalled);
    }

    [Fact]
    public async Task LoginAsync_NonInteractive_ThrowsHeadlessError()
    {
        var service = CreateService(isInteractive: false);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());

        Assert.Contains("non-interactive", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GH_TOKEN", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_GhNotAvailable_ThrowsGhCliNotFoundError()
    {
        _ghCli.Available = false;
        var service = CreateService(isInteractive: true);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());

        Assert.Contains("gh", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cli.github.com", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_GhLoginFails_PropagatesException()
    {
        _ghCli.Available = true;
        _ghCli.LoginThrows = true;

        var service = CreateService(isInteractive: true);
        await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());
    }

    // === GetStatusAsync ===

    [Fact]
    public async Task GetStatusAsync_GhTokenSet_ReturnsAuthenticatedViaGhToken()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "token123");

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, status.State);
        Assert.Equal(AuthCredentialSource.GhToken, status.Source);
    }

    [Fact]
    public async Task GetStatusAsync_GitHubTokenSet_ReturnsAuthenticatedViaGitHubToken()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GitHubToken, "token456");

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, status.State);
        Assert.Equal(AuthCredentialSource.GitHubToken, status.Source);
    }

    [Fact]
    public async Task GetStatusAsync_GhTokenTakesPrecedenceOverGitHubToken()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "gh_token");

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal(AuthCredentialSource.GhToken, status.Source);
        Assert.False(_ghCli.GetStatusCalled, "Should not call gh CLI when env var is available");
    }

    [Fact]
    public async Task GetStatusAsync_NoEnvVars_GhAuthenticated_ReturnsAuthenticatedViaSdk()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("ghuser", IsActive: true, "copilot, repo");

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, status.State);
        Assert.Equal(AuthCredentialSource.SdkCredentials, status.Source);
        Assert.Equal("ghuser", status.Username);
    }

    [Fact]
    public async Task GetStatusAsync_NoEnvVars_GhNotAuthenticated_ReturnsNotAuthenticated()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = null;

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.NotAuthenticated, status.State);
        Assert.Equal(AuthCredentialSource.None, status.Source);
        Assert.NotNull(status.ErrorMessage);
        Assert.Contains("lopen auth login", status.ErrorMessage);
    }

    [Fact]
    public async Task GetStatusAsync_EnvVarPresent_DoesNotCallGhCli()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "token");

        var service = CreateService();
        await service.GetStatusAsync();

        Assert.False(_ghCli.GetStatusCalled);
    }

    // === LogoutAsync ===

    [Fact]
    public async Task LogoutAsync_CallsGhLogout()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);

        var service = CreateService();
        await service.LogoutAsync();

        Assert.True(_ghCli.LogoutCalled);
    }

    [Fact]
    public async Task LogoutAsync_GhTokenStillSet_WarnsAboutEnvVar()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "token");
        var testLogger = new TestLogger<CopilotAuthService>();

        var service = new CopilotAuthService(_tokenResolver, _ghCli, testLogger, () => true);
        await service.LogoutAsync();

        Assert.True(_ghCli.LogoutCalled);
        Assert.Contains(testLogger.Messages, m => m.Contains("GH_TOKEN"));
    }

    [Fact]
    public async Task LogoutAsync_GitHubTokenStillSet_WarnsAboutEnvVar()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GitHubToken, "token");
        var testLogger = new TestLogger<CopilotAuthService>();

        var service = new CopilotAuthService(_tokenResolver, _ghCli, testLogger, () => true);
        await service.LogoutAsync();

        Assert.True(_ghCli.LogoutCalled);
        Assert.Contains(testLogger.Messages, m => m.Contains("GITHUB_TOKEN"));
    }

    [Fact]
    public async Task LogoutAsync_NoEnvVars_NoWarning()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        var testLogger = new TestLogger<CopilotAuthService>();

        var service = new CopilotAuthService(_tokenResolver, _ghCli, testLogger, () => true);
        await service.LogoutAsync();

        Assert.DoesNotContain(testLogger.Messages, m => m.Contains("GH_TOKEN") || m.Contains("GITHUB_TOKEN"));
    }

    // === ValidateAsync ===

    [Fact]
    public async Task ValidateAsync_Authenticated_DoesNotThrow()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "token");

        var service = CreateService();
        await service.ValidateAsync();
    }

    [Fact]
    public async Task ValidateAsync_NotAuthenticated_ThrowsWithGuidance()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = null;

        var service = CreateService();
        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.ValidateAsync());

        Assert.Contains("lopen auth login", ex.Message);
        Assert.Contains("GH_TOKEN", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_GhAuthenticated_DoesNotThrow()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("user", IsActive: true);
        _ghCli.CredentialsValid = true;

        var service = CreateService();
        await service.ValidateAsync();
    }

    // === InvalidCredentials ===

    [Fact]
    public async Task GetStatusAsync_GhCredsExpired_ReturnsInvalidCredentials()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("user", IsActive: true);
        _ghCli.CredentialsValid = false;

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.InvalidCredentials, status.State);
        Assert.Equal(AuthCredentialSource.SdkCredentials, status.Source);
        Assert.Equal("user", status.Username);
        Assert.NotNull(status.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_InvalidCredentials_ThrowsWithGuidance()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("user", IsActive: true);
        _ghCli.CredentialsValid = false;

        var service = CreateService();
        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.ValidateAsync());

        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lopen auth login", ex.Message);
    }

    // === Error message quality ===

    [Fact]
    public async Task HeadlessError_IncludesWhatWhyAndHowToFix()
    {
        var service = CreateService(isInteractive: false);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());

        // What
        Assert.Contains("Interactive login", ex.Message);
        // Why
        Assert.Contains("TTY", ex.Message);
        // How to fix
        Assert.Contains("GH_TOKEN", ex.Message);
        Assert.Contains("Copilot Requests", ex.Message);
    }

    [Fact]
    public async Task NotAuthenticatedError_IncludesWhatWhyAndHowToFix()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = null;

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.NotNull(status.ErrorMessage);
        // What
        Assert.Contains("Not authenticated", status.ErrorMessage);
        // Why
        Assert.Contains("environment variables", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        // How to fix
        Assert.Contains("lopen auth login", status.ErrorMessage);
    }

    // === Constructor validation ===

    [Fact]
    public void Constructor_NullTokenResolver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotAuthService(null!, _ghCli, _logger, () => true));
    }

    [Fact]
    public void Constructor_NullGhCli_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotAuthService(_tokenResolver, null!, _logger, () => true));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotAuthService(_tokenResolver, _ghCli, null!, () => true));
    }

    [Fact]
    public void Constructor_NullIsInteractive_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CopilotAuthService(_tokenResolver, _ghCli, _logger, null!));
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
        public bool LoginThrows { get; set; }
        public GhAuthStatusInfo? StatusInfo { get; set; } = new("testuser", true);
        public bool CredentialsValid { get; set; } = true;
        public bool LoginCalled { get; private set; }
        public bool LogoutCalled { get; private set; }
        public bool GetStatusCalled { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Available);

        public Task<string> LoginAsync(CancellationToken cancellationToken = default)
        {
            LoginCalled = true;
            if (LoginThrows)
            {
                throw new AuthenticationException(AuthErrorMessages.LoginFailed);
            }

            return Task.FromResult(LoginUsername);
        }

        public Task<GhAuthStatusInfo?> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            GetStatusCalled = true;
            return Task.FromResult(StatusInfo);
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCalled = true;
            return Task.CompletedTask;
        }

        public Task<bool> ValidateCredentialsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CredentialsValid);
    }

    private sealed class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
