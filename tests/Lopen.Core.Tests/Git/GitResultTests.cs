using Lopen.Core.Git;

namespace Lopen.Core.Tests.Git;

public class GitResultTests
{
    [Fact]
    public void Success_TrueWhenExitCodeZero()
    {
        var result = new GitResult(0, "output", "");
        Assert.True(result.Success);
    }

    [Fact]
    public void Success_FalseWhenExitCodeNonZero()
    {
        var result = new GitResult(1, "", "error");
        Assert.False(result.Success);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var result = new GitResult(128, "stdout", "stderr");

        Assert.Equal(128, result.ExitCode);
        Assert.Equal("stdout", result.StdOut);
        Assert.Equal("stderr", result.StdErr);
    }

    [Fact]
    public void Equality_WorksByValue()
    {
        var a = new GitResult(0, "out", "err");
        var b = new GitResult(0, "out", "err");
        Assert.Equal(a, b);
    }
}
