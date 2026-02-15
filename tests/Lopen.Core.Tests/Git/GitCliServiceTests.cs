using Lopen.Core.Git;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Git;

public class GitCliServiceTests
{
    private readonly GitCliService _service = new(NullLogger<GitCliService>.Instance);

    [Fact]
    public async Task CommitAllAsync_ThrowsGitException()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() =>
            _service.CommitAllAsync("test commit"));

        Assert.Equal("git commit", ex.Command);
        Assert.Contains("pending", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBranchAsync_ThrowsGitException()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() =>
            _service.CreateBranchAsync("lopen/auth"));

        Assert.Equal("git checkout -b", ex.Command);
    }

    [Fact]
    public async Task ResetToCommitAsync_ThrowsGitException()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() =>
            _service.ResetToCommitAsync("abc123"));

        Assert.Equal("git reset", ex.Command);
    }

    [Fact]
    public async Task GetLastCommitDateAsync_ThrowsGitException()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() =>
            _service.GetLastCommitDateAsync());

        Assert.Equal("git log", ex.Command);
    }

    [Fact]
    public async Task GetDiffAsync_ThrowsGitException()
    {
        var ex = await Assert.ThrowsAsync<GitException>(() =>
            _service.GetDiffAsync());

        Assert.Equal("git diff", ex.Command);
    }

    [Fact]
    public void ImplementsIGitService()
    {
        Assert.IsAssignableFrom<IGitService>(_service);
    }
}
