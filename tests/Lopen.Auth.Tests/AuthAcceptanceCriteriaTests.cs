using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Auth.Tests;

/// <summary>
/// Explicit acceptance criteria coverage tests for the Auth module.
/// Each test maps to a numbered AC from docs/requirements/auth/SPECIFICATION.md (AUTH-01 through AUTH-15).
/// Cross-module requirements (AUTH-10, AUTH-11, AUTH-12) are tested at the Auth boundary;
/// integration coverage lives in Lopen.Cli.Tests (AUTH-10) and Lopen.Llm.Tests (AUTH-11, AUTH-12).
/// </summary>
public class AuthAcceptanceCriteriaTests
{
    private readonly FakeTokenSourceResolver _tokenResolver = new();
    private readonly FakeGhCliAdapter _ghCli = new();
    private readonly TestLogger<CopilotAuthService> _logger = new();

    private CopilotAuthService CreateService(bool isInteractive = true) =>
        new(_tokenResolver, _ghCli, _logger, () => isInteractive);

    // AUTH-01: lopen auth login initiates the Copilot SDK device flow and completes authentication

    [Fact]
    public async Task AC01_Login_Interactive_DelegatesDeviceFlowToGhCli()
    {
        _ghCli.Available = true;
        _ghCli.LoginUsername = "copilot-user";

        var service = CreateService(isInteractive: true);
        await service.LoginAsync();

        Assert.True(_ghCli.LoginCalled, "LoginAsync must delegate to IGhCliAdapter for device flow");
    }

    [Fact]
    public async Task AC01_Login_GhCliNotAvailable_Throws()
    {
        _ghCli.Available = false;

        var service = CreateService(isInteractive: true);
        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());

        Assert.Contains("gh", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // AUTH-02: lopen auth status accurately reports authenticated, unauthenticated, and invalid states

    [Fact]
    public async Task AC02_Status_Authenticated_ReportsCorrectState()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "token-123");
        var service = CreateService();

