using Shouldly;
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

        retrieved.ShouldBe("ghp_test123");
    }

    [Fact]
    public async Task GetTokenAsync_NoFile_ReturnsNull()
    {
        var result = await _store.GetTokenAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ClearAsync_RemovesFile()
    {
        await _store.StoreTokenAsync("test-token");

        await _store.ClearAsync();

        File.Exists(_testPath).ShouldBeFalse();
        var result = await _store.GetTokenAsync();
        result.ShouldBeNull();
    }

    [Fact]
    public async Task StoreTokenAsync_CreatesDirectory()
    {
        var dir = Path.GetDirectoryName(_testPath)!;
        Directory.Exists(dir).ShouldBeFalse();

        await _store.StoreTokenAsync("test-token");

        Directory.Exists(dir).ShouldBeTrue();
        File.Exists(_testPath).ShouldBeTrue();
    }
}
