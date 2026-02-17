using System.CommandLine;
using Lopen.Auth;
using Lopen.Cli.Tests.Fakes;
using Lopen.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Cli.Tests.Commands;

public class AuthCommandTests
{
    private readonly FakeAuthService _fakeAuth = new();

    private (CommandLineConfiguration config, StringWriter output, StringWriter error) CreateConfig()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthService>(_fakeAuth);
        var provider = services.BuildServiceProvider();

        var output = new StringWriter();
        var error = new StringWriter();

        var root = new RootCommand("test");
        root.Add(AuthCommand.Create(provider, output, error));

        var config = new CommandLineConfiguration(root);
        return (config, output, error);
    }

    [Fact]
    public async Task Login_CallsLoginAsync()
    {
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "login"]);

        Assert.Equal(0, exitCode);
        Assert.True(_fakeAuth.LoginCalled);
        Assert.Contains("Login successful", output.ToString());
    }

    [Fact]
    public async Task Login_ReturnsExitCode1_OnException()
    {
        _fakeAuth.LoginException = new AuthenticationException("Auth failed");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "login"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Auth failed", error.ToString());
    }

    [Fact]
    public async Task Status_DisplaysAuthenticatedState()
    {
        _fakeAuth.StatusResult = new AuthStatusResult(
            AuthState.Authenticated,
            AuthCredentialSource.SdkCredentials,
            "testuser");
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "status"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("Authenticated", text);
        Assert.Contains("SdkCredentials", text);
        Assert.Contains("testuser", text);
    }

    [Fact]
    public async Task Status_DisplaysNotAuthenticatedState()
    {
        _fakeAuth.StatusResult = new AuthStatusResult(
            AuthState.NotAuthenticated,
            AuthCredentialSource.None);
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "status"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("NotAuthenticated", text);
        Assert.Contains("None", text);
        Assert.DoesNotContain("User:", text);
    }

    [Fact]
    public async Task Status_DisplaysErrorMessage()
    {
        _fakeAuth.StatusResult = new AuthStatusResult(
            AuthState.InvalidCredentials,
            AuthCredentialSource.GhToken,
            ErrorMessage: "Token expired");
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "status"]);

        Assert.Equal(0, exitCode);
        var text = output.ToString();
        Assert.Contains("InvalidCredentials", text);
        Assert.Contains("Token expired", text);
    }

    [Fact]
    public async Task Status_ReturnsExitCode1_OnException()
    {
        _fakeAuth.StatusException = new InvalidOperationException("Service unavailable");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "status"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Service unavailable", error.ToString());
    }

    [Fact]
    public async Task Logout_CallsLogoutAsync()
    {
        var (config, output, _) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "logout"]);

        Assert.Equal(0, exitCode);
        Assert.True(_fakeAuth.LogoutCalled);
        Assert.Contains("Logged out", output.ToString());
    }

    [Fact]
    public async Task Logout_ReturnsExitCode1_OnException()
    {
        _fakeAuth.LogoutException = new AuthenticationException("Logout failed");
        var (config, _, error) = CreateConfig();

        var exitCode = await config.InvokeAsync(["auth", "logout"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Logout failed", error.ToString());
    }
}
