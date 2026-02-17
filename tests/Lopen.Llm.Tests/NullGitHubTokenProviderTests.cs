namespace Lopen.Llm.Tests;

public class NullGitHubTokenProviderTests
{
    [Fact]
    public void GetToken_ReturnsNull()
    {
        var provider = new NullGitHubTokenProvider();

        Assert.Null(provider.GetToken());
    }
}
