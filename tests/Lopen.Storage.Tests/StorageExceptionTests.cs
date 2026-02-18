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

    [Fact]
    public void IsCritical_WithIOException_ReturnsTrue()
    {
        var ex = new StorageException("fail", "/path", new IOException("Disk full"));
        Assert.True(ex.IsCritical);
    }

    [Fact]
    public void IsCritical_WithUnauthorizedAccessException_ReturnsTrue()
    {
        var ex = new StorageException("fail", "/path", new UnauthorizedAccessException("Permission denied"));
        Assert.True(ex.IsCritical);
    }

    [Fact]
    public void IsCritical_WithOtherException_ReturnsFalse()
    {
        var ex = new StorageException("fail", "/path", new InvalidOperationException("other"));
        Assert.False(ex.IsCritical);
    }

    [Fact]
    public void IsCritical_WithoutInnerException_ReturnsFalse()
    {
        var ex = new StorageException("fail", "/path");
        Assert.False(ex.IsCritical);
    }

    [Fact]
    public void IsCritical_MessageOnly_ReturnsFalse()
    {
        var ex = new StorageException("fail");
        Assert.False(ex.IsCritical);
    }

    [Fact]
    public void WriteFailureStorageException_SetsOsErrorCode()
    {
        var ioEx = new IOException("No space left");
        var ex = new WriteFailureStorageException("fail", "/path", ioEx);

        Assert.Equal(ioEx.HResult, ex.OsErrorCode);
        Assert.True(ex.IsCritical);
        Assert.Equal("/path", ex.Path);
        Assert.Same(ioEx, ex.InnerException);
    }

    [Fact]
    public void WriteFailureStorageException_ClassifiesDiskFull()
    {
        var ioEx = new IOException("No space");
        SetHResult(ioEx, unchecked((int)0x80070070));
        var ex = new WriteFailureStorageException("fail", "/path", ioEx);

        Assert.Equal("Disk full", ex.OsErrorDescription);
    }

    [Fact]
    public void WriteFailureStorageException_ClassifiesEnospc()
    {
        var ioEx = new IOException("No space");
        SetHResult(ioEx, unchecked((int)0x8007001C));
        var ex = new WriteFailureStorageException("fail", "/path", ioEx);

        Assert.Equal("No space left on device (ENOSPC)", ex.OsErrorDescription);
    }

    [Fact]
    public void WriteFailureStorageException_ClassifiesUnknownHResult()
    {
        var ioEx = new IOException("Unknown error");
        SetHResult(ioEx, unchecked((int)0x80070005));
        var ex = new WriteFailureStorageException("fail", "/path", ioEx);

        Assert.StartsWith("I/O write failure", ex.OsErrorDescription);
    }

    private static void SetHResult(Exception ex, int hresult)
    {
        ex.HResult = hresult;
    }
}
