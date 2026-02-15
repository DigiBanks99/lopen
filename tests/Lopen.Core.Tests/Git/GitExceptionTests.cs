using Lopen.Core.Git;

namespace Lopen.Core.Tests.Git;

public class GitExceptionTests
{
    [Fact]
    public void Constructor_WithMessageAndCommand_SetsProperties()
    {
        var ex = new GitException("Failed to commit", "git commit");

        Assert.Equal("Failed to commit", ex.Message);
        Assert.Equal("git commit", ex.Command);
        Assert.Equal(0, ex.ExitCode);
        Assert.Equal(string.Empty, ex.StdErr);
    }

    [Fact]
    public void Constructor_WithExitCodeAndStdErr_SetsProperties()
    {
        var ex = new GitException("Failed", "git push", 128, "fatal: remote error");

        Assert.Equal("Failed", ex.Message);
        Assert.Equal("git push", ex.Command);
        Assert.Equal(128, ex.ExitCode);
        Assert.Equal("fatal: remote error", ex.StdErr);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsProperties()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new GitException("Outer", "git status", inner);

        Assert.Equal("Outer", ex.Message);
        Assert.Equal("git status", ex.Command);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(string.Empty, ex.StdErr);
    }

    [Fact]
    public void InheritsFromException()
    {
        var ex = new GitException("msg", "cmd");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
