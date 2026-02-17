using Lopen.Core.Documents;

namespace Lopen.Core.Tests.Documents;

public class XxHashContentHasherTests
{
    private readonly XxHashContentHasher _hasher = new();

    [Fact]
    public void ComputeHash_ReturnsDeterministicHash()
    {
        var hash1 = _hasher.ComputeHash("test content");
        var hash2 = _hasher.ComputeHash("test content");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHashes()
    {
        var hash1 = _hasher.ComputeHash("content A");
        var hash2 = _hasher.ComputeHash("content B");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsHexString()
    {
        var hash = _hasher.ComputeHash("test");
        Assert.Matches("^[0-9A-F]+$", hash);
    }

    [Fact]
    public void ComputeHash_ReturnsNonEmpty()
    {
        var hash = _hasher.ComputeHash("test");
        Assert.NotEmpty(hash);
    }

    [Fact]
    public void HasDrifted_SameContent_ReturnsFalse()
    {
        var hash = _hasher.ComputeHash("test content");
        Assert.False(_hasher.HasDrifted("test content", hash));
    }

    [Fact]
    public void HasDrifted_DifferentContent_ReturnsTrue()
    {
        var hash = _hasher.ComputeHash("original content");
        Assert.True(_hasher.HasDrifted("modified content", hash));
    }

    [Fact]
    public void HasDrifted_CaseInsensitiveHash()
    {
        var hash = _hasher.ComputeHash("test");
        Assert.False(_hasher.HasDrifted("test", hash.ToLowerInvariant()));
    }

    [Fact]
    public void ComputeHash_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.ComputeHash(null!));
    }

    [Fact]
    public void HasDrifted_ThrowsOnNullContent()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.HasDrifted(null!, "hash"));
    }

    [Fact]
    public void HasDrifted_ThrowsOnNullHash()
    {
        Assert.Throws<ArgumentNullException>(() => _hasher.HasDrifted("content", null!));
    }

    [Fact]
    public void ImplementsInterface()
    {
        Assert.IsAssignableFrom<IContentHasher>(_hasher);
    }
}
