namespace Lopen.Auth.Tests;

public class AuthStatusResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var result = new AuthStatusResult(
            AuthState.Authenticated,
            AuthCredentialSource.GhToken,
            Username: "testuser",
            ErrorMessage: null);

        Assert.Equal(AuthState.Authenticated, result.State);
        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.Equal("testuser", result.Username);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Constructor_DefaultsOptionalPropertiesToNull()
    {
        var result = new AuthStatusResult(AuthState.NotAuthenticated, AuthCredentialSource.None);

        Assert.Null(result.Username);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GhToken, "user1");
        var b = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GhToken, "user1");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentState_AreNotEqual()
    {
        var a = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GhToken);
        var b = new AuthStatusResult(AuthState.NotAuthenticated, AuthCredentialSource.GhToken);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentSource_AreNotEqual()
    {
        var a = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GhToken);
        var b = new AuthStatusResult(AuthState.Authenticated, AuthCredentialSource.GitHubToken);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void NotAuthenticated_WithErrorMessage_PreservesMessage()
    {
        var result = new AuthStatusResult(
            AuthState.NotAuthenticated,
            AuthCredentialSource.None,
            ErrorMessage: "Not authenticated. Run 'lopen auth login' or set GH_TOKEN.");

        Assert.Equal(AuthState.NotAuthenticated, result.State);
        Assert.Contains("lopen auth login", result.ErrorMessage);
    }

    [Fact]
    public void InvalidCredentials_WithErrorMessage_PreservesMessage()
    {
        var result = new AuthStatusResult(
            AuthState.InvalidCredentials,
            AuthCredentialSource.GhToken,
            ErrorMessage: "PAT requires Copilot Requests permission.");

        Assert.Equal(AuthState.InvalidCredentials, result.State);
        Assert.Equal(AuthCredentialSource.GhToken, result.Source);
        Assert.Contains("Copilot Requests", result.ErrorMessage);
    }
}
