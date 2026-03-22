using Lopen.Auth;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Lopen.Commands;

/// <summary>
/// Defines the 'auth' command group with login, status, and logout subcommands.
/// </summary>
public static class AuthCommand
{
    public static Command Create(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        TextWriter stdout = output ?? Console.Out;
        TextWriter stderr = error ?? Console.Error;

        Command auth = new("auth", "Manage authentication")
        {
            CreateLoginCommand(services, stdout, stderr),
            CreateStatusCommand(services, stdout, stderr),
            CreateLogoutCommand(services, stdout, stderr)
        };

        return auth;
    }

    private static Command CreateLoginCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        Command login = new("login", "Authenticate via Copilot SDK device flow");
        login.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            // AUTH-05: --headless flag explicitly blocks interactive login
            if (parseResult.GetValue(GlobalOptions.Headless))
            {
                await stderr.WriteLineAsync(AuthErrorMessages.HeadlessLoginNotSupported);
                return 1;
            }

            IAuthService authService = services.GetRequiredService<IAuthService>();
            try
            {
                await authService.LoginAsync(cancellationToken);
                await stdout.WriteLineAsync("Login successful.");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return login;
    }

    private static Command CreateStatusCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        Command status = new("status", "Check current authentication state");
        status.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            IAuthService authService = services.GetRequiredService<IAuthService>();
            try
            {
                AuthStatusResult result = await authService.GetStatusAsync(cancellationToken);
                await stdout.WriteLineAsync($"State:  {result.State}");
                await stdout.WriteLineAsync($"Source: {result.Source}");
                if (result.Username is not null)
                    await stdout.WriteLineAsync($"User:   {result.Username}");
                if (result.ErrorMessage is not null)
                    await stdout.WriteLineAsync($"Error:  {result.ErrorMessage}");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return status;
    }

    private static Command CreateLogoutCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        Command logout = new("logout", "Clear stored credentials");
        logout.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            IAuthService authService = services.GetRequiredService<IAuthService>();
            try
            {
                await authService.LogoutAsync(cancellationToken);
                await stdout.WriteLineAsync("Logged out successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return logout;
    }
}
