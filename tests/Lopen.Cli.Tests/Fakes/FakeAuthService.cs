using Lopen.Auth;

namespace Lopen.Cli.Tests.Fakes;

internal sealed class FakeAuthService : IAuthService
{
    public bool LoginCalled { get; private set; }
    public bool LogoutCalled { get; private set; }
    public bool GetStatusCalled { get; private set; }
    public bool ValidateCalled { get; private set; }

    public AuthStatusResult StatusResult { get; set; } = new(
        AuthState.Authenticated,
        AuthCredentialSource.SdkCredentials,
        "testuser");

    public Exception? LoginException { get; set; }
    public Exception? LogoutException { get; set; }
    public Exception? StatusException { get; set; }

    public Task LoginAsync(CancellationToken cancellationToken = default)
    {
        LoginCalled = true;
        if (LoginException is not null)
            throw LoginException;
        return Task.CompletedTask;
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        LogoutCalled = true;
        if (LogoutException is not null)
            throw LogoutException;
        return Task.CompletedTask;
    }

    public Task<AuthStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        GetStatusCalled = true;
        if (StatusException is not null)
            throw StatusException;
        return Task.FromResult(StatusResult);
    }

    public Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        ValidateCalled = true;
        return Task.CompletedTask;
    }
}
