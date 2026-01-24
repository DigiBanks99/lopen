using FluentAssertions;
using Xunit;

namespace Lopen.Core.Tests;

public class FileCredentialStoreTests : IDisposable
{
    private readonly string _testPath;
    private readonly FileCredentialStore _store;

    public FileCredentialStoreTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}", "credentials.json");
        _store = new FileCredentialStore(_testPath);
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_testPath);
        if (dir != null && Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task StoreAndRetrieve_Token_RoundTrips()
    {
        await _store.StoreTokenAsync("ghp_test123");

        var retrieved = await _store.GetTokenAsync();

        retrieved.Should().Be("ghp_test123");
    }

    [Fact]
    public async Task GetTokenAsync_NoFile_ReturnsNull()
    {
        var result = await _store.GetTokenAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ClearAsync_RemovesFile()
    {
        await _store.StoreTokenAsync("test-token");

        await _store.ClearAsync();

        File.Exists(_testPath).Should().BeFalse();
        var result = await _store.GetTokenAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task StoreTokenAsync_CreatesDirectory()
    {
        var dir = Path.GetDirectoryName(_testPath)!;
        Directory.Exists(dir).Should().BeFalse();

        await _store.StoreTokenAsync("test-token");

        Directory.Exists(dir).Should().BeTrue();
        File.Exists(_testPath).Should().BeTrue();
    }
}
