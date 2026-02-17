namespace Lopen.Auth.Tests;

public class GhCliAdapterTests
{
    // === ParseStatusOutput ===

    [Fact]
    public void ParseStatusOutput_ValidOutput_ExtractsUsername()
    {
        var output = """
            github.com
              ✓ Logged in to github.com account testuser (/home/user/.config/gh/hosts.yml)
              - Active account: true
              - Git operations protocol: https
              - Token: gho_****
              - Token scopes: copilot, gist, read:org, repo
            """;

        var result = GhCliAdapter.ParseStatusOutput(output);

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
        Assert.True(result.IsActive);
        Assert.Contains("copilot", result.TokenScopes!);
    }

    [Fact]
    public void ParseStatusOutput_InactiveAccount_IsActiveFalse()
    {
        var output = """
            github.com
              ✓ Logged in to github.com account anotheruser (/home/user/.config/gh/hosts.yml)
              - Active account: false
              - Git operations protocol: https
            """;

        var result = GhCliAdapter.ParseStatusOutput(output);

        Assert.NotNull(result);
        Assert.Equal("anotheruser", result.Username);
        Assert.False(result.IsActive);
    }

    [Fact]
    public void ParseStatusOutput_NoScopes_ScopesNull()
    {
        var output = """
            github.com
              ✓ Logged in to github.com account user123 (some/path)
              - Active account: true
            """;

        var result = GhCliAdapter.ParseStatusOutput(output);

        Assert.NotNull(result);
        Assert.Equal("user123", result.Username);
        Assert.Null(result.TokenScopes);
    }

    [Fact]
    public void ParseStatusOutput_EmptyOutput_ReturnsNull()
    {
        var result = GhCliAdapter.ParseStatusOutput("");
        Assert.Null(result);
    }

    [Fact]
    public void ParseStatusOutput_NullOutput_ReturnsNull()
    {
        var result = GhCliAdapter.ParseStatusOutput(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ParseStatusOutput_WhitespaceOutput_ReturnsNull()
    {
        var result = GhCliAdapter.ParseStatusOutput("   \n\t  ");
        Assert.Null(result);
    }

    [Fact]
    public void ParseStatusOutput_NotLoggedIn_ReturnsNull()
    {
        var output = """
            You are not logged into any GitHub hosts. To log in, run: gh auth login
            """;

        var result = GhCliAdapter.ParseStatusOutput(output);
        Assert.Null(result);
    }

    [Fact]
    public void ParseStatusOutput_DifferentFormat_ExtractsAccount()
    {
        // Some gh versions use slightly different output
        var output = "Logged in to github.com account MyUser (token)";

        var result = GhCliAdapter.ParseStatusOutput(output);

        Assert.NotNull(result);
        Assert.Equal("MyUser", result.Username);
    }

    [Fact]
    public void ParseStatusOutput_MultipleScopes_AllCaptured()
    {
        var output = """
            github.com
              ✓ Logged in to github.com account devuser (keyring)
              - Active account: true
              - Token scopes: admin:org, copilot, gist, read:org, repo, workflow
            """;

        var result = GhCliAdapter.ParseStatusOutput(output);

        Assert.NotNull(result);
        Assert.Contains("copilot", result.TokenScopes!);
        Assert.Contains("workflow", result.TokenScopes!);
    }

    // === GhAuthStatusInfo record ===

    [Fact]
    public void GhAuthStatusInfo_RecordEquality()
    {
        var a = new GhAuthStatusInfo("user", true, "copilot");
        var b = new GhAuthStatusInfo("user", true, "copilot");

        Assert.Equal(a, b);
    }

    [Fact]
    public void GhAuthStatusInfo_DifferentUsername_NotEqual()
    {
        var a = new GhAuthStatusInfo("user1", true);
        var b = new GhAuthStatusInfo("user2", true);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GhAuthStatusInfo_DefaultScopes_IsNull()
    {
        var info = new GhAuthStatusInfo("user", true);
        Assert.Null(info.TokenScopes);
    }
}