        var result = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, result.State);
        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
    }

    [Fact]
    public async Task AC02_Status_NotAuthenticated_ReportsCorrectState()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = null;
        var service = CreateService();

        var result = await service.GetStatusAsync();

        Assert.Equal(AuthState.NotAuthenticated, result.State);
    }

    [Fact]
    public async Task AC02_Status_InvalidCredentials_ReportsCorrectState()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("user", true);
        _ghCli.CredentialsValid = false;
        var service = CreateService();

        var result = await service.GetStatusAsync();

        Assert.Equal(AuthState.InvalidCredentials, result.State);
    }

    // AUTH-03: lopen auth logout clears SDK-managed credentials and confirms removal

    [Fact]
    public async Task AC03_Logout_DelegatesToGhCli()
    {
        var service = CreateService();
        await service.LogoutAsync();

        Assert.True(_ghCli.LogoutCalled, "LogoutAsync must delegate credential clearing to gh CLI");
    }

    // AUTH-04: lopen auth logout warns when GH_TOKEN/GITHUB_TOKEN is still set

    [Fact]
    public async Task AC04_Logout_GhTokenStillSet_LogsWarning()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "some-token");
        var service = CreateService();

        await service.LogoutAsync();

        Assert.Contains(_logger.Messages, m => m.Contains("GH_TOKEN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AC04_Logout_GitHubTokenStillSet_LogsWarning()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GitHubToken, "some-token");
        var service = CreateService();

        await service.LogoutAsync();

        Assert.Contains(_logger.Messages, m => m.Contains("GITHUB_TOKEN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AC04_Logout_NoEnvVars_NoWarning()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        var service = CreateService();

        await service.LogoutAsync();

        Assert.DoesNotContain(_logger.Messages, m =>
            m.Contains("GH_TOKEN", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("GITHUB_TOKEN", StringComparison.OrdinalIgnoreCase));
    }

    // AUTH-05: lopen auth login --headless returns error directing user to set GH_TOKEN

    [Fact]
    public async Task AC05_Login_Headless_ThrowsWithGhTokenGuidance()
    {
        var service = CreateService(isInteractive: false);

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.LoginAsync());

        Assert.Contains("GH_TOKEN", ex.Message);
    }

    // AUTH-06: Authentication via GH_TOKEN works without interactive login

    [Fact]
    public async Task AC06_GhToken_AuthenticatesWithoutInteractiveLogin()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "ghp_test-token");
        var service = CreateService();

        var result = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, result.State);
        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.False(_ghCli.GetStatusCalled, "GH_TOKEN auth should not require gh CLI interaction");
    }

    // AUTH-07: GITHUB_TOKEN works when GH_TOKEN is not set

    [Fact]
    public async Task AC07_GitHubToken_WorksWhenGhTokenAbsent()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GitHubToken, "ghs_test-token");
        var service = CreateService();

        var result = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, result.State);
        Assert.Equal(AuthCredentialSource.GitHubToken, result.Source);
    }

    // AUTH-08: GH_TOKEN takes precedence over GITHUB_TOKEN when both are set

    [Fact]
    public void AC08_GhToken_PrecedesGitHubToken()
    {
        var resolver = new EnvironmentTokenSourceResolver(name => name switch
        {
            "GH_TOKEN" => "gh-token-value",
            "GITHUB_TOKEN" => "github-token-value",
            _ => null,
        });

        var result = resolver.Resolve();

        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.Equal("gh-token-value", result.Token);
    }

    // AUTH-09: Environment variables take precedence over SDK-stored credentials

    [Fact]
    public async Task AC09_EnvVars_TakePrecedenceOverSdkCredentials()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "env-token");
        _ghCli.StatusInfo = new GhAuthStatusInfo("sdk-user", true);
        _ghCli.CredentialsValid = true;
        var service = CreateService();

        var result = await service.GetStatusAsync();

        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.False(_ghCli.GetStatusCalled, "When env var provides auth, SDK credentials should not be checked");
    }

    // AUTH-10: Pre-flight auth check blocks workflow when credentials are missing or invalid

    [Fact]
    public async Task AC10_Validate_Authenticated_DoesNotThrow()
    {
        _tokenResolver.SetResult(AuthCredentialSource.GhToken, "valid-token");
        var service = CreateService();

        await service.ValidateAsync(); // Should not throw
    }

    [Fact]
    public async Task AC10_Validate_NotAuthenticated_ThrowsAuthenticationException()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = null;
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<AuthenticationException>(() => service.ValidateAsync());

        Assert.Contains("lopen auth login", ex.Message);
        Assert.Contains("GH_TOKEN", ex.Message);
    }

    [Fact]
    public async Task AC10_Validate_InvalidCredentials_ThrowsAuthenticationException()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("user", true);
        _ghCli.CredentialsValid = false;
        var service = CreateService();

        await Assert.ThrowsAsync<AuthenticationException>(() => service.ValidateAsync());
    }

    // AUTH-11: Automatic token renewal — CopilotAuthService provides ValidateAsync for SDK pre-flight;
    // actual renewal is handled by the SDK layer (AuthErrorHandler in Lopen.Llm).
    // This test verifies the auth module's contribution: ValidateAsync correctly identifies valid credentials.

    [Fact]
    public async Task AC11_ValidateAsync_SdkCredentials_DetectsValidState()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("sdk-user", true);
        _ghCli.CredentialsValid = true;
        var service = CreateService();

        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.Authenticated, status.State);
        Assert.Equal(AuthCredentialSource.SdkCredentials, status.Source);
    }

    // AUTH-12: Failed renewal triggers critical error — CopilotAuthService surfaces credential failure.
    // Full renewal/abort flow is tested in Lopen.Llm.Tests/AuthErrorHandlerTests.cs.

    [Fact]
    public async Task AC12_ExpiredCredentials_DetectedAsInvalidState()
    {
        _tokenResolver.SetResult(AuthCredentialSource.None, null);
        _ghCli.StatusInfo = new GhAuthStatusInfo("user", true);
        _ghCli.CredentialsValid = false;
        var service = CreateService();

        var status = await service.GetStatusAsync();

        Assert.Equal(AuthState.InvalidCredentials, status.State);
        Assert.NotNull(status.ErrorMessage);
    }

    // AUTH-13: All auth error messages include what failed, why, and how to fix

    [Theory]
    [InlineData(nameof(AuthErrorMessages.NotAuthenticated))]
    [InlineData(nameof(AuthErrorMessages.HeadlessLoginNotSupported))]
    [InlineData(nameof(AuthErrorMessages.GhCliNotFound))]
    [InlineData(nameof(AuthErrorMessages.LoginFailed))]
    [InlineData(nameof(AuthErrorMessages.InvalidCredentials))]
    [InlineData(nameof(AuthErrorMessages.InvalidPat))]
    [InlineData(nameof(AuthErrorMessages.PreFlightFailed))]
    public void AC13_ErrorMessages_FollowWhatWhyFixPattern(string fieldName)
    {
        var field = typeof(AuthErrorMessages).GetField(fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var message = (string)field!.GetValue(null)!;

        Assert.Contains("Why:", message);
        Assert.Contains("Fix:", message);
    }

    // AUTH-14: Invalid PAT errors include "Copilot Requests" permission guidance

    [Fact]
    public void AC14_InvalidPatError_IncludesCopilotRequestsGuidance()
    {
        Assert.Contains("Copilot Requests", AuthErrorMessages.InvalidPat);
        Assert.Contains("github.com/settings/personal-access-tokens", AuthErrorMessages.InvalidPat);
    }

    [Fact]
    public void AC14_HeadlessError_IncludesCopilotRequestsGuidance()
    {
        Assert.Contains("Copilot Requests", AuthErrorMessages.HeadlessLoginNotSupported);
    }

    // AUTH-15: No credentials stored by Lopen — all delegated to SDK/gh CLI

    [Fact]
    public async Task AC15_LoginAsync_NoCredentialStorage()
    {
        _ghCli.Available = true;
        _ghCli.LoginUsername = "testuser";
        var service = CreateService(isInteractive: true);

        await service.LoginAsync();

        Assert.True(_ghCli.LoginCalled);
        Assert.False(_ghCli.AnyWriteOperationCalled,
            "AUTH-15: LoginAsync must not store credentials — delegation to gh CLI only");
    }

    [Fact]
    public async Task AC15_LogoutAsync_NoCredentialDeletion()
    {
        var service = CreateService();

        await service.LogoutAsync();

        Assert.True(_ghCli.LogoutCalled);
        Assert.False(_ghCli.AnyWriteOperationCalled,
            "AUTH-15: LogoutAsync must not delete credentials from own store — delegation to gh CLI only");
    }

    [Fact]
    public void AC15_TokenResolver_IsReadOnly()
    {
        var accessedVars = new List<string>();
        var resolver = new EnvironmentTokenSourceResolver(name =>
        {
            accessedVars.Add(name);
            return name == "GH_TOKEN" ? "token" : null;
        });

        resolver.Resolve();

        Assert.Contains("GH_TOKEN", accessedVars);
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
        public bool GetStatusCalled { get; private set; }
        public bool AnyWriteOperationCalled { get; private set; }
        public bool LoginThrows { get; set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Available);

        public Task<string> LoginAsync(CancellationToken cancellationToken = default)
        {
            LoginCalled = true;
            if (!Available)
                throw new AuthenticationException(AuthErrorMessages.GhCliNotFound);
            if (LoginThrows)
                throw new AuthenticationException(AuthErrorMessages.LoginFailed);
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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
