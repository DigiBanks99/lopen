namespace Lopen.Storage.Tests;

public class StorageExceptionTests
{
    [Fact]
    public void Constructor_MessageOnly_SetsMessage()
    {
        var ex = new StorageException("test error");

        Assert.Equal("test error", ex.Message);
        Assert.Null(ex.Path);
    }

    [Fact]
    public void Constructor_MessageAndPath_SetsBoth()
    {
        var ex = new StorageException("test error", "/some/path");

        Assert.Equal("test error", ex.Message);
        Assert.Equal("/some/path", ex.Path);
    }

    [Fact]
    public void Constructor_WithInnerException_SetsAll()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new StorageException("test error", "/some/path", inner);

        Assert.Equal("test error", ex.Message);
        Assert.Equal("/some/path", ex.Path);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void IsException_DerivedFromException()
    {
        var ex = new StorageException("test");

        Assert.IsAssignableFrom<Exception>(ex);
    }
}
