namespace Lopen.Auth.Tests;

public class AuthErrorMessagesTests
{
    [Theory]
    [InlineData(nameof(AuthErrorMessages.NotAuthenticated))]
    [InlineData(nameof(AuthErrorMessages.HeadlessLoginNotSupported))]
    [InlineData(nameof(AuthErrorMessages.GhCliNotFound))]
    [InlineData(nameof(AuthErrorMessages.LoginFailed))]
    [InlineData(nameof(AuthErrorMessages.InvalidCredentials))]
    [InlineData(nameof(AuthErrorMessages.InvalidPat))]
    [InlineData(nameof(AuthErrorMessages.PreFlightFailed))]
    public void AllErrorMessages_ContainWhyAndFix(string fieldName)
    {
        var field = typeof(AuthErrorMessages).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(field);

        var message = (string)field.GetValue(null)!;
        Assert.Contains("Why:", message);
        Assert.Contains("Fix:", message);
    }

    [Fact]
    public void HeadlessLoginNotSupported_MentionsCopilotRequestsPermission()
    {
        Assert.Contains("Copilot Requests", AuthErrorMessages.HeadlessLoginNotSupported);
    }

    [Fact]
    public void InvalidPat_MentionsCopilotRequestsPermission()
    {
        Assert.Contains("Copilot Requests", AuthErrorMessages.InvalidPat);
    }

    [Fact]
    public void InvalidPat_IncludesPatCreationUrl()
    {
        Assert.Contains("github.com/settings/personal-access-tokens", AuthErrorMessages.InvalidPat);
    }

    [Fact]
    public void GhCliNotFound_MentionsInstallUrl()
    {
        Assert.Contains("cli.github.com", AuthErrorMessages.GhCliNotFound);
    }

    [Fact]
    public void EnvVarStillSet_IncludesVariableName()
    {
        var message = AuthErrorMessages.EnvVarStillSet("GH_TOKEN");
        Assert.Contains("GH_TOKEN", message);
        Assert.Contains("Why:", message);
        Assert.Contains("Fix:", message);
    }

    [Fact]
    public void EnvVarStillSet_GitHubToken_IncludesVariableName()
    {
        var message = AuthErrorMessages.EnvVarStillSet("GITHUB_TOKEN");
        Assert.Contains("GITHUB_TOKEN", message);
    }

    [Fact]
    public void NotAuthenticated_IncludesLoginGuidance()
    {
        Assert.Contains("lopen auth login", AuthErrorMessages.NotAuthenticated);
        Assert.Contains("GH_TOKEN", AuthErrorMessages.NotAuthenticated);
    }

    [Fact]
    public void PreFlightFailed_IncludesLoginGuidance()
    {
        Assert.Contains("lopen auth login", AuthErrorMessages.PreFlightFailed);
        Assert.Contains("GH_TOKEN", AuthErrorMessages.PreFlightFailed);
    }
}
