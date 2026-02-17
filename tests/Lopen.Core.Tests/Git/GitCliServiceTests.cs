using Lopen.Core.Git;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Git;

public class GitCliServiceTests
{
    private readonly GitCliService _service = new(NullLogger<GitCliService>.Instance, Directory.GetCurrentDirectory());

    [Fact]
    public async Task GetDiffAsync_ReturnsString()
    {
        var result = await _service.GetDiffAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetLastCommitDateAsync_ReturnsDateOrNull()
    {
        var result = await _service.GetLastCommitDateAsync();

        // In a git repo, this should return a date; in a non-git context, null
        // We just verify it doesn't throw
        Assert.True(result is null || result.Value.Year > 2000);
    }

    [Fact]
    public async Task CommitAllAsync_NullMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CommitAllAsync(null!));
    }

    [Fact]
    public async Task CommitAllAsync_EmptyMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CommitAllAsync(""));
    }

    [Fact]
    public async Task CreateBranchAsync_NullName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.CreateBranchAsync(null!));
    }

    [Fact]
    public async Task ResetToCommitAsync_NullSha_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ResetToCommitAsync(null!));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GitCliService(null!, "/tmp"));
    }

    [Fact]
    public void Constructor_NullWorkingDirectory_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GitCliService(NullLogger<GitCliService>.Instance, null!));
    }

    [Fact]
    public void ImplementsIGitService()
    {
        Assert.IsAssignableFrom<IGitService>(_service);
    }
}
